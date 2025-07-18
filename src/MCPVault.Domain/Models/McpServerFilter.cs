using System;
using MCPVault.Domain.Entities;

namespace MCPVault.Domain.Models
{
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
}