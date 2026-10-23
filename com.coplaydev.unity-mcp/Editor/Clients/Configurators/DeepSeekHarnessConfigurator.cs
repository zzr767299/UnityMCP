using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// DeepSeek Harness (DSH) MCP client configurator.
    ///
    /// DSH has no settings page for MCP servers: a server is a plugin row in the profile's YAML
    /// patch layer, ~/.dsh/profiles/desktop/cordis.patch.yml, which the harness hot-reloads.
    /// @deepseek-ai/dsh-mcp-client ships inside the app but no bundle mounts it, so the row has to
    /// be introduced with an "insert:" patch entry rather than a bare override — see
    /// DeepSeekHarnessConfigHelper for the file format.
    ///
    /// DSH supports both stdio (uvx) and streamable HTTP transport modes.
    /// HTTP mode requires the Unity Editor running with the MCP HTTP server started.
    /// </summary>
    public class DeepSeekHarnessConfigurator : McpClientConfiguratorBase
    {
        private const string DshHomeDirectoryName = ".dsh";
        private const string ProfilesDirectoryName = "profiles";
        private const string DefaultProfileName = "desktop";
        private const string PatchFileName = "cordis.patch.yml";
        private const string SkillDirectoryName = "unity-mcp-skill";

        private static readonly string ResolvedConfigPath = BuildConfigPath();

        public DeepSeekHarnessConfigurator() : base(new McpClient
        {
            name = "DeepSeek Harness",
            windowsConfigPath = ResolvedConfigPath,
            macConfigPath = ResolvedConfigPath,
            linuxConfigPath = ResolvedConfigPath,
            SupportsHttpTransport = true
        })
        { }

        /// <summary>
        /// Resolves the patch layer of the profile DSH actually runs. The Electron app manages the
        /// "desktop" profile, so that path wins; if it is absent we fall back to any profile that
        /// already carries a patch file so a renamed profile still resolves.
        /// </summary>
        private static string BuildConfigPath()
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string profilesDirectory = Path.Combine(userHome, DshHomeDirectoryName, ProfilesDirectoryName);
            string desktopPatch = Path.Combine(profilesDirectory, DefaultProfileName, PatchFileName);

            try
            {
                if (Directory.Exists(Path.Combine(profilesDirectory, DefaultProfileName)))
                {
                    return desktopPatch;
                }

                if (Directory.Exists(profilesDirectory))
                {
                    string[] candidates = Directory.GetDirectories(profilesDirectory);
                    Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
                    foreach (string candidate in candidates)
                    {
                        string patchPath = Path.Combine(candidate, PatchFileName);
                        if (File.Exists(patchPath))
                        {
                            return patchPath;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to the canonical path; CheckStatus reports the miss.
            }

            return desktopPatch;
        }

        public override string GetConfigPath() => CurrentOsPath();

        public override bool SupportsSkills => true;

        public override string GetSkillInstallPath()
        {
            // DSH scans <dshHome>/skills for <name>/SKILL.md bundles (rank 400 in its search order).
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, DshHomeDirectoryName, "skills", SkillDirectoryName);
        }

        public override McpStatus CheckStatus(bool attemptAutoRewrite = true)
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path))
                {
                    client.SetStatus(McpStatus.NotConfigured);
                    client.configuredTransport = ConfiguredTransport.Unknown;
                    return client.status;
                }

                string yaml = File.ReadAllText(path);
                if (!DeepSeekHarnessConfigHelper.TryReadUnityEntry(yaml, out var entry))
                {
                    client.SetStatus(McpStatus.MissingConfig);
                    client.configuredTransport = ConfiguredTransport.Unknown;
                    return client.status;
                }

                ConfiguredTransport configured = ResolveTransport(entry);
                client.configuredTransport = configured;

                ConfiguredTransport expected = HttpEndpointUtility.GetCurrentServerTransport();
                bool matches = configured == expected;
                bool versionMismatch = false;
                string mismatchReason = null;

                if (matches)
                {
                    if (expected == ConfiguredTransport.Stdio)
                    {
                        // Validate the --from package source hasn't drifted (e.g. stable vs prerelease)
                        string configuredSource = McpConfigurationHelper.ExtractUvxUrl(entry.Args?.ToArray());
                        string expectedSource = GetExpectedPackageSourceForValidation();
                        if (!string.IsNullOrEmpty(configuredSource) && !string.IsNullOrEmpty(expectedSource))
                        {
                            if (McpConfigurationHelper.PathsEqual(configuredSource, expectedSource))
                            {
                                matches = true;
                            }
                            else
                            {
                                matches = false;
                                versionMismatch = true;
                                mismatchReason = DescribePackageMismatch(configuredSource, expectedSource);
                            }
                        }
                    }
                    else
                    {
                        matches = UrlsEqual(entry.Url, HttpEndpointUtility.GetMcpRpcUrl());
                    }
                }

                if (matches)
                {
                    client.SetStatus(McpStatus.Configured);
                    return client.status;
                }

                if (attemptAutoRewrite)
                {
                    Configure();
                }
                else
                {
                    client.SetStatus(
                        versionMismatch ? McpStatus.VersionMismatch : McpStatus.IncorrectPath,
                        mismatchReason);
                }
            }
            catch (Exception ex)
            {
                client.SetStatus(McpStatus.Error, ex.Message);
                client.configuredTransport = ConfiguredTransport.Unknown;
            }

            return client.status;
        }

        public override void Configure()
        {
            string path = GetConfigPath();

            string uvxPath = null;
            if (HttpEndpointUtility.GetCurrentServerTransport() == ConfiguredTransport.Stdio)
            {
                uvxPath = MCPServiceLocator.Paths.GetUvxPath();
                if (string.IsNullOrEmpty(uvxPath))
                {
                    throw new InvalidOperationException(
                        "uvx not found. Install uv/uvx or set the override in Advanced Settings.");
                }
            }

            string existingYaml = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string updatedYaml = DeepSeekHarnessConfigHelper.UpsertUnityEntry(existingYaml, uvxPath);

            McpConfigurationHelper.EnsureConfigDirectoryExists(path);
            McpConfigurationHelper.WriteAtomicFile(path, updatedYaml);

            client.SetStatus(McpStatus.Configured);
            client.configuredTransport = HttpEndpointUtility.GetCurrentServerTransport();
        }

        public override void Unregister()
        {
            string path = GetConfigPath();
            try
            {
                if (!File.Exists(path))
                {
                    client.SetStatus(McpStatus.NotConfigured);
                    client.configuredTransport = ConfiguredTransport.Unknown;
                    return;
                }

                string yaml = File.ReadAllText(path);
                string updatedYaml = DeepSeekHarnessConfigHelper.RemoveUnityEntry(yaml);
                if (!string.Equals(updatedYaml, yaml, StringComparison.Ordinal))
                {
                    McpConfigurationHelper.WriteAtomicFile(path, updatedYaml);
                }

                client.SetStatus(McpStatus.NotConfigured);
                client.configuredTransport = ConfiguredTransport.Unknown;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to unregister: {ex.Message}", ex);
            }
        }

        public override string GetConfigureActionLabel()
            => client.status == McpStatus.Configured ? "Unregister" : "Configure";

        public override string GetManualSnippet()
        {
            try
            {
                string uvxPath = HttpEndpointUtility.GetCurrentServerTransport() == ConfiguredTransport.Stdio
                    ? MCPServiceLocator.Paths.GetUvxPath()
                    : null;

                return $"# Add to {GetConfigPath()}{Environment.NewLine}"
                     + $"{Environment.NewLine}"
                     + DeepSeekHarnessConfigHelper.BuildManualSnippet(uvxPath);
            }
            catch (Exception ex)
            {
                return $"# error: {ex.Message}";
            }
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure DeepSeek Harness is installed (this writes its desktop profile patch file)",
            "Click Configure to add the unityMCP row to ~/.dsh/profiles/desktop/cordis.patch.yml",
            "OR click Manual Setup to copy the YAML and paste it into that file",
            "DSH hot-reloads the patch layer, so no restart is normally needed",
            "Note: keep HTTP Local selected while Unity is open, or switch to stdio to run the server standalone"
        };

        private ConfiguredTransport ResolveTransport(DeepSeekHarnessConfigHelper.UnityEntry entry)
        {
            if (string.Equals(entry.Transport, DeepSeekHarnessConfigHelper.StdioTransport,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ConfiguredTransport.Stdio;
            }

            if (string.Equals(entry.Transport, DeepSeekHarnessConfigHelper.HttpTransport,
                    StringComparison.OrdinalIgnoreCase))
            {
                string remoteUrl = HttpEndpointUtility.GetRemoteMcpRpcUrl();
                if (!string.IsNullOrEmpty(remoteUrl) && UrlsEqual(entry.Url, remoteUrl))
                {
                    return ConfiguredTransport.HttpRemote;
                }

                return ConfiguredTransport.Http;
            }

            return ConfiguredTransport.Unknown;
        }

        private static string DescribePackageMismatch(string configuredSource, string expectedSource)
        {
            bool configuredIsBeta = IsBetaPackageSource(configuredSource);
            bool expectedIsBeta = IsBetaPackageSource(expectedSource);

            if (configuredIsBeta && !expectedIsBeta)
            {
                return "Configured for prerelease server, but this package is stable. Re-configure to switch to stable.";
            }

            if (!configuredIsBeta && expectedIsBeta)
            {
                return "Configured for stable server, but this package is prerelease. Re-configure to switch to prerelease.";
            }

            return "Server version doesn't match the plugin. Re-configure to update.";
        }
    }
}
