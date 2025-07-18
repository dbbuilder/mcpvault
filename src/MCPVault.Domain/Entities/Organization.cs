using System;

namespace MCPVault.Domain.Entities
{
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Settings { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
        public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
        public virtual ICollection<McpServer> McpServers { get; set; } = new List<McpServer>();
    }
}