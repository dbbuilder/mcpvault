using System;
using System.Collections.Generic;

namespace MCPVault.Core.Authorization.Models
{
    public enum PermissionEffect
    {
        Allow,
        Deny
    }

    public class Permission
    {
        public Guid Id { get; set; }
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public PermissionEffect Effect { get; set; }
        public Guid? ResourceId { get; set; }
        public Dictionary<string, object>? Conditions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AuthorizationContext
    {
        public Guid UserId { get; set; }
        public string Resource { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Guid? ResourceId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Dictionary<string, string> Claims { get; set; } = new();
        public Dictionary<string, object> ContextData { get; set; } = new();
    }

    public class AuthorizationPolicy
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Dictionary<string, string> RequiredClaims { get; set; } = new();
        public string[] AllowedResources { get; set; } = Array.Empty<string>();
        public string[] AllowedActions { get; set; } = Array.Empty<string>();
        public Dictionary<string, object>? Conditions { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Guid PermissionId { get; set; }
        public DateTime AssignedAt { get; set; }
        public Guid? AssignedBy { get; set; }
    }

    public class UserPermission
    {
        public Guid UserId { get; set; }
        public Guid PermissionId { get; set; }
        public DateTime AssignedAt { get; set; }
        public Guid? AssignedBy { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class ResourcePermission
    {
        public Guid Id { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public Guid ResourceId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? RoleId { get; set; }
        public string Action { get; set; } = string.Empty;
        public PermissionEffect Effect { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class PermissionEvaluationResult
    {
        public bool IsAllowed { get; set; }
        public string? Reason { get; set; }
        public Permission? MatchedPermission { get; set; }
        public AuthorizationPolicy? MatchedPolicy { get; set; }
    }
}