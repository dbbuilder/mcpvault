using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.Authorization.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IPermissionRepository
    {
        Task<IEnumerable<Permission>> GetUserPermissionsAsync(Guid userId);
        Task<IEnumerable<Permission>> GetRolePermissionsAsync(Guid roleId);
        Task<Permission?> GetByIdAsync(Guid id);
        Task<Permission> CreateAsync(Permission permission);
        Task<bool> UpdateAsync(Permission permission);
        Task<bool> DeleteAsync(Guid id);
        
        Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId);
        Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId);
        Task<bool> AssignPermissionToUserAsync(Guid userId, Guid permissionId, DateTime? expiresAt = null);
        Task<bool> RemovePermissionFromUserAsync(Guid userId, Guid permissionId);
        
        Task<IEnumerable<ResourcePermission>> GetResourcePermissionsAsync(string resourceType, Guid resourceId);
        Task<ResourcePermission> CreateResourcePermissionAsync(ResourcePermission permission);
        Task<bool> DeleteResourcePermissionAsync(Guid id);
        
        Task<IEnumerable<Permission>> GetPermissionsByResourceAsync(string resource);
        Task<IEnumerable<Permission>> GetPermissionsByActionAsync(string action);
    }
}