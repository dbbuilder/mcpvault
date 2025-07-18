using System;
using System.ComponentModel.DataAnnotations;

namespace MCPVault.API.DTOs
{
    public class OrganizationDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Settings { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateOrganizationDto
    {
        [Required(ErrorMessage = "Organization name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Organization name must be between 3 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Organization slug is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Organization slug must be between 3 and 100 characters")]
        [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must be lowercase alphanumeric with hyphens only")]
        public string Slug { get; set; } = string.Empty;

        public string? Settings { get; set; }
    }

    public class UpdateOrganizationDto
    {
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Organization name must be between 3 and 100 characters")]
        public string? Name { get; set; }

        public bool? IsActive { get; set; }

        public string? Settings { get; set; }
    }
}