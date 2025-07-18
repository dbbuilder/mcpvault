using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.MCP.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IMcpServerRegistry
    {
        // Server registration and management
        Task<McpServer> RegisterServerAsync(McpServerRegistration registration, Guid organizationId, Guid userId);
        Task<McpServer> GetServerAsync(Guid serverId);
        Task<McpServer> GetServerByNameAsync(string name, Guid organizationId);
        Task<List<McpServer>> GetServersAsync(McpServerFilter filter);
        Task<McpServer> UpdateServerAsync(Guid serverId, McpServerUpdate update);
        Task<bool> DeleteServerAsync(Guid serverId);
        Task<bool> ActivateServerAsync(Guid serverId);
        Task<bool> DeactivateServerAsync(Guid serverId);

        // Server capabilities
        Task<McpServerCapabilities> GetServerCapabilitiesAsync(Guid serverId);
        Task<McpServerCapabilities> RefreshServerCapabilitiesAsync(Guid serverId);
        Task<List<McpToolDefinition>> GetServerToolsAsync(Guid serverId);
        Task<McpToolDefinition?> GetToolDefinitionAsync(Guid serverId, string toolName);

        // Health monitoring
        Task<McpServerHealth> CheckServerHealthAsync(Guid serverId);
        Task<Dictionary<Guid, McpServerHealth>> CheckAllServersHealthAsync(Guid organizationId);
        Task<List<McpServerHealth>> GetServerHealthHistoryAsync(Guid serverId, DateTime since);
        Task<bool> SetServerStatusAsync(Guid serverId, McpServerStatus status, string? reason = null);

        // Statistics and monitoring
        Task<McpServerStatistics> GetServerStatisticsAsync(Guid serverId, DateTime startDate, DateTime endDate);
        Task<Dictionary<Guid, McpServerStatistics>> GetOrganizationStatisticsAsync(Guid organizationId, DateTime startDate, DateTime endDate);
        Task RecordToolExecutionAsync(Guid serverId, string toolName, bool success, long executionTimeMs);

        // Credential management
        Task UpdateServerCredentialsAsync(Guid serverId, McpCredentials credentials);
        Task<bool> ValidateServerCredentialsAsync(Guid serverId);
        Task<DateTime?> GetCredentialExpirationAsync(Guid serverId);

        // Bulk operations
        Task<List<McpServer>> ImportServersAsync(List<McpServerRegistration> servers, Guid organizationId, Guid userId);
        Task<int> BulkUpdateServersAsync(List<Guid> serverIds, McpServerUpdate update);
        Task<int> BulkDeleteServersAsync(List<Guid> serverIds);

        // Search and discovery
        Task<List<McpServer>> SearchServersAsync(string searchTerm, Guid organizationId);
        Task<List<McpServer>> GetServersByTypeAsync(McpServerType type, Guid organizationId);
        Task<List<McpServer>> GetServersByStatusAsync(McpServerStatus status, Guid organizationId);
    }
}