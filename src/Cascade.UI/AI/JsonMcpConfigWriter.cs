using System.Text;

namespace Cascade.UI;

/// <summary>
/// Reads, merges, and writes MCP server entries in the standard JSON format
/// used by Claude Desktop, Continue.dev, and most MCP-compatible clients.
/// </summary>
/// <remarks>
/// <para>The expected file format is:</para>
/// <code>
/// {
///   "mcpServers": {
///     "server-key": {
///       "command": "/path/to/exe",
///       "args": ["--mcp"],
///       "description": "My App"
///     }
///   }
/// }
/// </code>
/// </remarks>
public sealed class JsonMcpConfigWriter : IAiClientConfigWriter
{
    /// <inheritdoc/>
    public bool EntryExists(string configPath, string serverKey)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        string json = File.ReadAllText(configPath, Encoding.UTF8);
        return json.Contains($"\"{serverKey}\"", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public void WriteEntry(string configPath, string serverKey, string commandPath, string[] args, string? description)
    {
        string json;
        if (File.Exists(configPath))
        {
            json = File.ReadAllText(configPath, Encoding.UTF8);
        }
        else
        {
            json = "{}";
        }

        string updated = MergeServerEntry(json, serverKey, commandPath, args, description);

        string? dir = System.IO.Path.GetDirectoryName(configPath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(configPath, updated, Encoding.UTF8);
    }

    /// <inheritdoc/>
    public void RemoveEntry(string configPath, string serverKey)
    {
        if (!File.Exists(configPath))
        {
            return;
        }

        string json = File.ReadAllText(configPath, Encoding.UTF8);
        string updated = RemoveServerEntry(json, serverKey);
        File.WriteAllText(configPath, updated, Encoding.UTF8);
    }

    /// <summary>
    /// Merges or adds a server entry into the JSON config using minimal string
    /// manipulation (no System.Text.Json dependency for NativeAOT trimming safety).
    /// </summary>
    internal static string MergeServerEntry(
        string json, string serverKey, string commandPath, string[] args, string? description)
    {
        string escapedKey = EscapeJsonString(serverKey);
        string escapedCommand = EscapeJsonString(commandPath);

        var sb = new StringBuilder();
        sb.Append('"');
        sb.Append(escapedKey);
        sb.AppendLine("\": {");
        sb.Append("      \"command\": \"");
        sb.Append(escapedCommand);
        sb.AppendLine("\",");
        sb.Append("      \"args\": [");
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append('"');
            sb.Append(EscapeJsonString(args[i]));
            sb.Append('"');
        }
        sb.Append(']');

        if (description is not null)
        {
            sb.AppendLine(",");
            sb.Append("      \"description\": \"");
            sb.Append(EscapeJsonString(description));
            sb.Append('"');
        }

        sb.AppendLine();
        sb.Append("    }");

        string entryBlock = sb.ToString();

        // Check if mcpServers section exists
        int mcpServersIdx = json.IndexOf("\"mcpServers\"", StringComparison.Ordinal);
        if (mcpServersIdx < 0)
        {
            // Add mcpServers section to the root object
            int lastBrace = json.LastIndexOf('}');
            if (lastBrace < 0)
            {
                return "{\n  \"mcpServers\": {\n    " + entryBlock + "\n  }\n}";
            }

            // Check if there's existing content before the closing brace
            string before = json.Substring(0, lastBrace).TrimEnd();
            bool hasContent = before.Length > 1 && before[^1] != '{';
            string separator = hasContent ? ",\n" : "\n";

            return before + separator + "  \"mcpServers\": {\n    " + entryBlock + "\n  }\n}";
        }

        // mcpServers exists — find its opening brace
        int openBrace = json.IndexOf('{', mcpServersIdx + "\"mcpServers\"".Length);
        if (openBrace < 0)
        {
            return json;
        }

        // Check if entry already exists — replace it
        string keyPattern = $"\"{escapedKey}\"";
        int existingKeyIdx = json.IndexOf(keyPattern, openBrace, StringComparison.Ordinal);
        if (existingKeyIdx >= 0)
        {
            return ReplaceExistingEntry(json, existingKeyIdx, keyPattern.Length, entryBlock);
        }

        // Entry doesn't exist — add it after the opening brace
        int insertPos = openBrace + 1;
        string afterBrace = json.Substring(insertPos).TrimStart();

        if (afterBrace.Length > 0 && afterBrace[0] != '}')
        {
            // Existing entries — add with comma
            return json.Substring(0, insertPos) + "\n    " + entryBlock + "," + json.Substring(insertPos);
        }
        else
        {
            // Empty mcpServers — add without comma
            return json.Substring(0, insertPos) + "\n    " + entryBlock + "\n  " + json.Substring(insertPos).TrimStart();
        }
    }

    /// <summary>
    /// Removes a server entry from the JSON config.
    /// </summary>
    internal static string RemoveServerEntry(string json, string serverKey)
    {
        string escapedKey = EscapeJsonString(serverKey);
        string keyPattern = $"\"{escapedKey}\"";
        int keyIdx = json.IndexOf(keyPattern, StringComparison.Ordinal);
        if (keyIdx < 0)
        {
            return json;
        }

        // Find the start of this entry (including any leading whitespace/comma)
        int entryStart = keyIdx;
        while (entryStart > 0 && json[entryStart - 1] is ' ' or '\t')
        {
            entryStart--;
        }

        // Find the end of the value object
        int colonIdx = json.IndexOf(':', keyIdx + keyPattern.Length);
        if (colonIdx < 0)
        {
            return json;
        }

        int valueStart = json.IndexOf('{', colonIdx);
        if (valueStart < 0)
        {
            return json;
        }

        int entryEnd = FindMatchingBrace(json, valueStart);
        if (entryEnd < 0)
        {
            return json;
        }

        entryEnd++; // past the closing brace

        // Handle comma: remove trailing comma, or leading comma if this is last entry
        if (entryEnd < json.Length && json[entryEnd] == ',')
        {
            entryEnd++;
        }
        else if (entryStart > 0 && json[entryStart - 1] == ',')
        {
            entryStart--;
        }

        // Also remove the newline after the entry
        while (entryEnd < json.Length && json[entryEnd] is '\r' or '\n')
        {
            entryEnd++;
        }

        return string.Concat(json.AsSpan(0, entryStart), json.AsSpan(entryEnd));
    }

    private static string ReplaceExistingEntry(string json, int keyIdx, int keyLen, string newEntry)
    {
        // Find the colon after the key
        int colonIdx = json.IndexOf(':', keyIdx + keyLen);
        if (colonIdx < 0)
        {
            return json;
        }

        // Find the opening brace of the value
        int valueStart = json.IndexOf('{', colonIdx);
        if (valueStart < 0)
        {
            return json;
        }

        // Find the matching closing brace
        int valueEnd = FindMatchingBrace(json, valueStart);
        if (valueEnd < 0)
        {
            return json;
        }

        // Find start of this key (including whitespace before key)
        int entryStart = keyIdx;
        while (entryStart > 0 && json[entryStart - 1] is ' ' or '\t')
        {
            entryStart--;
        }

        return string.Concat(json.AsSpan(0, entryStart), newEntry.AsSpan(), json.AsSpan(valueEnd + 1));
    }

    private static int FindMatchingBrace(string json, int openBraceIdx)
    {
        int depth = 0;
        bool inString = false;
        for (int i = openBraceIdx; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++; // skip escaped char
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        depth--;
                        if (depth == 0)
                        {
                            return i;
                        }
                        break;
                }
            }
        }
        return -1;
    }

    private static string EscapeJsonString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
