using System;
using System.Collections.Generic;

namespace MCPVault.Domain.Entities
{
    public class McpServer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public McpServerType ServerType { get; set; }
        public McpAuthenticationType AuthType { get; set; }
        public string? CredentialsJson { get; set; } // Stored encrypted in DB
        public string? ConnectionInfoJson { get; set; }
        public string? CapabilitiesJson { get; set; }
        public McpServerStatus Status { get; set; }
        public string? MetadataJson { get; set; }
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

    public class McpServerHealth
    {
        public Guid Id { get; set; }
        public Guid ServerId { get; set; }
        public McpServerStatus Status { get; set; }
        public DateTime CheckedAt { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DiagnosticInfoJson { get; set; }
    }

    public class McpServerStatistics
    {
        public Guid ServerId { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }

    public class McpToolExecution
    {
        public Guid Id { get; set; }
        public Guid ServerId { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public long ExecutionTimeMs { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}