using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Interfaces
{
    public interface IMcpServerRepository
    {
        Task<McpServer?> GetByIdAsync(Guid id);
        Task<IEnumerable<McpServer>> GetByOrganizationAsync(Guid organizationId);
        Task<IEnumerable<McpServer>> GetActiveByOrganizationAsync(Guid organizationId);
        Task<McpServer> CreateAsync(McpServer server);
        Task<bool> UpdateAsync(McpServer server);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> UpdateHealthStatusAsync(Guid id, string status, DateTime lastCheck);
        Task<McpServerVersion?> GetLatestVersionAsync(Guid serverId);
        Task<McpServerVersion> CreateVersionAsync(McpServerVersion version);
    }
}