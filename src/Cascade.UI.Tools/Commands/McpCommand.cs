using System.Globalization;
using System.Text.Json.Nodes;
using Cascade.UI;
using Cascade.UI.AI;
using Cascade.UI.Tools.Mcp;

namespace Cascade.UI.Tools.Commands;

/// <summary>
/// CLI command for one-shot MCP tool operations.
/// Connects to a running Cascade app instance via TCP, executes one tool call,
/// prints the result, and exits.
///
/// Usage: cascade mcp &lt;subcommand&gt; [options]
///
/// Help (<c>--help</c>/<c>-h</c> on any subcommand) never requires a running
/// instance — it is handled before any connection attempt. Malformed option
/// values are rejected with a usage error, never silently defaulted: a flag
/// that parses but lies poisons an agent's model of reality.
///
/// Tool metadata (names, schemas, CLI verbs) is sourced from the shared
/// <see cref="McpToolRegistry"/> in Cascade.UI so the CLI, live instance, and
/// headless bridge cannot drift.
/// </summary>
internal static class McpCommand
{
    internal static int Execute(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        string subcommand = args[0];
        string[] rest = args.Length > 1 ? args[1..] : [];

        // Per-verb help must work with zero instances running: handle it
        // here, before any subcommand can attempt instance discovery.
        if (rest.Any(a => a is "--help" or "-h"))
        {
            return PrintVerbHelp(subcommand);
        }

        // "info" is a CLI-only meta-verb (lists discovered instances); it does
        // not have a corresponding MCP tool entry, so resolve it before the
        // registry lookup.
        if (subcommand == "info")
        {
            return ExecuteInfo(rest);
        }

        // "docs" is a CLI-only meta-verb: emits the tool reference generated
        // from the shared registry. Needs no running instance.
        if (subcommand == "docs")
        {
            return ExecuteDocs(rest);
        }

        McpCliVerbBinding? binding = McpToolRegistry.FindByVerb(subcommand);
        if (binding is null)
        {
            return UnknownSubcommand(subcommand);
        }

        return subcommand switch
        {
            "screenshot" => ExecuteScreenshot(rest),
            "drag" => ExecuteDrag(rest),
            "type" => ExecuteType(rest),
            "scroll" => ExecuteScroll(rest),
            "find" => ExecuteFind(rest),
            "accessibility" => ExecuteAccessibility(rest),
            "atlas" => ExecuteAtlas(rest),
            "instances" => ExecuteInstances(rest),
            _ => ExecuteGenericVerb(binding, rest),
        };
    }

    // ── Subcommands ──────────────────────────────────────────────

    private static int ExecuteInfo(string[] args)
    {
        string? appId = GetOption(args, "--app");
        IReadOnlyList<InstanceEntry> instances = InstanceDiscovery.FindAllInstances(appId);

        if (instances.Count == 0)
        {
            Console.Error.WriteLine("No running Cascade instance found.");
            Console.Error.WriteLine("Start the app first, or specify --app <name>.");
            return 1;
        }

        Console.WriteLine($"Found {instances.Count} instance(s):");
        Console.WriteLine();
        foreach (InstanceEntry entry in instances)
        {
            Console.WriteLine($"  App:     {entry.Title}");
            Console.WriteLine($"  Window:  {entry.WindowId}");
            Console.WriteLine($"  Port:    {entry.Port}");
            Console.WriteLine($"  PID:     {entry.Pid}");
            Console.WriteLine($"  Focused: {entry.Focused}");
            Console.WriteLine();
        }

        return 0;
    }

