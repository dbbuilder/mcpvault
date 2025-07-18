using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP;
using OtpNet;

namespace MCPVault.Core.Authentication
{
    public class MfaService : IMfaService
    {
        private readonly IUserRepository _userRepository;
        private readonly MfaSettings _settings;
        private readonly ILogger<MfaService> _logger;
        private readonly RandomNumberGenerator _rng;

        public MfaService(
            IUserRepository userRepository,
            IOptions<MfaSettings> settings,
            ILogger<MfaService> logger)
        {
            _userRepository = userRepository;
            _settings = settings.Value;
            _logger = logger;
            _rng = RandomNumberGenerator.Create();
        }

        public async Task<MfaSetupResult> GenerateSecretAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            // Generate a new secret
            var secret = GenerateSecret();
            var base32Secret = Base32Encoding.ToString(secret);

            // Update user with new secret
            user.MfaSecret = base32Secret;
            await _userRepository.UpdateAsync(user);

            // Generate TOTP URI for QR code
            var totpUri = GenerateTotpUri(user.Email, base32Secret);

            _logger.LogInformation("Generated new MFA secret for user {UserId}", userId);

            return new MfaSetupResult
            {
                Secret = base32Secret,
                QrCodeUri = totpUri,
                ManualEntryKey = FormatSecretForManualEntry(base32Secret)
            };
        }

        public async Task<MfaEnableResult> EnableMfaAsync(Guid userId, string code)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            if (string.IsNullOrEmpty(user.MfaSecret))
            {
                throw new InvalidOperationException("MFA secret not generated for user");
            }

            // Validate the code
            var isValid = ValidateTotpCode(user.MfaSecret, code);
            if (!isValid)
            {
                _logger.LogWarning("Invalid MFA code provided for user {UserId}", userId);
                return new MfaEnableResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid verification code"
                };
            }

            // Enable MFA
            user.IsMfaEnabled = true;
            await _userRepository.UpdateAsync(user);

            // Generate backup codes
            var backupCodes = GenerateBackupCodesList();

            // In a real implementation, store hashed backup codes in database
            // For now, we'll just return them

            _logger.LogInformation("MFA enabled for user {UserId}", userId);

            return new MfaEnableResult
            {
                IsSuccess = true,
                BackupCodes = backupCodes
            };
        }

        public async Task<bool> DisableMfaAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            user.IsMfaEnabled = false;
            user.MfaSecret = null;
            
            var result = await _userRepository.UpdateAsync(user);
            
            if (result)
            {
                _logger.LogInformation("MFA disabled for user {UserId}", userId);
            }

            return result;
        }

        public async Task<bool> ValidateCodeAsync(Guid userId, string code)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || !user.IsMfaEnabled || string.IsNullOrEmpty(user.MfaSecret))
            {
                return false;
            }

            return ValidateTotpCode(user.MfaSecret, code);
        }

        public async Task<List<string>> GenerateBackupCodesAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException($"User {userId} not found");
            }

            if (!user.IsMfaEnabled)
            {
                throw new InvalidOperationException("MFA not enabled for user");
            }

            var backupCodes = GenerateBackupCodesList();
            
            // In a real implementation, store hashed backup codes in database
            _logger.LogInformation("Generated new backup codes for user {UserId}", userId);

            return backupCodes;
        }

        public async Task<bool> ValidateBackupCodeAsync(Guid userId, string backupCode)
        {
            // In a real implementation, this would:
            // 1. Look up the user's backup codes from database
            // 2. Hash the provided code and compare
            // 3. Mark the code as used if valid
            // 4. Return true if valid and unused

            await Task.CompletedTask;
            
            // Placeholder implementation
            _logger.LogInformation("Backup code validation attempted for user {UserId}", userId);
            return false;
        }

        public string GenerateTotpUri(string email, string secret)
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var encodedIssuer = Uri.EscapeDataString(_settings.Issuer);
            
            return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}";
        }

        private byte[] GenerateSecret()
        {
            var secret = new byte[_settings.SecretLength];
            _rng.GetBytes(secret);
            return secret;
        }

        private bool ValidateTotpCode(string secret, string code)
        {
            try
            {
                var secretBytes = Base32Encoding.ToBytes(secret);
                var totp = new Totp(secretBytes);
                
                // Verify with time window
                return totp.VerifyTotp(code, out _, new VerificationWindow(_settings.WindowSize, _settings.WindowSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating TOTP code");
                return false;
            }
        }

        private string FormatSecretForManualEntry(string secret)
        {
            // Format secret in groups of 4 characters for easier manual entry
            var formatted = new StringBuilder();
            for (int i = 0; i < secret.Length; i += 4)
            {
                if (i > 0) formatted.Append(' ');
                formatted.Append(secret.Substring(i, Math.Min(4, secret.Length - i)));
            }
            return formatted.ToString();
        }

        private List<string> GenerateBackupCodesList()
        {
            var codes = new List<string>();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            for (int i = 0; i < _settings.BackupCodeCount; i++)
            {
                var code = new char[_settings.BackupCodeLength];
                var buffer = new byte[_settings.BackupCodeLength];
                _rng.GetBytes(buffer);
                
                for (int j = 0; j < _settings.BackupCodeLength; j++)
                {
                    code[j] = chars[buffer[j] % chars.Length];
                }
                
                codes.Add(new string(code));
            }
            
            return codes;
        }

        public void Dispose()
        {
            _rng?.Dispose();
        }
    }
}