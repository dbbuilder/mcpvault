using System;
using System.Collections.Generic;

namespace MCPVault.Core.MCP.Models
{
    public class McpToolRequest
    {
        public Guid ServerId { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public string? RequestId { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }

    public class McpToolResponse
    {
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? Error { get; set; }
        public string? ErrorCode { get; set; }
        public long ExecutionTime { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class McpServerCapabilities
    {
        public Guid ServerId { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public List<string> AllowedTools { get; set; } = new();
        public Dictionary<string, McpToolDefinition> ToolDefinitions { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class McpToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, McpParameterDefinition> Parameters { get; set; } = new();
        public string? ReturnType { get; set; }
        public List<string>? RequiredPermissions { get; set; }
    }

    public class McpParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Required { get; set; }
        public object? DefaultValue { get; set; }
        public List<string>? AllowedValues { get; set; }
    }

    public class McpCredentials
    {
        public string? ApiKey { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? BearerToken { get; set; }
        public Dictionary<string, string>? CustomHeaders { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class McpConnectionInfo
    {
        public Guid ServerId { get; set; }
        public string ServerUrl { get; set; } = string.Empty;
        public string Protocol { get; set; } = "https";
        public int? Port { get; set; }
        public string? BasePath { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public bool UseSsl { get; set; } = true;
    }

    public class McpProxyRequest
    {
        public McpToolRequest Request { get; set; } = null!;
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
        public string? SessionId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class McpProxyResponse
    {
        public McpToolResponse Response { get; set; } = null!;
        public Guid RequestId { get; set; }
        public long ProxyExecutionTime { get; set; }
        public bool WasCached { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}