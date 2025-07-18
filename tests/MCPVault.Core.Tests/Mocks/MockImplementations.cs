using System;
using System.Threading.Tasks;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Tests.Mocks
{
    public class MockMcpServerRepository : IMcpServerRepository
    {
        public Task<McpServer?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.IEnumerable<McpServer>> GetByOrganizationAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<System.Collections.Generic.IEnumerable<McpServer>> GetActiveByOrganizationAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<McpServer> CreateAsync(McpServer server)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateAsync(McpServer server)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateHealthStatusAsync(Guid id, string status, DateTime lastCheck)
        {
            throw new NotImplementedException();
        }

        public Task<McpServerVersion?> GetLatestVersionAsync(Guid serverId)
        {
            throw new NotImplementedException();
        }

        public Task<McpServerVersion> CreateVersionAsync(McpServerVersion version)
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