using System;

namespace MCPVault.Domain.Entities
{
    public class TeamMember
    {
        public Guid TeamId { get; set; }
        public Guid UserId { get; set; }
        public bool IsLead { get; set; }
        public DateTime JoinedAt { get; set; }

        public virtual Team Team { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}