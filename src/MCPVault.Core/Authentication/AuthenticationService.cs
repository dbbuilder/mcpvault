using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MCPVault.Core.Interfaces;
using MCPVault.Domain.Entities;
using BCrypt.Net;

namespace MCPVault.Core.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IMfaService _mfaService;
        private readonly IAuditService _auditService;
        private readonly AuthenticationSettings _settings;
        private readonly ILogger<AuthenticationService> _logger;
        
        // In-memory storage for MFA tokens and refresh tokens (in production, use distributed cache)
        private readonly ConcurrentDictionary<string, Guid> _mfaTokens = new();
        private readonly ConcurrentDictionary<string, RefreshTokenInfo> _refreshTokens = new();

        public AuthenticationService(
            IUserRepository userRepository,
            IJwtTokenService jwtTokenService,
            IMfaService mfaService,
            IAuditService auditService,
            IOptions<AuthenticationSettings> settings,
            ILogger<AuthenticationService> logger)
        {
            _userRepository = userRepository;
            _jwtTokenService = jwtTokenService;
            _mfaService = mfaService;
            _auditService = auditService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<LoginResult> LoginAsync(string email, string password, string? ipAddress = null)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", email);
                    return new LoginResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Invalid email or password"
                    };
                }

                // Check if account is locked
                if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
                {
                    var remainingTime = user.LockedUntil.Value.Subtract(DateTime.UtcNow);
                    await _auditService.LogAuthenticationAsync(user.Id, "Login", false, ipAddress);
                    
                    return new LoginResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Account is locked. Try again in {Math.Ceiling(remainingTime.TotalMinutes)} minutes."
                    };
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    await _auditService.LogAuthenticationAsync(user.Id, "Login", false, ipAddress);
                    return new LoginResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Account is inactive"
                    };
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    await _userRepository.IncrementFailedLoginAttemptsAsync(user.Id);
                    
                    // Check if we need to lock the account
                    if (user.FailedLoginAttempts + 1 >= _settings.MaxFailedAttempts)
                    {
                        var lockUntil = DateTime.UtcNow.AddMinutes(_settings.LockoutDuration);
                        await _userRepository.LockUserAsync(user.Id, lockUntil);
                    }

                    await _auditService.LogAuthenticationAsync(user.Id, "Login", false, ipAddress);
                    
                    return new LoginResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Invalid email or password"
                    };
                }

                // Reset failed login attempts on successful password verification
                if (user.FailedLoginAttempts > 0)
                {
                    await _userRepository.ResetFailedLoginAttemptsAsync(user.Id);
                }

                // Check if MFA is enabled
                if (user.IsMfaEnabled)
                {
                    var mfaToken = Guid.NewGuid().ToString();
                    StoreMfaToken(mfaToken, user.Id);
                    
                    _logger.LogInformation("MFA required for user {UserId}", user.Id);
                    
                    return new LoginResult
                    {
                        IsSuccess = true,
                        RequiresMfa = true,
                        MfaToken = mfaToken
                    };
                }

                // Generate tokens
                var accessToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.OrganizationId);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();
                
                // Store refresh token
                var refreshTokenInfo = new RefreshTokenInfo
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
                    IsRevoked = false
                };
                StoreRefreshToken(refreshToken, refreshTokenInfo);

                // Update last login
                await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);
                await _auditService.LogAuthenticationAsync(user.Id, "Login", true, ipAddress);

                return new LoginResult
                {
                    IsSuccess = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    RequiresMfa = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", email);
                return new LoginResult
                {
                    IsSuccess = false,
                    ErrorMessage = "An error occurred during login"
                };
            }
        }

        public async Task<MfaResult> CompleteMfaAsync(string mfaToken, string code)
        {
            try
            {
                // Validate MFA token
                if (!_mfaTokens.TryGetValue(mfaToken, out var userId))
                {
                    return new MfaResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Invalid or expired MFA token"
                    };
                }

                // Remove the token to prevent reuse
                _mfaTokens.TryRemove(mfaToken, out _);

                // Validate MFA code
                var isValidCode = await _mfaService.ValidateCodeAsync(userId, code);
                if (!isValidCode)
                {
                    await _auditService.LogAuthenticationAsync(userId, "MFA_Complete", false, null);
                    return new MfaResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Invalid MFA code"
                    };
                }

                // Get user details for token generation
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return new MfaResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found or inactive"
                    };
                }

                // Generate tokens
                var accessToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.OrganizationId);
                var refreshToken = _jwtTokenService.GenerateRefreshToken();
                
                // Store refresh token
                var refreshTokenInfo = new RefreshTokenInfo
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
                    IsRevoked = false
                };
                StoreRefreshToken(refreshToken, refreshTokenInfo);

                // Update last login
                await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);
                await _auditService.LogAuthenticationAsync(user.Id, "MFA_Complete", true, null);

                return new MfaResult
                {
                    IsSuccess = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MFA completion");
                return new MfaResult
                {
                    IsSuccess = false,
                    ErrorMessage = "An error occurred during MFA verification"
                };
            }
        }

        public async Task<RefreshResult> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                // Validate refresh token
                if (!_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
                {
                    return new RefreshResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Invalid refresh token"
                    };
                }

                // Check if token is revoked or expired
                if (tokenInfo.IsRevoked || tokenInfo.ExpiresAt < DateTime.UtcNow)
                {
                    return new RefreshResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Refresh token is invalid or expired"
                    };
                }

                // Get user
                var user = await _userRepository.GetByIdAsync(tokenInfo.UserId);
                if (user == null || !user.IsActive)
                {
                    return new RefreshResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found or inactive"
                    };
                }

                // Revoke old refresh token
                tokenInfo.IsRevoked = true;
                tokenInfo.RevokedAt = DateTime.UtcNow;

                // Generate new tokens
                var newAccessToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.OrganizationId);
                var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
                
                // Store new refresh token
                var newRefreshTokenInfo = new RefreshTokenInfo
                {
                    UserId = user.Id,
                    Token = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
                    IsRevoked = false
                };
                StoreRefreshToken(newRefreshToken, newRefreshTokenInfo);

                await _auditService.LogAuthenticationAsync(user.Id, "TokenRefresh", true, null);

                return new RefreshResult
                {
                    IsSuccess = true,
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return new RefreshResult
                {
                    IsSuccess = false,
                    ErrorMessage = "An error occurred during token refresh"
                };
            }
        }

        public async Task<bool> LogoutAsync(string accessToken, string refreshToken)
        {
            try
            {
                // Validate access token to get user ID
                var (isValid, claims) = _jwtTokenService.ValidateToken(accessToken);
                if (!isValid || !claims.TryGetValue("sub", out var userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                {
                    return false;
                }

                // Revoke refresh token
                if (_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
                {
                    tokenInfo.IsRevoked = true;
                    tokenInfo.RevokedAt = DateTime.UtcNow;
                }

                await _auditService.LogAuthenticationAsync(userId, "Logout", true, null);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return false;
            }
        }

        public async Task<RegistrationResult> RegisterAsync(UserRegistrationData registrationData)
        {
            try
            {
                // Check if email already exists
                var existingUser = await _userRepository.GetByEmailAsync(registrationData.Email);
                if (existingUser != null)
                {
                    return new RegistrationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Email already registered"
                    };
                }

                // Validate password
                if (!await ValidatePasswordAsync(registrationData.Password))
                {
                    return new RegistrationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Password does not meet security requirements"
                    };
                }

                // Create new user
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = registrationData.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(registrationData.Password),
                    FirstName = registrationData.FirstName,
                    LastName = registrationData.LastName,
                    OrganizationId = registrationData.OrganizationId,
                    IsActive = true,
                    IsMfaEnabled = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var createdUser = await _userRepository.CreateAsync(user);
                await _auditService.LogAuthenticationAsync(createdUser.Id, "Registration", true, null);

                return new RegistrationResult
                {
                    IsSuccess = true,
                    User = createdUser
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new RegistrationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "An error occurred during registration"
                };
            }
        }

        public async Task<PasswordChangeResult> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return new PasswordChangeResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found"
                    };
                }

                // Verify old password
                if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
                {
                    await _auditService.LogAuthenticationAsync(userId, "PasswordChange", false, null);
                    return new PasswordChangeResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Current password is incorrect"
                    };
                }

                // Validate new password
                if (!await ValidatePasswordAsync(newPassword))
                {
                    return new PasswordChangeResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "New password does not meet security requirements"
                    };
                }

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                
                await _userRepository.UpdateAsync(user);
                await _auditService.LogAuthenticationAsync(userId, "PasswordChange", true, null);

                return new PasswordChangeResult
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password change for user {UserId}", userId);
                return new PasswordChangeResult
                {
                    IsSuccess = false,
                    ErrorMessage = "An error occurred during password change"
                };
            }
        }

        public Task<bool> ValidatePasswordAsync(string password)
        {
            if (string.IsNullOrEmpty(password))
                return Task.FromResult(false);

            if (password.Length < _settings.PasswordMinLength)
                return Task.FromResult(false);

            if (_settings.RequireUppercase && !password.Any(char.IsUpper))
                return Task.FromResult(false);

            if (_settings.RequireLowercase && !password.Any(char.IsLower))
                return Task.FromResult(false);

            if (_settings.RequireDigit && !password.Any(char.IsDigit))
                return Task.FromResult(false);

            if (_settings.RequireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*(),.?"":{}|<>]"))
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public void StoreMfaToken(string token, Guid userId)
        {
            // Store with 5-minute expiry
            _mfaTokens.TryAdd(token, userId);
            
            // In production, implement token expiry cleanup
            Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                _mfaTokens.TryRemove(token, out var removedUserId);
            });
        }

        public void StoreRefreshToken(string token, RefreshTokenInfo info)
        {
            _refreshTokens.TryAdd(token, info);
        }

        public bool IsRefreshTokenRevoked(string token)
        {
            return _refreshTokens.TryGetValue(token, out var info) && info.IsRevoked;
        }
    }
}