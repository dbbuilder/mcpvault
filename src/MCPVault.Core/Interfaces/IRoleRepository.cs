using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Interfaces
{
    public interface IRoleRepository
    {
        Task<Role?> GetByIdAsync(Guid id);
        Task<Role?> GetByNameAsync(string name);
        Task<IEnumerable<Role>> GetAllAsync();
        Task<IEnumerable<Role>> GetByOrganizationAsync(Guid organizationId);
        Task<IEnumerable<Role>> GetByUserAsync(Guid userId);
        Task<Role> CreateAsync(Role role);
        Task<bool> UpdateAsync(Role role);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> AssignRolesToUserAsync(Guid userId, Guid[] roleIds);
        Task<bool> RemoveRolesFromUserAsync(Guid userId, Guid[] roleIds);
        Task<bool> AssignPermissionsToRoleAsync(Guid roleId, Guid[] permissionIds);
        Task<bool> RemovePermissionsFromRoleAsync(Guid roleId, Guid[] permissionIds);
    }
}