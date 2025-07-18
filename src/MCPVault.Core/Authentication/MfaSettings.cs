namespace MCPVault.Core.Authentication
{
    public class MfaSettings
    {
        public string Issuer { get; set; } = "MCPVault";
        public int SecretLength { get; set; } = 32;
        public int CodeLength { get; set; } = 6;
        public int WindowSize { get; set; } = 3;
        public int QrCodeWidth { get; set; } = 200;
        public int QrCodeHeight { get; set; } = 200;
        public int BackupCodeCount { get; set; } = 10;
        public int BackupCodeLength { get; set; } = 8;
    }
}