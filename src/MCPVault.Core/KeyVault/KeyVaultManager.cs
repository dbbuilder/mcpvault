using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault.Models;
using MCPVault.Core.Security;

namespace MCPVault.Core.KeyVault
{
    public class KeyVaultManager : IKeyVaultManager
    {
        private readonly IKeyVaultProviderFactory _providerFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly KeyVaultConfiguration _configuration;
        private readonly ILogger<KeyVaultManager> _logger;
        private readonly ConcurrentDictionary<string, CachedSecret> _secretCache;
        private IKeyVaultProvider _currentProvider;
        private readonly object _providerLock = new();

        public KeyVaultManager(
            IKeyVaultProviderFactory providerFactory,
            IEncryptionService encryptionService,
            IOptions<KeyVaultConfiguration> options,
            ILogger<KeyVaultManager> logger)
        {
            _providerFactory = providerFactory;
            _encryptionService = encryptionService;
            _configuration = options.Value;
            _logger = logger;
            _secretCache = new ConcurrentDictionary<string, CachedSecret>();
            _currentProvider = _providerFactory.CreateProvider(_configuration.Provider);
        }

        public async Task<KeyVaultSecret> GetSecretAsync(string name)
        {
            // Check cache first if enabled
            if (_configuration.EnableCaching && _secretCache.TryGetValue(name, out var cached))
            {
                if (cached.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogDebug("Returning cached secret for {SecretName}", name);
                    return cached.Secret;
                }
                else
                {
                    // Remove expired entry
                    _secretCache.TryRemove(name, out _);
                }
            }

            // Get from provider
            var secret = await _currentProvider.GetSecretAsync(name);

            // Cache if enabled
            if (_configuration.EnableCaching)
            {
                var cacheEntry = new CachedSecret
                {
                    Secret = secret,
                    ExpiresAt = DateTime.UtcNow.Add(_configuration.CacheDuration)
                };
                _secretCache.TryAdd(name, cacheEntry);
            }

            return secret;
        }

        public async Task<KeyVaultSecret> SetSecretAsync(KeyVaultSecret secret)
        {
            var result = await _currentProvider.SetSecretAsync(secret);
            
            // Invalidate cache
            _secretCache.TryRemove(secret.Name, out _);
            
            _logger.LogInformation("Secret {SecretName} has been set", secret.Name);
            return result;
        }

        public async Task DeleteSecretAsync(string name)
        {
            await _currentProvider.DeleteSecretAsync(name);
            
            // Invalidate cache
            _secretCache.TryRemove(name, out _);
            
            _logger.LogInformation("Secret {SecretName} has been deleted", name);
        }

        public async Task<List<KeyVaultSecret>> ListSecretsAsync()
        {
            return await _currentProvider.ListSecretsAsync();
        }

        public async Task<List<KeyVaultSecret>> GetSecretVersionsAsync(string name)
        {
            return await _currentProvider.GetSecretVersionsAsync(name);
        }

        public async Task<KeyVaultSecret> RotateSecretAsync(string name, string newValue)
        {
            var result = await _currentProvider.RotateSecretAsync(name, newValue);
            
            // Invalidate cache
            _secretCache.TryRemove(name, out _);
            
            _logger.LogInformation("Secret {SecretName} has been rotated", name);
            return result;
        }

        public async Task<KeyVaultKey> GetKeyAsync(string name)
        {
            return await _currentProvider.GetKeyAsync(name);
        }

        public async Task<KeyVaultKey> CreateKeyAsync(string name, KeyType keyType, int? keySize = null)
        {
            return await _currentProvider.CreateKeyAsync(name, keyType, keySize);
        }

