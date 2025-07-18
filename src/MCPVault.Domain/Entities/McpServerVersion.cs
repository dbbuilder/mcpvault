using System;

namespace MCPVault.Domain.Entities
{
    public class McpServerVersion
    {
        public Guid Id { get; set; }
        public Guid ServerId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string? ChangeLog { get; set; }
        public string Configuration { get; set; } = "{}";
        public string Capabilities { get; set; } = "[]";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedBy { get; set; }

        public virtual McpServer Server { get; set; } = null!;
    }
}