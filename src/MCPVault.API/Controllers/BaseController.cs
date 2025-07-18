using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MCPVault.API.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        protected Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        protected Guid GetOrganizationId()
        {
            var orgIdClaim = User.FindFirst("OrganizationId")?.Value;
            return Guid.TryParse(orgIdClaim, out var orgId) ? orgId : Guid.Empty;
        }

        protected string[] GetUserRoles()
        {
            return User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        }

        protected bool IsInRole(string role)
        {
            return User.IsInRole(role);
        }
    }
}