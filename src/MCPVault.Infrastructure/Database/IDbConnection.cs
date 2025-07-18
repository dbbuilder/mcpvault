using System;
using System.Threading.Tasks;
using Npgsql;

namespace MCPVault.Infrastructure.Database
{
    public interface IDbConnection
    {
        Task<NpgsqlConnection> OpenConnectionAsync();
        Task<T> ExecuteScalarAsync<T>(string query, object? parameters = null);
        Task<int> ExecuteAsync(string query, object? parameters = null);
        Task ExecuteInTransactionAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> action);
        string GetConnectionString();
        Task<bool> HealthCheckAsync();
    }
}