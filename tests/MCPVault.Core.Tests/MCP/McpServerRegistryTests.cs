using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP;
using MCPVault.Core.MCP.Models;
using MCPVault.Domain.Interfaces;

namespace MCPVault.Core.Tests.MCP
{
    public class McpServerRegistryTests
    {
        private readonly Mock<IMcpServerRepository> _mockRepository;
        private readonly Mock<IMcpClient> _mockMcpClient;
        private readonly Mock<IKeyVaultManager> _mockKeyVaultManager;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<McpServerRegistry>> _mockLogger;
        private readonly McpServerRegistry _registry;

        public McpServerRegistryTests()
        {
            _mockRepository = new Mock<IMcpServerRepository>();
            _mockMcpClient = new Mock<IMcpClient>();
            _mockKeyVaultManager = new Mock<IKeyVaultManager>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<McpServerRegistry>>();

            _registry = new McpServerRegistry(
                _mockRepository.Object,
                _mockMcpClient.Object,
                _mockKeyVaultManager.Object,
                _mockHttpClientFactory.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task RegisterServerAsync_WithValidRegistration_CreatesServer()
        {
            // Arrange
            var registration = new McpServerRegistration
            {
                Name = "Test Server",
                Description = "Test Description",
                Url = "https://test.example.com",
                ServerType = McpServerType.Standard,
                AuthType = McpAuthenticationType.ApiKey,
                Credentials = new McpCredentials { ApiKey = "test-key" }
            };

            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _mockRepository.Setup(r => r.GetByNameAsync(registration.Name, organizationId))
                .ReturnsAsync((McpServer?)null);

            _mockMcpClient.Setup(c => c.SendRequestAsync(It.IsAny<McpToolRequest>()))
                .ReturnsAsync(new McpToolResponse { Success = true });

            _mockRepository.Setup(r => r.CreateAsync(It.IsAny<McpServer>()))
                .ReturnsAsync((McpServer s) => s);

            // Act
            var result = await _registry.RegisterServerAsync(registration, organizationId, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(registration.Name, result.Name);
            Assert.Equal(registration.Description, result.Description);
            Assert.Equal(registration.Url, result.Url);
            Assert.Equal(organizationId, result.OrganizationId);
            Assert.Equal(userId, result.CreatedBy);

            _mockKeyVaultManager.Verify(k => k.StoreEncryptedSecretAsync(
                It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterServerAsync_WithDuplicateName_ThrowsException()
        {
            // Arrange
            var registration = new McpServerRegistration
            {
                Name = "Existing Server",
                Url = "https://test.example.com"
            };

            var existingServer = new McpServer { Name = registration.Name };
            _mockRepository.Setup(r => r.GetByNameAsync(registration.Name, It.IsAny<Guid>()))
                .ReturnsAsync(existingServer);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _registry.RegisterServerAsync(registration, Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task GetServerAsync_WithCredentials_LoadsFromKeyVault()
        {
            // Arrange
            var serverId = Guid.NewGuid();
            var server = new McpServer
            {
                Id = serverId,
                Name = "Test Server",
                AuthType = McpAuthenticationType.ApiKey
            };

            var credentials = new McpCredentials { ApiKey = "secret-key" };
            var credentialsJson = System.Text.Json.JsonSerializer.Serialize(credentials);

            _mockRepository.Setup(r => r.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _mockKeyVaultManager.Setup(k => k.GetDecryptedSecretAsync($"mcp-server-{serverId}"))
                .ReturnsAsync(credentialsJson);

            // Act
            var result = await _registry.GetServerAsync(serverId);

            // Assert
            Assert.NotNull(result.Credentials);
            Assert.Equal(credentials.ApiKey, result.Credentials.ApiKey);
        }

        [Fact]
        public async Task CheckServerHealthAsync_UpdatesServerStatus()
        {
            // Arrange
            var serverId = Guid.NewGuid();
            var server = new McpServer
            {
                Id = serverId,
                Name = "Test Server",
                ConnectionInfo = new McpConnectionInfo
                {
                    ServerUrl = "https://test.example.com",
                    TimeoutSeconds = 30
                }
            };

            _mockRepository.Setup(r => r.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _mockMcpClient.Setup(c => c.SendRequestAsync(It.IsAny<McpToolRequest>()))
                .ReturnsAsync(new McpToolResponse { Success = true });

            // Act
            var health = await _registry.CheckServerHealthAsync(serverId);

            // Assert
            Assert.Equal(McpServerStatus.Online, health.Status);
            Assert.True(health.IsHealthy);

            _mockRepository.Verify(r => r.UpdateAsync(It.Is<McpServer>(s => 
                s.Status == McpServerStatus.Online)), Times.Once);
            _mockRepository.Verify(r => r.RecordHealthCheckAsync(It.IsAny<McpServerHealth>()), Times.Once);
        }

        [Fact]
        public async Task RefreshServerCapabilitiesAsync_FetchesAndStoresCapabilities()
        {
            // Arrange
            var serverId = Guid.NewGuid();
            var server = new McpServer
            {
                Id = serverId,
                Name = "Test Server",
                ConnectionInfo = new McpConnectionInfo
                {
                    ServerUrl = "https://test.example.com"
                }
            };

            var toolsResponse = new McpToolResponse
            {
                Success = true,
                Result = new
                {
                    serverId = serverId,
                    serverName = "Test Server",
                    toolDefinitions = new Dictionary<string, object>
                    {
                        ["tool1"] = new { name = "tool1", description = "Tool 1" }
                    },
                    allowedTools = new[] { "tool1" }
                }
            };

            _mockRepository.Setup(r => r.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _mockMcpClient.Setup(c => c.SendRequestAsync(It.Is<McpToolRequest>(r => 
                r.ToolName == "capabilities")))
                .ReturnsAsync(toolsResponse);

            // Act
            var capabilities = await _registry.RefreshServerCapabilitiesAsync(serverId);

            // Assert
            Assert.NotNull(capabilities);
            Assert.Equal(serverId, capabilities.ServerId);
            Assert.Equal(server.Name, capabilities.ServerName);

            _mockRepository.Verify(r => r.UpdateAsync(It.Is<McpServer>(s => 
                s.Capabilities != null)), Times.Once);
        }

        [Fact]
        public async Task UpdateServerAsync_UpdatesServerProperties()
        {
            // Arrange
            var serverId = Guid.NewGuid();
            var server = new McpServer
            {
                Id = serverId,
                Name = "Old Name",
                Description = "Old Description",
                IsActive = true
            };

            var update = new McpServerUpdate
            {
                Name = "New Name",
                Description = "New Description",
                IsActive = false
            };

            _mockRepository.Setup(r => r.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<McpServer>()))
                .ReturnsAsync(true);

            // Act
            var result = await _registry.UpdateServerAsync(serverId, update);

            // Assert
            Assert.Equal(update.Name, result.Name);
            Assert.Equal(update.Description, result.Description);
            Assert.Equal(update.IsActive.Value, result.IsActive);
        }

        [Fact]
        public async Task DeleteServerAsync_RemovesServerAndCredentials()
        {
            // Arrange
            var serverId = Guid.NewGuid();

            _mockRepository.Setup(r => r.DeleteAsync(serverId))
                .ReturnsAsync(true);

            // Act
            var result = await _registry.DeleteServerAsync(serverId);

            // Assert
            Assert.True(result);
            _mockKeyVaultManager.Verify(k => k.DeleteSecretAsync($"mcp-server-{serverId}"), Times.Once);
        }

        [Fact]
        public async Task GetServerStatisticsAsync_ReturnsStatistics()
        {
            // Arrange
            var serverId = Guid.NewGuid();
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;

            var expectedStats = new McpServerStatistics
            {
                ServerId = serverId,
                TotalRequests = 100,
                SuccessfulRequests = 95,
                FailedRequests = 5,
                SuccessRate = 0.95
            };

            _mockRepository.Setup(r => r.GetStatisticsAsync(serverId, startDate, endDate))
                .ReturnsAsync(expectedStats);

            // Act
            var stats = await _registry.GetServerStatisticsAsync(serverId, startDate, endDate);

            // Assert
            Assert.Equal(expectedStats.TotalRequests, stats.TotalRequests);
            Assert.Equal(expectedStats.SuccessRate, stats.SuccessRate);
        }

        [Fact]
        public async Task ImportServersAsync_ImportsMultipleServers()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            var registrations = new List<McpServerRegistration>
            {
                new() { Name = "Server1", Url = "https://server1.com" },
                new() { Name = "Server2", Url = "https://server2.com" },
                new() { Name = "Server3", Url = "https://server3.com" }
            };

            _mockRepository.Setup(r => r.GetByNameAsync(It.IsAny<string>(), organizationId))
                .ReturnsAsync((McpServer?)null);

            _mockMcpClient.Setup(c => c.SendRequestAsync(It.IsAny<McpToolRequest>()))
                .ReturnsAsync(new McpToolResponse { Success = true });

            _mockRepository.Setup(r => r.CreateAsync(It.IsAny<McpServer>()))
                .ReturnsAsync((McpServer s) => s);

            // Act
            var imported = await _registry.ImportServersAsync(registrations, organizationId, userId);

            // Assert
            Assert.Equal(3, imported.Count);
            Assert.All(imported, s => Assert.NotNull(s.Id));
        }

        [Fact]
        public async Task SearchServersAsync_ReturnsMatchingServers()
        {
            // Arrange
            var searchTerm = "test";
            var organizationId = Guid.NewGuid();
            var expectedServers = new List<McpServer>
            {
                new() { Name = "Test Server 1" },
                new() { Name = "Test Server 2" }
            };

            _mockRepository.Setup(r => r.SearchAsync(searchTerm, organizationId))
                .ReturnsAsync(expectedServers);

            // Act
            var results = await _registry.SearchServersAsync(searchTerm, organizationId);

            // Assert
            Assert.Equal(expectedServers.Count, results.Count);
        }
    }
}