using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MCPVault.API.Models.Requests;
using MCPVault.API.Models.Responses;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP.Models;

namespace MCPVault.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class McpServersController : BaseController
    {
        private readonly IMcpServerRegistry _serverRegistry;
        private readonly ILogger<McpServersController> _logger;

        public McpServersController(
            IMcpServerRegistry serverRegistry,
            ILogger<McpServersController> logger)
        {
            _serverRegistry = serverRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Register a new MCP server
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(McpServerResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> RegisterServer([FromBody] McpServerRegistrationRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var registration = new McpServerRegistration
                {
                    Name = request.Name,
                    Description = request.Description,
                    Url = request.Url,
                    ServerType = request.ServerType,
                    AuthType = request.AuthType,
                    Credentials = request.Credentials,
                    Metadata = request.Metadata,
                    Port = request.Port,
                    BasePath = request.BasePath,
                    UseSsl = request.UseSsl,
                    TimeoutSeconds = request.TimeoutSeconds
                };

                var server = await _serverRegistry.RegisterServerAsync(
                    registration, 
                    GetOrganizationId(), 
                    GetUserId());

                var response = MapToResponse(server);
                return CreatedAtAction(nameof(GetServer), new { id = server.Id }, response);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering MCP server");
                return StatusCode(500, new { message = "An error occurred while registering the server" });
            }
        }

        /// <summary>
        /// Get a specific MCP server
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(McpServerResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetServer(Guid id)
        {
            try
            {
                var server = await _serverRegistry.GetServerAsync(id);
                
                // Verify organization access
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                return Ok(MapToResponse(server));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get all MCP servers for the organization
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<McpServerResponse>), 200)]
        public async Task<IActionResult> GetServers(
            [FromQuery] McpServerType? type = null,
            [FromQuery] McpServerStatus? status = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageSize = 50,
            [FromQuery] int pageNumber = 1,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDescending = false)
        {
            var filter = new McpServerFilter
            {
                ServerType = type,
                Status = status,
                IsActive = isActive,
                OrganizationId = GetOrganizationId(),
                SearchTerm = searchTerm,
                PageSize = pageSize,
                PageNumber = pageNumber,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var servers = await _serverRegistry.GetServersAsync(filter);
            var responses = servers.Select(MapToResponse).ToList();

            return Ok(new PagedResponse<McpServerResponse>
            {
                Items = responses,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = responses.Count
            });
        }

        /// <summary>
        /// Update an MCP server
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(McpServerResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateServer(Guid id, [FromBody] McpServerUpdateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Verify organization access
                var existing = await _serverRegistry.GetServerAsync(id);
                if (existing.OrganizationId != GetOrganizationId())
                    return NotFound();

                var update = new McpServerUpdate
                {
                    Name = request.Name,
                    Description = request.Description,
                    Url = request.Url,
                    AuthType = request.AuthType,
                    Credentials = request.Credentials,
                    Metadata = request.Metadata,
                    IsActive = request.IsActive,
                    TimeoutSeconds = request.TimeoutSeconds
                };

                var server = await _serverRegistry.UpdateServerAsync(id, update);
                return Ok(MapToResponse(server));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Delete an MCP server
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteServer(Guid id)
        {
            try
            {
                // Verify organization access
                var existing = await _serverRegistry.GetServerAsync(id);
                if (existing.OrganizationId != GetOrganizationId())
                    return NotFound();

                var result = await _serverRegistry.DeleteServerAsync(id);
                return result ? NoContent() : NotFound();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get server capabilities
        /// </summary>
        [HttpGet("{id}/capabilities")]
        [ProducesResponseType(typeof(McpServerCapabilities), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetServerCapabilities(Guid id)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                var capabilities = await _serverRegistry.GetServerCapabilitiesAsync(id);
                return Ok(capabilities);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Refresh server capabilities
        /// </summary>
        [HttpPost("{id}/capabilities/refresh")]
        [ProducesResponseType(typeof(McpServerCapabilities), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RefreshServerCapabilities(Guid id)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                var capabilities = await _serverRegistry.RefreshServerCapabilitiesAsync(id);
                return Ok(capabilities);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Check server health
        /// </summary>
        [HttpGet("{id}/health")]
        [ProducesResponseType(typeof(McpServerHealth), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CheckServerHealth(Guid id)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                var health = await _serverRegistry.CheckServerHealthAsync(id);
                return Ok(health);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get server health history
        /// </summary>
        [HttpGet("{id}/health/history")]
        [ProducesResponseType(typeof(List<McpServerHealth>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetServerHealthHistory(
            Guid id, 
            [FromQuery] DateTime? since = null)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                var sinceDate = since ?? DateTime.UtcNow.AddDays(-7);
                var history = await _serverRegistry.GetServerHealthHistoryAsync(id, sinceDate);
                return Ok(history);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get server statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [ProducesResponseType(typeof(McpServerStatistics), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetServerStatistics(
            Guid id,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                var start = startDate ?? DateTime.UtcNow.AddDays(-30);
                var end = endDate ?? DateTime.UtcNow;

                var statistics = await _serverRegistry.GetServerStatisticsAsync(id, start, end);
                return Ok(statistics);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Check health of all organization servers
        /// </summary>
        [HttpPost("health/check-all")]
        [ProducesResponseType(typeof(Dictionary<Guid, McpServerHealth>), 200)]
        public async Task<IActionResult> CheckAllServersHealth()
        {
            var healthChecks = await _serverRegistry.CheckAllServersHealthAsync(GetOrganizationId());
            return Ok(healthChecks);
        }

        /// <summary>
        /// Import multiple servers
        /// </summary>
        [HttpPost("import")]
        [ProducesResponseType(typeof(List<McpServerResponse>), 200)]
        public async Task<IActionResult> ImportServers([FromBody] List<McpServerRegistrationRequest> requests)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var registrations = requests.Select(r => new McpServerRegistration
            {
                Name = r.Name,
                Description = r.Description,
                Url = r.Url,
                ServerType = r.ServerType,
                AuthType = r.AuthType,
                Credentials = r.Credentials,
                Metadata = r.Metadata,
                Port = r.Port,
                BasePath = r.BasePath,
                UseSsl = r.UseSsl,
                TimeoutSeconds = r.TimeoutSeconds
            }).ToList();

            var imported = await _serverRegistry.ImportServersAsync(
                registrations, 
                GetOrganizationId(), 
                GetUserId());

            var responses = imported.Select(MapToResponse).ToList();
            return Ok(responses);
        }

        /// <summary>
        /// Activate a server
        /// </summary>
        [HttpPost("{id}/activate")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ActivateServer(Guid id)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                await _serverRegistry.ActivateServerAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Deactivate a server
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeactivateServer(Guid id)
        {
            try
            {
                // Verify organization access
                var server = await _serverRegistry.GetServerAsync(id);
                if (server.OrganizationId != GetOrganizationId())
                    return NotFound();

                await _serverRegistry.DeactivateServerAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        private static McpServerResponse MapToResponse(McpServer server)
        {
            return new McpServerResponse
            {
                Id = server.Id,
                Name = server.Name,
                Description = server.Description,
                Url = server.Url,
                ServerType = server.ServerType,
                AuthType = server.AuthType,
                Status = server.Status,
                IsActive = server.IsActive,
                Metadata = server.Metadata,
                CreatedAt = server.CreatedAt,
                UpdatedAt = server.UpdatedAt,
                LastHealthCheck = server.LastHealthCheck,
                HasCapabilities = server.Capabilities != null,
                ToolCount = server.Capabilities?.ToolDefinitions?.Count ?? 0
            };
        }
    }
}