    private static int ExecuteScreenshot(string[] args)
    {
        string? output = GetOption(args, "--output") ?? GetOption(args, "-o");
        if (!TryGetDoubleOption(args, "--scale", 1.0, out double scale))
        {
            return 1;
        }

        if (!TryGetIntOption(args, "--after-frame", 0, out int afterFrame))
        {
            return 1;
        }

        var arguments = new JsonObject { ["scale"] = scale };
        if (afterFrame > 0)
        {
            arguments["after_frame"] = afterFrame;
        }

        string? regionSpec = GetOption(args, "--region");
        if (regionSpec is not null)
        {
            if (!TryParseRegion(regionSpec, out int rx, out int ry, out int rw, out int rh))
            {
                Console.Error.WriteLine($"Invalid --region '{regionSpec}'. Expected: --region x,y,width,height (integers, width/height > 0).");
                return 1;
            }

            arguments["region"] = new JsonObject
            {
                ["x"] = rx,
                ["y"] = ry,
                ["width"] = rw,
                ["height"] = rh,
            };

            // Region coordinates default to the returned-screenshot pixel space.
            // --region-space device requests raw framebuffer pixels (machine-
            // dependent at non-100% DPI; used by the golden-capture harness).
            string? regionSpace = GetOption(args, "--region-space");
            if (regionSpace is not null)
            {
                arguments["region_space"] = regionSpace;
            }
        }

        string? appId = GetOption(args, "--app");

        using McpClient? client = ConnectOrFail(appId);
        if (client is null)
        {
            return 1;
        }

        JsonNode? response = client.CallTool("cascade_screenshot", arguments);
        if (response is null)
        {
            Console.Error.WriteLine("No response from server.");
            return 1;
        }

        var (data, mimeType) = McpClient.ExtractImage(response);
        if (data is null)
        {
            // Might be a text response (error)
            string? text = McpClient.ExtractText(response);
            Console.Error.WriteLine(text ?? "Unknown error");
            return 1;
        }

        if (output is null)
        {
            // Default output file name
            output = "screenshot.png";
        }

        byte[] imageBytes = Convert.FromBase64String(data);
        File.WriteAllBytes(output, imageBytes);

        // The server appends a metadata text block (width, height,
        // captured_frame, after_frame_timed_out) after the image content.
        string? metadata = ExtractMetadataText(response);
        Console.WriteLine(metadata is null
            ? $"Screenshot saved to {output} ({imageBytes.Length:N0} bytes)"
            : $"Screenshot saved to {output} ({imageBytes.Length:N0} bytes) {metadata}");
        return 0;
    }

    /// <summary>
    /// Returns the first text content block from a raw tool response, or null.
    /// Used to surface the screenshot metadata block (which follows the image).
    /// </summary>
    private static string? ExtractMetadataText(JsonNode response)
    {
        if (response is not JsonObject obj ||
            obj["result"] is not JsonObject result ||
            result["content"] is not JsonArray content)
        {
            return null;
        }

        foreach (JsonNode? block in content)
        {
            if (block is JsonObject blockObj &&
                blockObj["type"]?.GetValue<string>() == "text")
            {
                return blockObj["text"]?.GetValue<string>();
            }
        }

        return null;
    }

    private static int ExecuteDocs(string[] args)
    {
        string markdown = McpToolDocs.GenerateMarkdown();

        string? output = GetOption(args, "--output") ?? GetOption(args, "-o");
        if (output is null)
        {
            Console.Write(markdown);
            return 0;
        }

        File.WriteAllText(output, markdown);
        Console.WriteLine($"Tool reference written to {output} ({markdown.Length:N0} chars)");
        return 0;
    }

    private static int ExecuteAtlas(string[] args)
    {
        string? output = GetOption(args, "--output") ?? GetOption(args, "-o");

        var arguments = new JsonObject();
        string? regionSpec = GetOption(args, "--region");
        if (regionSpec is not null)
        {
            if (!TryParseRegion(regionSpec, out int u, out int v, out int rw, out int rh))
            {
                Console.Error.WriteLine($"Invalid --region '{regionSpec}'. Expected: --region u,v,width,height (atlas texels, width/height > 0).");
                return 1;
            }

            arguments["u"] = u;
            arguments["v"] = v;
            arguments["width"] = rw;
            arguments["height"] = rh;
        }

        string? appId = GetOption(args, "--app");
        using McpClient? client = ConnectOrFail(appId);
        if (client is null)
        {
            return 1;
        }

        JsonNode? response = client.CallTool("cascade_atlas", arguments);
        if (response is null)
        {
            Console.Error.WriteLine("No response from server.");
            return 1;
        }

        var (data, _) = McpClient.ExtractImage(response);
        if (data is null)
        {
            string? text = McpClient.ExtractText(response);
            Console.Error.WriteLine(text ?? "Unknown error");
            return 1;
        }

        output ??= "atlas.png";
        byte[] imageBytes = Convert.FromBase64String(data);
        File.WriteAllBytes(output, imageBytes);

        string? metadata = ExtractMetadataText(response);
        Console.WriteLine(metadata is null
            ? $"Atlas region saved to {output} ({imageBytes.Length:N0} bytes)"
            : $"Atlas region saved to {output} ({imageBytes.Length:N0} bytes) {metadata}");
        return 0;
    }

