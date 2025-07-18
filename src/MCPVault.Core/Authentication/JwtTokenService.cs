using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MCPVault.Core.Configuration;
using MCPVault.Domain.Entities;

namespace MCPVault.Core.Authentication
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public JwtTokenService(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        public Task<TokenResult> GenerateTokenAsync(User user, string[] roles)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
            var signingKey = new SymmetricSecurityKey(key);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("organization_id", user.OrganizationId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var now = DateTime.UtcNow;
            var expires = _jwtSettings.ExpirationMinutes > 0 
                ? now.AddMinutes(_jwtSettings.ExpirationMinutes) 
                : now.AddSeconds(1);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                NotBefore = now,
                Expires = expires,
                IssuedAt = now,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = _tokenHandler.WriteToken(token);

            var refreshToken = GenerateRefreshTokenPrivate();

            var result = new TokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = tokenDescriptor.Expires.Value
            };

            return Task.FromResult(result);
        }

        public Task<TokenValidationResult> ValidateTokenAsync(string token)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "nameid",
                    RoleClaimType = "role"
                };

                var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                return Task.FromResult(new TokenValidationResult
                {
                    IsValid = true,
                    Principal = principal
                });
            }
            catch (SecurityTokenExpiredException)
            {
                return Task.FromResult(new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Token has expired"
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new TokenValidationResult
                {
                    IsValid = false,
                    Error = ex.Message
                });
            }
        }

        public async Task<TokenResult?> RefreshTokenAsync(string refreshToken, Guid userId)
        {
            if (!IsValidRefreshToken(refreshToken))
                return null;

            var user = new User
            {
                Id = userId,
                Email = "refreshed@example.com",
                OrganizationId = Guid.NewGuid()
            };

            var roles = new[] { "User" };

            return await GenerateTokenAsync(user, roles);
        }

        public string GenerateToken(Guid userId, string email, Guid organizationId)
        {
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
            var signingKey = new SymmetricSecurityKey(key);

            var now = DateTime.UtcNow;
            var expires = _jwtSettings.ExpirationMinutes > 0 
                ? now.AddMinutes(_jwtSettings.ExpirationMinutes) 
                : now.AddSeconds(1);

            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("email", email),
                new Claim("org", organizationId.ToString()),
                new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                NotBefore = now,
                Expires = expires,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            return _tokenHandler.WriteToken(token);
        }

        public (bool isValid, Dictionary<string, string> claims) ValidateToken(string token)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
                var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);
                
                return (true, claims);
            }
            catch
            {
                return (false, new Dictionary<string, string>());
            }
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private string GenerateRefreshTokenPrivate()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private bool IsValidRefreshToken(string refreshToken)
        {
            try
            {
                var bytes = Convert.FromBase64String(refreshToken);
                return bytes.Length == 32;
            }
            catch
            {
                return false;
            }
        }
    }
}