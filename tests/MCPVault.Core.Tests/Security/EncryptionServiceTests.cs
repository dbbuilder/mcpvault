using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using MCPVault.Core.Security;
using MCPVault.Core.Configuration;

namespace MCPVault.Core.Tests.Security
{
    public class EncryptionServiceTests
    {
        private readonly EncryptionService _encryptionService;
        private readonly EncryptionSettings _settings;

        public EncryptionServiceTests()
        {
            // Generate a proper 256-bit key for testing
            var keyBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }
            
            _settings = new EncryptionSettings
            {
                MasterKey = Convert.ToBase64String(keyBytes),
                Algorithm = "AES-256-GCM",
                KeyDerivationIterations = 100000
            };

            var options = new Mock<IOptions<EncryptionSettings>>();
            options.Setup(o => o.Value).Returns(_settings);

            _encryptionService = new EncryptionService(options.Object);
        }

        [Fact]
        public async Task EncryptAsync_WithPlainText_ReturnsEncryptedData()
        {
            // Arrange
            var plainText = "This is sensitive data";

            // Act
            var encryptedData = await _encryptionService.EncryptAsync(plainText);

            // Assert
            Assert.NotNull(encryptedData);
            Assert.NotEmpty(encryptedData.CipherText);
            Assert.NotEmpty(encryptedData.Nonce);
            Assert.NotEmpty(encryptedData.Tag);
            Assert.Equal(_settings.Algorithm, encryptedData.Algorithm);
            Assert.NotEqual(plainText, encryptedData.CipherText);
        }

        [Fact]
        public async Task DecryptAsync_WithValidEncryptedData_ReturnsOriginalText()
        {
            // Arrange
            var plainText = "This is sensitive data";
            var encryptedData = await _encryptionService.EncryptAsync(plainText);

            // Act
            var decryptedText = await _encryptionService.DecryptAsync(encryptedData);

            // Assert
            Assert.Equal(plainText, decryptedText);
        }

        [Fact]
        public async Task EncryptAsync_WithEmptyString_ReturnsEncryptedData()
        {
            // Arrange
            var plainText = "";

            // Act
            var encryptedData = await _encryptionService.EncryptAsync(plainText);

            // Assert
            Assert.NotNull(encryptedData);
            // Empty plaintext produces empty ciphertext in AES-GCM
            Assert.NotNull(encryptedData.CipherText);
            Assert.NotEmpty(encryptedData.Nonce);
            Assert.NotEmpty(encryptedData.Tag);
        }

        [Fact]
        public async Task DecryptAsync_WithTamperedCipherText_ThrowsException()
        {
            // Arrange
            var plainText = "This is sensitive data";
            var encryptedData = await _encryptionService.EncryptAsync(plainText);
            
            // Tamper with cipher text
            var tamperedData = new EncryptedData
            {
                CipherText = Convert.ToBase64String(Encoding.UTF8.GetBytes("tampered")),
                Nonce = encryptedData.Nonce,
                Tag = encryptedData.Tag,
                Algorithm = encryptedData.Algorithm
            };

            // Act & Assert
            await Assert.ThrowsAsync<CryptographicException>(() => 
                _encryptionService.DecryptAsync(tamperedData));
        }

        [Fact]
        public async Task DecryptAsync_WithTamperedTag_ThrowsException()
        {
            // Arrange
            var plainText = "This is sensitive data";
            var encryptedData = await _encryptionService.EncryptAsync(plainText);
            
            // Tamper with tag
            var tamperedData = new EncryptedData
            {
                CipherText = encryptedData.CipherText,
                Nonce = encryptedData.Nonce,
                Tag = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
                Algorithm = encryptedData.Algorithm
            };

            // Act & Assert
            await Assert.ThrowsAsync<CryptographicException>(() => 
                _encryptionService.DecryptAsync(tamperedData));
        }

        [Fact]
        public async Task EncryptAsync_MultipleCallsWithSameData_ProducesDifferentCipherText()
        {
            // Arrange
            var plainText = "This is sensitive data";

            // Act
            var encrypted1 = await _encryptionService.EncryptAsync(plainText);
            var encrypted2 = await _encryptionService.EncryptAsync(plainText);

            // Assert
            Assert.NotEqual(encrypted1.CipherText, encrypted2.CipherText);
            Assert.NotEqual(encrypted1.Nonce, encrypted2.Nonce);
            // But decryption should produce same result
            var decrypted1 = await _encryptionService.DecryptAsync(encrypted1);
            var decrypted2 = await _encryptionService.DecryptAsync(encrypted2);
            Assert.Equal(decrypted1, decrypted2);
            Assert.Equal(plainText, decrypted1);
        }

        [Fact]
        public void GenerateKey_ReturnsBase64EncodedKey()
        {
            // Act
            var key = _encryptionService.GenerateKey();

            // Assert
            Assert.NotNull(key);
            Assert.NotEmpty(key);
            // Verify it's valid base64
            var bytes = Convert.FromBase64String(key);
            Assert.Equal(32, bytes.Length); // 256 bits for AES-256
        }

        [Fact]
        public async Task DeriveKeyAsync_WithSamePasswordAndSalt_ProducesSameKey()
        {
            // Arrange
            var password = "SecurePassword123!";
            var salt = "UniqueSaltValue";

            // Act
            var key1 = await _encryptionService.DeriveKeyAsync(password, salt);
            var key2 = await _encryptionService.DeriveKeyAsync(password, salt);

            // Assert
            Assert.Equal(key1, key2);
        }

        [Fact]
        public async Task DeriveKeyAsync_WithDifferentPasswords_ProducesDifferentKeys()
        {
            // Arrange
            var password1 = "SecurePassword123!";
            var password2 = "DifferentPassword456!";
            var salt = "UniqueSaltValue";

            // Act
            var key1 = await _encryptionService.DeriveKeyAsync(password1, salt);
            var key2 = await _encryptionService.DeriveKeyAsync(password2, salt);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public async Task DeriveKeyAsync_WithDifferentSalts_ProducesDifferentKeys()
        {
            // Arrange
            var password = "SecurePassword123!";
            var salt1 = "UniqueSaltValue1";
            var salt2 = "UniqueSaltValue2";

            // Act
            var key1 = await _encryptionService.DeriveKeyAsync(password, salt1);
            var key2 = await _encryptionService.DeriveKeyAsync(password, salt2);

            // Assert
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public async Task EncryptWithKeyAsync_UsingDerivedKey_WorksCorrectly()
        {
            // Arrange
            var plainText = "Sensitive data encrypted with derived key";
            var password = "UserPassword123!";
            var salt = "user@example.com"; // Using email as salt
            var derivedKey = await _encryptionService.DeriveKeyAsync(password, salt);

            // Act
            var encrypted = await _encryptionService.EncryptWithKeyAsync(plainText, derivedKey);
            var decrypted = await _encryptionService.DecryptWithKeyAsync(encrypted, derivedKey);

            // Assert
            Assert.Equal(plainText, decrypted);
        }

        [Fact]
        public async Task DecryptWithKeyAsync_WithWrongKey_ThrowsException()
        {
            // Arrange
            var plainText = "Sensitive data";
            var correctKey = _encryptionService.GenerateKey();
            var wrongKey = _encryptionService.GenerateKey();
            var encrypted = await _encryptionService.EncryptWithKeyAsync(plainText, correctKey);

            // Act & Assert
            await Assert.ThrowsAsync<CryptographicException>(() => 
                _encryptionService.DecryptWithKeyAsync(encrypted, wrongKey));
        }
    }
}