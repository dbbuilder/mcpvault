using System;
using System.Threading.Tasks;
using MCPVault.Core.MCP.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IKeyVaultService
    {
        Task<McpCredentials?> GetCredentialsAsync(Guid serverId, Guid userId);
        Task<bool> StoreCredentialsAsync(Guid serverId, McpCredentials credentials);
        Task<bool> DeleteCredentialsAsync(Guid serverId);
        Task<bool> RotateCredentialsAsync(Guid serverId);
        Task<bool> ValidateCredentialsAsync(McpCredentials credentials);
    }
}