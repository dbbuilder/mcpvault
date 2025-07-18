using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MCPVault.Core.MCP.Models;

namespace MCPVault.API.Models.Requests
{
    public class McpServerRegistrationRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        public McpServerType ServerType { get; set; } = McpServerType.Standard;

        public McpAuthenticationType AuthType { get; set; } = McpAuthenticationType.None;

        public McpCredentials? Credentials { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

        public int? Port { get; set; }

        public string? BasePath { get; set; }

        public bool UseSsl { get; set; } = true;

        [Range(1, 300)]
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class McpServerUpdateRequest
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Url]
        public string? Url { get; set; }

        public McpAuthenticationType? AuthType { get; set; }

        public McpCredentials? Credentials { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }

        public bool? IsActive { get; set; }

        [Range(1, 300)]
        public int? TimeoutSeconds { get; set; }
    }
}