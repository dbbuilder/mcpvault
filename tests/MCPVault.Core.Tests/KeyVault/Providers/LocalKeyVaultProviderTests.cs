using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault.Models;
using MCPVault.Core.KeyVault.Providers;
using MCPVault.Core.Security;

namespace MCPVault.Core.Tests.KeyVault.Providers
{
    public class LocalKeyVaultProviderTests : IDisposable
    {
        private readonly Mock<IEncryptionService> _mockEncryptionService;
        private readonly Mock<ILogger<LocalKeyVaultProvider>> _mockLogger;
        private readonly LocalKeyVaultProvider _provider;
        private readonly string _testStoragePath;

        public LocalKeyVaultProviderTests()
        {
            _mockEncryptionService = new Mock<IEncryptionService>();
            _mockLogger = new Mock<ILogger<LocalKeyVaultProvider>>();
            _testStoragePath = Path.Combine(Path.GetTempPath(), "mcpvault-test", Guid.NewGuid().ToString());

            var options = Options.Create(new LocalKeyVaultConfiguration
            {
                StoragePath = _testStoragePath
            });

            // Setup encryption service mocks
            _mockEncryptionService.Setup(e => e.EncryptAsync(It.IsAny<string>()))
                .ReturnsAsync((string input) => new EncryptedData
                {
                    CipherText = $"encrypted_{input}",
                    Nonce = "test-nonce",
                    Tag = "test-tag",
                    Algorithm = "AES-256-GCM"
                });

            _mockEncryptionService.Setup(e => e.DecryptAsync(It.IsAny<EncryptedData>()))
                .ReturnsAsync((EncryptedData data) => data.CipherText.Replace("encrypted_", ""));

            _mockEncryptionService.Setup(e => e.GenerateKey())
                .Returns("test-key-material");

            _provider = new LocalKeyVaultProvider(_mockEncryptionService.Object, options, _mockLogger.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testStoragePath))
            {
                Directory.Delete(_testStoragePath, true);
            }
        }

