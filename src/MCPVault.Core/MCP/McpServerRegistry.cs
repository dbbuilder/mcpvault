using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;
using MCPVault.Core.MCP.Mappers;
using MCPVault.Domain.Interfaces;

namespace MCPVault.Core.MCP
{
    public class McpServerRegistry : IMcpServerRegistry
    {
        private readonly IMcpServerRepository _serverRepository;
        private readonly IMcpClient _mcpClient;
        private readonly IKeyVaultManager _keyVaultManager;
        private readonly ILogger<McpServerRegistry> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JsonSerializerOptions _jsonOptions;

        public McpServerRegistry(
            IMcpServerRepository serverRepository,
            IMcpClient mcpClient,
            IKeyVaultManager keyVaultManager,
            IHttpClientFactory httpClientFactory,
            ILogger<McpServerRegistry> logger)
        {
            _serverRepository = serverRepository;
            _mcpClient = mcpClient;
            _keyVaultManager = keyVaultManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<McpServer> RegisterServerAsync(McpServerRegistration registration, Guid organizationId, Guid userId)
        {
            // Validate registration
            if (string.IsNullOrWhiteSpace(registration.Name))
                throw new ArgumentException("Server name is required");

            if (string.IsNullOrWhiteSpace(registration.Url))
                throw new ArgumentException("Server URL is required");

            // Check if server with same name already exists
            var existingDomain = await _serverRepository.GetByNameAsync(registration.Name, organizationId);
            var existing = existingDomain != null ? McpServerMapper.ToCoreModel(existingDomain) : null;
            if (existing != null)
                throw new InvalidOperationException($"Server with name '{registration.Name}' already exists");

            // Create server entity
            var server = new McpServer
            {
                Id = Guid.NewGuid(),
                Name = registration.Name,
                Description = registration.Description,
                Url = registration.Url,
                ServerType = registration.ServerType,
                AuthType = registration.AuthType,
                Status = McpServerStatus.Unknown,
                Metadata = registration.Metadata,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrganizationId = organizationId,
                CreatedBy = userId,
                ConnectionInfo = new McpConnectionInfo
                {
                    ServerId = Guid.NewGuid(),
                    ServerUrl = registration.Url,
                    Protocol = registration.UseSsl ? "https" : "http",
                    Port = registration.Port,
                    BasePath = registration.BasePath,
                    TimeoutSeconds = registration.TimeoutSeconds,
                    UseSsl = registration.UseSsl
                }
            };

            // Store credentials securely if provided
            if (registration.Credentials != null)
            {
                await StoreServerCredentialsAsync(server.Id, registration.Credentials);
                server.Credentials = registration.Credentials; // Keep in memory for immediate use
            }

            // Test connection and get capabilities
            try
            {
                var health = await TestServerConnectionAsync(server);
                server.Status = health.Status;
                server.LastHealthCheck = health.CheckedAt;

                if (health.IsHealthy)
                {
                    server.Capabilities = await FetchServerCapabilitiesAsync(server);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to test connection for new server {ServerName}", server.Name);
                server.Status = McpServerStatus.Error;
            }

            // Save to repository
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.CreateAsync(domainServer);

            _logger.LogInformation("Registered MCP server {ServerName} with ID {ServerId}", server.Name, server.Id);
            return server;
        }

        public async Task<McpServer> GetServerAsync(Guid serverId)
        {
            var domainServer = await _serverRepository.GetByIdAsync(serverId);
            if (domainServer == null)
                throw new KeyNotFoundException($"Server with ID {serverId} not found");

            var server = McpServerMapper.ToCoreModel(domainServer);

            // Load credentials from secure storage
            if (server.AuthType != McpAuthenticationType.None)
            {
                server.Credentials = await LoadServerCredentialsAsync(serverId);
            }

            return server;
        }

        public async Task<McpServer> GetServerByNameAsync(string name, Guid organizationId)
        {
            var domainServer = await _serverRepository.GetByNameAsync(name, organizationId);
            if (domainServer == null)
                throw new KeyNotFoundException($"Server with name '{name}' not found");

            var server = McpServerMapper.ToCoreModel(domainServer);

            // Load credentials from secure storage
            if (server.AuthType != McpAuthenticationType.None)
            {
                server.Credentials = await LoadServerCredentialsAsync(server.Id);
            }

            return server;
        }

        public async Task<List<McpServer>> GetServersAsync(McpServerFilter filter)
        {
            var domainFilter = McpServerMapper.ToDomainFilter(filter);
            var domainServers = await _serverRepository.GetServersAsync(domainFilter);

            // Don't load credentials for list operations
            return domainServers.Select(McpServerMapper.ToCoreModel).ToList();
        }

        public async Task<McpServer> UpdateServerAsync(Guid serverId, McpServerUpdate update)
        {
            var server = await GetServerAsync(serverId);

            // Apply updates
            if (!string.IsNullOrWhiteSpace(update.Name))
                server.Name = update.Name;

            if (!string.IsNullOrWhiteSpace(update.Description))
                server.Description = update.Description;

            if (!string.IsNullOrWhiteSpace(update.Url))
            {
                server.Url = update.Url;
                server.ConnectionInfo.ServerUrl = update.Url;
            }

            if (update.AuthType.HasValue)
                server.AuthType = update.AuthType.Value;

            if (update.Metadata != null)
                server.Metadata = update.Metadata;

            if (update.IsActive.HasValue)
                server.IsActive = update.IsActive.Value;

            if (update.TimeoutSeconds.HasValue)
                server.ConnectionInfo.TimeoutSeconds = update.TimeoutSeconds.Value;

            server.UpdatedAt = DateTime.UtcNow;

            // Update credentials if provided
            if (update.Credentials != null)
            {
                await StoreServerCredentialsAsync(serverId, update.Credentials);
                server.Credentials = update.Credentials;
            }

            // Update in repository
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);

            _logger.LogInformation("Updated MCP server {ServerId}", serverId);
            return server;
        }

        public async Task<bool> DeleteServerAsync(Guid serverId)
        {
            var result = await _serverRepository.DeleteAsync(serverId);
            
            if (result)
            {
                // Delete credentials from secure storage
                await DeleteServerCredentialsAsync(serverId);
                _logger.LogInformation("Deleted MCP server {ServerId}", serverId);
            }

            return result;
        }

        public async Task<bool> ActivateServerAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            server.IsActive = true;
            server.UpdatedAt = DateTime.UtcNow;
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);
            
