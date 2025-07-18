using System;
using System.Collections.Generic;
using MCPVault.Core.MCP.Models;

namespace MCPVault.API.Models.Responses
{
    public class McpServerResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public McpServerType ServerType { get; set; }
        public McpAuthenticationType AuthType { get; set; }
        public McpServerStatus Status { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public bool HasCapabilities { get; set; }
        public int ToolCount { get; set; }
    }
}