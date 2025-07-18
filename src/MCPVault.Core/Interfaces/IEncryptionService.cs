using System.Threading.Tasks;
using MCPVault.Core.Security;

namespace MCPVault.Core.Interfaces
{
    public interface IEncryptionService
    {
        Task<EncryptedData> EncryptAsync(string plainText);
        Task<string> DecryptAsync(EncryptedData encryptedData);
        Task<EncryptedData> EncryptWithKeyAsync(string plainText, string key);
        Task<string> DecryptWithKeyAsync(EncryptedData encryptedData, string key);
        string GenerateKey();
        Task<string> DeriveKeyAsync(string password, string salt);
    }
}