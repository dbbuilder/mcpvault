using System;

namespace MCPVault.Core.Security
{
    public class CryptographicException : Exception
    {
        public CryptographicException() : base()
        {
        }

        public CryptographicException(string message) : base(message)
        {
        }

        public CryptographicException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}