using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using MCPVault.Core.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MCPVault.Core.Authorization.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AuthorizeDynamicAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _resourceType;
        private readonly string _action;
        private readonly string _resourceIdParameter;

        public AuthorizeDynamicAttribute(string resourceType, string action, string resourceIdParameter = "id")
        {
            _resourceType = resourceType;
            _action = action;
            _resourceIdParameter = resourceIdParameter;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authorizationService = context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationService>();

            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get resource ID from route data or query string
            Guid? resourceId = null;
            
            // Check route values first
            if (context.RouteData.Values.TryGetValue(_resourceIdParameter, out var routeValue))
            {
                if (Guid.TryParse(routeValue?.ToString(), out var parsedId))
                {
                    resourceId = parsedId;
                }
            }
            
            // Check query string
            if (!resourceId.HasValue)
            {
                var queryValue = context.HttpContext.Request.Query[_resourceIdParameter];
                if (!string.IsNullOrEmpty(queryValue) && Guid.TryParse(queryValue, out var parsedId))
                {
                    resourceId = parsedId;
                }
            }

            if (!resourceId.HasValue)
            {
                // If no resource ID found, fall back to general permission check
                var generalAuthorized = await authorizationService.AuthorizeAsync(userId, _resourceType, _action);
                if (!generalAuthorized)
                {
                    context.Result = new ForbidResult();
                }
                return;
            }

            // Check resource-specific permission
            var authorized = await authorizationService.AuthorizeResourceAsync(
                userId, _resourceType, resourceId.Value, _action);

            if (!authorized)
            {
                context.Result = new ForbidResult();
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class AuthorizeOrganizationAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check if user has an organization claim
            var orgClaim = user.FindFirst("org") ?? user.FindFirst("organization_id");
            if (orgClaim == null || !Guid.TryParse(orgClaim.Value, out var organizationId))
            {
                context.Result = new ForbidResult("User must belong to an organization");
                return;
            }

            // Store organization ID in HttpContext for later use
            context.HttpContext.Items["OrganizationId"] = organizationId;

            await Task.CompletedTask;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class AuthorizePolicyAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _policyName;

        public AuthorizePolicyAttribute(string policyName)
        {
            _policyName = policyName;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authorizationService = context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationService>();

            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Build authorization context from claims
            var authContext = new Authorization.Models.AuthorizationContext
            {
                UserId = userId,
                Resource = context.HttpContext.Request.Path.Value?.Split('/').Skip(2).FirstOrDefault() ?? string.Empty,
                Action = context.HttpContext.Request.Method.ToLowerInvariant() switch
                {
                    "get" => "read",
                    "post" => "create",
                    "put" => "update",
                    "patch" => "update",
                    "delete" => "delete",
                    _ => "unknown"
                }
            };

            // Add claims to context
            foreach (var claim in user.Claims)
            {
                authContext.Claims[claim.Type] = claim.Value;
            }

            // Get organization ID if available
            if (context.HttpContext.Items.TryGetValue("OrganizationId", out var orgId) && orgId is Guid organizationId)
            {
                authContext.OrganizationId = organizationId;
            }

            // Load and evaluate policy
            // In a real implementation, policies would be loaded from configuration or database
            var policies = GetPoliciesForName(_policyName);
            
            var authorized = await authorizationService.EvaluatePoliciesAsync(authContext, policies);
            if (!authorized)
            {
                context.Result = new ForbidResult($"Policy '{_policyName}' requirements not met");
            }
        }

        private Authorization.Models.AuthorizationPolicy[] GetPoliciesForName(string policyName)
        {
            // This would typically load from configuration or database
            // For now, return empty array - policies would be defined elsewhere
            return Array.Empty<Authorization.Models.AuthorizationPolicy>();
        }
    }
}