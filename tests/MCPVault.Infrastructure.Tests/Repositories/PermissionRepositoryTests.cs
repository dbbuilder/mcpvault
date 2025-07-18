using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Moq;
using Xunit;
using MCPVault.Core.Authorization.Models;
using MCPVault.Infrastructure.Repositories;

namespace MCPVault.Infrastructure.Tests.Repositories
{
    public class PermissionRepositoryTests
    {
        private readonly Mock<IDbConnection> _mockConnection;
        private readonly PermissionRepository _repository;

        public PermissionRepositoryTests()
        {
            _mockConnection = new Mock<IDbConnection>();
            _repository = new PermissionRepository(_mockConnection.Object);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetUserPermissionsAsync_WithValidUserId_ReturnsPermissions()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedPermissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "organizations",
                    Action = "read",
                    Effect = PermissionEffect.Allow
                },
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "users",
                    Action = "update",
                    Effect = PermissionEffect.Allow
                }
            };

            // Act
            var result = await _repository.GetUserPermissionsAsync(userId);

            // Assert
            Assert.NotNull(result);
            // Additional assertions would be made if database was available
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetRolePermissionsAsync_WithValidRoleId_ReturnsPermissions()
        {
            // Arrange
            var roleId = Guid.NewGuid();
            var expectedPermissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "mcpservers",
                    Action = "read",
                    Effect = PermissionEffect.Allow
                }
            };

            // Act
            var result = await _repository.GetRolePermissionsAsync(roleId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetByIdAsync_WithValidId_ReturnsPermission()
        {
            // Arrange
            var permissionId = Guid.NewGuid();
            var expectedPermission = new Permission
            {
                Id = permissionId,
                Resource = "organizations",
                Action = "create",
                Effect = PermissionEffect.Allow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.GetByIdAsync(permissionId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Arrange
            var invalidId = Guid.NewGuid();

            // Act
            var result = await _repository.GetByIdAsync(invalidId);

            // Assert
            Assert.Null(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task CreateAsync_WithValidPermission_ReturnsCreatedPermission()
        {
            // Arrange
            var permission = new Permission
            {
                Resource = "reports",
                Action = "export",
                Effect = PermissionEffect.Allow
            };

            // Act
            var result = await _repository.CreateAsync(permission);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(permission.Resource, result.Resource);
            Assert.Equal(permission.Action, result.Action);
            Assert.Equal(permission.Effect, result.Effect);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task UpdateAsync_WithValidPermission_ReturnsTrue()
        {
            // Arrange
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Resource = "reports",
                Action = "export",
                Effect = PermissionEffect.Deny,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.UpdateAsync(permission);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task DeleteAsync_WithValidId_ReturnsTrue()
        {
            // Arrange
            var permissionId = Guid.NewGuid();

            // Act
            var result = await _repository.DeleteAsync(permissionId);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task AssignPermissionToRoleAsync_WithValidIds_ReturnsTrue()
        {
            // Arrange
            var roleId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();

            // Act
            var result = await _repository.AssignPermissionToRoleAsync(roleId, permissionId);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task RemovePermissionFromRoleAsync_WithValidIds_ReturnsTrue()
        {
            // Arrange
            var roleId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();

            // Act
            var result = await _repository.RemovePermissionFromRoleAsync(roleId, permissionId);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task AssignPermissionToUserAsync_WithValidIds_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();
            var expiresAt = DateTime.UtcNow.AddDays(30);

            // Act
            var result = await _repository.AssignPermissionToUserAsync(userId, permissionId, expiresAt);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task RemovePermissionFromUserAsync_WithValidIds_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();

            // Act
            var result = await _repository.RemovePermissionFromUserAsync(userId, permissionId);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetResourcePermissionsAsync_WithValidResource_ReturnsPermissions()
        {
            // Arrange
            var resourceType = "organization";
            var resourceId = Guid.NewGuid();

            // Act
            var result = await _repository.GetResourcePermissionsAsync(resourceType, resourceId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task CreateResourcePermissionAsync_WithValidPermission_ReturnsCreated()
        {
            // Arrange
            var permission = new ResourcePermission
            {
                ResourceType = "mcpserver",
                ResourceId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Action = "execute",
                Effect = PermissionEffect.Allow
            };

            // Act
            var result = await _repository.CreateResourcePermissionAsync(permission);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task DeleteResourcePermissionAsync_WithValidId_ReturnsTrue()
        {
            // Arrange
            var permissionId = Guid.NewGuid();

            // Act
            var result = await _repository.DeleteResourcePermissionAsync(permissionId);

            // Assert
            Assert.True(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetPermissionsByResourceAsync_WithValidResource_ReturnsPermissions()
        {
            // Arrange
            var resource = "organizations";

            // Act
            var result = await _repository.GetPermissionsByResourceAsync(resource);

            // Assert
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetPermissionsByActionAsync_WithValidAction_ReturnsPermissions()
        {
            // Arrange
            var action = "delete";

            // Act
            var result = await _repository.GetPermissionsByActionAsync(action);

            // Assert
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetUserPermissionsAsync_ExcludesExpiredPermissions()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _repository.GetUserPermissionsAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.All(result, p => Assert.True(!p.Conditions?.ContainsKey("expiresAt") ?? true ||
                DateTime.Parse(p.Conditions["expiresAt"].ToString()) > DateTime.UtcNow));
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task AssignPermissionToRoleAsync_PreventsDuplicates()
        {
            // Arrange
            var roleId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();

            // Act - Assign twice
            await _repository.AssignPermissionToRoleAsync(roleId, permissionId);
            var secondResult = await _repository.AssignPermissionToRoleAsync(roleId, permissionId);

            // Assert - Second assignment should still return true (idempotent)
            Assert.True(secondResult);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task GetPermissionsByResourceAsync_WithWildcard_ReturnsMatchingPermissions()
        {
            // Arrange
            var resource = "organizations:*";

            // Act
            var result = await _repository.GetPermissionsByResourceAsync(resource);

            // Assert
            Assert.NotNull(result);
            Assert.All(result, p => Assert.StartsWith("organizations:", p.Resource));
        }
    }
}