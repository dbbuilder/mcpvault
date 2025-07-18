using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MCPVault.Core.Configuration;
using MCPVault.Core.Interfaces;

namespace MCPVault.Core.Security
{
    public class EncryptionService : IEncryptionService
    {
        private readonly EncryptionSettings _settings;
        private readonly byte[] _masterKey;

        public EncryptionService(IOptions<EncryptionSettings> settings)
        {
            _settings = settings.Value;
            _masterKey = Convert.FromBase64String(_settings.MasterKey);
        }

        public async Task<EncryptedData> EncryptAsync(string plainText)
        {
            return await EncryptWithKeyAsync(plainText, Convert.ToBase64String(_masterKey));
        }

        public async Task<string> DecryptAsync(EncryptedData encryptedData)
        {
            return await DecryptWithKeyAsync(encryptedData, Convert.ToBase64String(_masterKey));
        }

        public async Task<EncryptedData> EncryptWithKeyAsync(string plainText, string key)
        {
            return await Task.Run(() =>
            {
                var keyBytes = Convert.FromBase64String(key);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                using var aesGcm = new AesGcm(keyBytes, _settings.TagSize);
                
                // Generate a random nonce
                var nonce = new byte[_settings.NonceSize];
                RandomNumberGenerator.Fill(nonce);

                // Prepare buffers
                var cipherText = new byte[plainBytes.Length];
                var tag = new byte[_settings.TagSize];

                // Encrypt
                aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

                return new EncryptedData
                {
                    CipherText = Convert.ToBase64String(cipherText),
                    Nonce = Convert.ToBase64String(nonce),
                    Tag = Convert.ToBase64String(tag),
                    Algorithm = _settings.Algorithm,
                    Version = 1
                };
            });
        }

        public async Task<string> DecryptWithKeyAsync(EncryptedData encryptedData, string key)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var keyBytes = Convert.FromBase64String(key);
                    var cipherBytes = Convert.FromBase64String(encryptedData.CipherText);
                    var nonceBytes = Convert.FromBase64String(encryptedData.Nonce);
                    var tagBytes = Convert.FromBase64String(encryptedData.Tag);

                    using var aesGcm = new AesGcm(keyBytes, _settings.TagSize);
                    
                    // Prepare buffer for decrypted data
                    var plainBytes = new byte[cipherBytes.Length];

                    // Decrypt
                    aesGcm.Decrypt(nonceBytes, cipherBytes, tagBytes, plainBytes);

                    return Encoding.UTF8.GetString(plainBytes);
                }
                catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException || 
                                          ex is System.Security.Cryptography.AuthenticationTagMismatchException ||
                                          ex is ArgumentException)
                {
                    throw new CryptographicException("Decryption failed. The data may be corrupted or tampered with.", ex);
                }
            });
        }

        public string GenerateKey()
        {
            var key = new byte[_settings.KeySize];
            RandomNumberGenerator.Fill(key);
            return Convert.ToBase64String(key);
        }

        public async Task<string> DeriveKeyAsync(string password, string salt)
        {
            return await Task.Run(() =>
            {
                var saltBytes = Encoding.UTF8.GetBytes(salt);
                
                using var deriveBytes = new Rfc2898DeriveBytes(
                    password, 
                    saltBytes, 
                    _settings.KeyDerivationIterations,
                    HashAlgorithmName.SHA256);

                var key = deriveBytes.GetBytes(_settings.KeySize);
                return Convert.ToBase64String(key);
            });
        }
    }
}