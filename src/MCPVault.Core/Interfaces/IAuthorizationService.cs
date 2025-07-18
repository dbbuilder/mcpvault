using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MCPVault.Core.Authorization.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IAuthorizationService
    {
        Task<bool> AuthorizeAsync(Guid userId, string resource, string action, Dictionary<string, object>? context = null);
        Task<bool> AuthorizeResourceAsync(Guid userId, string resourceType, Guid resourceId, string action);
        Task<bool> AuthorizeWithClaimsAsync(ClaimsPrincipal principal, string resource, string action);
        Task<IEnumerable<Permission>> GetUserPermissionsAsync(Guid userId);
        Task<bool> EvaluatePoliciesAsync(AuthorizationContext context, IEnumerable<AuthorizationPolicy> policies);
        Task<PermissionEvaluationResult> EvaluatePermissionAsync(Guid userId, string resource, string action);
    }
}