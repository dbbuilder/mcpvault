using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Core.MCP;
using MCPVault.Core.MCP.Models;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using System.Net.Http;
using System.Threading;

namespace MCPVault.Core.Tests.MCP
{
    public class McpProxyServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IMcpServerRepository> _mcpServerRepositoryMock;
        private readonly Mock<IKeyVaultService> _keyVaultServiceMock;
        private readonly Mock<IAuditService> _auditServiceMock;
        private readonly Mock<ILogger<McpProxyService>> _loggerMock;
        private readonly McpProxyService _proxyService;

        public McpProxyServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _mcpServerRepositoryMock = new Mock<IMcpServerRepository>();
            _keyVaultServiceMock = new Mock<IKeyVaultService>();
            _auditServiceMock = new Mock<IAuditService>();
            _loggerMock = new Mock<ILogger<McpProxyService>>();

            _proxyService = new McpProxyService(
                _httpClientFactoryMock.Object,
                _mcpServerRepositoryMock.Object,
                _keyVaultServiceMock.Object,
                _auditServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ExecuteToolAsync_WithValidRequest_ReturnsSuccessResult()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                Name = "Test MCP Server",
                ServerUrl = "https://mcp.example.com",
                IsActive = true,
                OrganizationId = orgId
            };

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "test-tool",
                Parameters = new Dictionary<string, object>
                {
                    { "param1", "value1" },
                    { "param2", 123 }
                }
            };

            var expectedResponse = new McpToolResponse
            {
                Success = true,
                Result = new { data = "test result" },
                ExecutionTime = 150
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _keyVaultServiceMock.Setup(x => x.GetCredentialsAsync(serverId, userId))
                .ReturnsAsync(new McpCredentials { ApiKey = "test-key" });

            var httpClient = new HttpClient(new MockHttpMessageHandler(expectedResponse));
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var result = await _proxyService.ExecuteToolAsync(request, userId, orgId);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Result);
            _auditServiceMock.Verify(x => x.LogMcpToolExecutionAsync(
                It.IsAny<Guid>(), serverId, "test-tool", true, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteToolAsync_WithInactiveServer_ThrowsInvalidOperationException()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                Name = "Inactive Server",
                IsActive = false,
                OrganizationId = orgId
            };

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "test-tool"
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _proxyService.ExecuteToolAsync(request, userId, orgId));
        }

        [Fact]
        public async Task ExecuteToolAsync_WithNonExistentServer_ThrowsNotFoundException()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "test-tool"
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync((McpServer)null);

            await Assert.ThrowsAsync<NotFoundException>(
                async () => await _proxyService.ExecuteToolAsync(request, userId, orgId));
        }

        [Fact]
        public async Task GetServerCapabilitiesAsync_ReturnsFilteredCapabilities()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                Name = "Test Server",
                IsActive = true,
                OrganizationId = orgId,
                Capabilities = "[\"read\", \"write\", \"delete\", \"admin\"]"
            };

            var userPermissions = new[] { "read", "write" };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            var result = await _proxyService.GetServerCapabilitiesAsync(serverId, userId, orgId, userPermissions);

            Assert.NotNull(result);
            Assert.Equal(2, result.AllowedTools.Count);
            Assert.Contains("read", result.AllowedTools);
            Assert.Contains("write", result.AllowedTools);
            Assert.DoesNotContain("delete", result.AllowedTools);
            Assert.DoesNotContain("admin", result.AllowedTools);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithValidRequest_ReturnsTrue()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                IsActive = true,
                OrganizationId = orgId
            };

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "allowed-tool"
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            var result = await _proxyService.ValidateRequestAsync(request, userId, orgId);

            Assert.True(result);
        }

        [Fact]
        public async Task ValidateRequestAsync_WithDifferentOrganization_ReturnsFalse()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();
            var differentOrgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                IsActive = true,
                OrganizationId = differentOrgId
            };

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "test-tool"
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            var result = await _proxyService.ValidateRequestAsync(request, userId, orgId);

            Assert.False(result);
        }

        [Fact]
        public async Task ProxyRequestAsync_AppliesRateLimiting()
        {
            var serverId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgId = Guid.NewGuid();

            var server = new McpServer
            {
                Id = serverId,
                Name = "Rate Limited Server",
                ServerUrl = "https://mcp.example.com",
                IsActive = true,
                OrganizationId = orgId,
                Configuration = "{\"rateLimitPerMinute\": 2}"
            };

            var request = new McpToolRequest
            {
                ServerId = serverId,
                ToolName = "test-tool"
            };

            _mcpServerRepositoryMock.Setup(x => x.GetByIdAsync(serverId))
                .ReturnsAsync(server);

            _keyVaultServiceMock.Setup(x => x.GetCredentialsAsync(serverId, userId))
                .ReturnsAsync(new McpCredentials { ApiKey = "test-key" });

            var httpClient = new HttpClient(new MockHttpMessageHandler(new McpToolResponse { Success = true }));
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            // First two requests should succeed
            await _proxyService.ExecuteToolAsync(request, userId, orgId);
            await _proxyService.ExecuteToolAsync(request, userId, orgId);

            // Third request within the same minute should be rate limited
            await Assert.ThrowsAsync<RateLimitExceededException>(
                async () => await _proxyService.ExecuteToolAsync(request, userId, orgId));
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly object _response;

        public MockHttpMessageHandler(object response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(_response))
            };
            return Task.FromResult(response);
        }
    }
}