using System;
using System.Threading.Tasks;
using MCPVault.Core.Authentication;

namespace MCPVault.Core.Interfaces
{
    public interface IAuthenticationService
    {
        Task<LoginResult> LoginAsync(string email, string password, string? ipAddress = null);
        Task<MfaResult> CompleteMfaAsync(string mfaToken, string code);
        Task<RefreshResult> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string accessToken, string refreshToken);
        Task<RegistrationResult> RegisterAsync(UserRegistrationData registrationData);
        Task<PasswordChangeResult> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword);
        Task<bool> ValidatePasswordAsync(string password);
        void StoreMfaToken(string token, Guid userId);
        void StoreRefreshToken(string token, RefreshTokenInfo info);
        bool IsRefreshTokenRevoked(string token);
    }
}

namespace MCPVault.Core.Authentication
{
    public class LoginResult
    {
        public bool IsSuccess { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public bool RequiresMfa { get; set; }
        public string? MfaToken { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class MfaResult
    {
        public bool IsSuccess { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RefreshResult
    {
        public bool IsSuccess { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RegistrationResult
    {
        public bool IsSuccess { get; set; }
        public Domain.Entities.User? User { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PasswordChangeResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class UserRegistrationData
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public Guid OrganizationId { get; set; }
    }

    public class RefreshTokenInfo
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
    }

    public class AuthenticationSettings
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutDuration { get; set; } = 30;
        public int PasswordMinLength { get; set; } = 8;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialChar { get; set; } = true;
        public int RefreshTokenExpirationDays { get; set; } = 30;
    }
}