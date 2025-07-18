using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Dapper;

namespace MCPVault.Infrastructure.Database
{
    public class PostgreSqlConnection : IDbConnection
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgreSqlConnection> _logger;

        public PostgreSqlConnection(IConfiguration configuration, ILogger<PostgreSqlConnection> logger)
        {
            _connectionString = configuration["ConnectionStrings:PostgreSQL"] 
                ?? throw new InvalidOperationException("PostgreSQL connection string not configured");
            _logger = logger;
        }

        public async Task<NpgsqlConnection> OpenConnectionAsync()
        {
            _logger.LogInformation("Opening database connection");
            
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            
            return connection;
        }

        public async Task<T> ExecuteScalarAsync<T>(string query, object? parameters = null)
        {
            using var connection = await OpenConnectionAsync();
            return await connection.ExecuteScalarAsync<T>(query, parameters);
        }

        public async Task<int> ExecuteAsync(string query, object? parameters = null)
        {
            using var connection = await OpenConnectionAsync();
            return await connection.ExecuteAsync(query, parameters);
        }

        public async Task ExecuteInTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> action)
        {
            using var connection = await OpenConnectionAsync();
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                await action(connection, transaction);
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction rolled back due to error");
                throw;
            }
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                using var connection = await OpenConnectionAsync();
                var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
                return result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return false;
            }
        }
    }
}