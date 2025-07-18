using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MCPVault.API.Models;
using MCPVault.Core.Authentication;
using MCPVault.Core.Interfaces;

namespace MCPVault.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var result = await _authService.LoginAsync(request.Email, request.Password, ipAddress);

                if (!result.IsSuccess)
                {
                    return Unauthorized(new { Error = result.ErrorMessage });
                }

                if (result.RequiresMfa)
                {
                    return Ok(new
                    {
                        RequiresMfa = true,
                        MfaToken = result.MfaToken
                    });
                }

                return Ok(new
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken,
                    RequiresMfa = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return StatusCode(500, new { Error = "An error occurred during login" });
            }
        }

        [HttpPost("mfa/complete")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteMfa([FromBody] MfaRequest request)
        {
            try
            {
                var result = await _authService.CompleteMfaAsync(request.MfaToken, request.Code);

                if (!result.IsSuccess)
                {
                    return Unauthorized(new { Error = result.ErrorMessage });
                }

                return Ok(new
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MFA completion");
                return StatusCode(500, new { Error = "An error occurred during MFA verification" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);

                if (!result.IsSuccess)
                {
                    return Unauthorized(new { Error = result.ErrorMessage });
                }

                return Ok(new
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { Error = "An error occurred during token refresh" });
            }
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                var success = await _authService.LogoutAsync(request.AccessToken, request.RefreshToken);

                if (!success)
                {
                    return Unauthorized();
                }

                return Ok(new { Message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { Error = "An error occurred during logout" });
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var registrationData = new UserRegistrationData
                {
                    Email = request.Email,
                    Password = request.Password,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    OrganizationId = request.OrganizationId
                };

                var result = await _authService.RegisterAsync(registrationData);

                if (!result.IsSuccess)
                {
                    return BadRequest(new { Error = result.ErrorMessage });
                }

                return CreatedAtAction(nameof(GetProfile), new { id = result.User!.Id }, new
                {
                    UserId = result.User.Id,
                    Message = "User registered successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(500, new { Error = "An error occurred during registration" });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized();
                }

                var result = await _authService.ChangePasswordAsync(userId.Value, request.OldPassword, request.NewPassword);

                if (!result.IsSuccess)
                {
                    return BadRequest(new { Error = result.ErrorMessage });
                }

                return Ok(new { Message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password change");
                return StatusCode(500, new { Error = "An error occurred during password change" });
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public IActionResult GetProfile()
        {
            // Placeholder for profile endpoint referenced in CreatedAtAction
            return Ok();
        }

        private Guid? GetCurrentUserId()
        {
            if (HttpContext.Items.TryGetValue("UserId", out var userIdObj) && userIdObj is Guid userId)
            {
                return userId;
            }

            var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
            if (Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                return parsedUserId;
            }

            return null;
        }
    }
}