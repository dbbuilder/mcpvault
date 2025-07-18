using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;

namespace MCPVault.Core.MCP
{
    public class McpClient : IMcpClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<McpClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private HttpClient? _httpClient;
        private string? _serverUrl;
        private int _requestId = 0;
        private McpConnectionInfo? _connectionInfo;
        private McpCredentials? _credentials;

        public bool IsConnected { get; private set; }

        public McpClient(IHttpClientFactory httpClientFactory, ILogger<McpClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<bool> ConnectAsync(string serverUrl)
        {
            // Validate URL - let UriFormatException bubble up
            var uri = new Uri(serverUrl);
            
            try
            {
                _serverUrl = serverUrl;
                _httpClient = _httpClientFactory.CreateClient("MCP");
                _httpClient.BaseAddress = uri;

                // Send initialization request
                var initRequest = new McpRequest
                {
                    Id = GetNextRequestId(),
                    Method = "initialize",
                    Params = new Dictionary<string, object>
                    {
                        ["protocolVersion"] = "1.0",
                        ["clientInfo"] = new Dictionary<string, object>
                        {
                            ["name"] = "MCPVault",
                            ["version"] = "1.0.0"
                        }
                    }
                };

                var response = await SendRequestAsync(initRequest);
                
                if (response.Error != null)
                {
                    _logger.LogError("Failed to connect to MCP server: {Error}", response.Error.Message);
                    return false;
                }

                IsConnected = true;
                _logger.LogInformation("Connected to MCP server at {ServerUrl}", serverUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to MCP server at {ServerUrl}", serverUrl);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (IsConnected && _httpClient != null)
            {
                try
                {
                    var request = new McpRequest
                    {
                        Id = GetNextRequestId(),
                        Method = "shutdown"
                    };

                    await SendRequestAsync(request);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during disconnect");
                }
                finally
                {
                    IsConnected = false;
                    _httpClient?.Dispose();
                    _httpClient = null;
                }
            }
        }

        public async Task<List<McpTool>> GetToolsAsync()
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "tools/list"
            };

            var response = await SendRequestAsync(request);
            
            if (response.Error != null)
            {
                throw new McpException($"Failed to get tools: {response.Error.Message}");
            }

            var tools = new List<McpTool>();
            
            if (response.Result?.TryGetValue("tools", out var toolsObj) == true)
            {
                if (toolsObj is JsonElement toolsElement)
                {
                    foreach (var toolElement in toolsElement.EnumerateArray())
                    {
                        var tool = new McpTool
                        {
                            Name = toolElement.GetProperty("name").GetString() ?? "",
                            Description = toolElement.GetProperty("description").GetString() ?? "",
                            InputSchema = JsonSerializer.Deserialize<Dictionary<string, object>>(toolElement.GetProperty("inputSchema").GetRawText(), _jsonOptions) ?? new()
                        };
                        tools.Add(tool);
                    }
                }
                else if (toolsObj is object[] toolsArray)
                {
                    foreach (Dictionary<string, object> toolDict in toolsArray)
                    {
                        var tool = new McpTool
                        {
                            Name = toolDict.GetValueOrDefault("name")?.ToString() ?? "",
                            Description = toolDict.GetValueOrDefault("description")?.ToString() ?? "",
                            InputSchema = toolDict.GetValueOrDefault("inputSchema") as Dictionary<string, object> ?? new()
                        };
                        tools.Add(tool);
                    }
                }
            }

            return tools;
        }

        public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, TimeSpan? timeout = null)
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "tools/call",
                Params = new Dictionary<string, object>
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments
                }
            };

            var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
            var response = await SendRequestAsync(request, cts.Token);
            
            if (response.Error != null)
            {
                return new McpToolResult
                {
                    Success = false,
                    Error = response.Error.Message
                };
            }

            var result = new McpToolResult { Success = true };
            
            if (response.Result?.TryGetValue("content", out var contentObj) == true && contentObj is JsonElement contentElement)
            {
                foreach (var item in contentElement.EnumerateArray())
                {
                    var content = JsonSerializer.Deserialize<McpContent>(item.GetRawText(), _jsonOptions);
                    if (content != null)
                    {
                        result.Content.Add(content);
                    }
                }
            }

            return result;
        }

        public async Task<List<McpResource>> GetResourcesAsync()
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "resources/list"
            };

            var response = await SendRequestAsync(request);
            
            if (response.Error != null)
            {
                throw new McpException($"Failed to get resources: {response.Error.Message}");
            }

            var resources = new List<McpResource>();
            
            if (response.Result?.TryGetValue("resources", out var resourcesObj) == true && resourcesObj is JsonElement resourcesElement)
            {
                foreach (var resourceElement in resourcesElement.EnumerateArray())
                {
                    var resource = JsonSerializer.Deserialize<McpResource>(resourceElement.GetRawText(), _jsonOptions);
                    if (resource != null)
                    {
                        resources.Add(resource);
                    }
                }
            }

            return resources;
        }

        public async Task<McpResourceContent> ReadResourceAsync(string uri)
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "resources/read",
                Params = new Dictionary<string, object>
                {
                    ["uri"] = uri
                }
            };

            var response = await SendRequestAsync(request);
            
            if (response.Error != null)
            {
                throw new McpException($"Failed to read resource: {response.Error.Message}");
            }

            if (response.Result?.TryGetValue("contents", out var contentsObj) == true && 
                contentsObj is JsonElement contentsElement && 
                contentsElement.GetArrayLength() > 0)
            {
                var content = JsonSerializer.Deserialize<McpResourceContent>(contentsElement[0].GetRawText(), _jsonOptions);
                if (content != null)
                {
                    return content;
                }
            }

            throw new McpException("No content returned for resource");
        }

        public async Task<List<McpPrompt>> GetPromptsAsync()
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "prompts/list"
            };

            var response = await SendRequestAsync(request);
            
            if (response.Error != null)
            {
                throw new McpException($"Failed to get prompts: {response.Error.Message}");
            }

            var prompts = new List<McpPrompt>();
            
            if (response.Result?.TryGetValue("prompts", out var promptsObj) == true && promptsObj is JsonElement promptsElement)
            {
                foreach (var promptElement in promptsElement.EnumerateArray())
                {
                    var prompt = JsonSerializer.Deserialize<McpPrompt>(promptElement.GetRawText(), _jsonOptions);
                    if (prompt != null)
                    {
                        prompts.Add(prompt);
                    }
                }
            }

            return prompts;
        }

        public async Task<McpPromptResult> GetPromptAsync(string name, Dictionary<string, object> arguments)
        {
            EnsureConnected();

            var request = new McpRequest
            {
                Id = GetNextRequestId(),
                Method = "prompts/get",
                Params = new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["arguments"] = arguments
                }
            };

            var response = await SendRequestAsync(request);
            
            if (response.Error != null)
            {
                throw new McpException($"Failed to get prompt: {response.Error.Message}");
            }

            var result = JsonSerializer.Deserialize<McpPromptResult>(JsonSerializer.Serialize(response.Result, _jsonOptions), _jsonOptions);
            return result ?? new McpPromptResult();
        }

        private async Task<McpResponse> SendRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("Not connected to MCP server");
            }

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var httpResponse = await _httpClient.PostAsync("", content, cancellationToken);
                httpResponse.EnsureSuccessStatusCode();

                var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                var response = JsonSerializer.Deserialize<McpResponse>(responseJson, _jsonOptions);
                
                return response ?? new McpResponse { Error = new McpError { Code = -1, Message = "Invalid response" } };
            }
            catch (TaskCanceledException)
            {
                throw new TaskCanceledException("Request timed out");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed");
                return new McpResponse 
                { 
                    Error = new McpError 
                    { 
                        Code = -32603, 
                        Message = $"HTTP request failed: {ex.Message}" 
                    } 
                };
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to MCP server");
            }
        }

        private string GetNextRequestId()
        {
            return (++_requestId).ToString();
        }

        public void ConfigureConnection(McpConnectionInfo connectionInfo, McpCredentials? credentials = null)
        {
            _connectionInfo = connectionInfo;
            _credentials = credentials;

            // Create new HTTP client with configured connection info
            _httpClient = _httpClientFactory.CreateClient("MCP");
            
            var uriBuilder = new UriBuilder
            {
                Scheme = connectionInfo.Protocol,
                Host = new Uri(connectionInfo.ServerUrl).Host,
                Port = connectionInfo.Port ?? (connectionInfo.UseSsl ? 443 : 80),
                Path = connectionInfo.BasePath ?? ""
            };
            
            _httpClient.BaseAddress = uriBuilder.Uri;
            _httpClient.Timeout = TimeSpan.FromSeconds(connectionInfo.TimeoutSeconds);

            // Configure authentication
            if (credentials != null)
            {
                ConfigureAuthentication(_httpClient, credentials);
            }
        }

        public async Task<McpToolResponse> SendRequestAsync(McpToolRequest request)
        {
            if (_httpClient == null && _connectionInfo != null)
            {
                ConfigureConnection(_connectionInfo, _credentials);
            }

            if (_httpClient == null)
            {
                throw new InvalidOperationException("Client not configured. Call ConfigureConnection first.");
            }

            var mcpRequest = new McpRequest
            {
                Id = request.RequestId ?? GetNextRequestId(),
                Method = $"tools/{request.ToolName}",
                Params = request.Parameters ?? new Dictionary<string, object>()
            };

            // Add custom headers if provided
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var response = await SendRequestAsync(mcpRequest);
                
                var toolResponse = new McpToolResponse
                {
                    Success = response.Error == null,
                    Result = response.Result,
                    Error = response.Error?.Message,
                    ErrorCode = response.Error?.Code.ToString(),
                    ExecutionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };

                return toolResponse;
            }
            catch (Exception ex)
            {
                return new McpToolResponse
                {
                    Success = false,
                    Error = ex.Message,
                    ErrorCode = "CLIENT_ERROR",
                    ExecutionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }
        }

        private void ConfigureAuthentication(HttpClient client, McpCredentials credentials)
        {
            client.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrWhiteSpace(credentials.BearerToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", credentials.BearerToken);
            }
            else if (!string.IsNullOrWhiteSpace(credentials.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", credentials.ApiKey);
            }
            else if (!string.IsNullOrWhiteSpace(credentials.ClientId) && 
                     !string.IsNullOrWhiteSpace(credentials.ClientSecret))
            {
                var authValue = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{credentials.ClientId}:{credentials.ClientSecret}"));
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Basic", authValue);
            }

            // Add custom headers
            if (credentials.CustomHeaders != null)
            {
                foreach (var header in credentials.CustomHeaders)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }
    }
}