using System;
using System.Collections.Generic;

namespace MCPVault.Core.MCP.Models
{
    public class McpServer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public McpServerType ServerType { get; set; }
        public McpAuthenticationType AuthType { get; set; }
        public McpCredentials? Credentials { get; set; }
        public McpConnectionInfo ConnectionInfo { get; set; } = null!;
        public McpServerCapabilities? Capabilities { get; set; }
        public McpServerStatus Status { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid CreatedBy { get; set; }
    }

    public enum McpServerType
    {
        Standard = 0,
        Custom = 1,
        Enterprise = 2,
        Development = 3
    }

    public enum McpAuthenticationType
    {
        None = 0,
        ApiKey = 1,
        OAuth2 = 2,
        ClientCredentials = 3,
        BearerToken = 4,
        Custom = 5
    }

    public enum McpServerStatus
    {
        Unknown = 0,
        Online = 1,
        Offline = 2,
        Degraded = 3,
        Maintenance = 4,
        Error = 5
    }

    public class McpServerRegistration
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public McpServerType ServerType { get; set; }
        public McpAuthenticationType AuthType { get; set; }
        public McpCredentials? Credentials { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public int? Port { get; set; }
        public string? BasePath { get; set; }
        public bool UseSsl { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class McpServerUpdate
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public McpAuthenticationType? AuthType { get; set; }
        public McpCredentials? Credentials { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public bool? IsActive { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    public class McpServerHealth
    {
        public Guid ServerId { get; set; }
        public McpServerStatus Status { get; set; }
        public DateTime CheckedAt { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? DiagnosticInfo { get; set; }
        public bool IsHealthy => Status == McpServerStatus.Online || Status == McpServerStatus.Degraded;
    }

    public class McpServerFilter
    {
        public McpServerType? ServerType { get; set; }
        public McpServerStatus? Status { get; set; }
        public bool? IsActive { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? SearchTerm { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    public class McpServerStatistics
    {
        public Guid ServerId { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public Dictionary<string, int> ToolUsageCount { get; set; } = new();
        public Dictionary<string, double> ToolResponseTimes { get; set; } = new();
    }
}