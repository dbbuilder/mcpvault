using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Core.Authorization;
using MCPVault.Core.Authorization.Models;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using System.Linq;

namespace MCPVault.Core.Tests.Authorization
{
    public class AuthorizationServiceTests
    {
        private readonly Mock<IPermissionRepository> _mockPermissionRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ILogger<AuthorizationService>> _mockLogger;
        private readonly IAuthorizationService _authorizationService;

        public AuthorizationServiceTests()
        {
            _mockPermissionRepository = new Mock<IPermissionRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockLogger = new Mock<ILogger<AuthorizationService>>();
            _authorizationService = new AuthorizationService(
                _mockPermissionRepository.Object,
                _mockUserRepository.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task AuthorizeAsync_WithValidPermission_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();
            var resource = "organizations";
            var action = "read";

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                OrganizationId = organizationId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = resource,
                    Action = action,
                    Effect = PermissionEffect.Allow
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act
            var result = await _authorizationService.AuthorizeAsync(userId, resource, action);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AuthorizeAsync_WithDenyPermission_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var resource = "organizations";
            var action = "delete";

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = resource,
                    Action = action,
                    Effect = PermissionEffect.Deny
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act
            var result = await _authorizationService.AuthorizeAsync(userId, resource, action);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AuthorizeAsync_WithNoPermission_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var resource = "mcpservers";
            var action = "create";

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(new List<Permission>());

            // Act
            var result = await _authorizationService.AuthorizeAsync(userId, resource, action);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AuthorizeAsync_WithWildcardPermission_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var resource = "mcpservers";
            var action = "read";

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "*",
                    Action = "*",
                    Effect = PermissionEffect.Allow
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act
            var result = await _authorizationService.AuthorizeAsync(userId, resource, action);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AuthorizeResourceAsync_WithResourcePermission_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();
            var resourceType = "organization";
            var action = "update";

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = resourceType,
                    Action = action,
                    ResourceId = resourceId,
                    Effect = PermissionEffect.Allow
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act
            var result = await _authorizationService.AuthorizeResourceAsync(
                userId, resourceType, resourceId, action);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetUserPermissionsAsync_ReturnsAllPermissions()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();

            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var userPermissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "users",
                    Action = "read",
                    Effect = PermissionEffect.Allow
                }
            };

            var rolePermissions = new List<Permission>
            {
                new Permission
                {
                    Id = Guid.NewGuid(),
                    Resource = "organizations",
                    Action = "read",
                    Effect = PermissionEffect.Allow
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(userPermissions);

            _mockPermissionRepository.Setup(r => r.GetRolePermissionsAsync(roleId))
                .ReturnsAsync(rolePermissions);

            // Act
            var result = await _authorizationService.GetUserPermissionsAsync(userId);

            // Assert
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task EvaluatePoliciesAsync_WithMatchingPolicy_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var context = new AuthorizationContext
            {
                UserId = userId,
                Resource = "mcpservers",
                Action = "execute",
                ResourceId = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                Claims = new Dictionary<string, string>
                {
                    { "department", "engineering" }
                }
            };

            var policies = new List<AuthorizationPolicy>
            {
                new AuthorizationPolicy
                {
                    Name = "EngineeringMcpAccess",
                    RequiredClaims = new Dictionary<string, string>
                    {
                        { "department", "engineering" }
                    },
                    AllowedResources = new[] { "mcpservers" },
                    AllowedActions = new[] { "execute" }
                }
            };

            // Act
            var result = await _authorizationService.EvaluatePoliciesAsync(context, policies);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AuthorizeWithClaimsAsync_ValidatesClaimsCorrectly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new Claim("sub", userId.ToString()),
                new Claim("role", "Admin"),
                new Claim("org", Guid.NewGuid().ToString())
            };

            var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Resource = "organizations",
                    Action = "manage",
                    Effect = PermissionEffect.Allow
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act
            var result = await _authorizationService.AuthorizeWithClaimsAsync(
                claimsPrincipal, "organizations", "manage");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AuthorizeAsync_WithConditionBasedPermission_EvaluatesCondition()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();

            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                OrganizationId = organizationId
            };
            user.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

            var permissions = new List<Permission>
            {
                new Permission
                {
                    Resource = "reports",
                    Action = "export",
                    Effect = PermissionEffect.Allow,
                    Conditions = new Dictionary<string, object>
                    {
                        { "timeOfDay", "business_hours" },
                        { "ipRange", "10.0.0.0/8" }
                    }
                }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockPermissionRepository.Setup(r => r.GetUserPermissionsAsync(userId))
                .ReturnsAsync(permissions);

            // Act - Test during business hours
            var result = await _authorizationService.AuthorizeAsync(
                userId, "reports", "export", 
                new Dictionary<string, object> 
                { 
                    { "currentTime", 10 }, // 10 AM UTC - within business hours
                    { "sourceIp", "10.0.0.1" }
                });

            // Assert
            Assert.True(result);
        }
    }
}