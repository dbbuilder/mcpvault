using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using MCPVault.API.Controllers;
using MCPVault.Core.DTOs;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using MCPVault.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace MCPVault.API.Tests.Controllers
{
    public class UserControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ILogger<UserController>> _mockLogger;
        private readonly UserController _controller;

        public UserControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockLogger = new Mock<ILogger<UserController>>();
            _controller = new UserController(_mockUserService.Object, _mockLogger.Object);
            
            // Setup controller context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task GetById_WithValidId_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userDto = new UserDto
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockUserService.Setup(s => s.GetUserDetailsAsync(userId))
                .ReturnsAsync(userDto);

            // Act
            var result = await _controller.GetById(userId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUser = Assert.IsType<UserDto>(okResult.Value);
            Assert.Equal(userId, returnedUser.Id);
        }

        [Fact]
        public async Task GetById_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserService.Setup(s => s.GetUserDetailsAsync(userId))
                .ThrowsAsync(new NotFoundException("User not found"));

            // Act
            var result = await _controller.GetById(userId);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetByOrganization_ReturnsUsersList()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var users = new List<UserListDto>
            {
                new UserListDto { Id = Guid.NewGuid(), Email = "user1@example.com" },
                new UserListDto { Id = Guid.NewGuid(), Email = "user2@example.com" }
            };

            _mockUserService.Setup(s => s.GetUsersListAsync(organizationId))
                .ReturnsAsync(users);

            // Act
            var result = await _controller.GetByOrganization(organizationId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserListDto>>(okResult.Value);
            Assert.Equal(2, returnedUsers.Count());
        }

        [Fact]
        public async Task Create_WithValidData_ReturnsCreatedUser()
        {
            // Arrange
            var createRequest = new CreateUserRequest
            {
                Email = "newuser@example.com",
                Password = "SecurePassword123!",
                FirstName = "Jane",
                LastName = "Smith",
                OrganizationId = Guid.NewGuid()
            };

            var createdUser = new User
            {
                Id = Guid.NewGuid(),
                Email = createRequest.Email,
                FirstName = createRequest.FirstName,
                LastName = createRequest.LastName
            };

            var userDto = new UserDto
            {
                Id = createdUser.Id,
                Email = createdUser.Email,
                FirstName = createdUser.FirstName,
                LastName = createdUser.LastName
            };

            _mockUserService.Setup(s => s.CreateAsync(createRequest))
                .ReturnsAsync(createdUser);

            _mockUserService.Setup(s => s.GetUserDetailsAsync(createdUser.Id))
                .ReturnsAsync(userDto);

            // Act
            var result = await _controller.Create(createRequest);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnedUser = Assert.IsType<UserDto>(createdResult.Value);
            Assert.Equal(createRequest.Email, returnedUser.Email);
        }

        [Fact]
        public async Task Create_WithExistingEmail_ReturnsBadRequest()
        {
            // Arrange
            var createRequest = new CreateUserRequest
            {
                Email = "existing@example.com",
                Password = "SecurePassword123!",
                FirstName = "Jane",
                LastName = "Smith",
                OrganizationId = Guid.NewGuid()
            };

            _mockUserService.Setup(s => s.CreateAsync(createRequest))
                .ThrowsAsync(new ConflictException("Email already exists"));

            // Act
            var result = await _controller.Create(createRequest);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_WithValidData_ReturnsUpdatedUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var updateRequest = new UpdateUserRequest
            {
                FirstName = "Johnny",
                LastName = "Doe Jr."
            };

            var updatedUser = new User
            {
                Id = userId,
                Email = "user@example.com",
                FirstName = updateRequest.FirstName!,
                LastName = updateRequest.LastName!
            };

            var userDto = new UserDto
            {
                Id = userId,
                Email = updatedUser.Email,
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName
            };

            _mockUserService.Setup(s => s.UpdateAsync(userId, updateRequest))
                .ReturnsAsync(updatedUser);

            _mockUserService.Setup(s => s.GetUserDetailsAsync(userId))
                .ReturnsAsync(userDto);

            // Act
            var result = await _controller.Update(userId, updateRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUser = Assert.IsType<UserDto>(okResult.Value);
            Assert.Equal(updateRequest.FirstName, returnedUser.FirstName);
        }

        [Fact]
        public async Task Delete_WithValidId_ReturnsNoContent()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserService.Setup(s => s.DeleteAsync(userId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Delete(userId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task AssignRoles_WithValidData_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new AssignRolesRequest
            {
                RoleIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
            };

            _mockUserService.Setup(s => s.AssignRolesAsync(userId, request.RoleIds))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.AssignRoles(userId, request);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task RemoveRoles_WithValidData_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new AssignRolesRequest
            {
                RoleIds = new[] { Guid.NewGuid() }
            };

            _mockUserService.Setup(s => s.RemoveRolesAsync(userId, request.RoleIds))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RemoveRoles(userId, request);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task ChangePassword_WithValidData_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new ChangePasswordRequest
            {
                CurrentPassword = "CurrentPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserService.Setup(s => s.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ChangePassword(userId, request);

            // Assert
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task ChangePassword_WithInvalidCurrentPassword_ReturnsBadRequest()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new ChangePasswordRequest
            {
                CurrentPassword = "WrongPassword123!",
                NewPassword = "NewPassword123!"
            };

            _mockUserService.Setup(s => s.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword))
                .ThrowsAsync(new UnauthorizedException("Current password is incorrect"));

            // Act
            var result = await _controller.ChangePassword(userId, request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UnlockUser_WithValidId_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserService.Setup(s => s.UnlockUserAsync(userId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UnlockUser(userId);

            // Assert
            Assert.IsType<OkResult>(result);
        }
    }
}