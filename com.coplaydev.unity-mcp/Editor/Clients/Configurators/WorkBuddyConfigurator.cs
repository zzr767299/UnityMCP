using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// WorkBuddy MCP client configurator.
    /// WorkBuddy (desktop app, a CodeBuddy-family rebrand) reads MCP servers from
    /// ~/.workbuddy/mcp.json (override via WORKBUDDY_CONFIG_DIR) under the standard
    /// mcpServers container.
    ///
    /// HTTP entries use type "http" — the token the app itself writes (it normalizes
    /// "streamable-http" to "http" when importing). Per-server fields follow the
    /// app's own writer: type/command/args/url, optional headers, "timeout" in ms;
    /// enablement is controlled by a "disabled" flag (absent = enabled), so no
    /// "enabled" key is written.
    ///
    /// WorkBuddy supports both stdio (uvx) and HTTP transport modes.
    /// Default: stdio mode (works without Unity Editor for basic operations)
    /// HTTP mode: requires Unity Editor running with MCP HTTP server started
    /// </summary>
    public class WorkBuddyConfigurator : JsonFileMcpConfigurator
    {
        public WorkBuddyConfigurator() : base(new McpClient
        {
            name = "WorkBuddy",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".workbuddy", "mcp.json"),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".workbuddy", "mcp.json"),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".workbuddy", "mcp.json"),
            SupportsHttpTransport = true,
            DefaultUnityFields = { { "timeout", 60000 } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure the WorkBuddy desktop app is installed (config dir ~/.workbuddy)",
            "Click 'Auto Configure' to automatically add UnityMCP to ~/.workbuddy/mcp.json",
            "OR click 'Manual Setup' to copy the configuration JSON",
            "Open WorkBuddy > CodeBuddy Settings (gear icon) > Add MCP, or edit ~/.workbuddy/mcp.json directly",
            "Restart WorkBuddy and approve/trust the unityMCP server if prompted",
            "Note: For full functionality, open Unity Editor and start HTTP server"
        };
    }
}
