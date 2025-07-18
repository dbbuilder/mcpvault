using System;
using System.Threading.Tasks;

namespace MCPVault.Core.Interfaces
{
    public interface IAuditService
    {
        Task LogMcpToolExecutionAsync(
            Guid userId, 
            Guid serverId, 
            string toolName, 
            bool success, 
            string? details = null);
            
        Task LogAuthenticationAsync(
            Guid userId, 
            string action, 
            bool success, 
            string? ipAddress = null);
            
        Task LogDataAccessAsync(
            Guid userId, 
            string resourceType, 
            Guid resourceId, 
            string action, 
            bool success);
            
        Task LogSecurityEventAsync(
            Guid? userId, 
            string eventType, 
            string severity, 
            string details);
    }
}