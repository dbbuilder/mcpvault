using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault;
using MCPVault.Core.KeyVault.Models;
using Microsoft.Extensions.Logging;

namespace MCPVault.Core.Tests.KeyVault
{
    public class KeyVaultProviderTests
    {
        private readonly Mock<ILogger<TestKeyVaultProvider>> _mockLogger;
        private readonly TestKeyVaultProvider _provider;

        public KeyVaultProviderTests()
        {
            _mockLogger = new Mock<ILogger<TestKeyVaultProvider>>();
            _provider = new TestKeyVaultProvider(_mockLogger.Object);
        }

        [Fact]
        public async Task GetSecretAsync_WithValidName_ReturnsSecret()
        {
            // Arrange
            var secretName = "test-secret";
            var expectedValue = "secret-value";
            _provider.AddSecret(secretName, expectedValue);

            // Act
            var result = await _provider.GetSecretAsync(secretName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(secretName, result.Name);
            Assert.Equal(expectedValue, result.Value);
            Assert.NotNull(result.Version);
            Assert.True(result.CreatedOn <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GetSecretAsync_WithNonExistentSecret_ThrowsKeyVaultException()
        {
            // Arrange
            var secretName = "non-existent";

            // Act & Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync(secretName));
        }

        [Fact]
        public async Task SetSecretAsync_WithValidData_StoresSecret()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "new-secret",
                Value = "new-value",
                ContentType = "text/plain",
                Tags = new Dictionary<string, string>
                {
                    ["environment"] = "test",
                    ["owner"] = "testuser"
                }
            };

            // Act
            var result = await _provider.SetSecretAsync(secret);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(secret.Name, result.Name);
            Assert.Equal(secret.Value, result.Value);
            Assert.NotNull(result.Version);
            Assert.Equal(secret.Tags, result.Tags);
        }

        [Fact]
        public async Task DeleteSecretAsync_WithExistingSecret_DeletesSecret()
        {
            // Arrange
            var secretName = "to-delete";
            _provider.AddSecret(secretName, "value");

            // Act
            await _provider.DeleteSecretAsync(secretName);

            // Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync(secretName));
        }

        [Fact]
        public async Task ListSecretsAsync_ReturnsAllSecrets()
        {
            // Arrange
            _provider.AddSecret("secret1", "value1");
            _provider.AddSecret("secret2", "value2");
            _provider.AddSecret("secret3", "value3");

            // Act
            var secrets = await _provider.ListSecretsAsync();

            // Assert
            Assert.NotNull(secrets);
            Assert.Equal(3, secrets.Count);
            Assert.Contains(secrets, s => s.Name == "secret1");
            Assert.Contains(secrets, s => s.Name == "secret2");
            Assert.Contains(secrets, s => s.Name == "secret3");
        }

        [Fact]
        public async Task GetSecretVersionsAsync_ReturnsVersionHistory()
        {
            // Arrange
            var secretName = "versioned-secret";
            var secret1 = new KeyVaultSecret { Name = secretName, Value = "v1" };
            var secret2 = new KeyVaultSecret { Name = secretName, Value = "v2" };
            
            await _provider.SetSecretAsync(secret1);
            await Task.Delay(10); // Ensure different timestamps
            await _provider.SetSecretAsync(secret2);

            // Act
            var versions = await _provider.GetSecretVersionsAsync(secretName);

            // Assert
            Assert.NotNull(versions);
            Assert.Equal(2, versions.Count);
            Assert.Contains(versions, v => v.Value == "v1");
            Assert.Contains(versions, v => v.Value == "v2");
        }

        [Fact]
        public void ProviderType_ReturnsCorrectType()
        {
            // Assert
            Assert.Equal(KeyVaultProviderType.Local, _provider.ProviderType);
        }

        [Fact]
        public async Task ValidateConfigurationAsync_WithValidConfig_ReturnsTrue()
        {
            // Act
            var result = await _provider.ValidateConfigurationAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetKeyAsync_WithValidName_ReturnsKey()
        {
            // Arrange
            var keyName = "encryption-key";
            var keyMaterial = Convert.ToBase64String(new byte[32]); // 256-bit key
            _provider.AddKey(keyName, keyMaterial);

            // Act
            var result = await _provider.GetKeyAsync(keyName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(keyName, result.Name);
            Assert.Equal(keyMaterial, result.KeyMaterial);
            Assert.Equal(KeyType.Symmetric, result.KeyType);
        }

        [Fact]
        public async Task RotateSecretAsync_CreatesNewVersion()
        {
            // Arrange
            var secretName = "rotate-test";
            var originalValue = "original-value";
            var newValue = "rotated-value";
            
            var original = new KeyVaultSecret { Name = secretName, Value = originalValue };
            await _provider.SetSecretAsync(original);

            // Act
            var rotated = await _provider.RotateSecretAsync(secretName, newValue);

            // Assert
            Assert.NotNull(rotated);
            Assert.Equal(newValue, rotated.Value);
            Assert.NotEqual(original.Version, rotated.Version);
            
            // Verify we can still get the latest version
            var latest = await _provider.GetSecretAsync(secretName);
            Assert.Equal(newValue, latest.Value);
        }
    }

    // Test implementation for unit testing
    public class TestKeyVaultProvider : IKeyVaultProvider
    {
        private readonly Dictionary<string, List<KeyVaultSecret>> _secrets = new();
        private readonly Dictionary<string, KeyVaultKey> _keys = new();
        private readonly ILogger<TestKeyVaultProvider> _logger;

        public KeyVaultProviderType ProviderType => KeyVaultProviderType.Local;

        public TestKeyVaultProvider(ILogger<TestKeyVaultProvider> logger)
        {
            _logger = logger;
        }

        public void AddSecret(string name, string value)
        {
            var secret = new KeyVaultSecret
            {
                Name = name,
                Value = value,
                Version = Guid.NewGuid().ToString(),
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };

            if (!_secrets.ContainsKey(name))
            {
                _secrets[name] = new List<KeyVaultSecret>();
            }
            _secrets[name].Add(secret);
        }

        public void AddKey(string name, string keyMaterial)
        {
            _keys[name] = new KeyVaultKey
            {
                Name = name,
                KeyMaterial = keyMaterial,
                KeyType = KeyType.Symmetric,
                Version = Guid.NewGuid().ToString(),
                CreatedOn = DateTime.UtcNow
            };
        }

        public Task<KeyVaultSecret> GetSecretAsync(string name)
        {
            if (_secrets.TryGetValue(name, out var versions) && versions.Count > 0)
            {
                return Task.FromResult(versions[^1]); // Return latest version
            }
            throw new KeyVaultException($"Secret '{name}' not found");
        }

        public Task<KeyVaultSecret> SetSecretAsync(KeyVaultSecret secret)
        {
            secret.Version = Guid.NewGuid().ToString();
            secret.CreatedOn = DateTime.UtcNow;
            secret.UpdatedOn = DateTime.UtcNow;

            if (!_secrets.ContainsKey(secret.Name))
            {
                _secrets[secret.Name] = new List<KeyVaultSecret>();
            }
            _secrets[secret.Name].Add(secret);

            return Task.FromResult(secret);
        }

        public Task DeleteSecretAsync(string name)
        {
            _secrets.Remove(name);
            return Task.CompletedTask;
        }

        public Task<List<KeyVaultSecret>> ListSecretsAsync()
        {
            var allSecrets = new List<KeyVaultSecret>();
            foreach (var versions in _secrets.Values)
            {
                if (versions.Count > 0)
                {
                    allSecrets.Add(versions[^1]); // Add latest version only
                }
            }
            return Task.FromResult(allSecrets);
        }

        public Task<List<KeyVaultSecret>> GetSecretVersionsAsync(string name)
        {
            if (_secrets.TryGetValue(name, out var versions))
            {
                return Task.FromResult(new List<KeyVaultSecret>(versions));
            }
            return Task.FromResult(new List<KeyVaultSecret>());
        }

        public Task<bool> ValidateConfigurationAsync()
        {
            return Task.FromResult(true);
        }

        public Task<KeyVaultKey> GetKeyAsync(string name)
        {
            if (_keys.TryGetValue(name, out var key))
            {
                return Task.FromResult(key);
            }
            throw new KeyVaultException($"Key '{name}' not found");
        }

        public Task<KeyVaultKey> CreateKeyAsync(string name, KeyType keyType, int? keySize = null)
        {
            throw new NotImplementedException();
        }

        public Task<KeyVaultSecret> RotateSecretAsync(string name, string newValue)
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
                UpdatedOn = DateTime.UtcNow
            };

            _secrets[name].Add(newSecret);
            return Task.FromResult(newSecret);
        }
    }
}