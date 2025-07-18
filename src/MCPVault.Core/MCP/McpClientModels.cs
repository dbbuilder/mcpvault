using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MCPVault.Core.MCP
{
    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("result")]
        public Dictionary<string, object>? Result { get; set; }

        [JsonPropertyName("error")]
        public McpError? Error { get; set; }
    }

    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }

    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public Dictionary<string, object>? Params { get; set; }
    }

    public class McpTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> InputSchema { get; set; } = new();
    }

    public class McpToolResult
    {
        public bool Success { get; set; }
        public List<McpContent> Content { get; set; } = new();
        public string? Error { get; set; }
    }

    public class McpContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }
        
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    public class McpResource
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = string.Empty;
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }
    }

    public class McpResourceContent
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = string.Empty;
        
        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }
        
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        
        [JsonPropertyName("blob")]
        public string? Blob { get; set; }
    }

    public class McpPrompt
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("arguments")]
        public List<McpPromptArgument> Arguments { get; set; } = new();
    }

    public class McpPromptArgument
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }

    public class McpPromptResult
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("messages")]
        public List<McpPromptMessage> Messages { get; set; } = new();
    }

    public class McpPromptMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public McpContent Content { get; set; } = new();
    }
}