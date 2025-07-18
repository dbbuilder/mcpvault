using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPVault.Core.Interfaces;
using MCPVault.Core.KeyVault;
using MCPVault.Core.KeyVault.Models;
using MCPVault.Core.Security;

namespace MCPVault.Core.Tests.KeyVault
{
    public class KeyVaultManagerTests
    {
        private readonly Mock<IKeyVaultProviderFactory> _mockProviderFactory;
        private readonly Mock<IEncryptionService> _mockEncryptionService;
        private readonly Mock<ILogger<KeyVaultManager>> _mockLogger;
        private readonly Mock<IOptions<KeyVaultConfiguration>> _mockOptions;
        private readonly KeyVaultManager _manager;
        private readonly Mock<IKeyVaultProvider> _mockProvider;

        public KeyVaultManagerTests()
        {
            _mockProviderFactory = new Mock<IKeyVaultProviderFactory>();
            _mockEncryptionService = new Mock<IEncryptionService>();
            _mockLogger = new Mock<ILogger<KeyVaultManager>>();
            _mockOptions = new Mock<IOptions<KeyVaultConfiguration>>();
            _mockProvider = new Mock<IKeyVaultProvider>();

            var config = new KeyVaultConfiguration
            {
                Provider = KeyVaultProviderType.Azure,
                EnableCaching = true,
                CacheDuration = TimeSpan.FromMinutes(5)
            };
            _mockOptions.Setup(o => o.Value).Returns(config);

            _mockProviderFactory.Setup(f => f.CreateProvider(It.IsAny<KeyVaultProviderType>()))
                .Returns(_mockProvider.Object);

            _manager = new KeyVaultManager(
                _mockProviderFactory.Object,
                _mockEncryptionService.Object,
                _mockOptions.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetSecretAsync_WithCachingEnabled_ReturnsCachedValue()
        {
            // Arrange
            var secretName = "cached-secret";
            var secret = new KeyVaultSecret
            {
                Name = secretName,
                Value = "secret-value",
                Version = "v1"
            };

            _mockProvider.Setup(p => p.GetSecretAsync(secretName))
                .ReturnsAsync(secret);

            // Act - First call should hit the provider
            var result1 = await _manager.GetSecretAsync(secretName);
            
            // Act - Second call should return cached value
            var result2 = await _manager.GetSecretAsync(secretName);

            // Assert
            Assert.Equal(secret.Value, result1.Value);
            Assert.Equal(secret.Value, result2.Value);
            _mockProvider.Verify(p => p.GetSecretAsync(secretName), Times.Once);
        }

        [Fact]
        public async Task GetSecretAsync_WithCachingDisabled_AlwaysCallsProvider()
        {
            // Arrange
            var config = new KeyVaultConfiguration
            {
                Provider = KeyVaultProviderType.Azure,
                EnableCaching = false
            };
            _mockOptions.Setup(o => o.Value).Returns(config);

            var manager = new KeyVaultManager(
                _mockProviderFactory.Object,
                _mockEncryptionService.Object,
                _mockOptions.Object,
                _mockLogger.Object
            );

            var secretName = "uncached-secret";
            var secret = new KeyVaultSecret { Name = secretName, Value = "value" };

            _mockProvider.Setup(p => p.GetSecretAsync(secretName))
                .ReturnsAsync(secret);

            // Act
            await manager.GetSecretAsync(secretName);
            await manager.GetSecretAsync(secretName);

            // Assert
            _mockProvider.Verify(p => p.GetSecretAsync(secretName), Times.Exactly(2));
        }

        [Fact]
        public async Task SetSecretAsync_InvalidatesCache()
        {
            // Arrange
            var secretName = "test-secret";
            var originalSecret = new KeyVaultSecret 
            { 
                Name = secretName, 
                Value = "original-value" 
            };
            var updatedSecret = new KeyVaultSecret 
            { 
                Name = secretName, 
                Value = "updated-value" 
            };

            _mockProvider.Setup(p => p.GetSecretAsync(secretName))
                .ReturnsAsync(originalSecret);
            _mockProvider.Setup(p => p.SetSecretAsync(It.IsAny<KeyVaultSecret>()))
                .ReturnsAsync(updatedSecret);

            // Act - Cache the original value
            await _manager.GetSecretAsync(secretName);
            
            // Update the secret
            await _manager.SetSecretAsync(updatedSecret);
            
            // Get the secret again
            _mockProvider.Setup(p => p.GetSecretAsync(secretName))
                .ReturnsAsync(updatedSecret);
            var result = await _manager.GetSecretAsync(secretName);

            // Assert
            Assert.Equal(updatedSecret.Value, result.Value);
            _mockProvider.Verify(p => p.GetSecretAsync(secretName), Times.Exactly(2));
        }

        [Fact]
        public async Task StoreEncryptedSecretAsync_EncryptsBeforeStoring()
        {
            // Arrange
            var secretName = "encrypted-secret";
            var plainValue = "plain-text-value";
            var encryptedData = new EncryptedData
            {
                CipherText = "encrypted-cipher",
                Nonce = "nonce",
                Tag = "tag",
                Algorithm = "AES-256-GCM"
            };

            _mockEncryptionService.Setup(e => e.EncryptAsync(plainValue))
                .ReturnsAsync(encryptedData);

            _mockProvider.Setup(p => p.SetSecretAsync(It.IsAny<KeyVaultSecret>()))
                .ReturnsAsync((KeyVaultSecret s) => s);

            // Act
            var result = await _manager.StoreEncryptedSecretAsync(secretName, plainValue);

            // Assert
            Assert.NotNull(result);
            _mockEncryptionService.Verify(e => e.EncryptAsync(plainValue), Times.Once);
            _mockProvider.Verify(p => p.SetSecretAsync(It.Is<KeyVaultSecret>(s => 
                s.Name == secretName && 
                s.Value.Contains(encryptedData.CipherText))), Times.Once);
        }

        [Fact]
        public async Task GetDecryptedSecretAsync_DecryptsStoredValue()
        {
            // Arrange
            var secretName = "encrypted-secret";
            var plainValue = "decrypted-value";
            var encryptedJson = "{\"cipherText\":\"encrypted\",\"nonce\":\"nonce\",\"tag\":\"tag\"}";
            
            var storedSecret = new KeyVaultSecret
            {
                Name = secretName,
                Value = encryptedJson,
                ContentType = "application/encrypted+json"
            };

            var encryptedData = new EncryptedData
            {
                CipherText = "encrypted",
                Nonce = "nonce",
                Tag = "tag"
            };

            _mockProvider.Setup(p => p.GetSecretAsync(secretName))
                .ReturnsAsync(storedSecret);

            _mockEncryptionService.Setup(e => e.DecryptAsync(It.IsAny<EncryptedData>()))
                .ReturnsAsync(plainValue);

            // Act
            var result = await _manager.GetDecryptedSecretAsync(secretName);

            // Assert
            Assert.Equal(plainValue, result);
            _mockEncryptionService.Verify(e => e.DecryptAsync(It.IsAny<EncryptedData>()), Times.Once);
        }

        [Fact]
        public async Task RotateSecretAsync_UpdatesAndInvalidatesCache()
        {
            // Arrange
            var secretName = "rotate-test";
            var newValue = "new-rotated-value";
            var rotatedSecret = new KeyVaultSecret
            {
                Name = secretName,
                Value = newValue,
                Version = "v2"
            };

            _mockProvider.Setup(p => p.RotateSecretAsync(secretName, newValue))
                .ReturnsAsync(rotatedSecret);

            // Act
            var result = await _manager.RotateSecretAsync(secretName, newValue);

            // Assert
            Assert.Equal(newValue, result.Value);
            _mockProvider.Verify(p => p.RotateSecretAsync(secretName, newValue), Times.Once);
        }

        [Fact]
        public async Task GetProviderHealthAsync_ChecksProviderStatus()
        {
            // Arrange
            _mockProvider.Setup(p => p.ValidateConfigurationAsync())
                .ReturnsAsync(true);
            _mockProvider.Setup(p => p.ProviderType)
                .Returns(KeyVaultProviderType.Azure);

            // Act
            var health = await _manager.GetProviderHealthAsync();

            // Assert
            Assert.NotNull(health);
            Assert.True(health.IsHealthy);
            Assert.Equal(KeyVaultProviderType.Azure, health.Provider);
            Assert.True(health.LastChecked <= DateTime.UtcNow);
        }

        [Fact]
        public async Task SwitchProviderAsync_ChangesActiveProvider()
        {
            // Arrange
            var newProviderType = KeyVaultProviderType.AWS;
            var newProvider = new Mock<IKeyVaultProvider>();
            newProvider.Setup(p => p.ProviderType).Returns(newProviderType);
            newProvider.Setup(p => p.ValidateConfigurationAsync()).ReturnsAsync(true);

            _mockProviderFactory.Setup(f => f.CreateProvider(newProviderType))
                .Returns(newProvider.Object);

            // Act
            await _manager.SwitchProviderAsync(newProviderType);
            
            // Get a secret to verify new provider is used
            var secretName = "test";
            var secret = new KeyVaultSecret { Name = secretName, Value = "value" };
            newProvider.Setup(p => p.GetSecretAsync(secretName)).ReturnsAsync(secret);
            
            var result = await _manager.GetSecretAsync(secretName);

            // Assert
            Assert.Equal(secret.Value, result.Value);
            newProvider.Verify(p => p.GetSecretAsync(secretName), Times.Once);
            _mockProvider.Verify(p => p.GetSecretAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task BulkImportSecretsAsync_ImportsMultipleSecrets()
        {
            // Arrange
            var secrets = new List<KeyVaultSecret>
            {
                new KeyVaultSecret { Name = "secret1", Value = "value1" },
                new KeyVaultSecret { Name = "secret2", Value = "value2" },
                new KeyVaultSecret { Name = "secret3", Value = "value3" }
            };

            _mockProvider.Setup(p => p.SetSecretAsync(It.IsAny<KeyVaultSecret>()))
                .ReturnsAsync((KeyVaultSecret s) => s);

            // Act
            var results = await _manager.BulkImportSecretsAsync(secrets);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Success));
            _mockProvider.Verify(p => p.SetSecretAsync(It.IsAny<KeyVaultSecret>()), Times.Exactly(3));
        }
    }
}