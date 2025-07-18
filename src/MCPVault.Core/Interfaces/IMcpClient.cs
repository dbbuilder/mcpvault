using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.MCP;
using MCPVault.Core.MCP.Models;

namespace MCPVault.Core.Interfaces
{
    public interface IMcpClient
    {
        bool IsConnected { get; }
        
        Task<bool> ConnectAsync(string serverUrl);
        Task DisconnectAsync();
        
        Task<List<McpTool>> GetToolsAsync();
        Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> arguments, TimeSpan? timeout = null);
        
        Task<List<McpResource>> GetResourcesAsync();
        Task<McpResourceContent> ReadResourceAsync(string uri);
        
        Task<List<McpPrompt>> GetPromptsAsync();
        Task<McpPromptResult> GetPromptAsync(string name, Dictionary<string, object> arguments);
        
        // New methods for server registry support
        void ConfigureConnection(McpConnectionInfo connectionInfo, McpCredentials? credentials = null);
        Task<McpToolResponse> SendRequestAsync(McpToolRequest request);
    }
}