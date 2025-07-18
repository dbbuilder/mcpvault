using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault.Models;
using MCPVault.Core.Security;

namespace MCPVault.Core.KeyVault.Providers
{
    public class LocalKeyVaultProvider : IKeyVaultProvider
    {
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<LocalKeyVaultProvider> _logger;
        private readonly string _storagePath;
        private readonly Dictionary<string, List<KeyVaultSecret>> _secrets;
        private readonly Dictionary<string, KeyVaultKey> _keys;
        private readonly object _lock = new();

        public KeyVaultProviderType ProviderType => KeyVaultProviderType.Local;

        public LocalKeyVaultProvider(
            IEncryptionService encryptionService,
            IOptions<LocalKeyVaultConfiguration> options,
            ILogger<LocalKeyVaultProvider> logger)
        {
            _encryptionService = encryptionService;
            _logger = logger;
            _storagePath = options.Value.StoragePath ?? Path.Combine(Path.GetTempPath(), "mcpvault", "keyvault");
            _secrets = new Dictionary<string, List<KeyVaultSecret>>();
            _keys = new Dictionary<string, KeyVaultKey>();

            Directory.CreateDirectory(_storagePath);
            LoadFromDisk();
        }

        public Task<KeyVaultSecret> GetSecretAsync(string name)
        {
            lock (_lock)
            {
                if (_secrets.TryGetValue(name, out var versions) && versions.Count > 0)
                {
                    var latest = versions.OrderByDescending(v => v.CreatedOn).First();
                    if (latest.Enabled == false)
                    {
                        throw new KeyVaultException($"Secret '{name}' is disabled");
                    }
                    if (latest.ExpiresOn.HasValue && latest.ExpiresOn < DateTime.UtcNow)
                    {
                        throw new KeyVaultException($"Secret '{name}' has expired");
                    }
                    return Task.FromResult(latest);
                }
            }
            throw new KeyVaultException($"Secret '{name}' not found");
        }

        public Task<KeyVaultSecret> SetSecretAsync(KeyVaultSecret secret)
        {
            lock (_lock)
            {
                secret.Version = Guid.NewGuid().ToString();
                secret.CreatedOn = DateTime.UtcNow;
                secret.UpdatedOn = DateTime.UtcNow;

                if (!_secrets.ContainsKey(secret.Name))
                {
                    _secrets[secret.Name] = new List<KeyVaultSecret>();
                }
                _secrets[secret.Name].Add(secret);

                SaveToDisk();
                _logger.LogInformation("Stored secret {SecretName} with version {Version}", secret.Name, secret.Version);
                return Task.FromResult(secret);
            }
        }

        public Task DeleteSecretAsync(string name)
        {
            lock (_lock)
            {
                if (_secrets.Remove(name))
                {
                    SaveToDisk();
                    _logger.LogInformation("Deleted secret {SecretName}", name);
                }
                else
                {
                    throw new KeyVaultException($"Secret '{name}' not found");
                }
            }
            return Task.CompletedTask;
        }

        public Task<List<KeyVaultSecret>> ListSecretsAsync()
        {
            lock (_lock)
            {
                var allSecrets = new List<KeyVaultSecret>();
                foreach (var versions in _secrets.Values)
                {
                    if (versions.Count > 0)
                    {
                        var latest = versions.OrderByDescending(v => v.CreatedOn).First();
                        if (latest.Enabled != false)
                        {
                            allSecrets.Add(latest);
                        }
                    }
                }
                return Task.FromResult(allSecrets);
            }
        }

        public Task<List<KeyVaultSecret>> GetSecretVersionsAsync(string name)
        {
            lock (_lock)
            {
                if (_secrets.TryGetValue(name, out var versions))
                {
                    return Task.FromResult(versions.OrderByDescending(v => v.CreatedOn).ToList());
                }
                return Task.FromResult(new List<KeyVaultSecret>());
            }
        }

        public Task<KeyVaultSecret> RotateSecretAsync(string name, string newValue)
        {
            lock (_lock)
            {
                if (!_secrets.ContainsKey(name))
                {
                    throw new KeyVaultException($"Secret '{name}' not found");
                }

                var newSecret = new KeyVaultSecret
                {
                    Name = name,
                    Value = newValue,
                    Version = Guid.NewGuid().ToString(),
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    Enabled = true
                };

                // Copy metadata from latest version
                var latest = _secrets[name].OrderByDescending(v => v.CreatedOn).First();
                newSecret.ContentType = latest.ContentType;
                newSecret.Tags = latest.Tags;

                _secrets[name].Add(newSecret);
                SaveToDisk();

                _logger.LogInformation("Rotated secret {SecretName} to version {Version}", name, newSecret.Version);
                return Task.FromResult(newSecret);
            }
        }

