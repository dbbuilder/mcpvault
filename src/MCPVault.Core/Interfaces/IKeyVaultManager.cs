using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.KeyVault;
using MCPVault.Core.KeyVault.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IKeyVaultManager
    {
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
        
        // Encrypted secret operations
        Task<KeyVaultSecret> StoreEncryptedSecretAsync(string name, string plainValue);
        Task<string> GetDecryptedSecretAsync(string name);
        
        // Provider management
        Task<ProviderHealth> GetProviderHealthAsync();
        Task SwitchProviderAsync(KeyVaultProviderType newProviderType);
        
        // Bulk operations
        Task<List<BulkImportResult>> BulkImportSecretsAsync(List<KeyVaultSecret> secrets);
        
        // Cache management
        void ClearCache();
        CacheStatistics GetCacheStatistics();
    }
}