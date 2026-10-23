using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    public static class ConfigJsonBuilder
    {
        public static string BuildManualConfigJson(string uvPath, McpClient client)
        {
            var root = new JObject();
            bool isVSCode = client?.IsVsCodeLayout == true;
            if (!string.IsNullOrEmpty(client?.SchemaUrl)) root["$schema"] = client.SchemaUrl;
            JObject container = EnsureObjectPath(root, GetContainerPath(client, isVSCode));

            var unity = new JObject();
            PopulateUnityNode(unity, uvPath, client, isVSCode);

            container["unityMCP"] = unity;

            return root.ToString(Formatting.Indented);
        }

        public static JObject ApplyUnityServerToExistingConfig(JObject root, string uvPath, McpClient client)
        {
            if (root == null) root = new JObject();
            bool isVSCode = client?.IsVsCodeLayout == true;
            if (!string.IsNullOrEmpty(client?.SchemaUrl) && root["$schema"] == null) root["$schema"] = client.SchemaUrl;
            JObject container = EnsureObjectPath(root, GetContainerPath(client, isVSCode));
            JObject unity = container["unityMCP"] as JObject ?? new JObject();
            PopulateUnityNode(unity, uvPath, client, isVSCode);

            container["unityMCP"] = unity;
            return root;
        }

        /// <summary>
        /// Centralized builder that applies all caveats consistently.
        /// - Sets command/args with uvx and package version
        /// - Ensures env exists
        /// - Adds transport configuration (HTTP or stdio)
        /// - Adds disabled:false for Windsurf/Kiro only when missing
        /// </summary>
        private static void PopulateUnityNode(JObject unity, string uvPath, McpClient client, bool isVSCode)
        {
            // Get transport preference (default to HTTP)
            bool prefValue = EditorConfigurationCache.Instance.UseHttpTransport;
            bool clientSupportsHttp = client?.SupportsHttpTransport != false;
            bool useHttpTransport = clientSupportsHttp && prefValue;
            string httpProperty = string.IsNullOrEmpty(client?.HttpUrlProperty) ? "url" : client.HttpUrlProperty;
            var urlPropsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "url", "serverUrl" };
            urlPropsToRemove.Remove(httpProperty);

            if (useHttpTransport)
            {
                // HTTP mode: Use URL, no command
                string httpUrl = HttpEndpointUtility.GetMcpRpcUrl();
                unity[httpProperty] = httpUrl;

                foreach (var prop in urlPropsToRemove)
                {
                    if (unity[prop] != null) unity.Remove(prop);
                }

                // Remove command/args if they exist from previous config
                if (unity["command"] != null) unity.Remove("command");
                if (unity["args"] != null) unity.Remove("args");

                // Only include API key header for remote-hosted mode
                if (HttpEndpointUtility.IsRemoteScope())
                {
                    string apiKey = EditorPrefs.GetString(EditorPrefKeys.ApiKey, string.Empty);
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        var headers = new JObject { [AuthConstants.ApiKeyHeader] = apiKey };
                        unity["headers"] = headers;
                    }
                    else
                    {
                        if (unity["headers"] != null) unity.Remove("headers");
                    }
                }
                else
                {
                    // Local HTTP doesn't use API keys; remove any stale headers
                    if (unity["headers"] != null) unity.Remove("headers");
                }

                // Per-client override of the HTTP "type" value: Cline/Roo expect "streamableHttp"
                // and Kilo expects "remote"; both fall back to stdio when they see the generic
                // "http". Defaults to "http" (standard MCP protocol field) when unset, so clients
                // don't default to SSE on seeing a URL without a type.
                unity["type"] = string.IsNullOrEmpty(client?.HttpTypeValue) ? "http" : client.HttpTypeValue;
            }
            else
            {
                // Stdio mode: Use uvx command
                var (uvxPath, fromUrl, packageName) = AssetPathUtility.GetUvxCommandParts();

                var toolArgs = BuildUvxArgs(fromUrl, packageName);

                unity["command"] = uvxPath;
                unity["args"] = JArray.FromObject(toolArgs.ToArray());

                // Remove url/serverUrl if they exist from previous config
                if (unity["url"] != null) unity.Remove("url");
                if (unity["serverUrl"] != null) unity.Remove("serverUrl");

                // Include type for all clients — standard MCP protocol field. A few clients use a
                // different token for local transport (e.g. Kilo uses "local").
                unity["type"] = string.IsNullOrEmpty(client?.StdioTypeValue) ? "stdio" : client.StdioTypeValue;
            }

            bool requiresEnv = client?.EnsureEnvObject == true;
            bool stripEnv = client?.StripEnvWhenNotRequired == true;

            if (requiresEnv)
            {
                if (unity["env"] == null)
                {
                    unity["env"] = new JObject();
                }
            }
            else if (stripEnv && unity["env"] != null)
            {
                unity.Remove("env");
            }

            if (client?.DefaultUnityFields != null)
            {
                foreach (var kvp in client.DefaultUnityFields)
                {
                    if (unity[kvp.Key] == null)
                    {
                        unity[kvp.Key] = kvp.Value != null ? JToken.FromObject(kvp.Value) : JValue.CreateNull();
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the container path holding the server entries for a client.
        /// Most clients keep them under a single top-level key ("mcpServers", or "mcp" for
        /// Kilo); clients that nest their server map return a dot-separated path
        /// (ZCode: "mcp.servers").
        /// </summary>
        public static string GetContainerPath(McpClient client, bool isVSCode)
        {
            if (isVSCode) return "servers";
            return string.IsNullOrEmpty(client?.ServerContainerKey) ? "mcpServers" : client.ServerContainerKey;
        }

        /// <summary>
        /// Returns the container object at <paramref name="containerPath"/> without creating
        /// anything. Null when a segment is missing or is not an object.
        /// </summary>
        public static JObject FindContainer(JObject root, string containerPath)
        {
            if (root == null || string.IsNullOrEmpty(containerPath)) return null;

            JObject current = root;
            foreach (var segment in containerPath.Split('.'))
            {
                if (segment.Length == 0) continue;
                if (!(current[segment] is JObject next)) return null;
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Returns the unityMCP server node for a client's config layout, or null when the
        /// client has no Unity entry. Mirrors the layout Configure() writes.
        /// </summary>
        public static JToken FindUnityServer(JObject root, McpClient client)
        {
            if (root == null) return null;

            if (client != null && client.IsVsCodeLayout)
            {
                return root["servers"]?["unityMCP"] ?? root["mcp"]?["servers"]?["unityMCP"];
            }

            return FindContainer(root, GetContainerPath(client, false))?["unityMCP"];
        }

        private static JObject EnsureObjectPath(JObject root, string containerPath)
        {
            JObject current = root;
            foreach (var segment in containerPath.Split('.'))
            {
                if (segment.Length == 0) continue;
                current = EnsureObject(current, segment);
            }
            return current;
        }

        private static JObject EnsureObject(JObject parent, string name)
        {
            if (parent[name] is JObject o) return o;
            var created = new JObject();
            parent[name] = created;
            return created;
        }

        private static IList<string> BuildUvxArgs(string fromUrl, string packageName)
        {
            // Dev mode: force a fresh install/resolution (avoids stale cached builds while iterating).
            // `--no-cache` avoids reading from cache; `--refresh` ensures metadata is revalidated.
            // Note: --reinstall is not supported by uvx and will cause a warning.
            // Keep ordering consistent with other uvx builders: dev flags first, then --from <url>, then package name.
            var args = new List<string>();

            foreach (var flag in AssetPathUtility.GetUvxDevFlagsList())
                args.Add(flag);

            // Use centralized helper for beta server / prerelease args
            foreach (var arg in AssetPathUtility.GetBetaServerFromArgsList())
            {
                args.Add(arg);
            }
            args.Add(packageName);

            args.Add("--transport");
            args.Add("stdio");

            return args;
        }

    }
}
