namespace MCPVault.Core.Security
{
    public class EncryptedData
    {
        public string CipherText { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Algorithm { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
    }
}