using System;

namespace MCPVault.Core.MCP
{
    public class McpException : Exception
    {
        public string? ErrorCode { get; set; }
        
        public McpException(string message) : base(message) { }
        
        public McpException(string message, Exception innerException) 
            : base(message, innerException) { }
        
        public McpException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    public class NotFoundException : McpException
    {
        public NotFoundException(string message) : base(message, "NOT_FOUND") { }
    }

    public class UnauthorizedException : McpException
    {
        public UnauthorizedException(string message) : base(message, "UNAUTHORIZED") { }
    }

    public class RateLimitExceededException : McpException
    {
        public int RetryAfterSeconds { get; set; }
        
        public RateLimitExceededException(string message, int retryAfterSeconds = 60) 
            : base(message, "RATE_LIMIT_EXCEEDED")
        {
            RetryAfterSeconds = retryAfterSeconds;
        }
    }

    public class McpServerException : McpException
    {
        public int? StatusCode { get; set; }
        
        public McpServerException(string message, int? statusCode = null) 
            : base(message, "SERVER_ERROR")
        {
            StatusCode = statusCode;
        }
    }

    public class McpTimeoutException : McpException
    {
        public McpTimeoutException(string message) : base(message, "TIMEOUT") { }
    }

    public class McpValidationException : McpException
    {
        public McpValidationException(string message) : base(message, "VALIDATION_ERROR") { }
    }
}