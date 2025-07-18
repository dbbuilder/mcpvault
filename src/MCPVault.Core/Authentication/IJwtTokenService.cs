using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Authentication
{
    public interface IJwtTokenService
    {
        Task<TokenResult> GenerateTokenAsync(User user, string[] roles);
        Task<TokenValidationResult> ValidateTokenAsync(string token);
        Task<TokenResult?> RefreshTokenAsync(string refreshToken, Guid userId);
        string GenerateToken(Guid userId, string email, Guid organizationId);
        (bool isValid, Dictionary<string, string> claims) ValidateToken(string token);
        string GenerateRefreshToken();
    }

    public class TokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public ClaimsPrincipal? Principal { get; set; }
        public string? Error { get; set; }
    }
}