using System;

namespace MCPVault.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public Guid? ResourceId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual User? User { get; set; }
    }
}