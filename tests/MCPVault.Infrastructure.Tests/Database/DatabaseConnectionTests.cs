using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Infrastructure.Database;
using Npgsql;

namespace MCPVault.Infrastructure.Tests.Database
{
    public class DatabaseConnectionTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<PostgreSqlConnection>> _loggerMock;
        private readonly PostgreSqlConnection _dbConnection;

        public DatabaseConnectionTests()
        {
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<PostgreSqlConnection>>();
            
            var connectionString = "Host=localhost;Database=mcpvault_test;Username=test;Password=test";
            _configurationMock.Setup(x => x["ConnectionStrings:PostgreSQL"])
                .Returns(connectionString);

            _dbConnection = new PostgreSqlConnection(_configurationMock.Object, _loggerMock.Object);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task OpenConnectionAsync_WithValidConnectionString_ReturnsOpenConnection()
        {
            var connection = await _dbConnection.OpenConnectionAsync();

            Assert.NotNull(connection);
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
            
            await connection.CloseAsync();
        }

        [Fact]
        public async Task OpenConnectionAsync_LogsConnectionAttempt()
        {
            try
            {
                var connection = await _dbConnection.OpenConnectionAsync();
                await connection.CloseAsync();
            }
            catch
            {
                // Connection might fail in test environment
            }

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Opening database connection")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task OpenConnectionAsync_WithInvalidConnectionString_ThrowsException()
        {
            var badConfigMock = new Mock<IConfiguration>();
            badConfigMock.Setup(x => x["ConnectionStrings:PostgreSQL"])
                .Returns("Host=invalid;Database=invalid;Username=invalid;Password=invalid");

            var badConnection = new PostgreSqlConnection(badConfigMock.Object, _loggerMock.Object);

            await Assert.ThrowsAnyAsync<Exception>(
                async () => await badConnection.OpenConnectionAsync()
            );
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task ExecuteAsync_WithValidQuery_ReturnsExpectedResult()
        {
            var testQuery = "SELECT 1 + 1 AS result";

            var result = await _dbConnection.ExecuteScalarAsync<int>(testQuery);

            Assert.Equal(2, result);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task ExecuteInTransactionAsync_WithSuccessfulOperation_CommitsTransaction()
        {
            var executed = false;

            await _dbConnection.ExecuteInTransactionAsync(async (connection, transaction) =>
            {
                executed = true;
                var command = new NpgsqlCommand("SELECT 1", connection, transaction);
                await command.ExecuteScalarAsync();
            });

            Assert.True(executed);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task ExecuteInTransactionAsync_WithException_RollsBackTransaction()
        {
            var exception = new Exception("Test exception");

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await _dbConnection.ExecuteInTransactionAsync(async (connection, transaction) =>
                {
                    throw exception;
                });
            });

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Transaction rolled back")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public void GetConnectionString_ReturnsConfiguredConnectionString()
        {
            var expectedConnectionString = "Host=localhost;Database=mcpvault_test;Username=test;Password=test";

            var connectionString = _dbConnection.GetConnectionString();

            Assert.Equal(expectedConnectionString, connectionString);
        }

        [Fact(Skip = "Requires PostgreSQL database to be running")]
        public async Task HealthCheckAsync_WithValidConnection_ReturnsTrue()
        {
            var isHealthy = await _dbConnection.HealthCheckAsync();

            Assert.True(isHealthy);
        }

        [Fact]
        public async Task HealthCheckAsync_WithInvalidConnection_ReturnsFalse()
        {
            var badConfigMock = new Mock<IConfiguration>();
            badConfigMock.Setup(x => x["ConnectionStrings:PostgreSQL"])
                .Returns("Host=nonexistent;Database=invalid;Username=invalid;Password=invalid");

            var badConnection = new PostgreSqlConnection(badConfigMock.Object, _loggerMock.Object);

            var isHealthy = await badConnection.HealthCheckAsync();

            Assert.False(isHealthy);
        }
    }
}