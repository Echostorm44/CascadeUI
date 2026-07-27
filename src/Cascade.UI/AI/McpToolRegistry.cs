using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Declarative registry of all Cascade MCP tools. This is the single source of
/// truth consumed by three surfaces:
/// <list type="bullet">
///   <item>The live instance (<see cref="McpTools.RegisterAll(McpServer)"/>)</item>
///   <item>The headless bridge (Cascade.UI.McpBridge)</item>
///   <item>The <c>cascade mcp</c> CLI verb routing and help text</item>
/// </list>
/// Adding or removing an entry here adds/removes the tool from all three surfaces
/// (subject to each surface's build-mode filtering).
///
/// Tool names must be unique — MCP clients reject duplicate names in tools/list.
/// A tool that backs several CLI verbs (e.g. cascade_simulate_interaction →
/// click/focus/drag) declares one entry with multiple <see cref="McpCliVerbSpec"/>s.
/// </summary>
internal static class McpToolRegistry
{
    /// <summary>
    /// All registered tool entries. Order is preserved for tools/list and CLI help.
    /// </summary>
    internal static IReadOnlyList<McpToolRegistryEntry> Entries { get; } =
    [
        // ── Debug-only inspection tools ─────────────────────────────
        new(
            Name: "cascade_tree",
            Description: "Get hierarchical component and node tree. " +
                         "Use this as the starting point for any UI inspection. " +
                         "Do not use for accessibility auditing — use cascade_validate_accessibility instead. " +
                         "Returns tree structure with type, role, label, bounds, and source location.",
            InputSchemaJson: """{"type":"object","properties":{"depth":{"type":"integer","default":3,"description":"Max tree depth (1-10)"},"root_id":{"type":"string","description":"Node ID for subtree root; omit for full window"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "tree",
                    HelpSummary: "Print component tree as JSON",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("--depth", "depth", CliValueKind.Int) { DefaultValue = "3" },
                        new CliOptionMapping("--root", "root_id", CliValueKind.String),
                    ]),
            ]),

        new(
            Name: "cascade_inspect_node",
            Description: "Get complete details on a specific UI node including source location, bounds, " +
                         "theme tokens, accessibility info, reactive signals, and render statistics. " +
                         "Use cascade_tree first to find the node ID.",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string","description":"Node ID from cascade_tree"},"include_theme":{"type":"boolean","default":true},"include_signals":{"type":"boolean","default":true}},"required":["node_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "inspect",
                    HelpSummary: "Full details for a node",
                    Positionals:
                    [
                        new CliPositionalMapping("node_id", CliValueKind.String),
                    ]),
            ]),

        new(
            Name: "cascade_find_nodes",
            Description: "Search the UI tree by criteria: role, label, or component class, " +
                         "optionally narrowed by the boolean state filters disabled, focused, " +
                         "and visible. At least one criterion or filter is required; matching is " +
                         "case-insensitive (label and component match substrings, role matches " +
                         "exactly). Filters compose with the criteria as an intersection.",
            InputSchemaJson: """{"type":"object","properties":{"role":{"type":"string"},"label":{"type":"string","description":"Case-insensitive substring match against accessible label or display text"},"label_contains":{"type":"string","description":"Alias for label"},"component":{"type":"string","description":"C# class name (case-insensitive substring)"},"disabled":{"type":"boolean","description":"Filter by disabled state: true = only disabled controls, false = only enabled"},"focused":{"type":"boolean","description":"Filter by keyboard focus: true = only the focused element, false = only unfocused nodes"},"visible":{"type":"boolean","description":"Filter by visibility: true = laid out with non-zero bounds (scrolled-out still counts as visible), false = not laid out / zero-size"},"source_file":{"type":"string","description":"Case-insensitive substring match against the source file where the node was constructed. Requires launching the app with CASCADE_CAPTURE_SOURCE=1."},"limit":{"type":"integer","default":20}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "find",
                    HelpSummary: "Search nodes (case-insensitive)",
                    Positionals:
                    [
                        new CliPositionalMapping("query", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--by", "__by", CliValueKind.String) { DefaultValue = "label" },
                        new CliOptionMapping("--disabled", "disabled", CliValueKind.Boolean),
                        new CliOptionMapping("--focused", "focused", CliValueKind.Boolean),
                        new CliOptionMapping("--visible", "visible", CliValueKind.Boolean),
                        new CliOptionMapping("--source-file", "source_file", CliValueKind.String),
                    ]),
            ]),

        new(
            Name: "cascade_get_signals",
            Description: "Get reactive signal fields and computed properties with their current values and dependency chains.",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string","description":"Node ID"}},"required":["node_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_set_signal",
            Description: "Mutate a reactive signal field value for testing (debug builds only). " +
                         "The signal is resolved by its owning component's C# class name. " +
                         "Frame-synchronous: the response includes presented_frame and timed_out " +
                         "(timed_out=true means nothing repainted within ~2s).",
            InputSchemaJson: """{"type":"object","properties":{"component":{"type":"string","description":"C# class name of the component that owns the signal"},"signal":{"type":"string","description":"Signal field name"},"value":{"type":"string","description":"New value (parsed to the field's type)"},"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":["component","signal","value"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_get_layout",
            Description: "Get precise computed layout bounds (margin, border, padding, content box) for a node.",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string","description":"Node ID"}},"required":["node_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_measure",
            Description: "Measure spatial relationship between two nodes (distance, overlap, alignment).",
            InputSchemaJson: """{"type":"object","properties":{"from_id":{"type":"string"},"to_id":{"type":"string"}},"required":["from_id","to_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_theme_tokens",
            Description: "Get resolved theme token values for a node.",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string","description":"Node ID"}},"required":["node_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_get_render_stats",
            Description: "Get frame timing, layout/render/GPU duration, and performance stats.",
            InputSchemaJson: """{"type":"object","properties":{},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_diagnostics",
            Description: "Always-on allocation and native-resource diagnostics for the UI frame loop. " +
                         "Returns per-frame managed byte deltas, frame timing, and live native counters. " +
                         "Works in both DEBUG and RELEASE builds (including NativeAOT).",
            InputSchemaJson: """{"type":"object","properties":{"window_frames":{"type":"integer","default":240,"description":"Number of recent frames to summarize (max 240)"},"include_recent":{"type":"boolean","default":false,"description":"Include raw per-frame samples in output"}},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "diagnostics",
                    HelpSummary: "Per-frame allocation, timing, and native counters",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("--window-frames", "window_frames", CliValueKind.Int) { DefaultValue = "240" },
                        new CliOptionMapping("--include-recent", "include_recent", CliValueKind.Boolean),
                    ]),
            ]),

        new(
            Name: "cascade_get_source",
            Description: "Get the C# source code for a node's rendering method.",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string","description":"Node ID"}},"required":["node_id"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_history",
            Description: "Get chronological log of signal changes and re-renders.",
            InputSchemaJson: """{"type":"object","properties":{"limit":{"type":"integer","default":20}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_screenshot",
            Description: "Capture the current window as a PNG image. Returns base64-encoded image content " +
                         "plus a metadata text block with width, height, device_width/device_height (the full " +
                         "physical framebuffer), and captured_frame (the presented frame the pixels belong to). " +
                         "region coordinates are in the returned screenshot's own pixel space by default, so you " +
                         "can read a feature off a full capture and crop it directly (set region_space=\"device\" " +
                         "for raw framebuffer pixels). Pass after_frame to wait until that frame has presented " +
                         "before capturing — pair it with a mutating tool's presented_frame for sleep-free " +
                         "mutation→screenshot sequencing.",
            InputSchemaJson: """{"type":"object","properties":{"scale":{"type":"number","default":1,"description":"Scale factor 0.1-10"},"region":{"type":"object","properties":{"x":{"type":"number"},"y":{"type":"number"},"width":{"type":"number"},"height":{"type":"number"}}},"region_space":{"type":"string","enum":["screenshot","device"],"default":"screenshot","description":"Coordinate space for region: screenshot pixels (default) or raw device/framebuffer pixels"},"after_frame":{"type":"integer","description":"Wait until this presented-frame number is on screen before capturing"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: true,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "screenshot",
                    HelpSummary: "Capture screenshot to file",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("-o", "__output", CliValueKind.String),
                        new CliOptionMapping("--output", "__output", CliValueKind.String),
                        new CliOptionMapping("--region", "__region", CliValueKind.String),
                        new CliOptionMapping("--region-space", "region_space", CliValueKind.String),
                        new CliOptionMapping("--scale", "scale", CliValueKind.Double) { DefaultValue = "1" },
                        new CliOptionMapping("--after-frame", "after_frame", CliValueKind.Int),
                    ]),
            ]),

        new(
            Name: "cascade_pixel_sample",
            Description: "Get RGB values at a point or region of the rendered frame.",
            InputSchemaJson: """{"type":"object","properties":{"x":{"type":"number"},"y":{"type":"number"},"width":{"type":"number","default":1},"height":{"type":"number","default":1}},"required":["x","y"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_screenshot_diff",
            Description: "Compare the current frame against a saved baseline screenshot. " +
                         "Actions: capture_baseline, compare, list, clear. compare returns diff_count " +
                         "plus diff_rects — bounding rects of each changed region, so an agent can " +
                         "zoom straight to what moved (screenshot --region).",
            InputSchemaJson: """{"type":"object","properties":{"action":{"type":"string","enum":["capture_baseline","compare","list","clear"],"description":"What to do"},"name":{"type":"string","description":"Baseline name (required for capture_baseline and compare)"},"tolerance":{"type":"integer","default":2,"description":"Per-channel diff threshold 0-255 (compare only)"}},"required":["action"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "diff",
                    HelpSummary: "Screenshot baseline diff (capture_baseline/compare/list/clear)",
                    Positionals:
                    [
                        new CliPositionalMapping("action", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--name", "name", CliValueKind.String),
                        new CliOptionMapping("--tolerance", "tolerance", CliValueKind.Int),
                    ]),
            ]),

        new(
            Name: "cascade_whodrew",
            Description: "Answer \"what drew the pixel at (x, y)?\" — returns every draw from the last " +
                         "presented frame touching that pixel, in paint order: shape ops (kind, bounds, " +
                         "colors), image blits, and glyph quads (glyph id, font size, atlas rect), each " +
                         "with the DevTools node id that emitted it. The first call enables draw capture " +
                         "and repaints once. Source locations are not included (node-to-source mapping " +
                         "is not implemented yet); use the node id with cascade_inspect_node.",
            InputSchemaJson: """{"type":"object","properties":{"x":{"type":"number","description":"Device pixel X (same space as screenshot)"},"y":{"type":"number","description":"Device pixel Y"}},"required":["x","y"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "whodrew",
                    HelpSummary: "List every draw touching a pixel, with owning node",
                    Positionals:
                    [
                        new CliPositionalMapping("x", CliValueKind.Double),
                        new CliPositionalMapping("y", CliValueKind.Double),
                    ]),
            ]),

        new(
            Name: "cascade_glyph_instances",
            Description: "List the glyph quads the GPU drew in the last presented frame, optionally " +
                         "filtered to a screen rect: device position/size, glyph id, font size, atlas " +
                         "texel rect, color, clip, and the emitting node id. The first call enables " +
                         "draw capture and repaints once. Pair atlas rects with cascade_atlas to " +
                         "inspect the actual bitmaps.",
            InputSchemaJson: """{"type":"object","properties":{"x":{"type":"number","default":0,"description":"Filter rect min X (device pixels)"},"y":{"type":"number","default":0,"description":"Filter rect min Y"},"width":{"type":"number","description":"Filter rect width; omit for the full frame"},"height":{"type":"number","description":"Filter rect height; omit for the full frame"},"limit":{"type":"integer","default":200,"description":"Max instances returned (1-2000)"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "instances",
                    HelpSummary: "Glyph quads overlapping a screen rect, with owning nodes",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("--rect", "__rect", CliValueKind.String),
                        new CliOptionMapping("--limit", "limit", CliValueKind.Int),
                    ]),
            ]),

        new(
            Name: "cascade_atlas",
            Description: "Capture a region of the monochrome glyph atlas as a PNG (GPU readback, " +
                         "coverage rendered as grayscale). Returns image content plus a metadata text " +
                         "block with the atlas dimension and the captured rect. Use the atlas texel " +
                         "rects reported by cascade_glyph_instances or cascade_whodrew as the region. " +
                         "Rows are flipped to display orientation (the atlas stores glyph rows " +
                         "bottom-up), so a single glyph's rect reads upright.",
            InputSchemaJson: """{"type":"object","properties":{"u":{"type":"integer","default":0,"description":"Region min U in atlas texels"},"v":{"type":"integer","default":0,"description":"Region min V in atlas texels"},"width":{"type":"integer","description":"Region width; omit for the full atlas"},"height":{"type":"integer","description":"Region height; omit for the full atlas"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: true,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "atlas",
                    HelpSummary: "Save a glyph-atlas region as PNG",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("-o", "__output", CliValueKind.String),
                        new CliOptionMapping("--output", "__output", CliValueKind.String),
                        new CliOptionMapping("--region", "__region", CliValueKind.String),
                    ]),
            ]),

        new(
            Name: "cascade_simulate_interaction",
            Description: "Simulate user interaction: hover, click, press, release, focus, blur, right_click, drag. " +
                         "For drag: use start_x/start_y (node-relative), delta_x/delta_y or end_x/end_y, and steps. " +
                         "Use x/y to click at specific coordinates (e.g. overlay popups); they are in the returned " +
                         "screenshot's pixel space by default, so a point read off a screenshot can be clicked " +
                         "directly (coord_space=\"logical\" for raw logical coordinates). " +
                         "Frame-synchronous: the response includes presented_frame and timed_out — " +
                         "timed_out=true means nothing repainted within ~2s (no sleep needed before screenshots).",
            InputSchemaJson: """{"type":"object","properties":{"node_id":{"type":"string"},"interaction":{"type":"string","enum":["hover","click","press","release","focus","blur","right_click","unhover","drag"]},"x":{"type":"number"},"y":{"type":"number"},"coord_space":{"type":"string","enum":["screenshot","logical"],"default":"screenshot","description":"Coordinate space for x/y: returned-screenshot pixels (default) or raw logical/input pixels"},"start_x":{"type":"number"},"start_y":{"type":"number"},"end_x":{"type":"number"},"end_y":{"type":"number"},"delta_x":{"type":"number"},"delta_y":{"type":"number"},"steps":{"type":"integer","default":5},"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":["node_id","interaction"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "click",
                    HelpSummary: "Simulate click on a node (use --x/--y to click inside overlay popups)",
                    Positionals:
                    [
                        new CliPositionalMapping("node_id", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--x", "x", CliValueKind.Double),
                        new CliOptionMapping("--y", "y", CliValueKind.Double),
                        new CliOptionMapping("--coord-space", "coord_space", CliValueKind.String),
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ],
                    ConstantArguments: new Dictionary<string, JsonNode>
                    {
                        ["interaction"] = "click",
                    }),
                new McpCliVerbSpec(
                    Verb: "hover",
                    HelpSummary: "Hover the pointer over a node (use --x/--y for a point; drives tooltips/hover states)",
                    Positionals:
                    [
                        new CliPositionalMapping("node_id", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--x", "x", CliValueKind.Double),
                        new CliOptionMapping("--y", "y", CliValueKind.Double),
                        new CliOptionMapping("--coord-space", "coord_space", CliValueKind.String),
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ],
                    ConstantArguments: new Dictionary<string, JsonNode>
                    {
                        ["interaction"] = "hover",
                    }),
                new McpCliVerbSpec(
                    Verb: "focus",
                    HelpSummary: "Move keyboard focus to a node",
                    Positionals:
                    [
                        new CliPositionalMapping("node_id", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ],
                    ConstantArguments: new Dictionary<string, JsonNode>
                    {
                        ["interaction"] = "focus",
                    }),
                new McpCliVerbSpec(
                    Verb: "drag",
                    HelpSummary: "Simulate drag on a node",
                    Positionals:
                    [
                        new CliPositionalMapping("node_id", CliValueKind.String),
                    ],
                    Options:
                    [
                        new CliOptionMapping("--delta-x", "delta_x", CliValueKind.Double),
                        new CliOptionMapping("--delta-y", "delta_y", CliValueKind.Double),
                        new CliOptionMapping("--start-x", "start_x", CliValueKind.Double),
                        new CliOptionMapping("--start-y", "start_y", CliValueKind.Double),
                        new CliOptionMapping("--end-x", "end_x", CliValueKind.Double),
                        new CliOptionMapping("--end-y", "end_y", CliValueKind.Double),
                        new CliOptionMapping("--steps", "steps", CliValueKind.Int),
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ],
                    ConstantArguments: new Dictionary<string, JsonNode>
                    {
                        ["interaction"] = "drag",
                    }),
            ]),

        new(
            Name: "cascade_scroll",
            Description: "Scroll the viewport by a delta or scroll to a specific node. delta_y is intuitive — " +
                         "positive scrolls down (offset increases), negative scrolls up. An explicit x/y picks " +
                         "which view to scroll and is in the returned screenshot's pixel space by default " +
                         "(coord_space=\"logical\" for raw logical coordinates); omit x/y to scroll the center. " +
                         "Frame-synchronous: mutating calls include presented_frame and timed_out in the response " +
                         "(timed_out=true means nothing repainted within ~2s); pure queries (no delta, no node_id) do not wait.",
            InputSchemaJson: """{"type":"object","properties":{"delta_x":{"type":"number","default":0,"description":"Positive scrolls right, negative scrolls left"},"delta_y":{"type":"number","default":0,"description":"Positive scrolls down, negative scrolls up"},"x":{"type":"number"},"y":{"type":"number"},"coord_space":{"type":"string","enum":["screenshot","logical"],"default":"screenshot","description":"Coordinate space for x/y: returned-screenshot pixels (default) or raw logical/input pixels"},"node_id":{"type":"string","description":"Scroll to make this node visible"},"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "scroll",
                    HelpSummary: "Scroll viewport",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("--delta-y", "delta_y", CliValueKind.Double) { DefaultValue = "100" },
                        new CliOptionMapping("--delta-x", "delta_x", CliValueKind.Double) { DefaultValue = "0" },
                        new CliOptionMapping("--x", "x", CliValueKind.Double),
                        new CliOptionMapping("--y", "y", CliValueKind.Double),
                        new CliOptionMapping("--coord-space", "coord_space", CliValueKind.String),
                        new CliOptionMapping("--to", "node_id", CliValueKind.String),
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ]),
            ]),

        new(
            Name: "cascade_send_keys",
            Description: "Type text or send key events to the focused element. " +
                         "Frame-synchronous: the response includes presented_frame and timed_out " +
                         "(timed_out=true means nothing repainted within ~2s).",
            InputSchemaJson: """{"type":"object","properties":{"text":{"type":"string","description":"Text to type"},"key":{"type":"string","description":"Special key: Enter, Tab, Escape, Backspace, Delete, ArrowUp, ArrowDown, ArrowLeft, ArrowRight"},"modifiers":{"type":"array","items":{"type":"string","enum":["Ctrl","Shift","Alt"]}},"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "type",
                    HelpSummary: "Send keystrokes to focused element",
                    Positionals:
                    [
                        new CliPositionalMapping("text", CliValueKind.String) { ConsumeRemaining = true },
                    ],
                    Options:
                    [
                        new CliOptionMapping("--wait-frames", "wait_frames", CliValueKind.Int),
                    ]),
            ]),

        new(
            Name: "cascade_set_render_param",
            Description: "Set a rendering parameter without rebuilding. param=render_mode " +
                         "value=gpu|cpu switches the backend; param=dpi value=<96-based int> " +
                         "simulates a live OS DPI change (96=100%, 144=150%) exercising the " +
                         "window reposition + swapchain rescale + PixelRatio path. " +
                         "Frame-synchronous: the response includes presented_frame and timed_out " +
                         "(timed_out=true means nothing repainted within the wait window).",
            InputSchemaJson: """{"type":"object","properties":{"param":{"type":"string","description":"render_mode or dpi"},"value":{"type":"string","description":"render_mode: gpu|cpu; dpi: a 96-based integer (e.g. 144 = 150%)"},"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":["param","value"]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_undo",
            Description: "Trigger undo on the undo/redo stack. " +
                         "Frame-synchronous: the response includes presented_frame and timed_out.",
            InputSchemaJson: """{"type":"object","properties":{"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_redo",
            Description: "Trigger redo on the undo/redo stack. " +
                         "Frame-synchronous: the response includes presented_frame and timed_out.",
            InputSchemaJson: """{"type":"object","properties":{"wait_frames":{"type":"integer","default":1,"description":"How many new presented frames to wait for before responding (1-240)"}},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        // ── Release-mode tools ──────────────────────────────────────
        new(
            Name: "cascade_accessibility_tree",
            Description: "Get the accessibility tree for the application window.",
            InputSchemaJson: """{"type":"object","properties":{"depth":{"type":"integer","default":5}},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: true,
            CliVerbs:
            [
                new McpCliVerbSpec(
                    Verb: "accessibility",
                    HelpSummary: "Accessibility tree or WCAG audit",
                    Positionals: [],
                    Options:
                    [
                        new CliOptionMapping("--depth", "depth", CliValueKind.Int) { DefaultValue = "5" },
                        new CliOptionMapping("--validate", "__validate", CliValueKind.Boolean),
                    ]),
            ]),

        new(
            Name: "cascade_find_accessible",
            Description: "Search the accessibility tree by role or label.",
            InputSchemaJson: """{"type":"object","properties":{"role":{"type":"string"},"label":{"type":"string"}},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_validate_accessibility",
            Description: "Run a WCAG accessibility audit and return violations.",
            InputSchemaJson: """{"type":"object","properties":{},"required":[]}""",
            DebugOnly: true,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_window_info",
            Description: "Get window state, title, bounds, and display info.",
            InputSchemaJson: """{"type":"object","properties":{},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: true),

        new(
            Name: "cascade_list_windows",
            Description: "List all running Cascade application instances.",
            InputSchemaJson: """{"type":"object","properties":{},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: true),

        // ── Static tools (work without a live instance) ─────────────
        new(
            Name: "cascade_api_index",
            Description: "Get the full Cascade API reference as markdown. " +
                         "Includes components, layout, modifiers, themes, locale, icons, storage, routes.",
            InputSchemaJson: """{"type":"object","properties":{"section":{"type":"string","description":"Optional section: Components, Layout, Modifiers, Themes, Locale, Icons, Storage, Routes"}},"required":[]}""",
            DebugOnly: false,
            RawResponse: false,
            RequiresLiveInstance: false),
    ];

    /// <summary>
    /// Finds a registry entry by its MCP tool name.
    /// </summary>
    internal static McpToolRegistryEntry? FindByName(string name)
    {
        foreach (McpToolRegistryEntry entry in Entries)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the tool entry and verb spec for a CLI verb name.
    /// </summary>
    internal static McpCliVerbBinding? FindByVerb(string verb)
    {
        foreach (McpToolRegistryEntry entry in Entries)
        {
            foreach (McpCliVerbSpec spec in entry.CliVerbs)
            {
                if (string.Equals(spec.Verb, verb, StringComparison.Ordinal))
                {
                    return new McpCliVerbBinding(entry, spec);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns every CLI verb with its backing tool entry, in declaration order.
    /// </summary>
    internal static IEnumerable<McpCliVerbBinding> GetCliVerbs()
    {
        foreach (McpToolRegistryEntry entry in Entries)
        {
            foreach (McpCliVerbSpec spec in entry.CliVerbs)
            {
                yield return new McpCliVerbBinding(entry, spec);
            }
        }
    }
}

/// <summary>
/// A single declarative entry in the MCP tool registry. <paramref name="CliVerbs"/>
/// lists every CLI verb the tool backs — one tool entry may expose several verbs
/// (constant arguments distinguish them), but each tool name appears exactly once
/// so tools/list never contains duplicates.
/// </summary>
internal sealed record McpToolRegistryEntry(
    string Name,
    string Description,
    string InputSchemaJson,
    bool DebugOnly,
    bool RawResponse,
    bool RequiresLiveInstance,
    IReadOnlyList<McpCliVerbSpec>? CliVerbs = null)
{
    /// <summary>CLI verbs backed by this tool (empty when the tool has no CLI surface).</summary>
    public IReadOnlyList<McpCliVerbSpec> CliVerbs { get; } = CliVerbs ?? [];
}

/// <summary>
/// A CLI verb paired with the tool entry that backs it.
/// </summary>
internal sealed record McpCliVerbBinding(
    McpToolRegistryEntry Entry,
    McpCliVerbSpec Verb);

/// <summary>
/// Describes how a tool is exposed as a <c>cascade mcp &lt;verb&gt;</c> command.
/// </summary>
internal sealed record McpCliVerbSpec(
    string Verb,
    string HelpSummary,
    IReadOnlyList<CliPositionalMapping>? Positionals = null,
    IReadOnlyList<CliOptionMapping>? Options = null,
    IReadOnlyDictionary<string, JsonNode>? ConstantArguments = null)
{
    /// <summary>Positional arguments, in order.</summary>
    public IReadOnlyList<CliPositionalMapping> Positionals { get; } = Positionals ?? [];

    /// <summary>Named options.</summary>
    public IReadOnlyList<CliOptionMapping> Options { get; } = Options ?? [];

    /// <summary>Constant argument values always added to the tool call.</summary>
    public IReadOnlyDictionary<string, JsonNode> ConstantArguments { get; } = ConstantArguments ?? new Dictionary<string, JsonNode>();
}

/// <summary>
/// Maps a CLI positional argument to a tool argument property.
/// </summary>
internal sealed record CliPositionalMapping(
    string ArgumentProperty,
    CliValueKind Kind)
{
    /// <summary>
    /// When true, this positional consumes all remaining non-option arguments
    /// (used for commands like <c>cascade mcp type hello world</c>).
    /// </summary>
    public bool ConsumeRemaining { get; init; }
}

/// <summary>
/// Maps a CLI named option to a tool argument property.
/// </summary>
internal sealed record CliOptionMapping(
    string OptionName,
    string ArgumentProperty,
    CliValueKind Kind)
{
    /// <summary>Whether the option must be supplied.</summary>
    public bool Required { get; init; }

    /// <summary>String representation of the default value when omitted.</summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Supported CLI value kinds for generic parsing.
/// </summary>
internal enum CliValueKind
{
    String,
    Int,
    Double,
    Boolean,
}
