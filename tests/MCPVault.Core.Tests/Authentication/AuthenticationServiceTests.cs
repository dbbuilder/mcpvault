using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MCPVault.Core.Authentication;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using MCPVault.Core.MCP;
using Microsoft.Extensions.Options;

namespace MCPVault.Core.Tests.Authentication
{
    public class AuthenticationServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IJwtTokenService> _mockJwtTokenService;
        private readonly Mock<IMfaService> _mockMfaService;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<ILogger<AuthenticationService>> _mockLogger;
        private readonly AuthenticationSettings _authSettings;
        private readonly IAuthenticationService _authService;

        public AuthenticationServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockMfaService = new Mock<IMfaService>();
            _mockAuditService = new Mock<IAuditService>();
            _mockLogger = new Mock<ILogger<AuthenticationService>>();
            
            _authSettings = new AuthenticationSettings
            {
                MaxFailedAttempts = 5,
                LockoutDuration = 30,
                PasswordMinLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireSpecialChar = true,
                RefreshTokenExpirationDays = 30
            };

            _authService = new AuthenticationService(
                _mockUserRepository.Object,
                _mockJwtTokenService.Object,
                _mockMfaService.Object,
                _mockAuditService.Object,
                Options.Create(_authSettings),
                _mockLogger.Object);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
        {
            // Arrange
            var email = "test@example.com";
            var password = "Test123!@#";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true,
                IsMfaEnabled = false,
                OrganizationId = Guid.NewGuid(),
                FirstName = "Test",
                LastName = "User"
            };

            var accessToken = "access_token";
            var refreshToken = "refresh_token";

            _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(user);

