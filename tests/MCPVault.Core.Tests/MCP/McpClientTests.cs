using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using MCPVault.Core.MCP;

namespace MCPVault.Core.Tests.MCP
{
    public class McpClientTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<McpClient>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly McpClient _mcpClient;

        public McpClientTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<McpClient>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test-mcp-server.com")
            };
            
            _mockHttpClientFactory.Setup(f => f.CreateClient("MCP")).Returns(_httpClient);
            
            _mcpClient = new McpClient(_mockHttpClientFactory.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ConnectAsync_WithValidServer_EstablishesConnection()
        {
            // Arrange
            var serverUrl = "http://test-mcp-server.com";
            var connectionResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "1",
                Result = new Dictionary<string, object>
                {
                    ["protocolVersion"] = "1.0",
                    ["serverInfo"] = new Dictionary<string, object>
                    {
                        ["name"] = "Test MCP Server",
                        ["version"] = "1.0.0"
                    }
                }
            };

            SetupHttpResponse(connectionResponse);

            // Act
            var result = await _mcpClient.ConnectAsync(serverUrl);

            // Assert
            Assert.True(result);
            Assert.True(_mcpClient.IsConnected);
        }

        [Fact]
        public async Task DisconnectAsync_WhenConnected_ClosesConnection()
        {
            // Arrange
            await ConnectClient();

            // Act
            await _mcpClient.DisconnectAsync();

            // Assert
            Assert.False(_mcpClient.IsConnected);
        }

        [Fact]
        public async Task GetToolsAsync_WhenConnected_ReturnsAvailableTools()
        {
            // Arrange
            await ConnectClient();
            
            var toolsResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "2",
                Result = new Dictionary<string, object>
                {
                    ["tools"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["name"] = "calculator",
                            ["description"] = "Performs basic calculations",
                            ["inputSchema"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["expression"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Mathematical expression to evaluate"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            SetupHttpResponse(toolsResponse);

            // Act
            var tools = await _mcpClient.GetToolsAsync();

            // Assert
            Assert.NotNull(tools);
            Assert.Single(tools);
            Assert.Equal("calculator", tools[0].Name);
            Assert.Equal("Performs basic calculations", tools[0].Description);
        }

        [Fact]
        public async Task GetToolsAsync_WhenNotConnected_ThrowsInvalidOperationException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _mcpClient.GetToolsAsync());
        }

        [Fact]
        public async Task ExecuteToolAsync_WithValidTool_ReturnsResult()
        {
            // Arrange
            await ConnectClient();
            
            var toolName = "calculator";
            var arguments = new Dictionary<string, object>
            {
                ["expression"] = "2 + 2"
            };

            var executeResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "3",
                Result = new Dictionary<string, object>
                {
                    ["content"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = "4"
                        }
                    }
                }
            };

            SetupHttpResponse(executeResponse);

            // Act
            var result = await _mcpClient.ExecuteToolAsync(toolName, arguments);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("4", result.Content[0].Text);
        }

        [Fact]
        public async Task ExecuteToolAsync_WithError_ReturnsErrorResult()
        {
            // Arrange
            await ConnectClient();
            
            var toolName = "invalid-tool";
            var arguments = new Dictionary<string, object>();

            var errorResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "4",
                Error = new McpError
                {
                    Code = -32601,
                    Message = "Tool not found"
                }
            };

            SetupHttpResponse(errorResponse);

            // Act
            var result = await _mcpClient.ExecuteToolAsync(toolName, arguments);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("Tool not found", result.Error);
        }

        [Fact]
        public async Task GetResourcesAsync_WhenConnected_ReturnsAvailableResources()
        {
            // Arrange
            await ConnectClient();
            
            var resourcesResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "5",
                Result = new Dictionary<string, object>
                {
                    ["resources"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["uri"] = "file:///data/config.json",
                            ["name"] = "Configuration",
                            ["description"] = "Application configuration file",
                            ["mimeType"] = "application/json"
                        }
                    }
                }
            };

            SetupHttpResponse(resourcesResponse);

            // Act
            var resources = await _mcpClient.GetResourcesAsync();

            // Assert
            Assert.NotNull(resources);
            Assert.Single(resources);
            Assert.Equal("file:///data/config.json", resources[0].Uri);
            Assert.Equal("Configuration", resources[0].Name);
        }

        [Fact]
        public async Task ReadResourceAsync_WithValidUri_ReturnsContent()
        {
            // Arrange
            await ConnectClient();
            
            var resourceUri = "file:///data/config.json";
            var readResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "6",
                Result = new Dictionary<string, object>
                {
                    ["contents"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["uri"] = resourceUri,
                            ["mimeType"] = "application/json",
                            ["text"] = "{\"key\": \"value\"}"
                        }
                    }
                }
            };

            SetupHttpResponse(readResponse);

            // Act
            var content = await _mcpClient.ReadResourceAsync(resourceUri);

            // Assert
            Assert.NotNull(content);
            Assert.Equal(resourceUri, content.Uri);
            Assert.Equal("{\"key\": \"value\"}", content.Text);
        }

        [Fact]
        public async Task SendRequestAsync_WithTimeout_ThrowsTimeoutException()
        {
            // Arrange
            await ConnectClient();

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken token) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _mcpClient.ExecuteToolAsync("test", new Dictionary<string, object>(), TimeSpan.FromMilliseconds(100));
            });
        }

        [Fact]
        public async Task ConnectAsync_WithInvalidUrl_ReturnsFalse()
        {
            // Arrange
            var invalidUrl = "not-a-valid-url";

            // Act & Assert
            await Assert.ThrowsAsync<UriFormatException>(() => _mcpClient.ConnectAsync(invalidUrl));
        }

        private async Task ConnectClient()
        {
            var connectionResponse = new McpResponse
            {
                Jsonrpc = "2.0",
                Id = "1",
                Result = new Dictionary<string, object>
                {
                    ["protocolVersion"] = "1.0",
                    ["serverInfo"] = new Dictionary<string, object>
                    {
                        ["name"] = "Test MCP Server",
                        ["version"] = "1.0.0"
                    }
                }
            };

            SetupHttpResponse(connectionResponse);
            await _mcpClient.ConnectAsync("http://test-mcp-server.com");
        }

        private void SetupHttpResponse(McpResponse response)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(response, options);
            var httpResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }
    }
}