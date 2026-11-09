using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// CodeBuddy CN (腾讯 CodeBuddy IDE 国内版) MCP client configurator.
    ///
    /// The IDE's agent keeps user-scope MCP servers in ~/.codebuddy/mcp.json under the
    /// standard mcpServers container. The IDE creates that file as {"mcpServers":{}} on
    /// first use and watches it, so edits apply without a restart. Project-scope servers
    /// live in the workspace's .mcp.json; this configurator only ever writes the user file.
    ///
    /// Schema enforced by the bundled genie extension:
    ///   - stdio: type "stdio" plus command/args/env
    ///   - HTTP:  type must be "streamableHttp" — the generic "http" token fails the union
    ///            and the server is dropped — plus url and optional headers
    ///   - enablement is a "disabled" flag (absent = enabled), so no "enabled" key is written
    ///   - timeout is in milliseconds and the extension's own default is 60000, which also
    ///     covers a cold uvx start
    ///
    /// CodeBuddy CN supports both stdio (uvx) and HTTP transport modes.
    /// HTTP mode requires the Unity Editor running with the MCP HTTP server started.
    /// </summary>
    public class CodeBuddyCnConfigurator : JsonFileMcpConfigurator
    {
        private const string ConfigFileName = "mcp.json";

        public CodeBuddyCnConfigurator() : base(new McpClient
        {
            name = "CodeBuddy CN",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codebuddy", ConfigFileName),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codebuddy", ConfigFileName),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codebuddy", ConfigFileName),
            SupportsHttpTransport = true,
            HttpTypeValue = "streamableHttp",
            DefaultUnityFields = { { "timeout", 60000 } }
        })
        { }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure CodeBuddy CN is installed and has been launched once (config dir ~/.codebuddy)",
            "Click 'Auto Configure' to add UnityMCP to ~/.codebuddy/mcp.json",
            "OR click 'Manual Setup' to copy the configuration JSON",
            "Paste it into ~/.codebuddy/mcp.json, or use the IDE's MCP settings panel (it opens the same file)",
            "The IDE watches the file; if the server does not appear, reload the window",
            "Note: keep HTTP Local selected while Unity is open, or switch to stdio to run the server standalone"
        };
    }
}