            _mockJwtTokenService.Setup(s => s.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
                .Returns(accessToken);

            _mockJwtTokenService.Setup(s => s.GenerateRefreshToken())
                .Returns(refreshToken);

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(accessToken, result.AccessToken);
            Assert.Equal(refreshToken, result.RefreshToken);
            Assert.False(result.RequiresMfa);
            
            _mockUserRepository.Verify(r => r.UpdateLastLoginAsync(user.Id, It.IsAny<DateTime>()), Times.Once);
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(user.Id, "Login", true, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ReturnsFailure()
        {
            // Arrange
            var email = "test@example.com";
            var password = "WrongPassword";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
                IsActive = true,
                FailedLoginAttempts = 2
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid email or password", result.ErrorMessage);
            
            _mockUserRepository.Verify(r => r.IncrementFailedLoginAttemptsAsync(user.Id), Times.Once);
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(user.Id, "Login", false, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_WithInactiveUser_ReturnsFailure()
        {
            // Arrange
            var email = "test@example.com";
            var password = "Test123!@#";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = false
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Account is inactive", result.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_WithLockedAccount_ReturnsFailure()
        {
            // Arrange
            var email = "test@example.com";
            var password = "Test123!@#";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(10)
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.StartsWith("Account is locked", result.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_WithMfaEnabled_RequiresMfa()
        {
            // Arrange
            var email = "test@example.com";
            var password = "Test123!@#";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                IsMfaEnabled = true,
                OrganizationId = Guid.NewGuid()
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(user);

            // Act
            var result = await _authService.LoginAsync(email, password);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.RequiresMfa);
            Assert.NotEmpty(result.MfaToken);
            Assert.Null(result.AccessToken); // No access token until MFA is completed
        }

        [Fact]
        public async Task CompleteMfaAsync_WithValidCode_ReturnsTokens()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mfaToken = Guid.NewGuid().ToString();
            var mfaCode = "123456";
            var accessToken = "access_token";
            var refreshToken = "refresh_token";

            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid(),
                IsActive = true,
                IsMfaEnabled = true
            };

            // Setup MFA token validation
            _authService.StoreMfaToken(mfaToken, userId);

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockMfaService.Setup(s => s.ValidateCodeAsync(userId, mfaCode))
                .ReturnsAsync(true);

            _mockJwtTokenService.Setup(s => s.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
                .Returns(accessToken);

            _mockJwtTokenService.Setup(s => s.GenerateRefreshToken())
                .Returns(refreshToken);

            // Act
            var result = await _authService.CompleteMfaAsync(mfaToken, mfaCode);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(accessToken, result.AccessToken);
            Assert.Equal(refreshToken, result.RefreshToken);
            
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(userId, "MFA_Complete", true, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CompleteMfaAsync_WithInvalidCode_ReturnsFailure()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mfaToken = Guid.NewGuid().ToString();
            var mfaCode = "000000";

            _authService.StoreMfaToken(mfaToken, userId);

            _mockMfaService.Setup(s => s.ValidateCodeAsync(userId, mfaCode))
                .ReturnsAsync(false);

            // Act
            var result = await _authService.CompleteMfaAsync(mfaToken, mfaCode);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid MFA code", result.ErrorMessage);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokens()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var oldRefreshToken = "old_refresh_token";
            var newAccessToken = "new_access_token";
            var newRefreshToken = "new_refresh_token";

            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid(),
                IsActive = true
            };

            var tokenInfo = new RefreshTokenInfo
            {
                UserId = userId,
                Token = oldRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };

            _authService.StoreRefreshToken(oldRefreshToken, tokenInfo);

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockJwtTokenService.Setup(s => s.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
                .Returns(newAccessToken);

            _mockJwtTokenService.Setup(s => s.GenerateRefreshToken())
                .Returns(newRefreshToken);

            // Act
            var result = await _authService.RefreshTokenAsync(oldRefreshToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(newAccessToken, result.AccessToken);
            Assert.Equal(newRefreshToken, result.RefreshToken);
            
            // Verify old token is revoked
            Assert.True(_authService.IsRefreshTokenRevoked(oldRefreshToken));
        }

        [Fact]
        public async Task LogoutAsync_WithValidToken_RevokesToken()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var accessToken = "access_token";
            var refreshToken = "refresh_token";

            var tokenInfo = new RefreshTokenInfo
            {
                UserId = userId,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };

            _authService.StoreRefreshToken(refreshToken, tokenInfo);

            _mockJwtTokenService.Setup(s => s.ValidateToken(accessToken))
                .Returns((true, new Dictionary<string, string> { { "sub", userId.ToString() } }));

            // Act
            var result = await _authService.LogoutAsync(accessToken, refreshToken);

            // Assert
            Assert.True(result);
            Assert.True(_authService.IsRefreshTokenRevoked(refreshToken));
            
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(userId, "Logout", true, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WithValidData_CreatesUser()
        {
            // Arrange
            var registrationData = new UserRegistrationData
            {
                Email = "new@example.com",
                Password = "Test123!@#",
                FirstName = "New",
                LastName = "User",
                OrganizationId = Guid.NewGuid()
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(registrationData.Email))
                .ReturnsAsync((User?)null);

            _mockUserRepository.Setup(r => r.CreateAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            // Act
            var result = await _authService.RegisterAsync(registrationData);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.User);
            Assert.Equal(registrationData.Email, result.User.Email);
            Assert.True(BCrypt.Net.BCrypt.Verify(registrationData.Password, result.User.PasswordHash));
            
            _mockUserRepository.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Once);
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(It.IsAny<Guid>(), "Registration", true, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_WithExistingEmail_ReturnsFailure()
        {
            // Arrange
            var registrationData = new UserRegistrationData
            {
                Email = "existing@example.com",
                Password = "Test123!@#",
                FirstName = "New",
                LastName = "User",
                OrganizationId = Guid.NewGuid()
            };

            var existingUser = new User { Email = registrationData.Email };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(registrationData.Email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _authService.RegisterAsync(registrationData);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Email already registered", result.ErrorMessage);
        }

        [Fact]
        public async Task RegisterAsync_WithWeakPassword_ReturnsFailure()
        {
            // Arrange
            var registrationData = new UserRegistrationData
            {
                Email = "new@example.com",
                Password = "weak", // Too short, no uppercase, no digit, no special char
                FirstName = "New",
                LastName = "User",
                OrganizationId = Guid.NewGuid()
            };

            _mockUserRepository.Setup(r => r.GetByEmailAsync(registrationData.Email))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _authService.RegisterAsync(registrationData);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Password", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithValidOldPassword_ChangesPassword()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var oldPassword = "OldPass123!@#";
            var newPassword = "NewPass456!@#";
            
            var user = new User
            {
                Id = userId,
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(oldPassword),
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                .ReturnsAsync(true);

            // Act
            var result = await _authService.ChangePasswordAsync(userId, oldPassword, newPassword);

            // Assert
            Assert.True(result.IsSuccess);
            _mockUserRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Once);
            
            _mockAuditService.Verify(a => a.LogAuthenticationAsync(userId, "PasswordChange", true, It.IsAny<string>()), Times.Once);
        }
    }
}