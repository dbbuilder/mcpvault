using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using MCPVault.Infrastructure.Database;
using Dapper;

namespace MCPVault.Infrastructure.Repositories
{
    public class OrganizationRepository : IOrganizationRepository
    {
        private readonly IDbConnection _dbConnection;
        private readonly ILogger<OrganizationRepository> _logger;

        public OrganizationRepository(IDbConnection dbConnection, ILogger<OrganizationRepository> logger)
        {
            _dbConnection = dbConnection;
            _logger = logger;
        }

        public async Task<Organization?> GetByIdAsync(Guid id)
        {
            const string query = @"
                SELECT id, organization_id as OrganizationId, name, slug, is_active as IsActive, 
                       settings, created_at as CreatedAt, updated_at as UpdatedAt
                FROM auth.organizations
                WHERE id = @id";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var result = await connection.QuerySingleOrDefaultAsync<Organization>(query, new { id });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organization by ID: {OrganizationId}", id);
                throw;
            }
        }

        public async Task<Organization?> GetBySlugAsync(string slug)
        {
            const string query = @"
                SELECT id, organization_id as OrganizationId, name, slug, is_active as IsActive, 
                       settings, created_at as CreatedAt, updated_at as UpdatedAt
                FROM auth.organizations
                WHERE slug = @slug";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var result = await connection.QuerySingleOrDefaultAsync<Organization>(query, new { slug });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organization by slug: {Slug}", slug);
                throw;
            }
        }

        public async Task<IEnumerable<Organization>> GetAllAsync()
        {
            const string query = @"
                SELECT id, organization_id as OrganizationId, name, slug, is_active as IsActive, 
                       settings, created_at as CreatedAt, updated_at as UpdatedAt
                FROM auth.organizations
                ORDER BY name";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var result = await connection.QueryAsync<Organization>(query);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all organizations");
                throw;
            }
        }

        public async Task<IEnumerable<Organization>> GetActiveAsync()
        {
            const string query = @"
                SELECT id, organization_id as OrganizationId, name, slug, is_active as IsActive, 
                       settings, created_at as CreatedAt, updated_at as UpdatedAt
                FROM auth.organizations
                WHERE is_active = true
                ORDER BY name";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var result = await connection.QueryAsync<Organization>(query);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active organizations");
                throw;
            }
        }

        public async Task<Organization> CreateAsync(Organization organization)
        {
            const string query = @"
                INSERT INTO auth.organizations (name, slug, is_active, settings, created_at, updated_at)
                VALUES (@Name, @Slug, @IsActive, @Settings, @CreatedAt, @UpdatedAt)
                RETURNING id";

            try
            {
                organization.Id = Guid.NewGuid();
                organization.CreatedAt = DateTime.UtcNow;
                organization.UpdatedAt = organization.CreatedAt;

                using var connection = await _dbConnection.OpenConnectionAsync();
                var id = await connection.ExecuteScalarAsync<Guid>(query, organization);
                organization.Id = id;
                
                _logger.LogInformation("Created organization: {OrganizationId}", organization.Id);
                return organization;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organization: {OrganizationName}", organization.Name);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(Organization organization)
        {
            const string query = @"
                UPDATE auth.organizations
                SET name = @Name,
                    slug = @Slug,
                    is_active = @IsActive,
                    settings = @Settings,
                    updated_at = @UpdatedAt
                WHERE id = @Id";

            try
            {
                organization.UpdatedAt = DateTime.UtcNow;

                using var connection = await _dbConnection.OpenConnectionAsync();
                var affectedRows = await connection.ExecuteAsync(query, organization);
                
                var success = affectedRows > 0;
                if (success)
                {
                    _logger.LogInformation("Updated organization: {OrganizationId}", organization.Id);
                }
                else
                {
                    _logger.LogWarning("Organization not found for update: {OrganizationId}", organization.Id);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating organization: {OrganizationId}", organization.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            const string query = @"
                DELETE FROM auth.organizations
                WHERE id = @id";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var affectedRows = await connection.ExecuteAsync(query, new { id });
                
                var success = affectedRows > 0;
                if (success)
                {
                    _logger.LogInformation("Deleted organization: {OrganizationId}", id);
                }
                else
                {
                    _logger.LogWarning("Organization not found for deletion: {OrganizationId}", id);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organization: {OrganizationId}", id);
                throw;
            }
        }

        public async Task<bool> ExistsBySlugAsync(string slug)
        {
            const string query = @"
                SELECT COUNT(1)
                FROM auth.organizations
                WHERE slug = @slug";

            try
            {
                using var connection = await _dbConnection.OpenConnectionAsync();
                var count = await connection.ExecuteScalarAsync<int>(query, new { slug });
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking organization slug existence: {Slug}", slug);
                throw;
            }
        }
    }
}