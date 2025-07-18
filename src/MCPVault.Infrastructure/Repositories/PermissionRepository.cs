using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MCPVault.Core.Interfaces;
using MCPVault.Core.Authorization.Models;
using System.Text.Json;

namespace MCPVault.Infrastructure.Repositories
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly IDbConnection _connection;

        public PermissionRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(Guid userId)
        {
            const string sql = @"
                SELECT DISTINCT
                    p.id AS Id,
                    p.resource AS Resource,
                    p.action AS Action,
                    p.effect AS Effect,
                    p.resource_id AS ResourceId,
                    p.conditions AS Conditions,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt
                FROM permissions p
                INNER JOIN user_permissions up ON p.id = up.permission_id
                WHERE up.user_id = @UserId
                    AND (up.expires_at IS NULL OR up.expires_at > NOW())
                
                UNION
                
                SELECT DISTINCT
                    p.id AS Id,
                    p.resource AS Resource,
                    p.action AS Action,
                    p.effect AS Effect,
                    p.resource_id AS ResourceId,
                    p.conditions AS Conditions,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt
                FROM permissions p
                INNER JOIN role_permissions rp ON p.id = rp.permission_id
                INNER JOIN user_roles ur ON rp.role_id = ur.role_id
                WHERE ur.user_id = @UserId
                ORDER BY Resource, Action";

            var permissions = await _connection.QueryAsync<PermissionDto>(sql, new { UserId = userId });
            return permissions.Select(MapToPermission);
        }

        public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(Guid roleId)
        {
            const string sql = @"
                SELECT
                    p.id AS Id,
                    p.resource AS Resource,
                    p.action AS Action,
                    p.effect AS Effect,
                    p.resource_id AS ResourceId,
                    p.conditions AS Conditions,
                    p.created_at AS CreatedAt,
                    p.updated_at AS UpdatedAt
                FROM permissions p
                INNER JOIN role_permissions rp ON p.id = rp.permission_id
                WHERE rp.role_id = @RoleId
                ORDER BY p.resource, p.action";

            var permissions = await _connection.QueryAsync<PermissionDto>(sql, new { RoleId = roleId });
            return permissions.Select(MapToPermission);
        }

        public async Task<Permission?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    resource AS Resource,
                    action AS Action,
                    effect AS Effect,
                    resource_id AS ResourceId,
                    conditions AS Conditions,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM permissions
                WHERE id = @Id";

            var dto = await _connection.QueryFirstOrDefaultAsync<PermissionDto>(sql, new { Id = id });
            return dto != null ? MapToPermission(dto) : null;
        }

        public async Task<Permission> CreateAsync(Permission permission)
        {
            const string sql = @"
                INSERT INTO permissions (resource, action, effect, resource_id, conditions, created_at, updated_at)
                VALUES (@Resource, @Action, @Effect, @ResourceId, @Conditions::jsonb, NOW(), NOW())
                RETURNING id, created_at, updated_at";

            var parameters = new
            {
                permission.Resource,
                permission.Action,
                Effect = permission.Effect.ToString(),
                permission.ResourceId,
                Conditions = permission.Conditions != null ? JsonSerializer.Serialize(permission.Conditions) : null
            };

            var result = await _connection.QuerySingleAsync<dynamic>(sql, parameters);
            
            permission.Id = result.id;
            permission.CreatedAt = result.created_at;
            permission.UpdatedAt = result.updated_at;

            return permission;
        }

        public async Task<bool> UpdateAsync(Permission permission)
        {
            const string sql = @"
                UPDATE permissions
                SET resource = @Resource,
                    action = @Action,
                    effect = @Effect,
                    resource_id = @ResourceId,
                    conditions = @Conditions::jsonb,
                    updated_at = NOW()
                WHERE id = @Id";

            var parameters = new
            {
                permission.Id,
                permission.Resource,
                permission.Action,
                Effect = permission.Effect.ToString(),
                permission.ResourceId,
                Conditions = permission.Conditions != null ? JsonSerializer.Serialize(permission.Conditions) : null
            };

            var rowsAffected = await _connection.ExecuteAsync(sql, parameters);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            const string sql = "DELETE FROM permissions WHERE id = @Id";
            var rowsAffected = await _connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId)
        {
            const string sql = @"
                INSERT INTO role_permissions (role_id, permission_id, assigned_at)
                VALUES (@RoleId, @PermissionId, NOW())
                ON CONFLICT (role_id, permission_id) DO NOTHING";

            var rowsAffected = await _connection.ExecuteAsync(sql, new { RoleId = roleId, PermissionId = permissionId });
            return true; // Always return true for idempotent operation
        }

        public async Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId)
        {
            const string sql = @"
                DELETE FROM role_permissions 
                WHERE role_id = @RoleId AND permission_id = @PermissionId";

            var rowsAffected = await _connection.ExecuteAsync(sql, new { RoleId = roleId, PermissionId = permissionId });
            return rowsAffected > 0;
        }

        public async Task<bool> AssignPermissionToUserAsync(Guid userId, Guid permissionId, DateTime? expiresAt = null)
        {
            const string sql = @"
                INSERT INTO user_permissions (user_id, permission_id, assigned_at, expires_at)
                VALUES (@UserId, @PermissionId, NOW(), @ExpiresAt)
                ON CONFLICT (user_id, permission_id) 
                DO UPDATE SET expires_at = @ExpiresAt, assigned_at = NOW()";

            var rowsAffected = await _connection.ExecuteAsync(sql, new 
            { 
                UserId = userId, 
                PermissionId = permissionId,
                ExpiresAt = expiresAt
            });
            return true;
        }

        public async Task<bool> RemovePermissionFromUserAsync(Guid userId, Guid permissionId)
        {
            const string sql = @"
                DELETE FROM user_permissions 
                WHERE user_id = @UserId AND permission_id = @PermissionId";

            var rowsAffected = await _connection.ExecuteAsync(sql, new { UserId = userId, PermissionId = permissionId });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<ResourcePermission>> GetResourcePermissionsAsync(string resourceType, Guid resourceId)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    resource_type AS ResourceType,
                    resource_id AS ResourceId,
                    user_id AS UserId,
                    role_id AS RoleId,
                    action AS Action,
                    effect AS Effect,
                    created_at AS CreatedAt,
                    expires_at AS ExpiresAt
                FROM resource_permissions
                WHERE resource_type = @ResourceType AND resource_id = @ResourceId
                    AND (expires_at IS NULL OR expires_at > NOW())
                ORDER BY created_at DESC";

            var permissions = await _connection.QueryAsync<ResourcePermissionDto>(sql, new 
            { 
                ResourceType = resourceType, 
                ResourceId = resourceId 
            });

            return permissions.Select(MapToResourcePermission);
        }

        public async Task<ResourcePermission> CreateResourcePermissionAsync(ResourcePermission permission)
        {
            const string sql = @"
                INSERT INTO resource_permissions 
                    (resource_type, resource_id, user_id, role_id, action, effect, created_at, expires_at)
                VALUES (@ResourceType, @ResourceId, @UserId, @RoleId, @Action, @Effect, NOW(), @ExpiresAt)
                RETURNING id, created_at";

            var parameters = new
            {
                permission.ResourceType,
                permission.ResourceId,
                permission.UserId,
                permission.RoleId,
                permission.Action,
                Effect = permission.Effect.ToString(),
                permission.ExpiresAt
            };

            var result = await _connection.QuerySingleAsync<dynamic>(sql, parameters);
            
            permission.Id = result.id;
            permission.CreatedAt = result.created_at;

            return permission;
        }

        public async Task<bool> DeleteResourcePermissionAsync(Guid id)
        {
            const string sql = "DELETE FROM resource_permissions WHERE id = @Id";
            var rowsAffected = await _connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<Permission>> GetPermissionsByResourceAsync(string resource)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    resource AS Resource,
                    action AS Action,
                    effect AS Effect,
                    resource_id AS ResourceId,
                    conditions AS Conditions,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM permissions
                WHERE resource = @Resource OR resource LIKE @ResourcePattern
                ORDER BY resource, action";

            var permissions = await _connection.QueryAsync<PermissionDto>(sql, new 
            { 
                Resource = resource,
                ResourcePattern = resource.Replace("*", "%")
            });

            return permissions.Select(MapToPermission);
        }

        public async Task<IEnumerable<Permission>> GetPermissionsByActionAsync(string action)
        {
            const string sql = @"
                SELECT
                    id AS Id,
                    resource AS Resource,
                    action AS Action,
                    effect AS Effect,
                    resource_id AS ResourceId,
                    conditions AS Conditions,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt
                FROM permissions
                WHERE action = @Action
                ORDER BY resource, action";

            var permissions = await _connection.QueryAsync<PermissionDto>(sql, new { Action = action });
            return permissions.Select(MapToPermission);
        }

        private Permission MapToPermission(PermissionDto dto)
        {
            return new Permission
            {
                Id = dto.Id,
                Resource = dto.Resource,
                Action = dto.Action,
                Effect = Enum.Parse<PermissionEffect>(dto.Effect),
                ResourceId = dto.ResourceId,
                Conditions = string.IsNullOrEmpty(dto.Conditions) 
                    ? null 
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(dto.Conditions),
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }

        private ResourcePermission MapToResourcePermission(ResourcePermissionDto dto)
        {
            return new ResourcePermission
            {
                Id = dto.Id,
                ResourceType = dto.ResourceType,
                ResourceId = dto.ResourceId,
                UserId = dto.UserId,
                RoleId = dto.RoleId,
                Action = dto.Action,
                Effect = Enum.Parse<PermissionEffect>(dto.Effect),
                CreatedAt = dto.CreatedAt,
                ExpiresAt = dto.ExpiresAt
            };
        }

        private class PermissionDto
        {
            public Guid Id { get; set; }
            public string Resource { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string Effect { get; set; } = string.Empty;
            public Guid? ResourceId { get; set; }
            public string? Conditions { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private class ResourcePermissionDto
        {
            public Guid Id { get; set; }
            public string ResourceType { get; set; } = string.Empty;
            public Guid ResourceId { get; set; }
            public Guid? UserId { get; set; }
            public Guid? RoleId { get; set; }
            public string Action { get; set; } = string.Empty;
            public string Effect { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
        }
    }
}