        public Task<KeyVaultKey> GetKeyAsync(string name)
        {
            lock (_lock)
            {
                if (_keys.TryGetValue(name, out var key))
                {
                    if (key.Enabled == false)
                    {
                        throw new KeyVaultException($"Key '{name}' is disabled");
                    }
                    if (key.ExpiresOn.HasValue && key.ExpiresOn < DateTime.UtcNow)
                    {
                        throw new KeyVaultException($"Key '{name}' has expired");
                    }
                    return Task.FromResult(key);
                }
            }
            throw new KeyVaultException($"Key '{name}' not found");
        }

        public Task<KeyVaultKey> CreateKeyAsync(string name, KeyType keyType, int? keySize = null)
        {
            lock (_lock)
            {
                if (_keys.ContainsKey(name))
                {
                    throw new KeyVaultException($"Key '{name}' already exists");
                }

                var key = new KeyVaultKey
                {
                    Name = name,
                    KeyType = keyType,
                    Version = Guid.NewGuid().ToString(),
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    Enabled = true
                };

                // Generate key material based on type
                switch (keyType)
                {
                    case KeyType.Symmetric:
                        var size = keySize ?? 256;
                        key.KeySize = size;
                        key.KeyMaterial = _encryptionService.GenerateKey();
                        key.Operations = new List<KeyOperation> 
                        { 
                            KeyOperation.Encrypt, 
                            KeyOperation.Decrypt,
                            KeyOperation.WrapKey,
                            KeyOperation.UnwrapKey
                        };
                        break;
                    
                    case KeyType.RSA:
                    case KeyType.EC:
                        // For asymmetric keys, we'd need a proper crypto library
                        throw new NotImplementedException($"Key type {keyType} is not yet implemented in local provider");
                    
                    default:
                        throw new NotSupportedException($"Key type {keyType} is not supported");
                }

                _keys[name] = key;
                SaveToDisk();

                _logger.LogInformation("Created {KeyType} key {KeyName} with size {KeySize}", keyType, name, key.KeySize);
                return Task.FromResult(key);
            }
        }

        public Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                // Check if we can write to storage path
                var testFile = Path.Combine(_storagePath, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate local key vault configuration");
                return Task.FromResult(false);
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                var secretsFile = Path.Combine(_storagePath, "secrets.json");
                var keysFile = Path.Combine(_storagePath, "keys.json");

                if (File.Exists(secretsFile))
                {
                    var encryptedContent = File.ReadAllText(secretsFile);
                    var encryptedData = JsonSerializer.Deserialize<EncryptedData>(encryptedContent);
                    if (encryptedData != null)
                    {
                        var decryptedJson = _encryptionService.DecryptAsync(encryptedData).Result;
                        var secrets = JsonSerializer.Deserialize<Dictionary<string, List<KeyVaultSecret>>>(decryptedJson);
                        if (secrets != null)
                        {
                            foreach (var kvp in secrets)
                            {
                                _secrets[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }

                if (File.Exists(keysFile))
                {
                    var encryptedContent = File.ReadAllText(keysFile);
                    var encryptedData = JsonSerializer.Deserialize<EncryptedData>(encryptedContent);
                    if (encryptedData != null)
                    {
                        var decryptedJson = _encryptionService.DecryptAsync(encryptedData).Result;
                        var keys = JsonSerializer.Deserialize<Dictionary<string, KeyVaultKey>>(decryptedJson);
                        if (keys != null)
                        {
                            foreach (var kvp in keys)
                            {
                                _keys[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }

                _logger.LogInformation("Loaded {SecretCount} secrets and {KeyCount} keys from disk", 
                    _secrets.Count, _keys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load data from disk, starting with empty vault");
            }
        }

        private void SaveToDisk()
        {
            try
            {
                var secretsFile = Path.Combine(_storagePath, "secrets.json");
                var keysFile = Path.Combine(_storagePath, "keys.json");

                // Encrypt and save secrets
                var secretsJson = JsonSerializer.Serialize(_secrets);
                var encryptedSecrets = _encryptionService.EncryptAsync(secretsJson).Result;
                File.WriteAllText(secretsFile, JsonSerializer.Serialize(encryptedSecrets));

                // Encrypt and save keys
                var keysJson = JsonSerializer.Serialize(_keys);
                var encryptedKeys = _encryptionService.EncryptAsync(keysJson).Result;
                File.WriteAllText(keysFile, JsonSerializer.Serialize(encryptedKeys));

                _logger.LogDebug("Saved vault data to disk");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save vault data to disk");
                throw;
            }
        }
    }

    public class LocalKeyVaultConfiguration
    {
        public string? StoragePath { get; set; }
    }
}