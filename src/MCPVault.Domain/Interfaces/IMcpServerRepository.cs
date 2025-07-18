using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;
using MCPVault.Domain.Models;

namespace MCPVault.Domain.Interfaces
{
    public interface IMcpServerRepository
    {
        // CRUD operations
        Task<McpServer> CreateAsync(McpServer server);
        Task<McpServer?> GetByIdAsync(Guid id);
        Task<McpServer?> GetByNameAsync(string name, Guid organizationId);
        Task<bool> UpdateAsync(McpServer server);
        Task<bool> DeleteAsync(Guid id);

        // Query operations
        Task<List<McpServer>> GetServersAsync(McpServerFilter filter);
        Task<List<McpServer>> GetByOrganizationAsync(Guid organizationId);
        Task<List<McpServer>> SearchAsync(string searchTerm, Guid organizationId);
        Task<bool> ExistsAsync(Guid id);
        Task<bool> NameExistsAsync(string name, Guid organizationId);

        // Health monitoring
        Task RecordHealthCheckAsync(McpServerHealth health);
        Task<List<McpServerHealth>> GetHealthHistoryAsync(Guid serverId, DateTime since);
        Task<McpServerHealth?> GetLatestHealthCheckAsync(Guid serverId);

        // Statistics
        Task RecordToolExecutionAsync(Guid serverId, string toolName, bool success, long executionTimeMs);
        Task<McpServerStatistics> GetStatisticsAsync(Guid serverId, DateTime startDate, DateTime endDate);
        Task<Dictionary<string, int>> GetToolUsageCountAsync(Guid serverId, DateTime startDate, DateTime endDate);
        Task<Dictionary<string, double>> GetToolResponseTimesAsync(Guid serverId, DateTime startDate, DateTime endDate);

        // Batch operations
        Task<int> BulkUpdateStatusAsync(List<Guid> serverIds, McpServerStatus status);
        Task<int> BulkDeleteAsync(List<Guid> serverIds);
        Task<int> DeactivateInactiveServersAsync(DateTime inactiveSince);
    }
}