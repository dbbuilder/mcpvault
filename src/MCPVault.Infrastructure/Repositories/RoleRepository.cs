using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;

namespace MCPVault.Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly IDbConnection _connection;

        public RoleRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<Role?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT id, name, description, organization_id as OrganizationId, 
                       is_system as IsSystem, created_at as CreatedAt, updated_at as UpdatedAt
                FROM roles
                WHERE id = @Id";

            return await _connection.QueryFirstOrDefaultAsync<Role>(sql, new { Id = id });
        }

        public async Task<Role?> GetByNameAsync(string name)
        {
            const string sql = @"
                SELECT id, name, description, organization_id as OrganizationId, 
                       is_system as IsSystem, created_at as CreatedAt, updated_at as UpdatedAt
                FROM roles
                WHERE name = @Name";

            return await _connection.QueryFirstOrDefaultAsync<Role>(sql, new { Name = name });
        }

        public async Task<IEnumerable<Role>> GetAllAsync()
        {
            const string sql = @"
                SELECT id, name, description, organization_id as OrganizationId, 
                       is_system as IsSystem, created_at as CreatedAt, updated_at as UpdatedAt
                FROM roles
                ORDER BY name";

            return await _connection.QueryAsync<Role>(sql);
        }

        public async Task<IEnumerable<Role>> GetByOrganizationAsync(Guid organizationId)
        {
            const string sql = @"
                SELECT id, name, description, organization_id as OrganizationId, 
                       is_system as IsSystem, created_at as CreatedAt, updated_at as UpdatedAt
                FROM roles
                WHERE organization_id = @OrganizationId OR is_system = true
                ORDER BY name";

            return await _connection.QueryAsync<Role>(sql, new { OrganizationId = organizationId });
        }

        public async Task<IEnumerable<Role>> GetByUserAsync(Guid userId)
        {
            const string sql = @"
                SELECT r.id, r.name, r.description, r.organization_id as OrganizationId, 
                       r.is_system as IsSystem, r.created_at as CreatedAt, r.updated_at as UpdatedAt
                FROM roles r
                JOIN user_roles ur ON r.id = ur.role_id
                WHERE ur.user_id = @UserId
                ORDER BY r.name";

            return await _connection.QueryAsync<Role>(sql, new { UserId = userId });
        }

        public async Task<Role> CreateAsync(Role role)
        {
            const string sql = @"
                INSERT INTO roles (id, name, description, organization_id, is_system, created_at, updated_at)
                VALUES (@Id, @Name, @Description, @OrganizationId, @IsSystem, @CreatedAt, @UpdatedAt)
                RETURNING *";

            role.Id = Guid.NewGuid();
            role.CreatedAt = DateTime.UtcNow;
            role.UpdatedAt = DateTime.UtcNow;

            var result = await _connection.QuerySingleAsync<Role>(sql, role);
            return result;
        }

        public async Task<bool> UpdateAsync(Role role)
        {
            const string sql = @"
                UPDATE roles
                SET name = @Name,
                    description = @Description,
                    updated_at = @UpdatedAt
                WHERE id = @Id";

            role.UpdatedAt = DateTime.UtcNow;
            var affected = await _connection.ExecuteAsync(sql, role);
            return affected > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            const string sql = @"
                DELETE FROM roles
                WHERE id = @Id AND is_system = false";

            var affected = await _connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task<bool> AssignRolesToUserAsync(Guid userId, Guid[] roleIds)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Remove existing roles
                const string deleteSql = @"
                    DELETE FROM user_roles
                    WHERE user_id = @UserId";

                await _connection.ExecuteAsync(deleteSql, new { UserId = userId }, transaction);

                // Assign new roles
                const string insertSql = @"
                    INSERT INTO user_roles (user_id, role_id, assigned_at, assigned_by)
                    VALUES (@UserId, @RoleId, @AssignedAt, @AssignedBy)";

                foreach (var roleId in roleIds)
                {
                    await _connection.ExecuteAsync(insertSql, new 
                    { 
                        UserId = userId, 
                        RoleId = roleId,
                        AssignedAt = DateTime.UtcNow,
                        AssignedBy = Guid.Empty // TODO: Get from current user context
                    }, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> RemoveRolesFromUserAsync(Guid userId, Guid[] roleIds)
        {
            const string sql = @"
                DELETE FROM user_roles
                WHERE user_id = @UserId AND role_id = ANY(@RoleIds)";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId, RoleIds = roleIds });
            return affected > 0;
        }

        public async Task<bool> AssignPermissionsToRoleAsync(Guid roleId, Guid[] permissionIds)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                // Remove existing permissions
                const string deleteSql = @"
                    DELETE FROM role_permissions
                    WHERE role_id = @RoleId";

                await _connection.ExecuteAsync(deleteSql, new { RoleId = roleId }, transaction);

                // Assign new permissions
                const string insertSql = @"
                    INSERT INTO role_permissions (role_id, permission_id, granted_at, granted_by)
                    VALUES (@RoleId, @PermissionId, @GrantedAt, @GrantedBy)";

                foreach (var permissionId in permissionIds)
                {
                    await _connection.ExecuteAsync(insertSql, new 
                    { 
                        RoleId = roleId, 
                        PermissionId = permissionId,
                        GrantedAt = DateTime.UtcNow,
                        GrantedBy = Guid.Empty // TODO: Get from current user context
                    }, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> RemovePermissionsFromRoleAsync(Guid roleId, Guid[] permissionIds)
        {
            const string sql = @"
                DELETE FROM role_permissions
                WHERE role_id = @RoleId AND permission_id = ANY(@PermissionIds)";

            var affected = await _connection.ExecuteAsync(sql, new { RoleId = roleId, PermissionIds = permissionIds });
            return affected > 0;
        }
    }
}