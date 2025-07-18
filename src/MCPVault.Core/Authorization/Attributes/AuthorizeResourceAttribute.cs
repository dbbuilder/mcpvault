using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using MCPVault.Core.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MCPVault.Core.Authorization.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class AuthorizeResourceAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _resource;
        private readonly string _action;

        public AuthorizeResourceAttribute(string resource, string action)
        {
            _resource = resource;
            _action = action;
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

            var authorized = await authorizationService.AuthorizeAsync(userId, _resource, _action);
            if (!authorized)
            {
                context.Result = new ForbidResult();
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class AuthorizeAnyAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly (string resource, string action)[] _permissions;

        public AuthorizeAnyAttribute(params string[] permissions)
        {
            if (permissions.Length % 2 != 0)
            {
                throw new ArgumentException("Permissions must be provided in resource/action pairs");
            }

            _permissions = new (string, string)[permissions.Length / 2];
            for (int i = 0; i < permissions.Length; i += 2)
            {
                _permissions[i / 2] = (permissions[i], permissions[i + 1]);
            }
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

            foreach (var (resource, action) in _permissions)
            {
                var authorized = await authorizationService.AuthorizeAsync(userId, resource, action);
                if (authorized)
                {
                    return; // At least one permission matched
                }
            }

            context.Result = new ForbidResult();
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class AuthorizeAllAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly (string resource, string action)[] _permissions;

        public AuthorizeAllAttribute(params string[] permissions)
        {
            if (permissions.Length % 2 != 0)
            {
                throw new ArgumentException("Permissions must be provided in resource/action pairs");
            }

            _permissions = new (string, string)[permissions.Length / 2];
            for (int i = 0; i < permissions.Length; i += 2)
            {
                _permissions[i / 2] = (permissions[i], permissions[i + 1]);
            }
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

            foreach (var (resource, action) in _permissions)
            {
                var authorized = await authorizationService.AuthorizeAsync(userId, resource, action);
                if (!authorized)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
        }
    }
}