namespace MCPVault.Core.Configuration
{
    public class EncryptionSettings
    {
        public string MasterKey { get; set; } = string.Empty;
        public string Algorithm { get; set; } = "AES-256-GCM";
        public int KeyDerivationIterations { get; set; } = 100000;
        public int SaltSize { get; set; } = 32;
        public int NonceSize { get; set; } = 12;
        public int TagSize { get; set; } = 16;
        public int KeySize { get; set; } = 32; // 256 bits
    }
}