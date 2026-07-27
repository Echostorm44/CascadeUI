using System.Text.Json.Nodes;

namespace Cascade.UI.Testing;

/// <summary>
/// High-level test driver that wraps <see cref="McpTestClient"/> with typed methods
/// for all MCP tools. Use this in integration tests to drive a running Cascade app.
/// </summary>
/// <remarks>
/// <code>
/// await using var client = await McpTestClient.ConnectAsync("./MyApp.exe");
/// var runner = new McpTestRunner(client);
/// await runner.SimulateInteraction("btn-submit", "click");
/// var tree = await runner.GetTree();
/// </code>
/// </remarks>
public sealed class McpTestRunner
{
    private readonly McpTestClient client;

    /// <summary>Creates a runner wrapping the given client connection.</summary>
    public McpTestRunner(McpTestClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>The underlying MCP client for raw tool calls.</summary>
    public McpTestClient Client => client;

    // ── Component tree ──────────────────────────────────────────────

    /// <summary>
    /// Gets the component tree from the running app.
    /// </summary>
    /// <param name="depth">Maximum depth to traverse (default: unlimited).</param>
    /// <param name="rootId">Optional root node ID to start from.</param>
    public async Task<JsonNode?> GetTree(int? depth = null, string? rootId = null)
    {
        var args = new JsonObject();
        if (depth is not null)
        {
            args["depth"] = depth.Value;
        }
        if (rootId is not null)
        {
            args["root_id"] = rootId;
        }
        return await client.CallToolAsync("cascade_tree", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Inspects a specific node by ID.
    /// </summary>
    public async Task<JsonNode?> InspectNode(string nodeId)
    {
        return await client.CallToolAsync("cascade_inspect_node", new JsonObject
        {
            ["node_id"] = nodeId,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds nodes matching the given criteria.
    /// </summary>
    public async Task<JsonNode?> FindNodes(
        string? role = null,
        string? label = null,
        string? component = null,
        string? sourceFile = null,
        string? state = null)
    {
        var args = new JsonObject();
        if (role is not null) { args["role"] = role; }
        if (label is not null) { args["label"] = label; }
        if (component is not null) { args["component"] = component; }
        if (sourceFile is not null) { args["source_file"] = sourceFile; }
        if (state is not null) { args["state"] = state; }
        return await client.CallToolAsync("cascade_find_nodes", args).ConfigureAwait(false);
    }

    // ── Signals & state ──────────────────────────────────────────────

    /// <summary>
    /// Sets a reactive signal value on a component (debug builds only).
    /// </summary>
    public async Task<JsonNode?> SetSignal(string nodeId, string field, JsonNode value)
    {
        return await client.CallToolAsync("cascade_set_signal", new JsonObject
        {
            ["node_id"] = nodeId,
            ["field"] = field,
            ["value"] = value,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets reactive signal values for a component.
    /// </summary>
    public async Task<JsonNode?> GetSignals(string nodeId)
    {
        return await client.CallToolAsync("cascade_get_signals", new JsonObject
        {
            ["node_id"] = nodeId,
        }).ConfigureAwait(false);
    }

    // ── Interaction ──────────────────────────────────────────────────

    /// <summary>
    /// Simulates an interaction on a node.
    /// </summary>
    /// <param name="nodeId">Target node ID.</param>
    /// <param name="interaction">Interaction type: hover, click, press, release, focus, blur, right_click.</param>
    /// <param name="x">Optional x coordinate relative to the node.</param>
    /// <param name="y">Optional y coordinate relative to the node.</param>
    public async Task<JsonNode?> SimulateInteraction(string nodeId, string interaction, float? x = null, float? y = null)
    {
        var args = new JsonObject
        {
            ["node_id"] = nodeId,
            ["interaction"] = interaction,
        };
        if (x is not null) { args["x"] = x.Value; }
        if (y is not null) { args["y"] = y.Value; }
        return await client.CallToolAsync("cascade_simulate_interaction", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends key input to the app.
    /// </summary>
    /// <param name="text">Text to type.</param>
    /// <param name="keys">Key names to press (e.g., "Enter", "Tab").</param>
    /// <param name="modifiers">Modifier keys (e.g., "Ctrl", "Shift").</param>
    public async Task<JsonNode?> SendKeys(string? text = null, string[]? keys = null, string[]? modifiers = null)
    {
        var args = new JsonObject();
        if (text is not null) { args["text"] = text; }
        if (keys is not null) { args["keys"] = new JsonArray(keys.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray()); }
        if (modifiers is not null) { args["modifiers"] = new JsonArray(modifiers.Select(m => (JsonNode)JsonValue.Create(m)!).ToArray()); }
        return await client.CallToolAsync("cascade_send_keys", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Scrolls a node by delta or to a specific child.
    /// </summary>
    public async Task<JsonNode?> Scroll(string nodeId, float? deltaX = null, float? deltaY = null, string? scrollToNodeId = null)
    {
        var args = new JsonObject { ["node_id"] = nodeId };
        if (deltaX is not null) { args["delta_x"] = deltaX.Value; }
        if (deltaY is not null) { args["delta_y"] = deltaY.Value; }
        if (scrollToNodeId is not null) { args["scroll_to_node"] = scrollToNodeId; }
        return await client.CallToolAsync("cascade_scroll", args).ConfigureAwait(false);
    }

    // ── Visual ───────────────────────────────────────────────────────

    /// <summary>
    /// Takes a screenshot of the running app.
    /// </summary>
    /// <param name="region">Optional crop region as {x, y, width, height}.</param>
    /// <param name="scale">Optional scale factor (0.1–10).</param>
    public async Task<JsonNode?> Screenshot(JsonObject? region = null, float? scale = null)
    {
        var args = new JsonObject();
        if (region is not null) { args["region"] = region; }
        if (scale is not null) { args["scale"] = scale.Value; }
        return await client.CallToolAsync("cascade_screenshot", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Samples pixel RGB values at a point or region.
    /// </summary>
    public async Task<JsonNode?> PixelSample(int x, int y, int? width = null, int? height = null)
    {
        var args = new JsonObject { ["x"] = x, ["y"] = y };
        if (width is not null) { args["width"] = width.Value; }
        if (height is not null) { args["height"] = height.Value; }
        return await client.CallToolAsync("cascade_pixel_sample", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares current frame against a saved baseline.
    /// </summary>
    public async Task<JsonNode?> ScreenshotDiff(string baselinePath, float? threshold = null)
    {
        var args = new JsonObject { ["baseline"] = baselinePath };
        if (threshold is not null) { args["threshold"] = threshold.Value; }
        return await client.CallToolAsync("cascade_screenshot_diff", args).ConfigureAwait(false);
    }

    // ── Accessibility ────────────────────────────────────────────────

    /// <summary>
    /// Gets the accessibility tree.
    /// </summary>
    public async Task<JsonNode?> GetAccessibilityTree()
    {
        return await client.CallToolAsync("cascade_accessibility_tree").ConfigureAwait(false);
    }

    /// <summary>
    /// Searches the accessibility tree.
    /// </summary>
    public async Task<JsonNode?> FindAccessible(string? role = null, string? label = null)
    {
        var args = new JsonObject();
        if (role is not null) { args["role"] = role; }
        if (label is not null) { args["label"] = label; }
        return await client.CallToolAsync("cascade_find_accessible", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates accessibility (WCAG audit).
    /// </summary>
    public async Task<JsonNode?> ValidateAccessibility()
    {
        return await client.CallToolAsync("cascade_validate_accessibility").ConfigureAwait(false);
    }

    // ── Layout & rendering ──────────────────────────────────────────

    /// <summary>
    /// Gets computed layout bounds for a node.
    /// </summary>
    public async Task<JsonNode?> GetLayout(string nodeId)
    {
        return await client.CallToolAsync("cascade_get_layout", new JsonObject
        {
            ["node_id"] = nodeId,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Measures the spatial relationship between two nodes.
    /// </summary>
    public async Task<JsonNode?> Measure(string fromNodeId, string toNodeId)
    {
        return await client.CallToolAsync("cascade_measure", new JsonObject
        {
            ["from"] = fromNodeId,
            ["to"] = toNodeId,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets render statistics (frame timing, layout/render/GPU duration).
    /// </summary>
    public async Task<JsonNode?> GetRenderStats()
    {
        return await client.CallToolAsync("cascade_get_render_stats").ConfigureAwait(false);
    }

    // ── Resources ────────────────────────────────────────────────────

    /// <summary>
    /// Reads the accessibility tree as a resource.
    /// </summary>
    public async Task<JsonNode?> ReadAccessibilityTreeResource()
    {
        return await client.ReadResourceAsync("cascade://accessibility-tree").ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the signal change and re-render history.
    /// </summary>
    public async Task<JsonNode?> GetHistory(int? limit = null)
    {
        var args = new JsonObject();
        if (limit is not null) { args["limit"] = limit.Value; }
        return await client.CallToolAsync("cascade_history", args).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the window info.
    /// </summary>
    public async Task<JsonNode?> GetWindowInfo()
    {
        return await client.CallToolAsync("cascade_window_info").ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all running window instances.
    /// </summary>
    public async Task<JsonNode?> ListWindows()
    {
        return await client.CallToolAsync("cascade_list_windows").ConfigureAwait(false);
    }
}
