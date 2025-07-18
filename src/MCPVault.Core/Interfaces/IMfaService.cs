using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCPVault.Core.Interfaces
{
    public interface IMfaService
    {
        Task<MfaSetupResult> GenerateSecretAsync(Guid userId);
        Task<MfaEnableResult> EnableMfaAsync(Guid userId, string code);
        Task<bool> DisableMfaAsync(Guid userId);
        Task<bool> ValidateCodeAsync(Guid userId, string code);
        Task<List<string>> GenerateBackupCodesAsync(Guid userId);
        Task<bool> ValidateBackupCodeAsync(Guid userId, string backupCode);
        string GenerateTotpUri(string email, string secret);
    }

    public class MfaSetupResult
    {
        public string Secret { get; set; } = string.Empty;
        public string QrCodeUri { get; set; } = string.Empty;
        public string ManualEntryKey { get; set; } = string.Empty;
    }

    public class MfaEnableResult
    {
        public bool IsSuccess { get; set; }
        public List<string> BackupCodes { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}