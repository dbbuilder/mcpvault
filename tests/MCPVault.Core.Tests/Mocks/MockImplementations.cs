using System;
using System.Threading.Tasks;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;
using MCPVault.Domain.Interfaces;
using MCPVault.Domain.Models;

namespace MCPVault.Core.Tests.Mocks
{
    public class MockMcpServerRepository : IMcpServerRepository
    {
        public Task<Domain.Entities.McpServer?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.List<Domain.Entities.McpServer>> GetByOrganizationAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<Domain.Entities.McpServer?> GetByNameAsync(string name, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<Domain.Entities.McpServer> CreateAsync(Domain.Entities.McpServer server)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateAsync(Domain.Entities.McpServer server)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.List<Domain.Entities.McpServer>> GetServersAsync(Domain.Models.McpServerFilter filter)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.List<Domain.Entities.McpServer>> SearchAsync(string searchTerm, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> NameExistsAsync(string name, Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task RecordHealthCheckAsync(Domain.Entities.McpServerHealth health)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.List<Domain.Entities.McpServerHealth>> GetHealthHistoryAsync(Guid serverId, DateTime since)
        {
            throw new NotImplementedException();
        }

        public Task<Domain.Entities.McpServerHealth?> GetLatestHealthCheckAsync(Guid serverId)
        {
            throw new NotImplementedException();
        }

        public Task RecordToolExecutionAsync(Guid serverId, string toolName, bool success, long executionTimeMs)
        {
            throw new NotImplementedException();
        }

        public Task<Domain.Entities.McpServerStatistics> GetStatisticsAsync(Guid serverId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.Dictionary<string, int>> GetToolUsageCountAsync(Guid serverId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.Dictionary<string, double>> GetToolResponseTimesAsync(Guid serverId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public Task<int> BulkUpdateStatusAsync(System.Collections.Generic.List<Guid> serverIds, Domain.Entities.McpServerStatus status)
        {
            throw new NotImplementedException();
        }

        public Task<int> BulkDeleteAsync(System.Collections.Generic.List<Guid> serverIds)
        {
            throw new NotImplementedException();
        }

        public Task<int> DeactivateInactiveServersAsync(DateTime inactiveSince)
        {
            throw new NotImplementedException();
        }
    }

    public class MockKeyVaultService : IKeyVaultService
    {
        public Task<McpCredentials?> GetCredentialsAsync(Guid serverId, Guid userId)
        {
            return Task.FromResult<McpCredentials?>(new McpCredentials { ApiKey = "test-key" });
        }

        public Task<bool> StoreCredentialsAsync(Guid serverId, McpCredentials credentials)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DeleteCredentialsAsync(Guid serverId)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RotateCredentialsAsync(Guid serverId)
        {
            return Task.FromResult(true);
        }

        public Task<bool> ValidateCredentialsAsync(McpCredentials credentials)
        {
            return Task.FromResult(true);
        }
    }

    public class MockAuditService : IAuditService
    {
        public Task LogMcpToolExecutionAsync(Guid userId, Guid serverId, string toolName, bool success, string? details = null)
        {
            return Task.CompletedTask;
        }

        public Task LogAuthenticationAsync(Guid userId, string action, bool success, string? ipAddress = null)
        {
            return Task.CompletedTask;
        }

        public Task LogDataAccessAsync(Guid userId, string resourceType, Guid resourceId, string action, bool success)
        {
            return Task.CompletedTask;
        }

        public Task LogSecurityEventAsync(Guid? userId, string eventType, string severity, string details)
        {
            return Task.CompletedTask;
        }
    }
}