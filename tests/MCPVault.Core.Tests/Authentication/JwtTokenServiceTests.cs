using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;
using MCPVault.Core.Authentication;
using MCPVault.Core.Configuration;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Tests.Authentication
{
    public class JwtTokenServiceTests
    {
        private readonly Mock<IOptions<JwtSettings>> _jwtSettingsMock;
        private readonly JwtSettings _jwtSettings;
        private readonly JwtTokenService _tokenService;

        public JwtTokenServiceTests()
        {
            _jwtSettings = new JwtSettings
            {
                SecretKey = "test-secret-key-that-is-long-enough-for-hmac-sha256",
                Issuer = "MCPVault",
                Audience = "MCPVault",
                ExpirationMinutes = 30,
                RefreshExpirationDays = 7
            };

            _jwtSettingsMock = new Mock<IOptions<JwtSettings>>();
            _jwtSettingsMock.Setup(x => x.Value).Returns(_jwtSettings);

            _tokenService = new JwtTokenService(_jwtSettingsMock.Object);
        }

        [Fact]
        public async Task GenerateTokenAsync_WithValidUser_ReturnsValidTokenResult()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var roles = new[] { "Admin", "User" };

            var result = await _tokenService.GenerateTokenAsync(user, roles);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.RefreshToken));
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task GenerateTokenAsync_TokenContainsExpectedClaims()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var roles = new[] { "Admin", "User" };

            var result = await _tokenService.GenerateTokenAsync(user, roles);

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.ReadJwtToken(result.AccessToken);

            Assert.Equal(user.Id.ToString(), token.Claims.FirstOrDefault(x => x.Type == "nameid")?.Value);
            Assert.Equal(user.Email, token.Claims.FirstOrDefault(x => x.Type == "email")?.Value);
            Assert.Equal(user.OrganizationId.ToString(), token.Claims.FirstOrDefault(x => x.Type == "organization_id")?.Value);
            Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "Admin");
            Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "User");
        }

        [Fact]
        public async Task ValidateTokenAsync_WithValidToken_ReturnsSuccessResult()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var tokenResult = await _tokenService.GenerateTokenAsync(user, new[] { "User" });

            var validationResult = await _tokenService.ValidateTokenAsync(tokenResult.AccessToken);

            Assert.True(validationResult.IsValid);
            Assert.NotNull(validationResult.Principal);
            var nameIdClaim = validationResult.Principal.FindFirst("nameid") 
                ?? validationResult.Principal.FindFirst(ClaimTypes.NameIdentifier);
            Assert.Equal(user.Id.ToString(), nameIdClaim?.Value);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithExpiredToken_ReturnsInvalidResult()
        {
            var expiredSettings = new JwtSettings
            {
                SecretKey = _jwtSettings.SecretKey,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                ExpirationMinutes = 0,
                RefreshExpirationDays = 7
            };

            var expiredSettingsMock = new Mock<IOptions<JwtSettings>>();
            expiredSettingsMock.Setup(x => x.Value).Returns(expiredSettings);

            var expiredTokenService = new JwtTokenService(expiredSettingsMock.Object);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var tokenResult = await expiredTokenService.GenerateTokenAsync(user, new[] { "User" });
            await Task.Delay(1500);

            var validationResult = await expiredTokenService.ValidateTokenAsync(tokenResult.AccessToken);

            Assert.False(validationResult.IsValid);
            Assert.Contains("expired", validationResult.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithInvalidToken_ReturnsInvalidResult()
        {
            var invalidToken = "invalid.token.here";

            var validationResult = await _tokenService.ValidateTokenAsync(invalidToken);

            Assert.False(validationResult.IsValid);
            Assert.NotNull(validationResult.Error);
        }

        [Fact]
        public async Task GenerateTokenAsync_WithNullUser_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _tokenService.GenerateTokenAsync(null!, new[] { "User" })
            );
        }

        [Fact]
        public async Task GenerateTokenAsync_WithEmptyRoles_GeneratesTokenWithNoRoleClaims()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var result = await _tokenService.GenerateTokenAsync(user, Array.Empty<string>());

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.ReadJwtToken(result.AccessToken);

            Assert.DoesNotContain(token.Claims, c => c.Type == "role");
        }

        [Fact]
        public async Task RefreshTokenAsync_WithValidRefreshToken_ReturnsNewTokens()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var originalToken = await _tokenService.GenerateTokenAsync(user, new[] { "User" });

            var refreshResult = await _tokenService.RefreshTokenAsync(originalToken.RefreshToken, user.Id);

            Assert.NotNull(refreshResult);
            Assert.NotEqual(originalToken.AccessToken, refreshResult.AccessToken);
            Assert.NotEqual(originalToken.RefreshToken, refreshResult.RefreshToken);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithInvalidRefreshToken_ReturnsNull()
        {
            var invalidRefreshToken = "invalid-refresh-token";
            var userId = Guid.NewGuid();

            var refreshResult = await _tokenService.RefreshTokenAsync(invalidRefreshToken, userId);

            Assert.Null(refreshResult);
        }
    }
}