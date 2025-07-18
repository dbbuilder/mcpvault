using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MCPVault.Core.Authorization.Models;
using MCPVault.Core.Interfaces;

namespace MCPVault.Core.Authorization
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IPermissionRepository _permissionRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthorizationService> _logger;

        public AuthorizationService(
            IPermissionRepository permissionRepository,
            IUserRepository userRepository,
            ILogger<AuthorizationService> logger)
        {
            _permissionRepository = permissionRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<bool> AuthorizeAsync(Guid userId, string resource, string action, Dictionary<string, object>? context = null)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Authorization failed: User {UserId} not found", userId);
                    return false;
                }

                // Get all permissions for the user (direct and role-based)
                var permissions = await GetAllUserPermissionsAsync(userId, user.RoleIds);

                // Check for explicit deny first
                var denyPermission = permissions.FirstOrDefault(p => 
                    MatchesResource(p.Resource, resource) && 
                    MatchesAction(p.Action, action) && 
                    p.Effect == PermissionEffect.Deny);

                if (denyPermission != null)
                {
                    _logger.LogInformation("Authorization denied for user {UserId} on {Resource}:{Action} due to explicit deny", 
                        userId, resource, action);
                    return false;
                }

                // Check for allow permission
                var allowPermission = permissions.FirstOrDefault(p => 
                    MatchesResource(p.Resource, resource) && 
                    MatchesAction(p.Action, action) && 
                    p.Effect == PermissionEffect.Allow);

                if (allowPermission != null)
                {
                    // Evaluate conditions if present
                    if (allowPermission.Conditions != null)
                    {
                        var conditionsResult = await EvaluateConditionsAsync(allowPermission.Conditions, context ?? new Dictionary<string, object>());
                        if (!conditionsResult)
                        {
                            _logger.LogInformation("Authorization failed for user {UserId} on {Resource}:{Action} due to conditions", 
                                userId, resource, action);
                            return false;
                        }
                    }

                    _logger.LogInformation("Authorization granted for user {UserId} on {Resource}:{Action}", 
                        userId, resource, action);
                    return true;
                }

                _logger.LogInformation("Authorization denied for user {UserId} on {Resource}:{Action} - no matching permission", 
                    userId, resource, action);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> AuthorizeResourceAsync(Guid userId, string resourceType, Guid resourceId, string action)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return false;

                var permissions = await GetAllUserPermissionsAsync(userId, user.RoleIds);

                // Check for resource-specific permission
                var resourcePermission = permissions.FirstOrDefault(p =>
                    p.Resource == resourceType &&
                    p.Action == action &&
                    p.ResourceId == resourceId &&
                    p.Effect == PermissionEffect.Allow);

                if (resourcePermission != null)
                    return true;

                // Fall back to general permission
                return await AuthorizeAsync(userId, resourceType, action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resource authorization for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> AuthorizeWithClaimsAsync(ClaimsPrincipal principal, string resource, string action)
        {
            var userIdClaim = principal.FindFirst("sub") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("Authorization failed: No valid user ID in claims principal");
                return false;
            }

            var context = new Dictionary<string, object>();
            foreach (var claim in principal.Claims)
            {
                context[claim.Type] = claim.Value;
            }

            return await AuthorizeAsync(userId, resource, action, context);
        }

        public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return Enumerable.Empty<Permission>();

            return await GetAllUserPermissionsAsync(userId, user.RoleIds);
        }

        public async Task<bool> EvaluatePoliciesAsync(AuthorizationContext context, IEnumerable<AuthorizationPolicy> policies)
        {
            foreach (var policy in policies.Where(p => p.IsEnabled))
            {
                // Check if all required claims match
                var claimsMatch = policy.RequiredClaims.All(requiredClaim =>
                    context.Claims.TryGetValue(requiredClaim.Key, out var value) &&
                    value == requiredClaim.Value);

                if (!claimsMatch)
                    continue;

                // Check if resource is allowed
                var resourceMatch = policy.AllowedResources.Contains("*") ||
                    policy.AllowedResources.Contains(context.Resource);

                if (!resourceMatch)
                    continue;

                // Check if action is allowed
                var actionMatch = policy.AllowedActions.Contains("*") ||
                    policy.AllowedActions.Contains(context.Action);

                if (!actionMatch)
                    continue;

                // Evaluate conditions if present
                if (policy.Conditions != null)
                {
                    var conditionsResult = await EvaluateConditionsAsync(policy.Conditions, context.ContextData);
                    if (!conditionsResult)
                        continue;
                }

                // Policy matches
                _logger.LogInformation("Policy {PolicyName} granted access for user {UserId}", 
                    policy.Name, context.UserId);
                return true;
            }

            return false;
        }

        public async Task<PermissionEvaluationResult> EvaluatePermissionAsync(Guid userId, string resource, string action)
        {
            var result = new PermissionEvaluationResult();

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    result.IsAllowed = false;
                    result.Reason = "User not found";
                    return result;
                }

                var permissions = await GetAllUserPermissionsAsync(userId, user.RoleIds);

                // Check for explicit deny
                var denyPermission = permissions.FirstOrDefault(p =>
                    MatchesResource(p.Resource, resource) &&
                    MatchesAction(p.Action, action) &&
                    p.Effect == PermissionEffect.Deny);

                if (denyPermission != null)
                {
                    result.IsAllowed = false;
                    result.Reason = "Explicitly denied";
                    result.MatchedPermission = denyPermission;
                    return result;
                }

                // Check for allow
                var allowPermission = permissions.FirstOrDefault(p =>
                    MatchesResource(p.Resource, resource) &&
                    MatchesAction(p.Action, action) &&
                    p.Effect == PermissionEffect.Allow);

                if (allowPermission != null)
                {
                    result.IsAllowed = true;
                    result.Reason = "Permission granted";
                    result.MatchedPermission = allowPermission;
                    return result;
                }

                result.IsAllowed = false;
                result.Reason = "No matching permission found";
                return result;
            }
            catch (Exception ex)
            {
                result.IsAllowed = false;
                result.Reason = $"Error evaluating permission: {ex.Message}";
                return result;
            }
        }

        private async Task<IEnumerable<Permission>> GetAllUserPermissionsAsync(Guid userId, Guid[] roleIds)
        {
            var permissions = new List<Permission>();

            // Get direct user permissions
            var userPermissions = await _permissionRepository.GetUserPermissionsAsync(userId);
            permissions.AddRange(userPermissions);

            // Get role-based permissions
            foreach (var roleId in roleIds)
            {
                var rolePermissions = await _permissionRepository.GetRolePermissionsAsync(roleId);
                permissions.AddRange(rolePermissions);
            }

            return permissions.Distinct();
        }

        private bool MatchesResource(string permissionResource, string requestedResource)
        {
            if (permissionResource == "*")
                return true;

            if (permissionResource == requestedResource)
                return true;

            // Support wildcard patterns like "organizations:*"
            if (permissionResource.EndsWith(":*"))
            {
                var prefix = permissionResource.Substring(0, permissionResource.Length - 2);
                return requestedResource.StartsWith(prefix + ":");
            }

            return false;
        }

        private bool MatchesAction(string permissionAction, string requestedAction)
        {
            if (permissionAction == "*")
                return true;

            if (permissionAction == requestedAction)
                return true;

            // Support comma-separated actions
            if (permissionAction.Contains(","))
            {
                var actions = permissionAction.Split(',').Select(a => a.Trim());
                return actions.Contains(requestedAction);
            }

            return false;
        }

        private async Task<bool> EvaluateConditionsAsync(Dictionary<string, object> conditions, Dictionary<string, object> context)
        {
            foreach (var condition in conditions)
            {
                switch (condition.Key.ToLower())
                {
                    case "timeofday":
                        if (!EvaluateTimeOfDayCondition(condition.Value.ToString(), context))
                            return false;
                        break;

                    case "iprange":
                        if (!EvaluateIpRangeCondition(condition.Value.ToString(), context))
                            return false;
                        break;

                    // Add more condition evaluators as needed
                    default:
                        _logger.LogWarning("Unknown condition type: {ConditionType}", condition.Key);
                        break;
                }
            }

            return await Task.FromResult(true);
        }

        private bool EvaluateTimeOfDayCondition(string? timeCondition, Dictionary<string, object> context)
        {
            if (timeCondition == "business_hours" && context.TryGetValue("currentTime", out var timeValue))
            {
                if (int.TryParse(timeValue.ToString(), out var hour))
                {
                    return hour >= 8 && hour <= 18; // 8 AM to 6 PM
                }
            }
            return true;
        }

        private bool EvaluateIpRangeCondition(string? ipRange, Dictionary<string, object> context)
        {
            if (string.IsNullOrEmpty(ipRange) || !context.TryGetValue("sourceIp", out var ipValue))
                return true;

            // Simple IP range check - in production, use proper IP address parsing
            var ip = ipValue.ToString();
            if (ipRange == "10.0.0.0/8" && ip != null)
            {
                return ip.StartsWith("10.");
            }

            return true;
        }
    }
}