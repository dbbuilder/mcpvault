using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MCPVault.Core.DTOs;
using MCPVault.Core.Exceptions;
using MCPVault.Core.Interfaces;

namespace MCPVault.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserDto>> GetById(Guid id)
        {
            try
            {
                var user = await _userService.GetUserDetailsAsync(id);
                return Ok(user);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("organization/{organizationId}")]
        [ProducesResponseType(200)]
        public async Task<ActionResult<IEnumerable<UserListDto>>> GetByOrganization(Guid organizationId)
        {
            var users = await _userService.GetUsersListAsync(organizationId);
            return Ok(users);
        }

        [HttpPost]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
        {
            try
            {
                var user = await _userService.CreateAsync(request);
                var userDto = await _userService.GetUserDetailsAsync(user.Id);
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, userDto);
            }
            catch (ConflictException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request)
        {
            try
            {
                var user = await _userService.UpdateAsync(id, request);
                var userDto = await _userService.GetUserDetailsAsync(user.Id);
                return Ok(userDto);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ConflictException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _userService.DeleteAsync(id);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/roles")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AssignRoles(Guid id, AssignRolesRequest request)
        {
            try
            {
                await _userService.AssignRolesAsync(id, request.RoleIds);
                return Ok();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}/roles")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RemoveRoles(Guid id, AssignRolesRequest request)
        {
            try
            {
                await _userService.RemoveRolesAsync(id, request.RoleIds);
                return Ok();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/change-password")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ChangePassword(Guid id, ChangePasswordRequest request)
        {
            try
            {
                await _userService.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword);
                return Ok();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/unlock")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UnlockUser(Guid id)
        {
            try
            {
                await _userService.UnlockUserAsync(id);
                return Ok();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}