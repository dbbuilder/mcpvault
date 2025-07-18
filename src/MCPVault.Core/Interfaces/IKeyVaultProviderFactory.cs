using MCPVault.Core.KeyVault.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IKeyVaultProviderFactory
    {
        IKeyVaultProvider CreateProvider(KeyVaultProviderType providerType);
        IKeyVaultProvider CreateProvider(KeyVaultConfiguration configuration);
    }
}