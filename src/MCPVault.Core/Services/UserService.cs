using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.DTOs;
using MCPVault.Core.Exceptions;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IAuthenticationService _authService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IAuthenticationService authService,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _authService = authService;
            _logger = logger;
        }

        public async Task<User> GetByIdAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                throw new NotFoundException($"User with ID {id} not found");
            }
            return user;
        }

        public async Task<IEnumerable<User>> GetByOrganizationAsync(Guid organizationId)
        {
            return await _userRepository.GetByOrganizationAsync(organizationId);
        }

        public async Task<User> CreateAsync(CreateUserRequest request)
        {
            // Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new ConflictException($"User with email {request.Email} already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                OrganizationId = request.OrganizationId,
                IsActive = request.IsActive,
                IsMfaEnabled = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FailedLoginAttempts = 0
            };

            var createdUser = await _userRepository.CreateAsync(user);
            _logger.LogInformation("Created new user with ID {UserId}", createdUser.Id);

            return createdUser;
        }

        public async Task<User> UpdateAsync(Guid id, UpdateUserRequest request)
        {
            var user = await GetByIdAsync(id);

            // If email is being changed, check if it's already taken
            if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
            {
                var existingUser = await _userRepository.GetByEmailAsync(request.Email);
                if (existingUser != null && existingUser.Id != id)
                {
                    throw new ConflictException($"Email {request.Email} is already in use");
                }
                user.Email = request.Email;
            }

            if (!string.IsNullOrEmpty(request.FirstName))
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrEmpty(request.LastName))
            {
                user.LastName = request.LastName;
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            _logger.LogInformation("Updated user with ID {UserId}", id);

            return user;
        }

        public async Task DeleteAsync(Guid id)
        {
            var user = await GetByIdAsync(id);
            await _userRepository.DeleteAsync(id);
            _logger.LogInformation("Deleted user with ID {UserId}", id);
        }

        public async Task AssignRolesAsync(Guid userId, Guid[] roleIds)
        {
            var user = await GetByIdAsync(userId);

            // Verify all roles exist
            foreach (var roleId in roleIds)
            {
                var role = await _roleRepository.GetByIdAsync(roleId);
                if (role == null)
                {
                    throw new NotFoundException($"Role with ID {roleId} not found");
                }
            }

            await _roleRepository.AssignRolesToUserAsync(userId, roleIds);
            _logger.LogInformation("Assigned {RoleCount} roles to user {UserId}", roleIds.Length, userId);
        }

        public async Task RemoveRolesAsync(Guid userId, Guid[] roleIds)
        {
            var user = await GetByIdAsync(userId);
            await _roleRepository.RemoveRolesFromUserAsync(userId, roleIds);
            _logger.LogInformation("Removed {RoleCount} roles from user {UserId}", roleIds.Length, userId);
        }

        public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await GetByIdAsync(userId);

            // Verify current password
            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                throw new UnauthorizedException("Current password is incorrect");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            _logger.LogInformation("Password changed for user {UserId}", userId);
        }

        public async Task<bool> UnlockUserAsync(Guid userId)
        {
            var user = await GetByIdAsync(userId);
            var result = await _userRepository.UnlockUserAsync(userId);
            
            if (result)
            {
                _logger.LogInformation("Unlocked user {UserId}", userId);
            }

            return result;
        }

        public async Task<UserDto> GetUserDetailsAsync(Guid id)
        {
            var user = await GetByIdAsync(id);
            var roles = await _roleRepository.GetByUserAsync(id);

            return new UserDto
            {
                Id = user.Id,
                OrganizationId = user.OrganizationId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                IsMfaEnabled = user.IsMfaEnabled,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt,
                RoleIds = roles.Select(r => r.Id).ToArray(),
                RoleNames = roles.Select(r => r.Name).ToArray()
            };
        }

        public async Task<IEnumerable<UserListDto>> GetUsersListAsync(Guid organizationId)
        {
            var users = await _userRepository.GetByOrganizationAsync(organizationId);

            return users.Select(u => new UserListDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt
            });
        }
    }
}