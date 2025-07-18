using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Xunit;
using MCPVault.Core.Interfaces;
using MCPVault.Core.Services;
using MCPVault.Core.DTOs;
using MCPVault.Domain.Entities;
using MCPVault.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace MCPVault.Core.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockRoleRepository = new Mock<IRoleRepository>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockLogger = new Mock<ILogger<UserService>>();
            
            _userService = new UserService(
                _mockUserRepository.Object,
                _mockRoleRepository.Object,
                _mockAuthService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetByIdAsync_WithValidId_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(expectedUser);

            // Act
            var result = await _userService.GetByIdAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedUser.Id, result.Id);
            Assert.Equal(expectedUser.Email, result.Email);
        }

        [Fact]
        public async Task GetByIdAsync_WithInvalidId_ThrowsNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => 
                _userService.GetByIdAsync(userId));
        }

        [Fact]
        public async Task CreateAsync_WithValidData_CreatesUser()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var createRequest = new CreateUserRequest
            {
                Email = "newuser@example.com",
                Password = "SecurePassword123!",
                FirstName = "Jane",
                LastName = "Smith",
                OrganizationId = organizationId
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(createRequest.Email))
                .ReturnsAsync((User?)null);

            _mockUserRepository.Setup(r => r.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            // Act
            var result = await _userService.CreateAsync(createRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createRequest.Email, result.Email);
            Assert.Equal(createRequest.FirstName, result.FirstName);
            Assert.Equal(createRequest.LastName, result.LastName);
            Assert.Equal(organizationId, result.OrganizationId);
            _mockUserRepository.Verify(r => r.CreateAsync(It.Is<User>(u => 
                !string.IsNullOrEmpty(u.PasswordHash) && u.PasswordHash.StartsWith("$2"))), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WithExistingEmail_ThrowsConflictException()
        {
            // Arrange
            var createRequest = new CreateUserRequest
            {
                Email = "existing@example.com",
                Password = "SecurePassword123!",
                FirstName = "Jane",
                LastName = "Smith",
                OrganizationId = Guid.NewGuid()
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(createRequest.Email))
                .ReturnsAsync(new User { Email = createRequest.Email });

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() => 
                _userService.CreateAsync(createRequest));
        }

        [Fact]
        public async Task UpdateAsync_WithValidData_UpdatesUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe",
                OrganizationId = Guid.NewGuid()
            };

            var updateRequest = new UpdateUserRequest
            {
                FirstName = "Johnny",
                LastName = "Doe Jr.",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var result = await _userService.UpdateAsync(userId, updateRequest);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(updateRequest.FirstName, result.FirstName);
            Assert.Equal(updateRequest.LastName, result.LastName);
            Assert.Equal(updateRequest.IsActive, result.IsActive);
        }

        [Fact]
        public async Task UpdateAsync_ChangingEmailToExisting_ThrowsConflictException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            var updateRequest = new UpdateUserRequest
            {
                Email = "existing@example.com"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.GetByEmailAsync(updateRequest.Email))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = updateRequest.Email });

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() => 
                _userService.UpdateAsync(userId, updateRequest));
        }

        [Fact]
        public async Task DeleteAsync_WithValidId_DeletesUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.DeleteAsync(userId))
                .ReturnsAsync(true);

            // Act
            await _userService.DeleteAsync(userId);

            // Assert
            _mockUserRepository.Verify(r => r.DeleteAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetByOrganizationAsync_ReturnsUsers()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new User { Id = Guid.NewGuid(), OrganizationId = organizationId }
            };

            _mockUserRepository.Setup(r => r.GetByOrganizationAsync(organizationId))
                .ReturnsAsync(users);

            // Act
            var result = await _userService.GetByOrganizationAsync(organizationId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task AssignRolesAsync_WithValidRoles_AssignsRoles()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var roleIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            
            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            foreach (var roleId in roleIds)
            {
                _mockRoleRepository.Setup(r => r.GetByIdAsync(roleId))
                    .ReturnsAsync(new Role { Id = roleId, Name = $"Role_{roleId}" });
            }

            _mockRoleRepository.Setup(r => r.AssignRolesToUserAsync(userId, It.IsAny<Guid[]>()))
                .ReturnsAsync(true);

            // Act
            await _userService.AssignRolesAsync(userId, roleIds);

            // Assert
            _mockRoleRepository.Verify(r => r.AssignRolesToUserAsync(userId, roleIds), Times.Once);
        }

        [Fact]
        public async Task AssignRolesAsync_WithInvalidRole_ThrowsNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var validRoleId = Guid.NewGuid();
            var invalidRoleId = Guid.NewGuid();
            var roleIds = new[] { validRoleId, invalidRoleId };
            
            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            _mockRoleRepository.Setup(r => r.GetByIdAsync(validRoleId))
                .ReturnsAsync(new Role { Id = validRoleId, Name = "ValidRole" });

            _mockRoleRepository.Setup(r => r.GetByIdAsync(invalidRoleId))
                .ReturnsAsync((Role?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => 
                _userService.AssignRolesAsync(userId, roleIds));
        }

        [Fact]
        public async Task ChangePasswordAsync_WithValidCurrentPassword_ChangesPassword()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "CurrentPassword123!";
            var newPassword = "NewPassword123!";
            var hashedCurrentPassword = BCrypt.Net.BCrypt.HashPassword(currentPassword);

            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = hashedCurrentPassword
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            await _userService.ChangePasswordAsync(userId, currentPassword, newPassword);

            // Assert
            _mockUserRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => 
                !string.IsNullOrEmpty(u.PasswordHash) && u.PasswordHash.StartsWith("$2"))), Times.Once);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ThrowsUnauthorizedException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var currentPassword = "WrongPassword123!";
            var newPassword = "NewPassword123!";
            var hashedCurrentPassword = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");

            var existingUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = hashedCurrentPassword
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(existingUser);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedException>(() => 
                _userService.ChangePasswordAsync(userId, currentPassword, newPassword));
        }
    }
}