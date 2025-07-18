using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;

namespace MCPVault.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnection _connection;

        public UserRepository(IDbConnection connection)
        {
            _connection = connection;
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            const string sql = @"
                SELECT id, email, password_hash as PasswordHash, first_name as FirstName, 
                       last_name as LastName, organization_id as OrganizationId, 
                       is_active as IsActive, is_mfa_enabled as IsMfaEnabled,
                       mfa_secret as MfaSecret, failed_login_attempts as FailedLoginAttempts,
                       locked_until as LockedUntil, last_login_at as LastLoginAt,
                       created_at as CreatedAt, updated_at as UpdatedAt
                FROM users
                WHERE id = @Id";

            return await _connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT id, email, password_hash as PasswordHash, first_name as FirstName, 
                       last_name as LastName, organization_id as OrganizationId, 
                       is_active as IsActive, is_mfa_enabled as IsMfaEnabled,
                       mfa_secret as MfaSecret, failed_login_attempts as FailedLoginAttempts,
                       locked_until as LockedUntil, last_login_at as LastLoginAt,
                       created_at as CreatedAt, updated_at as UpdatedAt
                FROM users
                WHERE email = @Email";

            return await _connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<IEnumerable<User>> GetByOrganizationAsync(Guid organizationId)
        {
            const string sql = @"
                SELECT id, email, password_hash as PasswordHash, first_name as FirstName, 
                       last_name as LastName, organization_id as OrganizationId, 
                       is_active as IsActive, is_mfa_enabled as IsMfaEnabled,
                       mfa_secret as MfaSecret, failed_login_attempts as FailedLoginAttempts,
                       locked_until as LockedUntil, last_login_at as LastLoginAt,
                       created_at as CreatedAt, updated_at as UpdatedAt
                FROM users
                WHERE organization_id = @OrganizationId
                ORDER BY email";

            return await _connection.QueryAsync<User>(sql, new { OrganizationId = organizationId });
        }

        public async Task<User> CreateAsync(User user)
        {
            const string sql = @"
                INSERT INTO users (id, email, password_hash, first_name, last_name, 
                                  organization_id, is_active, is_mfa_enabled, 
                                  created_at, updated_at)
                VALUES (@Id, @Email, @PasswordHash, @FirstName, @LastName, 
                        @OrganizationId, @IsActive, @IsMfaEnabled, 
                        @CreatedAt, @UpdatedAt)
                RETURNING *";

            var result = await _connection.QuerySingleAsync<User>(sql, user);
            return result;
        }

        public async Task<bool> UpdateAsync(User user)
        {
            const string sql = @"
                UPDATE users
                SET email = @Email,
                    password_hash = @PasswordHash,
                    first_name = @FirstName,
                    last_name = @LastName,
                    is_active = @IsActive,
                    is_mfa_enabled = @IsMfaEnabled,
                    mfa_secret = @MfaSecret,
                    failed_login_attempts = @FailedLoginAttempts,
                    locked_until = @LockedUntil,
                    last_login_at = @LastLoginAt,
                    updated_at = @UpdatedAt
                WHERE id = @Id";

            var affected = await _connection.ExecuteAsync(sql, user);
            return affected > 0;
        }

        public async Task<bool> IncrementFailedLoginAttemptsAsync(Guid userId)
        {
            const string sql = @"
                UPDATE users
                SET failed_login_attempts = failed_login_attempts + 1,
                    updated_at = NOW()
                WHERE id = @UserId";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId });
            return affected > 0;
        }

        public async Task<bool> ResetFailedLoginAttemptsAsync(Guid userId)
        {
            const string sql = @"
                UPDATE users
                SET failed_login_attempts = 0,
                    updated_at = NOW()
                WHERE id = @UserId";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId });
            return affected > 0;
        }

        public async Task<bool> LockUserAsync(Guid userId, DateTime lockedUntil)
        {
            const string sql = @"
                UPDATE users
                SET locked_until = @LockedUntil,
                    updated_at = NOW()
                WHERE id = @UserId";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId, LockedUntil = lockedUntil });
            return affected > 0;
        }

        public async Task<bool> UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt)
        {
            const string sql = @"
                UPDATE users
                SET last_login_at = @LastLoginAt,
                    updated_at = NOW()
                WHERE id = @UserId";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId, LastLoginAt = lastLoginAt });
            return affected > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            const string sql = @"
                DELETE FROM users
                WHERE id = @Id";

            var affected = await _connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task<bool> UnlockUserAsync(Guid id)
        {
            const string sql = @"
                UPDATE users
                SET locked_until = NULL,
                    failed_login_attempts = 0,
                    updated_at = NOW()
                WHERE id = @Id";

            var affected = await _connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task SetMfaSecretAsync(Guid userId, string secret)
        {
            const string sql = @"
                UPDATE users
                SET mfa_secret = @Secret,
                    is_mfa_enabled = true,
                    updated_at = NOW()
                WHERE id = @UserId";

            await _connection.ExecuteAsync(sql, new { UserId = userId, Secret = secret });
        }

        public async Task DisableMfaAsync(Guid userId)
        {
            const string sql = @"
                UPDATE users
                SET mfa_secret = NULL,
                    is_mfa_enabled = false,
                    updated_at = NOW()
                WHERE id = @UserId";

            await _connection.ExecuteAsync(sql, new { UserId = userId });
        }

        public async Task<string[]> GetBackupCodesAsync(Guid userId)
        {
            const string sql = @"
                SELECT code
                FROM user_backup_codes
                WHERE user_id = @UserId AND is_used = false";

            var codes = await _connection.QueryAsync<string>(sql, new { UserId = userId });
            return codes.ToArray();
        }

        public async Task SaveBackupCodesAsync(Guid userId, string[] codes)
        {
            const string deleteSql = @"
                DELETE FROM user_backup_codes
                WHERE user_id = @UserId";

            const string insertSql = @"
                INSERT INTO user_backup_codes (user_id, code, is_used, created_at)
                VALUES (@UserId, @Code, false, NOW())";

            using var transaction = _connection.BeginTransaction();
            try
            {
                await _connection.ExecuteAsync(deleteSql, new { UserId = userId }, transaction);
                
                foreach (var code in codes)
                {
                    await _connection.ExecuteAsync(insertSql, new { UserId = userId, Code = code }, transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UseBackupCodeAsync(Guid userId, string code)
        {
            const string sql = @"
                UPDATE user_backup_codes
                SET is_used = true,
                    used_at = NOW()
                WHERE user_id = @UserId AND code = @Code AND is_used = false";

            var affected = await _connection.ExecuteAsync(sql, new { UserId = userId, Code = code });
            return affected > 0;
        }
    }
}