        [Fact]
        public async Task SetSecretAsync_CreatesNewSecret()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "test-secret",
                Value = "secret-value",
                ContentType = "text/plain",
                Tags = new Dictionary<string, string> { { "env", "test" } }
            };

            // Act
            var result = await _provider.SetSecretAsync(secret);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(secret.Name, result.Name);
            Assert.Equal(secret.Value, result.Value);
            Assert.NotNull(result.Version);
            Assert.NotNull(result.CreatedOn);
            Assert.NotNull(result.UpdatedOn);
        }

        [Fact]
        public async Task GetSecretAsync_ReturnsStoredSecret()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "test-secret",
                Value = "secret-value"
            };
            await _provider.SetSecretAsync(secret);

            // Act
            var result = await _provider.GetSecretAsync("test-secret");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(secret.Name, result.Name);
            Assert.Equal(secret.Value, result.Value);
        }

        [Fact]
        public async Task GetSecretAsync_ThrowsWhenSecretNotFound()
        {
            // Act & Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync("non-existent"));
        }

        [Fact]
        public async Task GetSecretAsync_ThrowsWhenSecretDisabled()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "disabled-secret",
                Value = "value",
                Enabled = true
            };
            var storedSecret = await _provider.SetSecretAsync(secret);
            
            // Update to disabled
            storedSecret.Enabled = false;
            await _provider.SetSecretAsync(storedSecret);

            // Act & Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync("disabled-secret"));
        }

        [Fact]
        public async Task GetSecretAsync_ThrowsWhenSecretExpired()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "expired-secret",
                Value = "value",
                ExpiresOn = DateTime.UtcNow.AddMinutes(-1)
            };
            await _provider.SetSecretAsync(secret);

            // Act & Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync("expired-secret"));
        }

        [Fact]
        public async Task ListSecretsAsync_ReturnsEnabledSecrets()
        {
            // Arrange
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = "secret1", 
                Value = "value1", 
                Enabled = true 
            });
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = "secret2", 
                Value = "value2", 
                Enabled = true 
            });
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = "secret3", 
                Value = "value3", 
                Enabled = false 
            });

            // Act
            var result = await _provider.ListSecretsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, s => Assert.True(s.Enabled ?? true));
        }

        [Fact]
        public async Task GetSecretVersionsAsync_ReturnsAllVersions()
        {
            // Arrange
            var secretName = "versioned-secret";
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = secretName, 
                Value = "v1" 
            });
            await Task.Delay(10); // Ensure different timestamps
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = secretName, 
                Value = "v2" 
            });
            await Task.Delay(10);
            await _provider.SetSecretAsync(new KeyVaultSecret 
            { 
                Name = secretName, 
                Value = "v3" 
            });

            // Act
            var versions = await _provider.GetSecretVersionsAsync(secretName);

            // Assert
            Assert.Equal(3, versions.Count);
            Assert.Equal("v3", versions[0].Value); // Latest first
            Assert.Equal("v2", versions[1].Value);
            Assert.Equal("v1", versions[2].Value);
        }

        [Fact]
        public async Task DeleteSecretAsync_RemovesSecret()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "delete-test",
                Value = "value"
            };
            await _provider.SetSecretAsync(secret);

            // Act
            await _provider.DeleteSecretAsync("delete-test");

            // Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetSecretAsync("delete-test"));
        }

        [Fact]
        public async Task RotateSecretAsync_CreatesNewVersion()
        {
            // Arrange
            var secretName = "rotate-test";
            var originalSecret = new KeyVaultSecret
            {
                Name = secretName,
                Value = "original-value",
                ContentType = "text/plain",
                Tags = new Dictionary<string, string> { { "app", "test" } }
            };
            await _provider.SetSecretAsync(originalSecret);

            // Act
            var rotated = await _provider.RotateSecretAsync(secretName, "new-value");

            // Assert
            Assert.Equal("new-value", rotated.Value);
            Assert.Equal(originalSecret.ContentType, rotated.ContentType);
            Assert.Equal(originalSecret.Tags, rotated.Tags);
            
            // Verify we have 2 versions
            var versions = await _provider.GetSecretVersionsAsync(secretName);
            Assert.Equal(2, versions.Count);
        }

        [Fact]
        public async Task CreateKeyAsync_CreatesSymmetricKey()
        {
            // Arrange
            var keyName = "test-key";

            // Act
            var key = await _provider.CreateKeyAsync(keyName, KeyType.Symmetric, 256);

            // Assert
            Assert.NotNull(key);
            Assert.Equal(keyName, key.Name);
            Assert.Equal(KeyType.Symmetric, key.KeyType);
            Assert.Equal(256, key.KeySize);
            Assert.NotNull(key.KeyMaterial);
            Assert.Contains(KeyOperation.Encrypt, key.Operations);
            Assert.Contains(KeyOperation.Decrypt, key.Operations);
        }

        [Fact]
        public async Task GetKeyAsync_ReturnsStoredKey()
        {
            // Arrange
            var keyName = "test-key";
            await _provider.CreateKeyAsync(keyName, KeyType.Symmetric);

            // Act
            var key = await _provider.GetKeyAsync(keyName);

            // Assert
            Assert.NotNull(key);
            Assert.Equal(keyName, key.Name);
        }

        [Fact]
        public async Task GetKeyAsync_ThrowsWhenKeyNotFound()
        {
            // Act & Assert
            await Assert.ThrowsAsync<KeyVaultException>(() => 
                _provider.GetKeyAsync("non-existent-key"));
        }

        [Fact]
        public async Task ValidateConfigurationAsync_ReturnsTrue()
        {
            // Act
            var result = await _provider.ValidateConfigurationAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DataPersistence_SurvivesRestart()
        {
            // Arrange
            var secret = new KeyVaultSecret
            {
                Name = "persistent-secret",
                Value = "persistent-value"
            };
            await _provider.SetSecretAsync(secret);

            var key = await _provider.CreateKeyAsync("persistent-key", KeyType.Symmetric);

            // Act - Create new provider instance
            var options = Options.Create(new LocalKeyVaultConfiguration
            {
                StoragePath = _testStoragePath
            });
            var newProvider = new LocalKeyVaultProvider(_mockEncryptionService.Object, options, _mockLogger.Object);

            // Assert
            var loadedSecret = await newProvider.GetSecretAsync("persistent-secret");
            Assert.Equal(secret.Value, loadedSecret.Value);

            var loadedKey = await newProvider.GetKeyAsync("persistent-key");
            Assert.Equal(key.Name, loadedKey.Name);
        }
    }
}