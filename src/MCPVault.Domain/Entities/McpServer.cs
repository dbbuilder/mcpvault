using System;

namespace MCPVault.Domain.Entities
{
    public class McpServer
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ServerUrl { get; set; } = string.Empty;
        public string ServerType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Capabilities { get; set; } = "[]";
        public bool IsActive { get; set; }
        public string? Configuration { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public string? HealthStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Organization Organization { get; set; } = null!;
        public virtual ICollection<McpServerVersion> Versions { get; set; } = new List<McpServerVersion>();
    }
}