using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.API.Controllers;
using MCPVault.API.Models;
using MCPVault.Core.Interfaces;
using MCPVault.Core.Authentication;

namespace MCPVault.API.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthenticationService> _mockAuthService;
        private readonly Mock<ILogger<AuthController>> _mockLogger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockLogger = new Mock<ILogger<AuthController>>();
            _controller = new AuthController(_mockAuthService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "ValidPassword123!"
            };

            var loginResult = new LoginResult
            {
                IsSuccess = true,
                AccessToken = "valid-access-token",
                RefreshToken = "valid-refresh-token",
                RequiresMfa = false
            };

            _mockAuthService.Setup(s => s.LoginAsync(loginRequest.Email, loginRequest.Password, It.IsAny<string>()))
                .ReturnsAsync(loginResult);

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(200, objectResult.StatusCode);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "test@example.com",
                Password = "WrongPassword"
            };

            var loginResult = new LoginResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid email or password"
            };

            _mockAuthService.Setup(s => s.LoginAsync(loginRequest.Email, loginRequest.Password, It.IsAny<string>()))
                .ReturnsAsync(loginResult);

            // Act
            var result = await _controller.Login(loginRequest);

            // Assert
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal(401, objectResult.StatusCode);
        }

        [Fact]
        public async Task CompleteMfa_WithValidCode_ReturnsOkWithTokens()
        {
            // Arrange
            var mfaRequest = new MfaRequest
            {
                MfaToken = "mfa-token-123",
                Code = "123456"
            };

            var mfaResult = new MfaResult
            {
                IsSuccess = true,
                AccessToken = "valid-access-token",
                RefreshToken = "valid-refresh-token"
            };

            _mockAuthService.Setup(s => s.CompleteMfaAsync(mfaRequest.MfaToken, mfaRequest.Code))
                .ReturnsAsync(mfaResult);

            // Act
            var result = await _controller.CompleteMfa(mfaRequest);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RefreshToken_WithValidToken_ReturnsOkWithNewTokens()
        {
            // Arrange
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = "valid-refresh-token"
            };

            var refreshResult = new RefreshResult
            {
                IsSuccess = true,
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token"
            };

            _mockAuthService.Setup(s => s.RefreshTokenAsync(refreshRequest.RefreshToken))
                .ReturnsAsync(refreshResult);

            // Act
            var result = await _controller.RefreshToken(refreshRequest);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Logout_WithValidTokens_ReturnsOk()
        {
            // Arrange
            var logoutRequest = new LogoutRequest
            {
                AccessToken = "valid-access-token",
                RefreshToken = "valid-refresh-token"
            };

            _mockAuthService.Setup(s => s.LogoutAsync(logoutRequest.AccessToken, logoutRequest.RefreshToken))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Logout(logoutRequest);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsCreated()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = "newuser@example.com",
                Password = "SecurePassword123!",
                FirstName = "John",
                LastName = "Doe",
                OrganizationId = Guid.NewGuid()
            };

            var registrationResult = new RegistrationResult
            {
                IsSuccess = true,
                User = new Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    Email = registerRequest.Email,
                    FirstName = registerRequest.FirstName,
                    LastName = registerRequest.LastName
                }
            };

            _mockAuthService.Setup(s => s.RegisterAsync(It.IsAny<UserRegistrationData>()))
                .ReturnsAsync(registrationResult);

            // Act
            var result = await _controller.Register(registerRequest);

            // Assert
            Assert.IsType<CreatedAtActionResult>(result);
        }

        [Fact]
        public async Task ChangePassword_WithValidData_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var changePasswordRequest = new ChangePasswordRequest
            {
                OldPassword = "OldPassword123!",
                NewPassword = "NewPassword123!"
            };

            var changePasswordResult = new PasswordChangeResult
            {
                IsSuccess = true
            };

            _mockAuthService.Setup(s => s.ChangePasswordAsync(userId, changePasswordRequest.OldPassword, changePasswordRequest.NewPassword))
                .ReturnsAsync(changePasswordResult);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
            _controller.HttpContext.Items["UserId"] = userId;

            // Act
            var result = await _controller.ChangePassword(changePasswordRequest);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
    }
}