            _logger.LogInformation("Activated MCP server {ServerId}", serverId);
            return true;
        }

        public async Task<bool> DeactivateServerAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            server.IsActive = false;
            server.UpdatedAt = DateTime.UtcNow;
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);
            
            _logger.LogInformation("Deactivated MCP server {ServerId}", serverId);
            return true;
        }

        public async Task<McpServerCapabilities> GetServerCapabilitiesAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            
            if (server.Capabilities == null || 
                server.Capabilities.LastUpdated < DateTime.UtcNow.AddHours(-1))
            {
                // Refresh if not cached or older than 1 hour
                return await RefreshServerCapabilitiesAsync(serverId);
            }

            return server.Capabilities;
        }

        public async Task<McpServerCapabilities> RefreshServerCapabilitiesAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            var capabilities = await FetchServerCapabilitiesAsync(server);
            
            server.Capabilities = capabilities;
            server.UpdatedAt = DateTime.UtcNow;
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);

            return capabilities;
        }

        public async Task<List<McpToolDefinition>> GetServerToolsAsync(Guid serverId)
        {
            var capabilities = await GetServerCapabilitiesAsync(serverId);
            return capabilities.ToolDefinitions.Values.ToList();
        }

        public async Task<McpToolDefinition?> GetToolDefinitionAsync(Guid serverId, string toolName)
        {
            var capabilities = await GetServerCapabilitiesAsync(serverId);
            return capabilities.ToolDefinitions.GetValueOrDefault(toolName);
        }

        public async Task<McpServerHealth> CheckServerHealthAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            var health = await TestServerConnectionAsync(server);

            // Update server status
            server.Status = health.Status;
            server.LastHealthCheck = health.CheckedAt;
            server.UpdatedAt = DateTime.UtcNow;
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);

            // Record health check
            var domainHealth = McpServerMapper.ToDomainHealth(health);
            await _serverRepository.RecordHealthCheckAsync(domainHealth);

            return health;
        }

        public async Task<Dictionary<Guid, McpServerHealth>> CheckAllServersHealthAsync(Guid organizationId)
        {
            var domainServers = await _serverRepository.GetByOrganizationAsync(organizationId);
            var servers = domainServers.Select(McpServerMapper.ToCoreModel).ToList();
            var healthChecks = new Dictionary<Guid, McpServerHealth>();

            var tasks = servers.Select(async server =>
            {
                try
                {
                    // Load credentials for health check
                    if (server.AuthType != McpAuthenticationType.None)
                    {
                        server.Credentials = await LoadServerCredentialsAsync(server.Id);
                    }

                    var health = await TestServerConnectionAsync(server);
                    healthChecks[server.Id] = health;

                    // Update server status
                    server.Status = health.Status;
                    server.LastHealthCheck = health.CheckedAt;
                    var domainServer = McpServerMapper.ToDomainEntity(server);
                    await _serverRepository.UpdateAsync(domainServer);
                    var domainHealth = McpServerMapper.ToDomainHealth(health);
                    await _serverRepository.RecordHealthCheckAsync(domainHealth);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check health for server {ServerId}", server.Id);
                    healthChecks[server.Id] = new McpServerHealth
                    {
                        ServerId = server.Id,
                        Status = McpServerStatus.Error,
                        CheckedAt = DateTime.UtcNow,
                        ErrorMessage = ex.Message
                    };
                }
            });

            await Task.WhenAll(tasks);
            return healthChecks;
        }

        public async Task<List<McpServerHealth>> GetServerHealthHistoryAsync(Guid serverId, DateTime since)
        {
            var domainHistory = await _serverRepository.GetHealthHistoryAsync(serverId, since);
            return domainHistory.Select(McpServerMapper.ToCoreHealth).ToList();
        }

        public async Task<bool> SetServerStatusAsync(Guid serverId, McpServerStatus status, string? reason = null)
        {
            var server = await GetServerAsync(serverId);
            server.Status = status;
            server.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrWhiteSpace(reason))
            {
                if (server.Metadata == null)
                    server.Metadata = new Dictionary<string, string>();
                server.Metadata["StatusReason"] = reason;
            }

            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);
            
            _logger.LogInformation("Set server {ServerId} status to {Status}", serverId, status);
            return true;
        }

        public async Task<McpServerStatistics> GetServerStatisticsAsync(Guid serverId, DateTime startDate, DateTime endDate)
        {
            var domainStats = await _serverRepository.GetStatisticsAsync(serverId, startDate, endDate);
            return McpServerMapper.ToCoreStatistics(domainStats);
        }

        public async Task<Dictionary<Guid, McpServerStatistics>> GetOrganizationStatisticsAsync(Guid organizationId, DateTime startDate, DateTime endDate)
        {
            var domainServers = await _serverRepository.GetByOrganizationAsync(organizationId);
            var statistics = new Dictionary<Guid, McpServerStatistics>();

            foreach (var server in domainServers)
            {
                var domainStats = await _serverRepository.GetStatisticsAsync(server.Id, startDate, endDate);
                statistics[server.Id] = McpServerMapper.ToCoreStatistics(domainStats);
            }

            return statistics;
        }

        public async Task RecordToolExecutionAsync(Guid serverId, string toolName, bool success, long executionTimeMs)
        {
            await _serverRepository.RecordToolExecutionAsync(serverId, toolName, success, executionTimeMs);
        }

        public async Task UpdateServerCredentialsAsync(Guid serverId, McpCredentials credentials)
        {
            var server = await GetServerAsync(serverId);
            await StoreServerCredentialsAsync(serverId, credentials);
            server.Credentials = credentials;
            server.UpdatedAt = DateTime.UtcNow;
            var domainServer = McpServerMapper.ToDomainEntity(server);
            await _serverRepository.UpdateAsync(domainServer);
        }

        public async Task<bool> ValidateServerCredentialsAsync(Guid serverId)
        {
            var server = await GetServerAsync(serverId);
            
            try
            {
                var health = await TestServerConnectionAsync(server);
                return health.IsHealthy;
            }
            catch
            {
                return false;
            }
        }

        public async Task<DateTime?> GetCredentialExpirationAsync(Guid serverId)
        {
            var credentials = await LoadServerCredentialsAsync(serverId);
            return credentials?.ExpiresAt;
        }

        public async Task<List<McpServer>> ImportServersAsync(List<McpServerRegistration> servers, Guid organizationId, Guid userId)
        {
            var imported = new List<McpServer>();

            foreach (var registration in servers)
            {
                try
                {
                    var server = await RegisterServerAsync(registration, organizationId, userId);
                    imported.Add(server);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import server {ServerName}", registration.Name);
                }
            }

            return imported;
        }

        public async Task<int> BulkUpdateServersAsync(List<Guid> serverIds, McpServerUpdate update)
        {
            var updated = 0;

            foreach (var serverId in serverIds)
            {
                try
                {
                    await UpdateServerAsync(serverId, update);
                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update server {ServerId}", serverId);
                }
            }

            return updated;
        }

        public async Task<int> BulkDeleteServersAsync(List<Guid> serverIds)
        {
            var deleted = 0;

            foreach (var serverId in serverIds)
            {
                try
                {
                    if (await DeleteServerAsync(serverId))
                        deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete server {ServerId}", serverId);
                }
            }

            return deleted;
        }

        public async Task<List<McpServer>> SearchServersAsync(string searchTerm, Guid organizationId)
        {
            var domainServers = await _serverRepository.SearchAsync(searchTerm, organizationId);
            return domainServers.Select(McpServerMapper.ToCoreModel).ToList();
        }

        public async Task<List<McpServer>> GetServersByTypeAsync(McpServerType type, Guid organizationId)
        {
            var filter = new McpServerFilter
            {
                ServerType = type,
                OrganizationId = organizationId
            };
            var domainFilter = McpServerMapper.ToDomainFilter(filter);
            var domainServers = await _serverRepository.GetServersAsync(domainFilter);
            return domainServers.Select(McpServerMapper.ToCoreModel).ToList();
        }

        public async Task<List<McpServer>> GetServersByStatusAsync(McpServerStatus status, Guid organizationId)
        {
            var filter = new McpServerFilter
            {
                Status = status,
                OrganizationId = organizationId
            };
            var domainFilter = McpServerMapper.ToDomainFilter(filter);
            var domainServers = await _serverRepository.GetServersAsync(domainFilter);
            return domainServers.Select(McpServerMapper.ToCoreModel).ToList();
        }

        // Private helper methods
        private async Task<McpCredentials?> LoadServerCredentialsAsync(Guid serverId)
        {
            try
            {
                var secretName = $"mcp-server-{serverId}";
                var encryptedJson = await _keyVaultManager.GetDecryptedSecretAsync(secretName);
                return JsonSerializer.Deserialize<McpCredentials>(encryptedJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load credentials for server {ServerId}", serverId);
                return null;
            }
        }

        private async Task StoreServerCredentialsAsync(Guid serverId, McpCredentials credentials)
        {
            var secretName = $"mcp-server-{serverId}";
            var json = JsonSerializer.Serialize(credentials, _jsonOptions);
            await _keyVaultManager.StoreEncryptedSecretAsync(secretName, json);
        }

        private async Task DeleteServerCredentialsAsync(Guid serverId)
        {
            try
            {
                var secretName = $"mcp-server-{serverId}";
                await _keyVaultManager.DeleteSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete credentials for server {ServerId}", serverId);
            }
        }

        private async Task<McpServerHealth> TestServerConnectionAsync(McpServer server)
        {
            var startTime = DateTime.UtcNow;
            var health = new McpServerHealth
            {
                ServerId = server.Id,
                CheckedAt = startTime
            };

            try
            {
                // Create test request
                var testRequest = new McpToolRequest
                {
                    ServerId = server.Id,
                    ToolName = "ping",
                    RequestId = Guid.NewGuid().ToString()
                };

                // Configure client with server connection info
                _mcpClient.ConfigureConnection(server.ConnectionInfo, server.Credentials);

                // Send ping request
                var response = await _mcpClient.SendRequestAsync(testRequest);
                
                health.ResponseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                health.Status = response.Success ? McpServerStatus.Online : McpServerStatus.Degraded;
                health.ErrorMessage = response.Error;
            }
            catch (HttpRequestException ex)
            {
                health.Status = McpServerStatus.Offline;
                health.ErrorMessage = $"Connection failed: {ex.Message}";
                health.ResponseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            catch (TaskCanceledException)
            {
                health.Status = McpServerStatus.Offline;
                health.ErrorMessage = "Connection timeout";
                health.ResponseTimeMs = server.ConnectionInfo.TimeoutSeconds * 1000;
            }
            catch (Exception ex)
            {
                health.Status = McpServerStatus.Error;
                health.ErrorMessage = ex.Message;
                health.ResponseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            }

            return health;
        }

        private async Task<McpServerCapabilities> FetchServerCapabilitiesAsync(McpServer server)
        {
            try
            {
                // Configure client
                _mcpClient.ConfigureConnection(server.ConnectionInfo, server.Credentials);

                // Request capabilities
                var request = new McpToolRequest
                {
                    ServerId = server.Id,
                    ToolName = "capabilities",
                    RequestId = Guid.NewGuid().ToString()
                };

                var response = await _mcpClient.SendRequestAsync(request);
                
                if (!response.Success || response.Result == null)
                {
                    throw new Exception($"Failed to fetch capabilities: {response.Error}");
                }

                // Parse capabilities from response
                var json = JsonSerializer.Serialize(response.Result);
                var capabilities = JsonSerializer.Deserialize<McpServerCapabilities>(json, _jsonOptions);
                
                if (capabilities != null)
                {
                    capabilities.ServerId = server.Id;
                    capabilities.ServerName = server.Name;
                    capabilities.LastUpdated = DateTime.UtcNow;
                }

                return capabilities ?? new McpServerCapabilities
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch capabilities for server {ServerId}", server.Id);
                return new McpServerCapabilities
                {
                    ServerId = server.Id,
                    ServerName = server.Name,
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
    }
}