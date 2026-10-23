using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// ZCode MCP client configurator.
    /// ZCode reads MCP servers from ~/.zcode/cli/config.json under the nested "mcp.servers"
    /// container (a same-scope .agents/mcp.json is only a fallback and is ignored once .zcode
    /// defines any server, so this configurator always writes the .zcode file).
    ///
    /// The configuration schema is strict — an unknown key makes ZCode drop the server — so only
    /// canonical keys are written: type/url/command/args/env/headers/enabled/timeoutMs.
    ///
    /// ZCode supports both stdio (uvx) and HTTP transport modes.
    /// HTTP mode requires the Unity Editor running with the MCP HTTP server started.
    /// </summary>
    public class ZCodeConfigurator : JsonFileMcpConfigurator
    {
        private const string ConfigFileName = "config.json";

        public ZCodeConfigurator() : base(new McpClient
        {
            name = "ZCode",
            windowsConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zcode", "cli", ConfigFileName),
            macConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zcode", "cli", ConfigFileName),
            linuxConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zcode", "cli", ConfigFileName),
            ServerContainerKey = "mcp.servers",
            SupportsHttpTransport = true,
            // ZCode defaults to a 30s timeout; give the server the same headroom the plugin
            // grants Codex (60s) so a cold uvx start does not trip the tool-listing timeout.
            DefaultUnityFields = { { "enabled", true }, { "timeoutMs", 60000 } }
        })
        { }

        public override bool SupportsSkills => true;

        public override string GetSkillInstallPath()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".zcode", "skills", "unity-mcp-skill");
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure ZCode is installed (this writes its user-scope config)",
            "Click Configure to add UnityMCP to ~/.zcode/cli/config.json under mcp.servers",
            "OR click Manual Setup to copy the configuration JSON and paste it into that file",
            "Restart ZCode, then confirm the server in Settings > MCP",
            "Note: keep HTTP Local selected while Unity is open, or switch to stdio to run the server standalone"
        };
    }
}
