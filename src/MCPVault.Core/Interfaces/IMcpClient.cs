using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPVault.Core.MCP;

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
    }
}