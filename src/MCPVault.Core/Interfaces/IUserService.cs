using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.DTOs;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Interfaces
{
    public interface IUserService
    {
        Task<User> GetByIdAsync(Guid id);
        Task<IEnumerable<User>> GetByOrganizationAsync(Guid organizationId);
        Task<User> CreateAsync(CreateUserRequest request);
        Task<User> UpdateAsync(Guid id, UpdateUserRequest request);
        Task DeleteAsync(Guid id);
        Task AssignRolesAsync(Guid userId, Guid[] roleIds);
        Task RemoveRolesAsync(Guid userId, Guid[] roleIds);
        Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<bool> UnlockUserAsync(Guid userId);
        Task<UserDto> GetUserDetailsAsync(Guid id);
        Task<IEnumerable<UserListDto>> GetUsersListAsync(Guid organizationId);
    }
}