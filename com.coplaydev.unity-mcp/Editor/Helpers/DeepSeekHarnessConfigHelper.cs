using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using UnityEditor;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// DeepSeek Harness (DSH) configuration helpers.
    ///
    /// DSH composes its plugin tree from YAML patch entries: the bundles listed in
    /// ~/.dsh/profiles/&lt;profile&gt;/package.json, then the user's own
    /// ~/.dsh/profiles/&lt;profile&gt;/cordis.patch.yml. A patch entry that overrides a row a bundle
    /// already mounts is written bare ("- id: x" plus "name:" and "config:"); a plugin no bundle
    /// mounts — which is the case for @deepseek-ai/dsh-mcp-client — has to be introduced by an
    /// "insert:" patch entry.
    ///
    /// That file is hand-editable and comment-heavy, so this helper edits it as text: it locates
    /// the Unity row by id and replaces or deletes exactly those lines, leaving comments, other
    /// rows and "!!js" expressions byte-for-byte intact.
    /// </summary>
    public static class DeepSeekHarnessConfigHelper
    {
        public const string PluginName = "@deepseek-ai/dsh-mcp-client";
        public const string EntryId = "mcp-unityMCP";
        public const string ServerName = "unityMCP";
        public const string HttpTransport = "streamable-http";
        public const string StdioTransport = "stdio";

        // A row inside the insert entry, and the indentation of its keys. The official bundles
        // use a two-space step, so the row sits at four spaces under "- insert:".
        private const string RowIndent = "    ";
        private const string KeyIndent = "      ";
        private const string FieldIndent = "        ";
        private const string ListIndent = "          ";

        private static readonly Regex RowStartRegex =
            new Regex(@"^(\s*)-\s+id\s*:(?<value>.*)$", RegexOptions.Compiled);

        // A patch entry begins with a dash in column 0; everything else is nested inside one.
        private static readonly Regex TopLevelEntryRegex = new Regex(@"^-\s", RegexOptions.Compiled);

        private static readonly Regex KeyRegex =
            new Regex(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:(?<value>.*)$", RegexOptions.Compiled);

        private static readonly Regex ListItemRegex = new Regex(@"^(\s*)-\s+(?<value>.*)$", RegexOptions.Compiled);

        /// <summary>The Unity row as it currently appears in the patch file.</summary>
        public sealed class UnityEntry
        {
            public string Transport;
            public string Url;
            public string Command;
            public IList<string> Args = new List<string>();
        }

        /// <summary>
        /// Reads the Unity row out of the patch file. Returns false when the file has no row with
        /// our id, which is what the configurator reports as MissingConfig.
        /// </summary>
        public static bool TryReadUnityEntry(string yaml, out UnityEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(yaml))
            {
                return false;
            }

            var lines = SplitLines(yaml);
            if (!TryFindUnityRow(lines, out int start, out int end))
            {
                return false;
            }

            var result = new UnityEntry();
            string listKey = null;
            int listIndent = -1;

            for (int i = start + 1; i < end; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var itemMatch = ListItemRegex.Match(line);
                if (listKey != null && itemMatch.Success && itemMatch.Groups[1].Length > listIndent)
                {
                    if (listKey == "args")
                    {
                        result.Args.Add(Unquote(itemMatch.Groups["value"].Value));
                    }
                    continue;
                }

                var keyMatch = KeyRegex.Match(line);
                if (!keyMatch.Success)
                {
                    continue;
                }

                listKey = null;
                string key = keyMatch.Groups["key"].Value;
                string value = Unquote(keyMatch.Groups["value"].Value);

                switch (key)
                {
                    case "transport":
                        result.Transport = value;
                        break;
                    case "url":
                        result.Url = value;
                        break;
                    case "command":
                        result.Command = value;
                        break;
                    case "args":
                        listKey = "args";
                        listIndent = line.Length - line.TrimStart().Length;
                        break;
                }
            }

            entry = result;
            return true;
        }

        /// <summary>
        /// Returns the patch file with the Unity row written: replaced in place when it already
        /// exists, otherwise appended as a fresh insert patch entry.
        /// </summary>
        public static string UpsertUnityEntry(string yaml, string uvxPath)
        {
            string newline = DetectNewline(yaml);
            var lines = SplitLines(yaml);
            var rowLines = BuildRowLines(uvxPath);

            if (TryFindUnityRow(lines, out int start, out int end))
            {
                lines.RemoveRange(start, end - start);
                lines.InsertRange(start, rowLines);
                return JoinLines(lines, newline);
            }

            TrimTrailingBlank(lines);
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }
            lines.Add("- insert:");
            lines.AddRange(rowLines);
            return JoinLines(lines, newline);
        }

        /// <summary>
        /// Returns the patch file with the Unity row removed. The enclosing insert entry is dropped
        /// too once it holds no other rows, so a Configure/Unregister round-trip leaves no debris.
        /// </summary>
        public static string RemoveUnityEntry(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
            {
                return yaml;
            }

            string newline = DetectNewline(yaml);
            var lines = SplitLines(yaml);
            if (!TryFindUnityRow(lines, out int start, out int end))
            {
                return yaml;
            }

            lines.RemoveRange(start, end - start);

            int insertIndex = -1;
            for (int i = start - 1; i >= 0; i--)
            {
                if (TopLevelEntryRegex.IsMatch(lines[i]))
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex >= 0
                && lines[insertIndex].TrimStart().StartsWith("- insert:", StringComparison.Ordinal))
            {
                int stop = lines.Count;
                for (int i = insertIndex + 1; i < lines.Count; i++)
                {
                    if (TopLevelEntryRegex.IsMatch(lines[i]))
                    {
                        stop = i;
                        break;
                    }
                }

                bool insertHasOtherRows = false;
                for (int i = insertIndex + 1; i < stop; i++)
                {
                    if (lines[i].Trim().Length > 0)
                    {
                        insertHasOtherRows = true;
                        break;
                    }
                }

                // Only collapse the insert entry when nothing but blank lines sat between it and our
                // row — a comment the user placed there keeps the entry alive.
                bool nothingButBlanksBeforeRow = true;
                for (int i = insertIndex + 1; i < start; i++)
                {
                    if (lines[i].Trim().Length > 0)
                    {
                        nothingButBlanksBeforeRow = false;
                        break;
                    }
                }

                if (!insertHasOtherRows && nothingButBlanksBeforeRow)
                {
                    lines.RemoveRange(insertIndex, start - insertIndex);
                    start = insertIndex;
                }
            }

            CollapseBlankRun(lines, start);
            TrimTrailingBlank(lines);
            return JoinLines(lines, newline);
        }

        /// <summary>Renders the row for the clipboard/manual path, wrapped in its insert entry.</summary>
        public static string BuildManualSnippet(string uvxPath)
        {
            var lines = new List<string> { "- insert:" };
            lines.AddRange(BuildRowLines(uvxPath));
            return string.Join(Environment.NewLine, lines);
        }

        private static List<string> BuildRowLines(string uvxPath)
        {
            var lines = new List<string>
            {
                $"{RowIndent}- id: {EntryId}",
                $"{KeyIndent}name: '{PluginName}'",
                $"{KeyIndent}config:",
                $"{FieldIndent}serverName: {ServerName}"
            };

            if (HttpEndpointUtility.GetCurrentServerTransport() == ConfiguredTransport.Stdio)
            {
                if (string.IsNullOrWhiteSpace(uvxPath))
                {
                    throw new InvalidOperationException(
                        "uvx not found. Install uv/uvx or set the override in Advanced Settings.");
                }

                lines.Add($"{FieldIndent}transport: {StdioTransport}");
                lines.Add($"{FieldIndent}command: '{uvxPath}'");
                lines.Add($"{FieldIndent}args:");
                foreach (string arg in BuildStdioArgs())
                {
                    lines.Add($"{ListIndent}- '{arg}'");
                }

                // DSH scrubs credential-shaped variables out of the child environment; SystemRoot
                // is not one of them and is what the server needs on Windows.
                if (MCPServiceLocator.Platform.IsWindows())
                {
                    lines.Add($"{FieldIndent}env:");
                    lines.Add($"{ListIndent}SystemRoot: '{MCPServiceLocator.Platform.GetSystemRoot()}'");
                }
            }
            else
            {
                lines.Add($"{FieldIndent}transport: {HttpTransport}");
                lines.Add($"{FieldIndent}url: {HttpEndpointUtility.GetMcpRpcUrl()}");
            }

            return lines;
        }

        private static IEnumerable<string> BuildStdioArgs()
        {
            var args = new List<string>();
            args.AddRange(AssetPathUtility.GetUvxDevFlagsList());
            args.AddRange(AssetPathUtility.GetBetaServerFromArgsList());
            args.Add(AssetPathUtility.GetUvxCommandParts().packageName);
            args.Add("--transport");
            args.Add("stdio");
            return args;
        }

        private static bool TryFindUnityRow(List<string> lines, out int start, out int end)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var match = RowStartRegex.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }

                if (!string.Equals(Unquote(match.Groups["value"].Value), EntryId, StringComparison.Ordinal))
                {
                    continue;
                }

                start = i;
                end = lines.Count;
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (TopLevelEntryRegex.IsMatch(lines[j]) || RowStartRegex.IsMatch(lines[j]))
                    {
                        end = j;
                        break;
                    }
                }
                return true;
            }

            start = -1;
            end = -1;
            return false;
        }

        /// <summary>Strips YAML quoting (or a trailing comment from a plain scalar).</summary>
        private static string Unquote(string value)
        {
            if (value == null)
            {
                return null;
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            char quote = trimmed[0];
            if (quote == '\'' || quote == '"')
            {
                int close = trimmed.IndexOf(quote, 1);
                return close > 0 ? trimmed.Substring(1, close - 1) : trimmed.Substring(1);
            }

            int comment = trimmed.IndexOf(" #", StringComparison.Ordinal);
            return (comment >= 0 ? trimmed.Substring(0, comment) : trimmed).Trim();
        }

        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] parts = normalized.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                // Drop the empty tail a trailing newline produces; JoinLines re-adds it.
                if (i == parts.Length - 1 && parts[i].Length == 0)
                {
                    break;
                }
                lines.Add(parts[i]);
            }

            return lines;
        }

        private static string JoinLines(List<string> lines, string newline)
        {
            return string.Join(newline, lines) + newline;
        }

        private static string DetectNewline(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                int lf = text.IndexOf('\n');
                if (lf > 0 && text[lf - 1] == '\r')
                {
                    return "\r\n";
                }
                if (lf >= 0)
                {
                    return "\n";
                }
            }

            return Environment.NewLine;
        }

        private static void TrimTrailingBlank(List<string> lines)
        {
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private static void CollapseBlankRun(List<string> lines, int index)
        {
            while (index > 0 && index < lines.Count
                   && lines[index - 1].Trim().Length == 0
                   && lines[index].Trim().Length == 0)
            {
                lines.RemoveAt(index);
            }
        }
    }
}
