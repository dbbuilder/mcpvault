using System;
using System.Collections.Generic;

namespace MCPVault.Core.KeyVault.Models
{
    public class KeyVaultSecret
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? ContentType { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public bool? Enabled { get; set; } = true;
    }

    public class KeyVaultKey
    {
        public string Name { get; set; } = string.Empty;
        public string? KeyMaterial { get; set; }
        public KeyType KeyType { get; set; }
        public string? Version { get; set; }
        public int? KeySize { get; set; }
        public List<KeyOperation>? Operations { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public bool? Enabled { get; set; } = true;
    }

    public enum KeyType
    {
        RSA,
        EC,
        Symmetric,
        Oct
    }

    public enum KeyOperation
    {
        Encrypt,
        Decrypt,
        Sign,
        Verify,
        WrapKey,
        UnwrapKey
    }

    public enum KeyVaultProviderType
    {
        Azure,
        AWS,
        GCP,
        HashiCorp,
        Local
    }

    public class KeyVaultConfiguration
    {
        public KeyVaultProviderType Provider { get; set; }
        public string? VaultUrl { get; set; }
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Region { get; set; }
        public string? ProjectId { get; set; }
        public string? ServiceAccountJson { get; set; }
        public Dictionary<string, string>? AdditionalSettings { get; set; }
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableCaching { get; set; } = true;
    }

    public class KeyVaultException : Exception
    {
        public string? ErrorCode { get; set; }
        public Dictionary<string, object>? Details { get; set; }

        public KeyVaultException(string message) : base(message)
        {
        }

        public KeyVaultException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        public KeyVaultException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    public class SecretMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string? Id { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public string? ContentType { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    public class KeyMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string? Id { get; set; }
        public KeyType KeyType { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }
}