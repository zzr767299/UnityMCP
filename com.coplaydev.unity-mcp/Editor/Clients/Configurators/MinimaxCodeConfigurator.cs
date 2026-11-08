using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// MiniMax Code CLI MCP client configurator.
    /// MiniMax Code (npm: @minimax-ai/code) uses a JSON-based configuration file with a
    /// standard mcpServers section.
    /// Config path: ~/.minimax/mcp.json
    ///
    /// MiniMax Code supports both stdio (uvx) and HTTP transport modes.
    /// HTTP entries are written with type "streamable-http" — the token used by the
    /// official docs' HTTP example (plugin-distributed *.mcp.json files reject the
    /// generic "http" type, so "streamable-http" is the safe choice everywhere).
    /// Default: stdio mode (works without Unity Editor for basic operations)
    /// HTTP mode: requires Unity Editor running with MCP HTTP server started
    /// </summary>
    public class MinimaxCodeConfigurator : JsonFileMcpConfigurator
    {
        public MinimaxCodeConfigurator() : base(new McpClient
        {
            name = "MiniMax Code",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minimax", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minimax", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minimax", "mcp.json"),
            SupportsHttpTransport = true,
            HttpTypeValue = "streamable-http",
            DefaultUnityFields = { { "enabled", true }, { "timeout", 60000 } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure MiniMax Code CLI is installed (npm install -g @minimax-ai/code)",
            "Click 'Auto Configure' to automatically add UnityMCP to ~/.minimax/mcp.json",
            "OR click 'Manual Setup' to copy the configuration JSON",
            "Open ~/.minimax/mcp.json and paste the configuration",
            "Save and restart MiniMax Code",
            "Use 'mcode mcp list --human' to verify Unity MCP is connected",
            "Note: For full functionality, open Unity Editor and start HTTP server"
        };
    }
}
