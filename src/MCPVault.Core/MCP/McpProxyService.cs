using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;
using MCPVault.Core.MCP.Mappers;
using MCPVault.Domain.Entities;
using System.Collections.Concurrent;

namespace MCPVault.Core.MCP
{
    public class McpProxyService : IMcpProxyService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Domain.Interfaces.IMcpServerRepository _mcpServerRepository;
        private readonly IKeyVaultService _keyVaultService;
        private readonly IAuditService _auditService;
        private readonly ILogger<McpProxyService> _logger;
        private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache;

        public McpProxyService(
            IHttpClientFactory httpClientFactory,
            Domain.Interfaces.IMcpServerRepository mcpServerRepository,
            IKeyVaultService keyVaultService,
            IAuditService auditService,
            ILogger<McpProxyService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _mcpServerRepository = mcpServerRepository;
            _keyVaultService = keyVaultService;
            _auditService = auditService;
            _logger = logger;
            _rateLimitCache = new ConcurrentDictionary<string, RateLimitInfo>();
        }

        public async Task<McpToolResponse> ExecuteToolAsync(
            McpToolRequest request, 
            Guid userId, 
            Guid organizationId)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Get server details first
                var domainServer = await _mcpServerRepository.GetByIdAsync(request.ServerId);
                if (domainServer == null)
                {
                    throw new NotFoundException($"MCP server {request.ServerId} not found");
                }
                
                var server = McpServerMapper.ToCoreModel(domainServer);

                if (!server.IsActive)
                {
                    throw new InvalidOperationException($"MCP server {server.Name} is not active");
                }

                // Validate request after we know server exists
                var isValid = await ValidateRequestAsync(request, userId, organizationId);
                if (!isValid)
                {
                    throw new UnauthorizedException("Request validation failed");
                }

                // Check rate limits
                await CheckRateLimitAsync(server, userId);

                // Get credentials
                var credentials = await _keyVaultService.GetCredentialsAsync(server.Id, userId);
                if (credentials == null)
                {
                    throw new UnauthorizedException("No credentials found for this server");
                }

                // Execute the request
                var response = await ProxyRequestAsync(server, request, credentials);