    private static int ExecuteInstances(string[] args)
    {
        var arguments = new JsonObject();

        string? rectSpec = GetOption(args, "--rect");
        if (rectSpec is not null)
        {
            if (!TryParseRegion(rectSpec, out int rx, out int ry, out int rw, out int rh))
            {
                Console.Error.WriteLine($"Invalid --rect '{rectSpec}'. Expected: --rect x,y,width,height (device pixels, width/height > 0).");
                return 1;
            }

            arguments["x"] = rx;
            arguments["y"] = ry;
            arguments["width"] = rw;
            arguments["height"] = rh;
        }

        if (!TryGetIntOption(args, "--limit", 0, out int limit))
        {
            return 1;
        }
        if (limit > 0)
        {
            arguments["limit"] = limit;
        }

        return CallToolAndPrint(args, "cascade_glyph_instances", arguments);
    }

    private static int ExecuteDrag(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: cascade mcp drag <node_id> --delta-x <dx> --delta-y <dy> [--start-x <x>] [--start-y <y>] [--end-x <x>] [--end-y <y>] [--steps <n>]");
            return 1;
        }

        string nodeId = args[0];
        var arguments = new JsonObject
        {
            ["node_id"] = nodeId,
            ["interaction"] = "drag",
        };

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--delta-x" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--delta-x", args[++i], out float deltaX))
                    {
                        return 1;
                    }
                    arguments["delta_x"] = deltaX;
                    break;
                case "--delta-y" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--delta-y", args[++i], out float deltaY))
                    {
                        return 1;
                    }
                    arguments["delta_y"] = deltaY;
                    break;
                case "--start-x" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--start-x", args[++i], out float startX))
                    {
                        return 1;
                    }
                    arguments["start_x"] = startX;
                    break;
                case "--start-y" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--start-y", args[++i], out float startY))
                    {
                        return 1;
                    }
                    arguments["start_y"] = startY;
                    break;
                case "--end-x" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--end-x", args[++i], out float endX))
                    {
                        return 1;
                    }
                    arguments["end_x"] = endX;
                    break;
                case "--end-y" when i + 1 < args.Length:
                    if (!TryParseFloatValue("--end-y", args[++i], out float endY))
                    {
                        return 1;
                    }
                    arguments["end_y"] = endY;
                    break;
                case "--steps" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int steps))
                    {
                        Console.Error.WriteLine($"Invalid --steps '{args[i]}': expected an integer.");
                        return 1;
                    }
                    arguments["steps"] = steps;
                    break;
                case "--wait-frames" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int waitFrames))
                    {
                        Console.Error.WriteLine($"Invalid --wait-frames '{args[i]}': expected an integer.");
                        return 1;
                    }
                    arguments["wait_frames"] = waitFrames;
                    break;
            }
        }

        return CallToolAndPrint(args, "cascade_simulate_interaction", arguments);
    }

    private static int ExecuteType(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cascade mcp type <text> | type --key <name> [--ctrl] [--shift] [--alt]");
            return 1;
        }

        // Key mode: `type --key Z --ctrl` sends a single key press with modifiers —
        // an accelerator (e.g. Ctrl+Z) that plain text input cannot express.
        string? keyName = GetOption(args, "--key");
        if (keyName is not null)
        {
            var keyArgs = new JsonObject { ["key"] = keyName };
            var modNames = new List<string>();
            if (HasFlag(args, "--ctrl"))
            {
                modNames.Add("CTRL");
            }
            if (HasFlag(args, "--shift"))
            {
                modNames.Add("SHIFT");
            }
            if (HasFlag(args, "--alt"))
            {
                modNames.Add("ALT");
            }
            if (modNames.Count > 0)
            {
                // Build via Parse to stay trim/AOT-safe (JsonArray.Add<T> is not).
                string modsJson = "[" + string.Join(",", modNames.Select(m => $"\"{m}\"")) + "]";
                keyArgs["modifiers"] = JsonNode.Parse(modsJson);
            }
            if (!TryGetIntOption(args, "--wait-frames", 0, out int keyWaitFrames))
            {
                return 1;
            }
            if (keyWaitFrames > 0)
            {
                keyArgs["wait_frames"] = keyWaitFrames;
            }

            return CallToolAndPrint(args, "cascade_send_keys", keyArgs);
        }

        // Join all args as the text to type (allows spaces without quoting),
        // skipping option pairs like "--app <name>".
        var textParts = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            textParts.Add(args[i]);
        }

        if (textParts.Count == 0)
        {
            Console.Error.WriteLine("Usage: cascade mcp type <text>");
            return 1;
        }

        string text = string.Join(' ', textParts);
        var arguments = new JsonObject { ["text"] = text };

        // Option pairs are excluded from the joined text above, so
        // --wait-frames only needs to be forwarded as a tool argument.
        if (!TryGetIntOption(args, "--wait-frames", 0, out int waitFrames))
        {
            return 1;
        }
        if (waitFrames > 0)
        {
            arguments["wait_frames"] = waitFrames;
        }

        return CallToolAndPrint(args, "cascade_send_keys", arguments);
    }

    private static int ExecuteScroll(string[] args)
    {
        if (!TryGetDoubleOption(args, "--delta-y", 100, out double deltaY) ||
            !TryGetDoubleOption(args, "--delta-x", 0, out double deltaX))
        {
            return 1;
        }

        string? nodeId = GetOption(args, "--to");

        var arguments = new JsonObject();
        if (nodeId is not null)
        {
            arguments["node_id"] = nodeId;
        }
        else
        {
            arguments["delta_x"] = deltaX;
            arguments["delta_y"] = deltaY;
        }

        if (!TryGetDoubleOption(args, "--x", double.NaN, out double xVal) ||
            !TryGetDoubleOption(args, "--y", double.NaN, out double yVal))
        {
            return 1;
        }

        if (!double.IsNaN(xVal))
        {
            arguments["x"] = xVal;
        }
        if (!double.IsNaN(yVal))
        {
            arguments["y"] = yVal;
        }

        // x/y default to the returned-screenshot pixel space; --coord-space logical
        // passes raw logical coordinates.
        string? coordSpace = GetOption(args, "--coord-space");
        if (coordSpace is not null)
        {
            arguments["coord_space"] = coordSpace;
        }

        if (!TryGetIntOption(args, "--wait-frames", 0, out int waitFrames))
        {
            return 1;
        }
        if (waitFrames > 0)
        {
            arguments["wait_frames"] = waitFrames;
        }

        return CallToolAndPrint(args, "cascade_scroll", arguments);
    }

    private static int ExecuteFind(string[] args)
    {
        string? query = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null;
        string by = GetOption(args, "--by") ?? "label";

        bool disabled = args.Contains("--disabled");
        bool focused = args.Contains("--focused");
        bool visible = args.Contains("--visible");
        bool hasFilter = disabled || focused || visible;

        if (query is null && !hasFilter)
        {
            Console.Error.WriteLine(
                "Usage: cascade mcp find <query> [--by role|label|component] [--disabled] [--focused] [--visible]");
            return 1;
        }

        if (query is not null && by is not ("role" or "label" or "component"))
        {
            Console.Error.WriteLine($"Invalid --by '{by}'. Valid fields: role, label, component.");
            return 1;
        }

        var arguments = new JsonObject();
        if (query is not null)
        {
            arguments[by] = query;
        }
        if (disabled)
        {
            arguments["disabled"] = true;
        }
        if (focused)
        {
            arguments["focused"] = true;
        }
        if (visible)
        {
            arguments["visible"] = true;
        }

        return CallToolAndPrint(args, "cascade_find_nodes", arguments);
    }

    private static int ExecuteAccessibility(string[] args)
    {
        bool validate = HasFlag(args, "--validate");
        if (validate)
        {
            return CallToolAndPrint(args, "cascade_validate_accessibility", new JsonObject());
        }

        if (!TryGetIntOption(args, "--depth", 5, out int depth))
        {
            return 1;
        }

        var arguments = new JsonObject { ["depth"] = depth };
        return CallToolAndPrint(args, "cascade_accessibility_tree", arguments);
    }

    /// <summary>
    /// Executes a verb whose arguments can be parsed generically from the
    /// registry's CLI specification (positionals, options, and constant args).
    /// </summary>
    private static int ExecuteGenericVerb(McpCliVerbBinding binding, string[] args)
    {
        McpCliVerbSpec spec = binding.Verb;
        JsonObject arguments = new();

        // Add constant arguments first
        foreach ((string key, JsonNode value) in spec.ConstantArguments)
        {
            arguments[key] = value.DeepClone();
        }

        // Parse positional arguments
        int positionalIndex = 0;
        int argIndex = 0;
        while (argIndex < args.Length && positionalIndex < spec.Positionals.Count)
        {
            string arg = args[argIndex];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                break;
            }

            CliPositionalMapping positional = spec.Positionals[positionalIndex];
            if (positional.ConsumeRemaining)
            {
                var remaining = new List<string>();
                while (argIndex < args.Length && !args[argIndex].StartsWith("--", StringComparison.Ordinal))
                {
                    remaining.Add(args[argIndex]);
                    argIndex++;
                }
                arguments[positional.ArgumentProperty] = string.Join(' ', remaining);
                positionalIndex++;
                break;
            }

            if (!TryParseValue(arg, positional.Kind, out JsonNode? parsed))
            {
                Console.Error.WriteLine($"Invalid value '{arg}' for {positional.ArgumentProperty}.");
                return 1;
            }

            arguments[positional.ArgumentProperty] = parsed;
            positionalIndex++;
            argIndex++;
        }

        // Parse named options
        for (int i = argIndex; i < args.Length; i++)
        {
            string arg = args[i];
            CliOptionMapping? mapping = FindOptionMapping(spec.Options, arg);
            if (mapping is null)
            {
                // Unknown option — skip (may be --app, which is handled by CallToolAndPrint)
                if (arg is "--app" && i + 1 < args.Length)
                {
                    i++;
                }
                continue;
            }

            if (mapping.Kind == CliValueKind.Boolean)
            {
                arguments[mapping.ArgumentProperty] = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Option {mapping.OptionName} requires a value.");
                return 1;
            }

            string raw = args[++i];
            if (!TryParseValue(raw, mapping.Kind, out JsonNode? parsed))
            {
                Console.Error.WriteLine($"Invalid value '{raw}' for {mapping.OptionName}.");
                return 1;
            }

            arguments[mapping.ArgumentProperty] = parsed;
        }

        // Apply defaults for missing options
        foreach (CliOptionMapping mapping in spec.Options)
        {
            if (mapping.DefaultValue is not null && !arguments.ContainsKey(mapping.ArgumentProperty))
            {
                if (TryParseValue(mapping.DefaultValue, mapping.Kind, out JsonNode? defaultValue))
                {
                    arguments[mapping.ArgumentProperty] = defaultValue;
                }
            }
        }

        return CallToolAndPrint(args, binding.Entry.Name, arguments);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static CliOptionMapping? FindOptionMapping(IReadOnlyList<CliOptionMapping> options, string optionName)
    {
        foreach (CliOptionMapping mapping in options)
        {
            if (string.Equals(mapping.OptionName, optionName, StringComparison.Ordinal))
            {
                return mapping;
            }
        }

        return null;
    }

    private static bool TryParseValue(string raw, CliValueKind kind, out JsonNode? value)
    {
        value = kind switch
        {
            CliValueKind.String => JsonValue.Create(raw),
            CliValueKind.Int => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue) ? JsonValue.Create(intValue) : null,
            CliValueKind.Double => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue) ? JsonValue.Create(doubleValue) : null,
            CliValueKind.Boolean => bool.TryParse(raw, out bool boolValue) ? JsonValue.Create(boolValue) : null,
            _ => null,
        };

        return value is not null;
    }

    private static int CallToolAndPrint(string[] args, string toolName, JsonObject arguments)
    {
        string? appId = GetOption(args, "--app");

        using McpClient? client = ConnectOrFail(appId);
        if (client is null)
        {
            return 1;
        }

        JsonNode? response = client.CallTool(toolName, arguments);
        if (response is null)
        {
            Console.Error.WriteLine("No response from server.");
            return 1;
        }

        string? text = McpClient.ExtractText(response);
        if (text is null)
        {
            Console.Error.WriteLine("Could not extract result from response.");
            return 1;
        }

        Console.WriteLine(text);
        return 0;
    }

    private static McpClient? ConnectOrFail(string? appId)
    {
        InstanceDiscovery.DiscoveryResult discovery = InstanceDiscovery.Discover(appId);

        if (discovery.Status == InstanceDiscovery.DiscoveryStatus.NoneRunning)
        {
            Console.Error.WriteLine("No running Cascade instance found.");
            Console.Error.WriteLine("Start the app first, or specify --app <name>.");
            return null;
        }

        if (discovery.Status == InstanceDiscovery.DiscoveryStatus.MultipleRunning)
        {
            Console.Error.WriteLine($"Multiple Cascade instances are running ({discovery.AllInstances.Count}). Specify which with --app <name>:");
            foreach (InstanceEntry entry in discovery.AllInstances)
            {
                Console.Error.WriteLine($"  --app {entry.Title}   (pid {entry.Pid}, port {entry.Port}, window {entry.WindowId})");
            }

            return null;
        }

        InstanceEntry target = discovery.Instance!;
        McpClient? client = McpClient.Connect(target.Port);
        if (client is null)
        {
            Console.Error.WriteLine($"Failed to connect to instance on port {target.Port}.");
        }

        return client;
    }

    private static bool TryParseRegion(string spec, out int x, out int y, out int width, out int height)
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;

        string[] parts = spec.Split(',');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
            !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
            !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out width) ||
            !int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
        {
            return false;
        }

        return width > 0 && height > 0;
    }

    private static bool TryParseFloatValue(string optionName, string raw, out float value)
    {
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        Console.Error.WriteLine($"Invalid {optionName} '{raw}': expected a number.");
        return false;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Reads an integer option. Returns false (after printing a usage error)
    /// when the option is present but malformed — malformed values must fail
    /// loudly, not silently fall back to the default.
    /// </summary>
    private static bool TryGetIntOption(string[] args, string name, int defaultValue, out int value)
    {
        value = defaultValue;
        string? raw = GetOption(args, name);
        if (raw is null)
        {
            return true;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        Console.Error.WriteLine($"Invalid {name} '{raw}': expected an integer.");
        return false;
    }

    /// <summary>
    /// Reads a numeric option (invariant culture). Returns false (after
    /// printing a usage error) when the option is present but malformed.
    /// </summary>
    private static bool TryGetDoubleOption(string[] args, string name, double defaultValue, out double value)
    {
        value = defaultValue;
        string? raw = GetOption(args, name);
        if (raw is null)
        {
            return true;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        Console.Error.WriteLine($"Invalid {name} '{raw}': expected a number.");
        return false;
    }

    private static bool HasFlag(string[] args, string name)
    {
        return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }

    // ── Help ─────────────────────────────────────────────────────

    private static void PrintHelp()
    {
        Console.WriteLine("cascade mcp — MCP tool convenience commands");
        Console.WriteLine();
        Console.WriteLine("Usage: cascade mcp <subcommand> [options]");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine($"  {"info [--app <name>]",-50} List running Cascade instances");
        Console.WriteLine($"  {"docs [-o <file>]",-50} Tool reference generated from the registry");

        foreach (McpCliVerbBinding binding in McpToolRegistry.GetCliVerbs())
        {
            McpCliVerbSpec spec = binding.Verb;
            string summary = spec.HelpSummary;
            string usage = BuildUsageLine(spec);
            Console.WriteLine($"  {usage,-50} {summary}");
        }

        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --app <name>   Target a specific app. Without --app, the one running");
        Console.WriteLine("                 instance is auto-detected; with several running, the");
        Console.WriteLine("                 command lists them and asks for --app.");
        Console.WriteLine("  --help, -h     Show help (works for every subcommand, no app needed)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  cascade mcp info");
        Console.WriteLine("  cascade mcp tree --depth 5");
        Console.WriteLine("  cascade mcp screenshot -o capture.png --region 10,10,40,30 --scale 8");
        Console.WriteLine("  cascade mcp find Submit --by label");
        Console.WriteLine("  cascade mcp click Button:0");
    }

    private static string BuildUsageLine(McpCliVerbSpec spec)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(spec.Verb);

        foreach (CliPositionalMapping positional in spec.Positionals)
        {
            sb.Append(' ');
            sb.Append(positional.ConsumeRemaining ? $"<{positional.ArgumentProperty}...>" : $"<{positional.ArgumentProperty}>");
        }

        foreach (CliOptionMapping option in spec.Options)
        {
            sb.Append(' ');
            sb.Append(option.Kind == CliValueKind.Boolean ? $"[{option.OptionName}]" : $"[{option.OptionName} <value>]");
        }

        return sb.ToString();
    }

    private static int PrintVerbHelp(string subcommand)
    {
        if (subcommand == "info")
        {
            Console.WriteLine("Usage: cascade mcp info [--app <name>]");
            Console.WriteLine();
            Console.WriteLine("List running Cascade instances and their MCP listener ports.");
            Console.WriteLine("Without --app, every live instance is shown; with --app, only the");
            Console.WriteLine("named app is queried. This command does not require a connection");
            Console.WriteLine("to an instance — it reads the shared instance registry directly.");
            return 0;
        }

        if (subcommand == "docs")
        {
            Console.WriteLine("Usage: cascade mcp docs [-o <file>]");
            Console.WriteLine();
            Console.WriteLine("Print the MCP tool reference generated from the shared tool registry");
            Console.WriteLine("(the same source that drives the live tools, the bridge, and these CLI");
            Console.WriteLine("verbs — the page cannot drift from reality). With -o, write to a file.");
            Console.WriteLine("The checked-in copy lives at Documents/Development/mcp-tool-reference.md.");
            return 0;
        }

        McpCliVerbBinding? binding = McpToolRegistry.FindByVerb(subcommand);
        if (binding is null)
        {
            return UnknownSubcommand(subcommand);
        }

        McpCliVerbSpec spec = binding.Verb;
        Console.WriteLine($"Usage: cascade mcp {BuildUsageLine(spec)} [--app <name>]");
        Console.WriteLine();
        Console.WriteLine(binding.Entry.Description);
        Console.WriteLine();

        if (spec.Positionals.Count > 0)
        {
            Console.WriteLine("Arguments:");
            foreach (CliPositionalMapping positional in spec.Positionals)
            {
                Console.WriteLine($"  <{positional.ArgumentProperty}>  ({positional.Kind})");
            }
            Console.WriteLine();
        }

        if (spec.Options.Count > 0)
        {
            Console.WriteLine("Options:");
            foreach (CliOptionMapping option in spec.Options)
            {
                string defaultHint = option.DefaultValue is not null ? $" (default {option.DefaultValue})" : "";
                Console.WriteLine($"  {option.OptionName,-20} {option.ArgumentProperty} ({option.Kind}){defaultHint}");
            }
            Console.WriteLine();
        }

        // Preserve detailed help text for a few verbs that have CLI-only behavior.
        switch (subcommand)
        {
            case "screenshot":
                Console.WriteLine("  -o, --output FILE     Output path (default screenshot.png)");
                Console.WriteLine("  --region x,y,w,h      Crop to a window-space region before scaling");
                Console.WriteLine("  --scale N             Scale factor 0.1-10. Upscales are nearest-");
                Console.WriteLine("                        neighbor: each source pixel becomes an NxN");
                Console.WriteLine("                        block, exact for pixel inspection.");
                Console.WriteLine("  --after-frame F       Wait until presented frame F is on screen before");
                Console.WriteLine("                        capturing. Pair with a mutating verb's");
                Console.WriteLine("                        presented_frame for sleep-free sequencing. The");
                Console.WriteLine("                        output reports captured_frame either way.");
                break;

            case "drag":
                Console.WriteLine("Simulates a drag: MouseDown at start, interpolated moves, MouseUp at end.");
                Console.WriteLine("Start defaults to node center. Provide either deltas or end coordinates.");
                break;

            case "type":
                Console.WriteLine("Types text into the focused element. Multiple words are joined");
                Console.WriteLine("with spaces, so quoting is optional.");
                break;

            case "scroll":
                Console.WriteLine("  --delta-y N       Pixels to scroll (positive = down; default 100)");
                Console.WriteLine("  --to ID           Scroll until the node is visible (overrides deltas)");
                Console.WriteLine("  --x/--y N         Position to scroll at (defaults to center)");
                Console.WriteLine("  --wait-frames N   Presented frames to wait for before returning");
                Console.WriteLine();
                Console.WriteLine("The response includes presented_frame and timed_out — no sleeps needed");
                Console.WriteLine("before a follow-up screenshot (use screenshot --after-frame).");
                break;

            case "find":
                Console.WriteLine("  --by label       Match accessible label or display text (default;");
                Console.WriteLine("                   case-insensitive substring)");
                Console.WriteLine("  --by component   Match C# class name (case-insensitive substring)");
                Console.WriteLine("  --by role        Match accessible role exactly (case-insensitive)");
                Console.WriteLine("  --disabled       Only disabled controls (composes with the query)");
                Console.WriteLine("  --focused        Only the keyboard-focused element");
                Console.WriteLine("  --visible        Only laid-out nodes with non-zero bounds");
                Console.WriteLine("  (a filter alone is allowed, e.g. 'find --disabled')");
                break;

            case "accessibility":
                Console.WriteLine("  --depth N    Max tree depth (default 5)");
                Console.WriteLine("  --validate   Run a WCAG audit instead of printing the tree");
                break;

            case "whodrew":
                Console.WriteLine("Lists every draw from the last presented frame touching the pixel,");
                Console.WriteLine("in paint order (shapes, then images, then glyphs), each with the");
                Console.WriteLine("DevTools node id that emitted it. Coordinates are device pixels —");
                Console.WriteLine("the same space screenshots use. The first call enables draw capture");
                Console.WriteLine("and repaints once.");
                break;

            case "instances":
                Console.WriteLine("  --rect x,y,w,h   Only glyphs overlapping this device-pixel rect");
                Console.WriteLine("  --limit N        Max instances returned (default 200)");
                Console.WriteLine();
                Console.WriteLine("Each instance reports its atlas texel rect — pass it to");
                Console.WriteLine("'cascade mcp atlas --region u,v,w,h' to see the actual bitmap.");
                break;

            case "atlas":
                Console.WriteLine("  -o, --output FILE     Output path (default atlas.png)");
                Console.WriteLine("  --region u,v,w,h      Atlas texel rect (omit for the full atlas)");
                Console.WriteLine();
                Console.WriteLine("Coverage is rendered as grayscale. Use the atlas rects reported by");
                Console.WriteLine("'instances' or 'whodrew' as the region.");
                break;

            case "diff":
                Console.WriteLine("Actions: capture_baseline --name N, compare --name N [--tolerance T],");
                Console.WriteLine("list, clear [--name N]. compare reports diff_count plus diff_rects —");
                Console.WriteLine("bounding rects of each changed region (zoom in with");
                Console.WriteLine("'screenshot --region x,y,w,h --scale 8').");
                break;
        }

        return 0;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"Unknown subcommand: {subcommand}");
        Console.Error.WriteLine("Run 'cascade mcp --help' for available subcommands.");
        return 1;
    }
}
