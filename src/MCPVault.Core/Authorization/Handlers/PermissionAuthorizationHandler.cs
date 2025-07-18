using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;

namespace MCPVault.Core.Authorization.Handlers
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Resource { get; }
        public string Action { get; }

        public PermissionRequirement(string resource, string action)
        {
            Resource = resource;
            Action = action;
        }
    }

    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly Interfaces.IAuthorizationService _authorizationService;
        private readonly ILogger<PermissionAuthorizationHandler> _logger;

        public PermissionAuthorizationHandler(
            Interfaces.IAuthorizationService authorizationService,
            ILogger<PermissionAuthorizationHandler> logger)
        {
            _authorizationService = authorizationService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst("sub") ?? 
                             context.User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("No valid user ID found in claims");
                context.Fail();
                return;
            }

            var authorized = await _authorizationService.AuthorizeAsync(
                userId, requirement.Resource, requirement.Action);

            if (authorized)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogInformation("User {UserId} denied access to {Resource}:{Action}",
                    userId, requirement.Resource, requirement.Action);
                context.Fail();
            }
        }
    }

    public class OrganizationRequirement : IAuthorizationRequirement
    {
        public Guid? RequiredOrganizationId { get; }

        public OrganizationRequirement(Guid? requiredOrganizationId = null)
        {
            RequiredOrganizationId = requiredOrganizationId;
        }
    }

    public class OrganizationAuthorizationHandler : AuthorizationHandler<OrganizationRequirement>
    {
        private readonly ILogger<OrganizationAuthorizationHandler> _logger;

        public OrganizationAuthorizationHandler(ILogger<OrganizationAuthorizationHandler> logger)
        {
            _logger = logger;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OrganizationRequirement requirement)
        {
            var orgClaim = context.User.FindFirst("org") ?? 
                          context.User.FindFirst("organization_id");

            if (orgClaim == null || !Guid.TryParse(orgClaim.Value, out var userOrgId))
            {
                _logger.LogWarning("No valid organization ID found in claims");
                context.Fail();
                return Task.CompletedTask;
            }

            if (requirement.RequiredOrganizationId.HasValue)
            {
                // Check specific organization
                if (userOrgId == requirement.RequiredOrganizationId.Value)
                {
                    context.Succeed(requirement);
                }
                else
                {
                    _logger.LogWarning("User organization {UserOrgId} does not match required {RequiredOrgId}",
                        userOrgId, requirement.RequiredOrganizationId.Value);
                    context.Fail();
                }
            }
            else
            {
                // Just check that user belongs to any organization
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    public class RoleRequirement : IAuthorizationRequirement
    {
        public string[] RequiredRoles { get; }

        public RoleRequirement(params string[] requiredRoles)
        {
            RequiredRoles = requiredRoles;
        }
    }

    public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            var userRoles = context.User.Claims
                .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();

            if (requirement.RequiredRoles.Any(role => userRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}