                // Log success
                await _auditService.LogMcpToolExecutionAsync(
                    userId, 
                    server.Id, 
                    request.ToolName, 
                    true,
                    $"Execution time: {response.ExecutionTime}ms");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing MCP tool {Tool} on server {Server}", 
                    request.ToolName, request.ServerId);

                // Log failure
                await _auditService.LogMcpToolExecutionAsync(
                    userId, 
                    request.ServerId, 
                    request.ToolName, 
                    false,
                    ex.Message);

                throw;
            }
        }

        public async Task<McpServerCapabilities> GetServerCapabilitiesAsync(
            Guid serverId, 
            Guid userId, 
            Guid organizationId,
            string[] userPermissions)
        {
            var server = await _mcpServerRepository.GetByIdAsync(serverId);
            if (server == null)
            {
                throw new NotFoundException($"MCP server {serverId} not found");
            }
            
            var coreServer = McpServerMapper.ToCoreModel(server);

            if (coreServer.OrganizationId != organizationId)
            {
                throw new UnauthorizedException("Access denied to this server");
            }

            var allCapabilities = coreServer.Capabilities?.AllowedTools ?? new List<string>();
            
            // Filter capabilities based on user permissions
            var allowedCapabilities = allCapabilities
                .Where(cap => userPermissions.Contains(cap))
                .ToList();

            return new McpServerCapabilities
            {
                ServerId = coreServer.Id,
                ServerName = coreServer.Name,
                AllowedTools = allowedCapabilities,
                LastUpdated = coreServer.UpdatedAt
            };
        }

        public async Task<bool> ValidateRequestAsync(
            McpToolRequest request, 
            Guid userId, 
            Guid organizationId)
        {
            try
            {
                var domainServer = await _mcpServerRepository.GetByIdAsync(request.ServerId);
                if (domainServer == null)
                {
                    return false;
                }
                
                var server = McpServerMapper.ToCoreModel(domainServer);

                // Validate organization access
                if (server.OrganizationId != organizationId)
                {
                    _logger.LogWarning("User {UserId} attempted to access server {ServerId} from different organization", 
                        userId, request.ServerId);
                    return false;
                }

                // Validate server is active
                if (!server.IsActive)
                {
                    return false;
                }

                // Additional validation logic can be added here
                // - Check user permissions for specific tools
                // - Validate request parameters
                // - Check security policies

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating MCP request");
                return false;
            }
        }

        private async Task<McpToolResponse> ProxyRequestAsync(
            Core.MCP.Models.McpServer server, 
            McpToolRequest request, 
            McpCredentials credentials)
        {
            var httpClient = _httpClientFactory.CreateClient("MCP");
            
            // Configure request
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{server.ConnectionInfo.ServerUrl}/execute/{request.ToolName}");
            
            // Add authentication
            if (!string.IsNullOrEmpty(credentials.BearerToken))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.BearerToken);
            }
            else if (!string.IsNullOrEmpty(credentials.ApiKey))
            {
                httpRequest.Headers.Add("X-API-Key", credentials.ApiKey);
            }

            // Add custom headers
            if (credentials.CustomHeaders != null)
            {
                foreach (var header in credentials.CustomHeaders)
                {
                    httpRequest.Headers.Add(header.Key, header.Value);
                }
            }

            // Add request body
            var jsonContent = JsonSerializer.Serialize(request.Parameters ?? new Dictionary<string, object>());
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Execute request
            var startTime = DateTime.UtcNow;
            var httpResponse = await httpClient.SendAsync(httpRequest);
            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Handle response
            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new McpServerException($"Server returned {httpResponse.StatusCode}: {responseContent}", 
                    (int)httpResponse.StatusCode);
            }

            var response = JsonSerializer.Deserialize<McpToolResponse>(responseContent) ?? new McpToolResponse();
            response.ExecutionTime = (long)executionTime;

            return response;
        }

        private async Task CheckRateLimitAsync(Core.MCP.Models.McpServer server, Guid userId)
        {
            var config = server.Metadata ?? new Dictionary<string, string>();
            
            if (config.TryGetValue("rateLimitPerMinute", out var rateLimitStr) && 
                int.TryParse(rateLimitStr, out var rateLimit))
            {
                var key = $"{server.Id}:{userId}";
                var now = DateTime.UtcNow;

                var rateLimitInfo = _rateLimitCache.AddOrUpdate(key, 
                    k => new RateLimitInfo { Count = 1, WindowStart = now },
                    (k, existing) =>
                    {
                        if (now.Subtract(existing.WindowStart).TotalMinutes >= 1)
                        {
                            existing.Count = 1;
                            existing.WindowStart = now;
                        }
                        else
                        {
                            existing.Count++;
                        }
                        return existing;
                    });

                if (rateLimitInfo.Count > rateLimit)
                {
                    var retryAfter = 60 - (int)now.Subtract(rateLimitInfo.WindowStart).TotalSeconds;
                    throw new RateLimitExceededException(
                        $"Rate limit exceeded. Limit: {rateLimit} requests per minute", 
                        retryAfter);
                }
            }

            await Task.CompletedTask;
        }

        private class RateLimitInfo
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    public interface IMcpProxyService
    {
        Task<McpToolResponse> ExecuteToolAsync(McpToolRequest request, Guid userId, Guid organizationId);
        Task<McpServerCapabilities> GetServerCapabilitiesAsync(Guid serverId, Guid userId, Guid organizationId, string[] userPermissions);
        Task<bool> ValidateRequestAsync(McpToolRequest request, Guid userId, Guid organizationId);
    }
}