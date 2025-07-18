using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetByOrganizationAsync(Guid organizationId);
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> UpdateLastLoginAsync(Guid id, DateTime lastLoginAt);
        Task<bool> IncrementFailedLoginAttemptsAsync(Guid id);
        Task<bool> ResetFailedLoginAttemptsAsync(Guid id);
        Task<bool> LockUserAsync(Guid id, DateTime lockedUntil);
        Task<bool> UnlockUserAsync(Guid id);
    }
}