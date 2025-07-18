using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.KeyVault.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IKeyVaultProvider
    {
        KeyVaultProviderType ProviderType { get; }
        
        // Secret operations
        Task<KeyVaultSecret> GetSecretAsync(string name);
        Task<KeyVaultSecret> SetSecretAsync(KeyVaultSecret secret);
        Task DeleteSecretAsync(string name);
        Task<List<KeyVaultSecret>> ListSecretsAsync();
        Task<List<KeyVaultSecret>> GetSecretVersionsAsync(string name);
        Task<KeyVaultSecret> RotateSecretAsync(string name, string newValue);
        
        // Key operations
        Task<KeyVaultKey> GetKeyAsync(string name);
        Task<KeyVaultKey> CreateKeyAsync(string name, KeyType keyType, int? keySize = null);
        
        // Configuration
        Task<bool> ValidateConfigurationAsync();
    }
}