        public async Task<KeyVaultSecret> StoreEncryptedSecretAsync(string name, string plainValue)
        {
            // Encrypt the value
            var encryptedData = await _encryptionService.EncryptAsync(plainValue);
            
            // Serialize encrypted data
            var encryptedJson = JsonSerializer.Serialize(encryptedData);
            
            // Store as secret with content type indicating encryption
            var secret = new KeyVaultSecret
            {
                Name = name,
                Value = encryptedJson,
                ContentType = "application/encrypted+json",
                Tags = new Dictionary<string, string>
                {
                    ["encrypted"] = "true",
                    ["algorithm"] = encryptedData.Algorithm
                }
            };

            return await SetSecretAsync(secret);
        }

        public async Task<string> GetDecryptedSecretAsync(string name)
        {
            var secret = await GetSecretAsync(name);
            
            // Check if it's encrypted
            if (secret.ContentType != "application/encrypted+json")
            {
                throw new InvalidOperationException($"Secret {name} is not encrypted");
            }

            // Deserialize encrypted data
            var encryptedData = JsonSerializer.Deserialize<EncryptedData>(secret.Value);
            if (encryptedData == null)
            {
                throw new InvalidOperationException($"Failed to deserialize encrypted data for secret {name}");
            }

            // Decrypt and return
            return await _encryptionService.DecryptAsync(encryptedData);
        }

        public async Task<ProviderHealth> GetProviderHealthAsync()
        {
            try
            {
                var isHealthy = await _currentProvider.ValidateConfigurationAsync();
                return new ProviderHealth
                {
                    Provider = _currentProvider.ProviderType,
                    IsHealthy = isHealthy,
                    LastChecked = DateTime.UtcNow,
                    Message = isHealthy ? "Provider is healthy" : "Provider validation failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider health");
                return new ProviderHealth
                {
                    Provider = _currentProvider.ProviderType,
                    IsHealthy = false,
                    LastChecked = DateTime.UtcNow,
                    Message = $"Health check failed: {ex.Message}"
                };
            }
        }

        public async Task SwitchProviderAsync(KeyVaultProviderType newProviderType)
        {
            lock (_providerLock)
            {
                _currentProvider = _providerFactory.CreateProvider(newProviderType);
                _secretCache.Clear(); // Clear cache when switching providers
            }

            // Validate new provider
            var isValid = await _currentProvider.ValidateConfigurationAsync();
            if (!isValid)
            {
                throw new InvalidOperationException($"Failed to validate {newProviderType} provider configuration");
            }

            _logger.LogInformation("Switched to {ProviderType} key vault provider", newProviderType);
        }

        public async Task<List<BulkImportResult>> BulkImportSecretsAsync(List<KeyVaultSecret> secrets)
        {
            var results = new List<BulkImportResult>();

            foreach (var secret in secrets)
            {
                try
                {
                    await SetSecretAsync(secret);
                    results.Add(new BulkImportResult
                    {
                        SecretName = secret.Name,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import secret {SecretName}", secret.Name);
                    results.Add(new BulkImportResult
                    {
                        SecretName = secret.Name,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return results;
        }

        public void ClearCache()
        {
            _secretCache.Clear();
            _logger.LogInformation("Secret cache cleared");
        }

        public CacheStatistics GetCacheStatistics()
        {
            var validEntries = _secretCache.Values.Count(c => c.ExpiresAt > DateTime.UtcNow);
            var expiredEntries = _secretCache.Count - validEntries;

            return new CacheStatistics
            {
                TotalEntries = _secretCache.Count,
                ValidEntries = validEntries,
                ExpiredEntries = expiredEntries,
                CacheDuration = _configuration.CacheDuration,
                EnableCaching = _configuration.EnableCaching
            };
        }

        private class CachedSecret
        {
            public KeyVaultSecret Secret { get; set; } = null!;
            public DateTime ExpiresAt { get; set; }
        }
    }

    public class ProviderHealth
    {
        public KeyVaultProviderType Provider { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastChecked { get; set; }
        public string? Message { get; set; }
    }

    public class BulkImportResult
    {
        public string SecretName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int ValidEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public TimeSpan CacheDuration { get; set; }
        public bool EnableCaching { get; set; }
    }
}