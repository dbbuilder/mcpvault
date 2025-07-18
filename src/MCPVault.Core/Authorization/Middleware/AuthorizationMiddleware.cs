using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Interfaces;
using System.Text.Json;

namespace MCPVault.Core.Authorization.Middleware
{
    using Microsoft.AspNetCore.Builder;
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthorizationMiddleware> _logger;

        public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthorizationService authorizationService, IAuditService auditService)
        {
            // Skip authorization for public endpoints
            if (IsPublicEndpoint(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Skip if user is not authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            // Extract user information
            var userIdClaim = context.User.FindFirst("sub") ?? context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("Authenticated user without valid user ID claim");
                await _next(context);
                return;
            }

            // Extract organization ID from claims
            var orgClaim = context.User.FindFirst("org") ?? context.User.FindFirst("organization_id");
            Guid? organizationId = null;
            if (orgClaim != null && Guid.TryParse(orgClaim.Value, out var orgId))
            {
                organizationId = orgId;
                context.Items["OrganizationId"] = orgId;
            }

            // Store user ID in context for easy access
            context.Items["UserId"] = userId;

            // Log the access attempt
            var resource = ExtractResourceFromPath(context.Request.Path);
            var action = MapHttpMethodToAction(context.Request.Method);

            try
            {
                // Basic authorization check for the resource
                if (!string.IsNullOrEmpty(resource) && !string.IsNullOrEmpty(action))
                {
                    var hasGeneralAccess = await authorizationService.AuthorizeAsync(userId, resource, action);
                    
                    // Store the result for informational purposes
                    context.Items["HasGeneralAccess"] = hasGeneralAccess;

                    // Log the authorization check
                    await auditService.LogDataAccessAsync(
                        userId,
                        resource,
                        Guid.Empty, // No specific resource ID at this level
                        action,
                        hasGeneralAccess);
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization middleware execution");
                throw;
            }
        }

        private bool IsPublicEndpoint(PathString path)
        {
            var publicPaths = new[]
            {
                "/health",
                "/swagger",
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/refresh"
            };

            return publicPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
        }

        private string ExtractResourceFromPath(PathString path)
        {
            var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments == null || segments.Length < 2)
                return string.Empty;

            // Typically: /api/{resource}/...
            if (segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
            {
                return segments[1].ToLowerInvariant();
            }

            return segments[0].ToLowerInvariant();
        }

        private string MapHttpMethodToAction(string httpMethod)
        {
            return httpMethod.ToUpperInvariant() switch
            {
                "GET" => "read",
                "POST" => "create",
                "PUT" => "update",
                "PATCH" => "update",
                "DELETE" => "delete",
                _ => "unknown"
            };
        }
    }

    public static class AuthorizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomAuthorization(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthorizationMiddleware>();
        }
    }
}