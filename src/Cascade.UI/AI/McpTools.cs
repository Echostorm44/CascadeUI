using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace Cascade.UI.AI;

/// <summary>
/// Registers all framework-level MCP tools (cascade_* namespace) with the
/// MCP server. Debug-only tools provide full component tree inspection,
/// signal manipulation, and performance stats. Release-mode tools provide
/// accessibility tree and window info.
/// </summary>
internal static class McpTools
{
    // In-memory baseline storage for screenshot diff tool
#if CASCADE_DEVTOOLS
    private static readonly Dictionary<string, ImageData> screenshotBaselines = new(StringComparer.OrdinalIgnoreCase);
#endif

    /// <summary>
    /// Registers all framework inspection tools with the server.
    /// Debug-only tools are tagged and filtered out in release builds.
    /// Tool metadata comes from <see cref="McpToolRegistry"/>; handlers are
    /// resolved here by tool name so the registry stays declarative.
    /// </summary>
    public static void RegisterAll(McpServer server)
    {
        foreach (McpToolRegistryEntry entry in McpToolRegistry.Entries)
        {
            Func<JsonObject, string> handler = ResolveHandler(entry.Name);
            server.RegisterTool(new McpToolDefinition
            {
                Name = entry.Name,
                Description = entry.Description,
                InputSchemaJson = entry.InputSchemaJson,
                DebugOnly = entry.DebugOnly,
                RawResponse = entry.RawResponse,
                Handler = handler,
            });
        }
    }

    /// <summary>
    /// Resolves the live-instance handler for a tool by name.
    /// </summary>
    private static Func<JsonObject, string> ResolveHandler(string name)
    {
        return name switch
        {
            "cascade_tree" => HandleCascadeTree,
            "cascade_inspect_node" => HandleCascadeInspectNode,
            "cascade_find_nodes" => HandleCascadeFindNodes,
            "cascade_get_signals" => HandleCascadeGetSignals,
            "cascade_set_signal" => HandleCascadeSetSignal,
            "cascade_get_source" => HandleCascadeGetSource,
            "cascade_get_render_stats" => HandleCascadeGetRenderStats,
            "cascade_diagnostics" => HandleCascadeDiagnostics,
            "cascade_history" => HandleCascadeHistory,
            "cascade_get_layout" => HandleCascadeGetLayout,
            "cascade_measure" => HandleCascadeMeasure,
            "cascade_theme_tokens" => HandleCascadeThemeTokens,
            "cascade_validate_accessibility" => HandleCascadeValidateAccessibility,
            "cascade_get_animation_state" => HandleCascadeGetAnimationState,
            "cascade_simulate_interaction" => HandleCascadeSimulateInteraction,
            "cascade_scroll" => HandleCascadeScroll,
            "cascade_send_keys" => HandleCascadeSendKeys,
            "cascade_undo" => HandleCascadeUndo,
            "cascade_redo" => HandleCascadeRedo,
            "cascade_screenshot" => HandleCascadeScreenshot,
            "cascade_pixel_sample" => HandleCascadePixelSample,
            "cascade_screenshot_diff" => HandleCascadeScreenshotDiff,
            "cascade_whodrew" => HandleCascadeWhodrew,
            "cascade_glyph_instances" => HandleCascadeGlyphInstances,
            "cascade_atlas" => HandleCascadeAtlas,
            "cascade_set_render_param" => HandleCascadeSetRenderParam,
            "cascade_accessibility_tree" => HandleCascadeAccessibilityTree,
            "cascade_find_accessible" => HandleCascadeFindAccessible,
            "cascade_window_info" => HandleCascadeWindowInfo,
            "cascade_list_windows" => HandleCascadeListWindows,
            "cascade_api_index" => HandleCascadeApiIndex,
            _ => throw new InvalidOperationException($"No handler registered for tool '{name}'."),
        };
    }

    // ── Tool handlers ───────────────────────────────────────────

    private static string HandleCascadeTree(JsonObject parameters)
    {
        int depth = GetInt(parameters, "depth", 3);
        depth = Math.Clamp(depth, 1, 10);
        string? rootId = GetString(parameters, "root_id");

#if CASCADE_DEVTOOLS
        var status = DevTools.NodeTreeWalker.TryFindNode(rootId, out var root);
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotWired)
        {
            return ErrorJson(DevTools.NodeTreeWalker.NotWiredMessage, rootId);
        }
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotFound || root is null)
        {
            return ErrorJson("Node not found", rootId);
        }

        var snapshot = DevTools.NodeTreeWalker.Snapshot(root, depth, 0);
        return SerializeTreeSnapshot(snapshot, depth);
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeInspectNode(JsonObject parameters)
    {
        string? nodeId = GetString(parameters, "node_id");
        if (nodeId is null)
        {
            return ErrorJson("node_id is required", null);
        }

#if CASCADE_DEVTOOLS
        var status = DevTools.NodeTreeWalker.TryFindNode(nodeId, out var node);
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotWired)
        {
            return ErrorJson(DevTools.NodeTreeWalker.NotWiredMessage, nodeId);
        }
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotFound || node is null)
        {
            return ErrorJson("Node not found", nodeId);
        }

        var detail = DevTools.NodeTreeWalker.DetailSnapshot(node);
        return SerializeNodeDetail(detail);
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeFindNodes(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? role = GetString(parameters, "role");
        string? label = GetString(parameters, "label") ?? GetString(parameters, "label_contains");
        string? component = GetString(parameters, "component");
        bool? disabled = GetBoolOrNull(parameters, "disabled");
        bool? focused = GetBoolOrNull(parameters, "focused");
        bool? visible = GetBoolOrNull(parameters, "visible");
        string? sourceFile = GetString(parameters, "source_file");
        int limit = Math.Clamp(GetInt(parameters, "limit", 20), 1, 200);

        if (role is null && label is null && component is null
            && disabled is null && focused is null && visible is null && sourceFile is null)
        {
            return ErrorJson(
                "At least one search criterion or filter is required: role, label, " +
                "label_contains, component, disabled, focused, visible, or source_file. " +
                "Use cascade_tree for unfiltered structural exploration.", null);
        }

        // source_file requires opt-in construction-time capture (a PDB stack walk
        // per node), which is off unless the app was launched with
        // CASCADE_CAPTURE_SOURCE=1. Captured nodes carry no source otherwise.
        if (sourceFile is not null && !Node.SourceCaptureEnabled)
        {
            return ErrorJson(
                "source_file filtering needs node source-location capture, which is off. " +
                "Relaunch the app with environment variable CASCADE_CAPTURE_SOURCE=1.", null);
        }

        var status = DevTools.NodeTreeWalker.TryFindNode(null, out var root);
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotWired || root is null)
        {
            return ErrorJson(DevTools.NodeTreeWalker.NotWiredMessage, null);
        }

        var allBounds = DevTools.NodeTreeWalker.GetAllNodeBounds();
        var sb = new StringBuilder();
        sb.Append("{\"nodes\":[");

        int totalMatches = 0;
        int returned = 0;
        foreach (var (nodeId, bounds, _) in allBounds)
        {
            var node = DevTools.NodeTreeWalker.FindNode(nodeId);
            if (node is null)
            {
                continue;
            }

            string typeName = node.GetType().Name;
            (string? nodeRole, string? nodeLabel) = DescribeNodeForSearch(node);

            if (component is not null && !typeName.Contains(component, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (role is not null && !string.Equals(nodeRole, role, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (label is not null &&
                (nodeLabel is null || !nodeLabel.Contains(label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (disabled is bool wantDisabled && IsNodeDisabled(node) != wantDisabled)
            {
                continue;
            }

            if (focused is bool wantFocused)
            {
                bool isFocused = node is Node focusNode && ReferenceEquals(FocusManager.FocusedElement, focusNode);
                if (isFocused != wantFocused)
                {
                    continue;
                }
            }

            if (visible is bool wantVisible)
            {
                bool isVisible = node is Node visNode && !visNode.IsLayoutEmpty && bounds.Width > 0 && bounds.Height > 0;
                if (isVisible != wantVisible)
                {
                    continue;
                }
            }

            string? nodeSource = (node as Node)?.SourceFile;
            if (sourceFile is not null &&
                (nodeSource is null || !nodeSource.Contains(sourceFile, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            totalMatches++;
            if (returned >= limit)
            {
                continue;
            }

            if (returned > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"id\":\"{EscapeJson(nodeId)}\",\"type\":\"{EscapeJson(typeName)}\"");
            if (nodeRole is not null)
            {
                sb.Append($",\"role\":\"{EscapeJson(nodeRole)}\"");
            }
            if (nodeLabel is not null)
            {
                sb.Append($",\"label\":\"{EscapeJson(nodeLabel)}\"");
            }
            sb.Append($",\"bounds\":{{\"x\":{bounds.X},\"y\":{bounds.Y},\"width\":{bounds.Width},\"height\":{bounds.Height}}}");
            if (nodeSource is not null)
            {
                sb.Append($",\"source\":{{\"file\":\"{EscapeJson(nodeSource)}\",\"line\":{((Node)node).SourceLine}}}");
            }
            sb.Append('}');
            returned++;
        }

        sb.Append($"],\"total_matches\":{totalMatches}}}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

#if CASCADE_DEVTOOLS
    /// <summary>
    /// Extracts the searchable role and label text for a node. The label is
    /// the accessible label when set, otherwise the visible display text of
    /// text-bearing controls (Label, Button).
    /// </summary>
    private static (string? role, string? label) DescribeNodeForSearch(object node)
    {
        string? role = null;
        string? label = null;

        if (node is Node uiNode)
        {
            var layoutData = uiNode.LayoutData;
            if (layoutData.A11yRole != AccessibleRole.None)
            {
                role = layoutData.A11yRole.ToString();
            }
            label = layoutData.A11yLabel;
        }

        label ??= node switch
        {
            Label labelNode => labelNode.Text ?? labelNode.LocText.Resolve(),
            Button button => button.Label.Resolve(),
            _ => null,
        };

        return (role, label);
    }

    /// <summary>
    /// Whether an interactive control is disabled. There is no uniform
    /// Node-level disabled surface (each control has its own internal
    /// <c>IsDisabled</c>, and many are generic, e.g. Select&lt;T&gt;), so this
    /// reads it reflectively — consistent with the rest of the DEVTOOLS-gated
    /// signal/field reflection and uniform across generic and non-generic
    /// controls. Returns false for nodes that have no disabled concept.
    /// (The layout data's A11yDisabled is unpopulated today; WP-3515 note.)
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Control reflection surface preserved via TrimmerRoots.xml when CASCADE_DEVTOOLS is defined.")]
    private static bool IsNodeDisabled(object node)
    {
        var prop = node.GetType().GetProperty(
            "IsDisabled",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        return prop is not null
            && prop.PropertyType == typeof(bool)
            && prop.GetValue(node) is true;
    }
#endif

    private static string HandleCascadeGetSignals(JsonObject parameters)
    {
        string? component = GetString(parameters, "component");
        if (component is null)
        {
            return ErrorJson("component is required", null);
        }

#if CASCADE_DEVTOOLS
        var signals = DevTools.NodeTreeWalker.GetAllSignals();
        var computed = DevTools.NodeTreeWalker.GetAllComputed();

        var sb = new StringBuilder();
        sb.Append($"{{\"component\":\"{EscapeJson(component)}\"");
        sb.Append(",\"signals\":[");

        int signalCount = 0;
        foreach (var signal in signals)
        {
            if (!string.Equals(signal.ComponentName, component, StringComparison.Ordinal))
            {
                continue;
            }

            if (signalCount > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"name\":\"{EscapeJson(signal.FieldName)}\",\"type\":\"{EscapeJson(signal.ValueType)}\"");
            sb.Append($",\"value\":\"{EscapeJson(signal.CurrentValue)}\",\"is_readonly\":{BoolStr(signal.IsReadOnly)}}}");
            signalCount++;
        }

        sb.Append("],\"computed\":[");

        int computedCount = 0;
        foreach (var comp in computed)
        {
            if (!string.Equals(comp.ComponentName, component, StringComparison.Ordinal))
            {
                continue;
            }

            if (computedCount > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"name\":\"{EscapeJson(comp.PropertyName)}\",\"type\":\"{EscapeJson(comp.ValueType)}\"");
            sb.Append($",\"value\":\"{EscapeJson(comp.CurrentValue)}\"}}");
            computedCount++;
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeSetSignal(JsonObject parameters)
    {
        string? component = GetString(parameters, "component");
        string? signal = GetString(parameters, "signal");

        if (component is null || signal is null)
        {
            return ErrorJson("component and signal are required", null);
        }

        string? valueStr = parameters["value"]?.ToString();
        if (valueStr is null)
        {
            return ErrorJson("value is required", null);
        }

#if CASCADE_DEVTOOLS
        // Signal mutations must happen on the UI thread because they trigger
        // reactivity notifications → render scheduling → SetTimer.
        bool success = false;
        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                success = DevTools.NodeTreeWalker.TrySetSignal(component, signal, valueStr);
            }).Wait();
        }
        catch (Exception ex)
        {
            return ErrorJson($"Signal set failed: {ex.Message}", null);
        }

        var sb = new StringBuilder();
        sb.Append($"{{\"success\":{BoolStr(success)}");
        sb.Append($",\"component\":\"{EscapeJson(component)}\"");
        sb.Append($",\"signal\":\"{EscapeJson(signal)}\"}}");
        return WithPresentation(sb.ToString(), baselineFrame, GetWaitFrames(parameters));
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeGetSource(JsonObject parameters)
    {
        string? nodeId = GetString(parameters, "node_id");
        if (nodeId is null)
        {
            return ErrorJson("node_id is required", null);
        }

#if CASCADE_DEVTOOLS
        var status = DevTools.NodeTreeWalker.TryFindNode(nodeId, out var node);
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotWired)
        {
            return ErrorJson(DevTools.NodeTreeWalker.NotWiredMessage, nodeId);
        }
        if (status == DevTools.NodeTreeWalker.FindNodeStatus.NotFound || node is null)
        {
            return ErrorJson("Node not found", nodeId);
        }

        var detail = DevTools.NodeTreeWalker.DetailSnapshot(node);
        var sb = new StringBuilder();
        sb.Append($"{{\"node_id\":\"{EscapeJson(nodeId)}\"");
        sb.Append($",\"type\":\"{EscapeJson(detail.TypeName)}\"");
        if (detail.SourceLocation is not null)
        {
            sb.Append($",\"source_location\":\"{EscapeJson(detail.SourceLocation)}\"");
        }
        sb.Append('}');
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeGetRenderStats(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? component = GetString(parameters, "component");
        int topN = GetInt(parameters, "top_n", 10);

        var stats = DevTools.PerformancePanel.GetComponentStats();
        var sb = new StringBuilder();

        if (component is null)
        {
            sb.Append("{\"mode\":\"app_wide\",\"components\":[");
            int count = 0;
            foreach (var stat in stats)
            {
                if (count >= topN)
                {
                    break;
                }
                if (count > 0)
                {
                    sb.Append(',');
                }
                sb.Append($"{{\"component\":\"{EscapeJson(stat.ComponentName)}\"");
                sb.Append($",\"render_count\":{stat.RenderCount}");
                sb.Append($",\"average_render_ms\":{stat.AverageRenderMs:F2}}}");
                count++;
            }
            sb.Append("]}");
        }
        else
        {
            var match = stats.FirstOrDefault(s => string.Equals(s.ComponentName, component, StringComparison.Ordinal));
            if (match is not null)
            {
                sb.Append($"{{\"component\":\"{EscapeJson(component)}\"");
                sb.Append($",\"render_count\":{match.RenderCount}");
                sb.Append($",\"average_render_ms\":{match.AverageRenderMs:F2}}}");
            }
            else
            {
                sb.Append($"{{\"component\":\"{EscapeJson(component)}\",\"error\":\"Component not found\"}}");
            }
        }

        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeDiagnostics(JsonObject parameters)
    {
        int windowFrames = GetInt(parameters, "window_frames", 240);
        windowFrames = Math.Clamp(windowFrames, 1, Cascade.UI.Diagnostics.DiagnosticsHub.Capacity);
        bool includeRecent = parameters["include_recent"]?.GetValue<bool>() ?? false;

        var summary = Cascade.UI.Diagnostics.DiagnosticsHub.Summarize(windowFrames);
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"total_frames\":{Cascade.UI.Diagnostics.DiagnosticsHub.TotalFrames}");
        sb.Append($",\"window_frames\":{summary.FrameCount}");
        sb.Append($",\"mean_bytes_per_frame\":{summary.MeanBytesPerFrame}");
        sb.Append($",\"mean_layout_bytes_per_frame\":{summary.MeanLayoutBytesPerFrame}");
        sb.Append($",\"mean_paint_bytes_per_frame\":{summary.MeanPaintBytesPerFrame}");
        sb.Append($",\"max_bytes_per_frame\":{summary.MaxBytesPerFrame}");
        sb.Append($",\"max_layout_bytes_per_frame\":{summary.MaxLayoutBytesPerFrame}");
        sb.Append($",\"max_paint_bytes_per_frame\":{summary.MaxPaintBytesPerFrame}");
        sb.Append($",\"estimated_bytes_per_second\":{summary.EstimatedBytesPerSecond}");
        sb.Append($",\"average_frame_ms\":{summary.AverageFrameMs:F3}");
        sb.Append($",\"estimated_fps\":{summary.EstimatedFps:F2}");
        sb.Append(",\"native\":{");
        sb.Append($"\"compiled_paths_live\":{Cascade.UI.Diagnostics.NativeMemoryCounters.CompiledPathsLive}");
        sb.Append($",\"compiled_paths_total_created\":{Cascade.UI.Diagnostics.NativeMemoryCounters.CompiledPathsTotalCreated}");
        sb.Append($",\"compiled_paths_total_destroyed\":{Cascade.UI.Diagnostics.NativeMemoryCounters.CompiledPathsTotalDestroyed}");
        sb.Append($",\"images_live\":{Cascade.UI.Diagnostics.NativeMemoryCounters.ImagesLive}");
        sb.Append($",\"images_total_created\":{Cascade.UI.Diagnostics.NativeMemoryCounters.ImagesTotalCreated}");
        sb.Append($",\"images_total_destroyed\":{Cascade.UI.Diagnostics.NativeMemoryCounters.ImagesTotalDestroyed}");
        sb.Append($",\"fonts_live\":{Cascade.UI.Diagnostics.NativeMemoryCounters.FontsLive}");
        sb.Append($",\"fonts_total_created\":{Cascade.UI.Diagnostics.NativeMemoryCounters.FontsTotalCreated}");
        sb.Append($",\"fonts_total_destroyed\":{Cascade.UI.Diagnostics.NativeMemoryCounters.FontsTotalDestroyed}");
        sb.Append('}');

#if CASCADE_DEVTOOLS
        // DevTools: native-side memory snapshot from cascade_memory_stats.
        // Source of truth for "where did the native bytes go" — reads the
        // Rust handle maps directly and queries wgpu for its allocator
        // report (wgpu counters, surfaced through the Etch GPU backend).
        //
        // Most byte totals are always accurate. Fields prefixed wgpu_buffer_
        // memory_bytes, wgpu_texture_memory_bytes, and the wgpu object counts
        // require wgpu/counters at Rust compile time (Cargo feature 'devtools')
        // and are 0 otherwise. 'counters_feature_enabled' reports which mode
        // the running native lib is in.
        if (Cascade.UI.Diagnostics.NativeMemorySnapshotProvider.TrySnapshot(out var ms))
        {
            sb.Append(",\"native_memory\":{");
            sb.Append($"\"version\":{ms.Version}");
            sb.Append($",\"counters_feature_enabled\":{BoolStr(ms.CountersFeatureEnabled)}");
            sb.Append($",\"allocator_report_available\":{BoolStr(ms.AllocatorReportAvailable)}");

            sb.Append($",\"device_count\":{ms.DeviceCount}");
            sb.Append($",\"surface_count\":{ms.SurfaceCount}");
            sb.Append($",\"font_count\":{ms.FontCount}");
            sb.Append($",\"image_count\":{ms.ImageCount}");
            sb.Append($",\"path_count\":{ms.PathCount}");
            sb.Append($",\"scene_frame_count\":{ms.SceneFrameCount}");

            sb.Append($",\"font_data_bytes\":{ms.FontDataBytes}");
            sb.Append($",\"image_pixel_bytes\":{ms.ImagePixelBytes}");
            sb.Append($",\"path_element_bytes\":{ms.PathElementBytes}");

            sb.Append($",\"surface_intermediate_bytes\":{ms.SurfaceIntermediateBytes}");
            sb.Append($",\"surface_swapchain_bytes_est\":{ms.SurfaceSwapchainBytesEst}");

            sb.Append($",\"wgpu_allocated_bytes\":{ms.WgpuAllocatedBytes}");
            sb.Append($",\"wgpu_reserved_bytes\":{ms.WgpuReservedBytes}");
            sb.Append($",\"wgpu_memory_blocks\":{ms.WgpuMemoryBlocks}");
            sb.Append($",\"wgpu_live_allocations\":{ms.WgpuLiveAllocations}");

            sb.Append($",\"wgpu_buffer_memory_bytes\":{ms.WgpuBufferMemoryBytes}");
            sb.Append($",\"wgpu_texture_memory_bytes\":{ms.WgpuTextureMemoryBytes}");
            sb.Append($",\"wgpu_buffers\":{ms.WgpuBuffers}");
            sb.Append($",\"wgpu_textures\":{ms.WgpuTextures}");
            sb.Append($",\"wgpu_texture_views\":{ms.WgpuTextureViews}");
            sb.Append($",\"wgpu_bind_groups\":{ms.WgpuBindGroups}");
            sb.Append($",\"wgpu_render_pipelines\":{ms.WgpuRenderPipelines}");
            sb.Append($",\"wgpu_compute_pipelines\":{ms.WgpuComputePipelines}");
            sb.Append($",\"wgpu_shader_modules\":{ms.WgpuShaderModules}");
            sb.Append('}');
        }
#endif

        sb.Append(",\"process\":{");
        using (var proc = System.Diagnostics.Process.GetCurrentProcess())
        {
            sb.Append($"\"working_set_bytes\":{proc.WorkingSet64}");
            sb.Append($",\"private_bytes\":{proc.PrivateMemorySize64}");
            sb.Append($",\"managed_total_bytes\":{GC.GetTotalMemory(false)}");

            // CPU% since the previous call to this tool. First call returns 0
            // (no baseline); the perf-smoke harness naturally makes two calls
            // which is exactly what's needed. Normalized to 0-100 across all
            // logical CPUs: 100 means every core fully pegged, 25 on a quad-
            // core means one core fully busy, etc.
            double cpuPercent = SampleProcessCpuPercent(proc);
            sb.Append($",\"cpu_percent\":{cpuPercent:F2}");
            sb.Append($",\"cpu_logical_count\":{Environment.ProcessorCount}");
        }
        sb.Append('}');

        // Frame-loop sentinels. Exposes every flag Tick() reads to decide
        // whether to stop the platform timer. If the loop is not idling when
        // it should be, exactly one of these is reporting true.
        var orchestrator = App.ActiveOrchestrator;
        if (orchestrator is not null)
        {
            var s = orchestrator.Sentinels;
            sb.Append(",\"sentinels\":{");
            sb.Append($"\"frames_in_flight\":{BoolStr(s.FramesInFlight)}");
            sb.Append($",\"would_hold_frame_loop\":{BoolStr(s.WouldHoldFrameLoop)}");
            sb.Append($",\"render_dirty_count\":{s.RenderDirtyCount}");
            sb.Append($",\"animations_active\":{BoolStr(s.AnimationsActive)}");
            sb.Append($",\"animations_active_count\":{s.AnimationsActiveCount}");
            sb.Append($",\"shared_animations_active\":{BoolStr(s.SharedAnimationsActive)}");
            sb.Append($",\"shared_animations_count\":{s.SharedAnimationsCount}");
            sb.Append($",\"caret_active\":{BoolStr(s.CaretActive)}");
            sb.Append($",\"spinners_active\":{BoolStr(s.SpinnersActive)}");
            sb.Append($",\"chart_animations_active\":{BoolStr(s.ChartAnimationsActive)}");
            sb.Append($",\"toasts_active\":{BoolStr(s.ToastsActive)}");
            sb.Append($",\"continuous_canvases_active\":{BoolStr(s.ContinuousCanvasesActive)}");
            sb.Append($",\"state_transitions_active\":{BoolStr(s.StateTransitionsActive)}");

#if CASCADE_DEVTOOLS
            // DevTools: per-callsite tally of SignalActiveTransition() calls in
            // the last completed frame. Positive keys are NodePainter.cs line
            // numbers; negative keys are synthetic markers:
            //   -1 = ControlStateAnimator.Reconcile (Hover/Press/Focus/Disabled spring)
            //   -2 = ControlStateAnimator.ReconcileOpen (popup open spring)
            //   -3 = ControlStateAnimator.ReconcileValue (toggle/checkbox value spring)
            // Empty object when idle. Use this to pinpoint runaway continuous
            // animations that keep the frame loop alive.
            var sigCounts = Cascade.UI.ControlStateAnimator.LastFrameSignalerCounts;
            sb.Append(",\"state_transition_signalers\":{");
            bool firstSig = true;
            foreach (var (line, count) in sigCounts)
            {
                if (!firstSig)
                {
                    sb.Append(',');
                }
                sb.Append('"').Append(line).Append("\":").Append(count);
                firstSig = false;
            }
            sb.Append('}');
#endif
            sb.Append('}');
        }

        // GC stats — cumulative since process start. Agents can diff two
        // cascade_diagnostics calls to compute per-interval GC activity.
        // total_pause_ms comes from GC.GetTotalPauseDuration() (.NET 7+);
        // total_allocated_bytes is precise across all threads (.NET 5+).
        sb.Append(",\"gc\":{");
        sb.Append($"\"gen0_count\":{GC.CollectionCount(0)}");
        sb.Append($",\"gen1_count\":{GC.CollectionCount(1)}");
        sb.Append($",\"gen2_count\":{GC.CollectionCount(2)}");
        sb.Append($",\"total_allocated_bytes\":{GC.GetTotalAllocatedBytes(precise: false)}");
        sb.Append($",\"total_pause_ms\":{GC.GetTotalPauseDuration().TotalMilliseconds:F3}");
        sb.Append($",\"latency_mode\":\"{System.Runtime.GCSettings.LatencyMode}\"");
        sb.Append($",\"is_server_gc\":{(System.Runtime.GCSettings.IsServerGC ? "true" : "false")}");
        sb.Append('}');

        if (includeRecent)
        {
            Span<Cascade.UI.Diagnostics.FrameStats> recent = stackalloc Cascade.UI.Diagnostics.FrameStats[Cascade.UI.Diagnostics.DiagnosticsHub.Capacity];
            int n = Cascade.UI.Diagnostics.DiagnosticsHub.GetRecent(recent, windowFrames);
            sb.Append(",\"recent\":[");
            for (int i = 0; i < n; i++)
            {
                ref readonly var s = ref recent[i];
                if (i > 0) { sb.Append(','); }
                sb.Append($"{{\"frame\":{s.FrameIndex}");
                sb.Append($",\"total_bytes\":{s.TotalBytes}");
                sb.Append($",\"layout_bytes\":{s.LayoutBytes}");
                sb.Append($",\"paint_bytes\":{s.PaintBytes}");
                sb.Append($",\"frame_ms\":{s.FrameMs:F3}");
                sb.Append($",\"layout_ms\":{s.LayoutMs:F3}");
                sb.Append($",\"paint_ms\":{s.PaintMs:F3}}}");
            }
            sb.Append(']');
        }

        var phases = Cascade.UI.Diagnostics.DiagnosticsHub.GetPhaseSnapshot();
        if (phases.Count > 0)
        {
            sb.Append(",\"phases\":[");
            for (int i = 0; i < phases.Count; i++)
            {
                if (i > 0) { sb.Append(','); }
                var p = phases[i];
                string escaped = p.Name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
                long mean = p.Count > 0 ? p.Bytes / p.Count : 0;
                sb.Append($"{{\"name\":\"{escaped}\"");
                sb.Append($",\"total_bytes\":{p.Bytes}");
                sb.Append($",\"count\":{p.Count}");
                sb.Append($",\"mean_bytes\":{mean}}}");
            }
            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string HandleCascadeHistory(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        int limit = GetInt(parameters, "limit", 50);
        limit = Math.Clamp(limit, 1, 500);
        string? componentFilter = GetString(parameters, "component");
        string? signalFilter = GetString(parameters, "signal");

        var sb = new StringBuilder();
        sb.Append('{');

        // Frame render history
        var samples = DevTools.PerformancePanel.GetFrameSamples(limit);
        sb.Append("\"frames\":[");
        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            var sample = samples[i];
            sb.Append($"{{\"timestamp_ms\":{sample.TimestampMs:F0}");
            sb.Append($",\"frame_ms\":{sample.FrameTimeMs:F2}");
            sb.Append($",\"layout_ms\":{sample.LayoutTimeMs:F2}");
            sb.Append($",\"render_ms\":{sample.RenderTimeMs:F2}");
            sb.Append($",\"gpu_ms\":{sample.GpuTimeMs:F2}");
            sb.Append($",\"dropped\":{BoolStr(sample.Dropped)}}}");
        }
        sb.Append(']');

        // Signal change history
        var changes = DevTools.PerformancePanel.GetSignalChanges(limit, componentFilter, signalFilter);
        sb.Append(",\"signal_changes\":[");
        for (int i = 0; i < changes.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            var evt = changes[i];
            sb.Append($"{{\"timestamp_ms\":{evt.TimestampMs}");
            sb.Append($",\"component\":\"{EscapeJson(evt.ComponentName)}\"");
            sb.Append($",\"signal\":\"{EscapeJson(evt.SignalName)}\"");
            sb.Append($",\"value\":\"{EscapeJson(evt.Value)}\"}}");
        }
        sb.Append(']');

        sb.Append($",\"total_frames\":{DevTools.PerformancePanel.TotalFrameSamples}");
        sb.Append($",\"total_signal_changes\":{DevTools.PerformancePanel.TotalSignalChanges}");
        sb.Append($",\"limit\":{limit}}}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeGetLayout(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        var nodeIds = parameters["node_ids"]?.AsArray();
        if (nodeIds is null || nodeIds.Count == 0)
        {
            return ErrorJson("node_ids is required", null);
        }

        var sb = new StringBuilder();
        sb.Append("{\"layouts\":[");

        int count = 0;
        foreach (var idNode in nodeIds)
        {
            string? nodeId = idNode?.ToString();
            if (nodeId is null)
            {
                continue;
            }

            var node = DevTools.NodeTreeWalker.FindNode(nodeId);
            if (node is null)
            {
                continue;
            }

            if (count > 0)
            {
                sb.Append(',');
            }

            var boxModel = DevTools.NodeTreeWalker.GetBoxModel(node);
            sb.Append($"{{\"id\":\"{EscapeJson(nodeId)}\"");
            sb.Append($",\"content\":{{\"x\":{boxModel.ContentBounds.X},\"y\":{boxModel.ContentBounds.Y},\"width\":{boxModel.ContentBounds.Width},\"height\":{boxModel.ContentBounds.Height}}}");
            sb.Append($",\"outer\":{{\"x\":{boxModel.OuterBounds.X},\"y\":{boxModel.OuterBounds.Y},\"width\":{boxModel.OuterBounds.Width},\"height\":{boxModel.OuterBounds.Height}}}");
            sb.Append('}');
            count++;
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeMeasure(JsonObject parameters)
    {
        string? idA = GetString(parameters, "node_id_a");
        string? idB = GetString(parameters, "node_id_b");
        if (idA is null || idB is null)
        {
            return ErrorJson("node_id_a and node_id_b are required", null);
        }

#if CASCADE_DEVTOOLS
        var boundsA = DevTools.NodeTreeWalker.GetNodeBoundsById(idA);
        var boundsB = DevTools.NodeTreeWalker.GetNodeBoundsById(idB);

        if (boundsA is null || boundsB is null)
        {
            return ErrorJson("One or both nodes not found", null);
        }

        var a = boundsA.Value;
        var b = boundsB.Value;

        float hGap = Math.Max(0, Math.Max(b.X - (a.X + a.Width), a.X - (b.X + b.Width)));
        float vGap = Math.Max(0, Math.Max(b.Y - (a.Y + a.Height), a.Y - (b.Y + b.Height)));

        var sb = new StringBuilder();
        sb.Append("{\"node_a\":{");
        sb.Append($"\"id\":\"{EscapeJson(idA)}\"");
        sb.Append($",\"bounds\":{{\"x\":{a.X},\"y\":{a.Y},\"width\":{a.Width},\"height\":{a.Height}}}}}");
        sb.Append(",\"node_b\":{");
        sb.Append($"\"id\":\"{EscapeJson(idB)}\"");
        sb.Append($",\"bounds\":{{\"x\":{b.X},\"y\":{b.Y},\"width\":{b.Width},\"height\":{b.Height}}}}}");
        sb.Append($",\"gap\":{{\"horizontal\":{hGap},\"vertical\":{vGap}}}");

        string relativePosition;
        if (b.X > a.X + a.Width)
        {
            relativePosition = "node_b is to the right of node_a";
        }
        else if (a.X > b.X + b.Width)
        {
            relativePosition = "node_b is to the left of node_a";
        }
        else if (b.Y > a.Y + a.Height)
        {
            relativePosition = "node_b is below node_a";
        }
        else if (a.Y > b.Y + b.Height)
        {
            relativePosition = "node_b is above node_a";
        }
        else
        {
            relativePosition = "nodes overlap";
        }

        sb.Append($",\"relative_position\":\"{relativePosition}\"");
        sb.Append('}');
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeThemeTokens(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? control = GetString(parameters, "control");
        var theme = ThemeSwitcher.Current;
        bool isDark = ThemeSwitcher.IsDarkMode;

        var sb = new StringBuilder();
        sb.Append($"{{\"theme_class\":\"{EscapeJson(theme.GetType().Name)}\"");
        sb.Append($",\"mode\":\"{(isDark ? "dark" : "light")}\"");

        // Global color tokens
        var colors = theme.Colors;
        sb.Append($",\"colors\":{{");
        sb.Append($"\"background\":\"{colors.Background}\"");
        sb.Append($",\"surface\":\"{colors.Surface}\"");
        sb.Append($",\"primary\":\"{colors.Primary}\"");
        sb.Append($",\"text\":\"{colors.Text}\"");
        sb.Append($",\"text_muted\":\"{colors.TextMuted}\"");
        sb.Append($",\"text_on_primary\":\"{colors.TextOnPrimary}\"");
        sb.Append($",\"border\":\"{colors.Border}\"");
        sb.Append($",\"danger\":\"{colors.Danger}\"");
        sb.Append($",\"warning\":\"{colors.Warning}\"");
        sb.Append($",\"success\":\"{colors.Success}\"");
        sb.Append($",\"focus\":\"{colors.Focus}\"");
        sb.Append('}');

        // Spacing tokens
        var spacing = theme.Spacing;
        sb.Append($",\"spacing\":{{\"base\":{spacing.Base},\"xs\":{spacing.Xs},\"sm\":{spacing.Sm},\"md\":{spacing.Md},\"lg\":{spacing.Lg},\"xl\":{spacing.Xl}}}");

        // Radius tokens
        var radius = theme.Radius;
        sb.Append($",\"radius\":{{\"none\":{radius.None},\"sm\":{radius.Sm},\"md\":{radius.Md},\"lg\":{radius.Lg},\"full\":{radius.Full}}}");

        if (control is not null)
        {
            sb.Append($",\"control\":\"{EscapeJson(control)}\"");
        }

        sb.Append('}');
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeValidateAccessibility(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? severity = GetString(parameters, "severity") ?? "all";

        var tree = DevTools.NodeTreeWalker.GetAccessibilityTree();
        var violations = new List<(string severity, string rule, string nodeId, string description, string fix)>();

        CollectAccessibilityViolations(tree, violations);

        var sb = new StringBuilder();
        int errors = 0;
        int warnings = 0;

        foreach (var (sev, _, _, _, _) in violations)
        {
            if (string.Equals(sev, "error", StringComparison.Ordinal))
            {
                errors++;
            }
            else
            {
                warnings++;
            }
        }

        sb.Append($"{{\"summary\":{{\"errors\":{errors},\"warnings\":{warnings}}}");
        sb.Append(",\"violations\":[");

        int count = 0;
        foreach (var (sev, rule, nodeId, description, fix) in violations)
        {
            if (!string.Equals(severity, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sev, severity, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (count > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"severity\":\"{EscapeJson(sev)}\"");
            sb.Append($",\"rule\":\"{EscapeJson(rule)}\"");
            sb.Append($",\"node_id\":\"{EscapeJson(nodeId)}\"");
            sb.Append($",\"description\":\"{EscapeJson(description)}\"");
            sb.Append($",\"fix\":\"{EscapeJson(fix)}\"}}");
            count++;
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeGetAnimationState(JsonObject parameters)
    {
        string? nodeId = GetString(parameters, "node_id");
        if (nodeId is null)
        {
            return ErrorJson("node_id is required", null);
        }

#if CASCADE_DEVTOOLS
        var orchestrator = App.ActiveOrchestrator;
        if (orchestrator is null)
        {
            return ErrorJson("No active orchestrator", null);
        }

        var scheduler = orchestrator.Animations;
        var snapshot = scheduler.GetSnapshot();
        bool animating = snapshot.Count > 0 && snapshot.Any(s => !s.IsComplete && !s.IsPaused);

        var sb = new StringBuilder();
        sb.Append($"{{\"node_id\":\"{EscapeJson(nodeId)}\"");
        sb.Append($",\"animating\":{BoolStr(animating)}");
        sb.Append($",\"active_count\":{scheduler.ActiveCount}");
        sb.Append(",\"animations\":[");

        bool first = true;
        foreach (var anim in snapshot)
        {
            if (anim.IsComplete)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }
            first = false;

            sb.Append($"{{\"id\":{anim.Id}");
            sb.Append($",\"paused\":{BoolStr(anim.IsPaused)}}}");
        }

        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeSimulateInteraction(JsonObject parameters)
    {
        string? nodeId = GetString(parameters, "node_id");
        string? interaction = GetString(parameters, "interaction");
        if (nodeId is null || interaction is null)
        {
            return ErrorJson("node_id and interaction are required", null);
        }

#if CASCADE_DEVTOOLS
        // Drag interaction needs coordinate parameters
        if (interaction == "drag")
        {
            return HandleDragInteraction(parameters, nodeId);
        }

        // Optional viewport coordinate overrides (for clicking overlay popups, etc.).
        // Coordinates default to the returned-screenshot pixel space so a point read
        // off a screenshot can be clicked directly; coord_space="logical" passes raw
        // logical (input) coordinates instead.
        bool logicalCoords =
            string.Equals(GetString(parameters, "coord_space"), "logical", StringComparison.OrdinalIgnoreCase);
        double coordScale = logicalCoords ? 1.0 : LiveScreenshotToLogicalScale();
        float? overrideX = null;
        float? overrideY = null;
        if (parameters["x"] is not null)
        {
            overrideX = (float)(GetDouble(parameters, "x", 0) * coordScale);
        }
        if (parameters["y"] is not null)
        {
            overrideY = (float)(GetDouble(parameters, "y", 0) * coordScale);
        }

        // SimulateInteraction dispatches input events which call RequestRepaint → SetTimer.
        // SetTimer requires the UI thread, so we must marshal the entire interaction there.
        // InvokeAsync blocks until completion and returns the result.
        bool success = false;
        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                success = DevTools.NodeTreeWalker.SimulateInteraction(nodeId, interaction, overrideX, overrideY);
            }).Wait();
        }
        catch (Exception ex)
        {
            return ErrorJson($"Interaction dispatch failed: {ex.Message}", nodeId);
        }

        var sb = new StringBuilder();
        sb.Append($"{{\"success\":{BoolStr(success)},\"interaction\":\"{EscapeJson(interaction)}\"");
        sb.Append($",\"node_id\":\"{EscapeJson(nodeId)}\"}}");
        return WithPresentation(sb.ToString(), baselineFrame, GetWaitFrames(parameters));
#else
        return DebugOnlyError();
#endif
    }

#if CASCADE_DEVTOOLS
    private static string HandleDragInteraction(JsonObject parameters, string nodeId)
    {
        string? startXStr = GetString(parameters, "start_x");
        string? startYStr = GetString(parameters, "start_y");
        string? endXStr = GetString(parameters, "end_x");
        string? endYStr = GetString(parameters, "end_y");
        string? deltaXStr = GetString(parameters, "delta_x");
        string? deltaYStr = GetString(parameters, "delta_y");
        int steps = GetInt(parameters, "steps", 5);

        if (steps < 1)
        {
            steps = 1;
        }

        (bool success, float sx, float sy, float endXResult, float endYResult) = (false, 0, 0, 0, 0);
        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Resolve start position: explicit coords or node center
                float? explicitStartX = startXStr is not null ? (float)GetDouble(parameters, "start_x", 0) : null;
                float? explicitStartY = startYStr is not null ? (float)GetDouble(parameters, "start_y", 0) : null;

                // Resolve end position: explicit coords, or start + delta
                float? explicitEndX = endXStr is not null ? (float)GetDouble(parameters, "end_x", 0) : null;
                float? explicitEndY = endYStr is not null ? (float)GetDouble(parameters, "end_y", 0) : null;
                float? deltaX = deltaXStr is not null ? (float)GetDouble(parameters, "delta_x", 0) : null;
                float? deltaY = deltaYStr is not null ? (float)GetDouble(parameters, "delta_y", 0) : null;

                (success, sx, sy, endXResult, endYResult) = DevTools.NodeTreeWalker.SimulateDrag(
                    nodeId, explicitStartX, explicitStartY,
                    explicitEndX, explicitEndY, deltaX, deltaY, steps);
            }).Wait();
        }
        catch (Exception exception)
        {
            return ErrorJson($"Drag failed: {exception.Message}", nodeId);
        }

        var sb = new StringBuilder();
        sb.Append($"{{\"success\":{BoolStr(success)},\"interaction\":\"drag\"");
        sb.Append($",\"node_id\":\"{EscapeJson(nodeId)}\"");
        sb.Append($",\"start\":{{\"x\":{sx:F1},\"y\":{sy:F1}}}");
        sb.Append($",\"end\":{{\"x\":{endXResult:F1},\"y\":{endYResult:F1}}}");
        sb.Append($",\"steps\":{steps}}}");
        return WithPresentation(sb.ToString(), baselineFrame, GetWaitFrames(parameters));
    }
#endif

    private static string HandleCascadeScroll(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? nodeId = GetString(parameters, "node_id");

        // scroll-to-node mode
        if (nodeId is not null)
        {
            (bool success, float offsetY, float maxY) = (false, 0, 0);
            long toNodeBaseline = Diagnostics.PresentMonitor.PresentedFrames;
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    (success, offsetY, maxY) = DevTools.NodeTreeWalker.ScrollToNode(nodeId);
                }).Wait();
            }
            catch (Exception ex)
            {
                return ErrorJson($"Scroll failed: {ex.Message}", nodeId);
            }

            return WithPresentation(
                $"{{\"success\":{BoolStr(success)},\"scroll_offset_y\":{offsetY:F1},\"max_scroll_y\":{maxY:F1},\"mode\":\"scroll_to_node\",\"node_id\":\"{EscapeJson(nodeId)}\"}}",
                toNodeBaseline, GetWaitFrames(parameters));
        }

        // delta-based scroll mode
        float deltaY = (float)GetDouble(parameters, "delta_y", 0);
        float deltaX = (float)GetDouble(parameters, "delta_x", 0);

        if (Math.Abs(deltaY) < 0.001 && Math.Abs(deltaX) < 0.001)
        {
            // No delta and no node_id — return current scroll state
            return $"{{\"success\":true,\"scroll_offset_y\":{InputDispatcher.ScrollViewOffsetY:F1},\"max_scroll_y\":{InputDispatcher.ScrollViewMaxY:F1},\"mode\":\"query\"}}";
        }

        // Default position: center of the client area (logical coordinate space).
        // An explicit x/y defaults to the returned-screenshot pixel space (so a
        // point read off a screenshot scrolls the right view); coord_space="logical"
        // passes raw logical coordinates. The default center is already logical.
        var clientBounds = App.Window.ClientBounds;
        bool scrollLogicalCoords =
            string.Equals(GetString(parameters, "coord_space"), "logical", StringComparison.OrdinalIgnoreCase);
        double scrollCoordScale = scrollLogicalCoords ? 1.0 : LiveScreenshotToLogicalScale();
        float x = parameters["x"] is not null
            ? (float)(GetDouble(parameters, "x", 0) * scrollCoordScale)
            : clientBounds.Width / 2;
        float y = parameters["y"] is not null
            ? (float)(GetDouble(parameters, "y", 0) * scrollCoordScale)
            : clientBounds.Height / 2;

        (bool scrollSuccess, float finalOffsetY, float finalMaxY) = (false, 0, 0);
        long deltaBaseline = Diagnostics.PresentMonitor.PresentedFrames;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                (scrollSuccess, finalOffsetY, finalMaxY) = DevTools.NodeTreeWalker.SimulateScroll(x, y, deltaX, deltaY);
            }).Wait();
        }
        catch (Exception ex)
        {
            return ErrorJson($"Scroll failed: {ex.Message}", null);
        }

        return WithPresentation(
            $"{{\"success\":{BoolStr(scrollSuccess)},\"scroll_offset_y\":{finalOffsetY:F1},\"max_scroll_y\":{finalMaxY:F1},\"mode\":\"delta\"}}",
            deltaBaseline, GetWaitFrames(parameters));
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeSendKeys(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string? text = GetString(parameters, "text");
        string? key = GetString(parameters, "key");
        var keysArray = parameters.ContainsKey("keys") ? parameters["keys"]?.AsArray() : null;

        int providedCount = (text is not null ? 1 : 0) + (key is not null ? 1 : 0) + (keysArray is not null ? 1 : 0);
        if (providedCount == 0)
        {
            return ErrorJson("Provide one of: text, key, or keys", null);
        }

        if (providedCount > 1)
        {
            return ErrorJson("Provide exactly one of: text, key, or keys", null);
        }

        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        int waitFrames = GetWaitFrames(parameters);

        // text mode — type a string
        if (text is not null)
        {
            int count = 0;
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    count = DevTools.NodeTreeWalker.SimulateTextInput(text);
                }).Wait();
            }
            catch (Exception ex)
            {
                return ErrorJson($"Text input failed: {ex.Message}", null);
            }

            return WithPresentation(
                BuildSendKeysResult(count > 0, $"Typed {count} characters"), baselineFrame, waitFrames);
        }

        // key mode — single key press
        if (key is not null)
        {
            return WithPresentation(DispatchSingleKey(key, parameters), baselineFrame, waitFrames);
        }

        // keys mode — sequence of actions
        if (keysArray is not null)
        {
            int totalActions = 0;
            bool allSuccess = true;

            foreach (var item in keysArray)
            {
                if (item is not JsonObject actionObj)
                {
                    continue;
                }

                string? actionText = GetString(actionObj, "text");
                string? actionKey = GetString(actionObj, "key");

                if (actionText is not null)
                {
                    int count = 0;
                    try
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            count = DevTools.NodeTreeWalker.SimulateTextInput(actionText);
                        }).Wait();
                    }
                    catch
                    {
                        allSuccess = false;
                    }
                    totalActions += count;
                }
                else if (actionKey is not null)
                {
                    string result = DispatchSingleKey(actionKey, actionObj);
                    if (result.Contains("\"success\":false", StringComparison.Ordinal))
                    {
                        allSuccess = false;
                    }
                    totalActions++;
                }
            }

            return WithPresentation(
                BuildSendKeysResult(allSuccess, $"Processed {totalActions} actions in sequence"), baselineFrame, waitFrames);
        }

        return ErrorJson("No valid input provided", null);
#else
        return DebugOnlyError();
#endif
    }

#if CASCADE_DEVTOOLS
    private static string DispatchSingleKey(string keyName, JsonObject parameters)
    {
        Key parsedKey = ParseKeyName(keyName);
        if (parsedKey == Key.None && !string.Equals(keyName, "None", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorJson($"Unknown key name: {keyName}. Valid keys: Enter, Tab, Escape, Backspace, Delete, Space, ArrowLeft, ArrowRight, ArrowUp, ArrowDown, Home, End, PageUp, PageDown, F1-F12, A-Z, 0-9", null);
        }

        ModifierKeys modifiers = ModifierKeys.None;
        if (parameters.ContainsKey("modifiers"))
        {
            var modsArray = parameters["modifiers"]?.AsArray();
            if (modsArray is not null)
            {
                foreach (var mod in modsArray)
                {
                    string? modStr = mod?.GetValue<string>();
                    if (modStr is not null)
                    {
                        modifiers |= modStr.ToUpperInvariant() switch
                        {
                            "CTRL" => ModifierKeys.Ctrl,
                            "SHIFT" => ModifierKeys.Shift,
                            "ALT" => ModifierKeys.Alt,
                            _ => ModifierKeys.None,
                        };
                    }
                }
            }
        }

        // For letter keys without modifiers, also send the character
        char? character = null;
        if (parsedKey >= Key.A && parsedKey <= Key.Z && modifiers == ModifierKeys.None)
        {
            character = (char)('a' + (parsedKey - Key.A));
        }
        else if (parsedKey >= Key.D0 && parsedKey <= Key.D9 && modifiers == ModifierKeys.None)
        {
            character = (char)('0' + (parsedKey - Key.D0));
        }
        else if (parsedKey == Key.Space)
        {
            character = ' ';
        }

        bool success = false;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                success = DevTools.NodeTreeWalker.SimulateKeyPress(parsedKey, modifiers, character);
            }).Wait();
        }
        catch (Exception ex)
        {
            return ErrorJson($"Key dispatch failed: {ex.Message}", null);
        }

        return BuildSendKeysResult(success, $"Sent key: {keyName}");
    }
#endif

#if CASCADE_DEVTOOLS
    private static string BuildSendKeysResult(bool success, string description)
    {
        var focused = FocusManager.FocusedElement;
        var sb = new StringBuilder();
        sb.Append($"{{\"success\":{BoolStr(success)},\"description\":\"{EscapeJson(description)}\"");
        if (focused is not null)
        {
            sb.Append($",\"focused_element\":{{\"type\":\"{focused.GetType().Name}\"}}");
        }
        else
        {
            sb.Append(",\"focused_element\":null");
        }
        sb.Append('}');
        return sb.ToString();
    }
#endif

#if CASCADE_DEVTOOLS
    private static Key ParseKeyName(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "ENTER" or "RETURN" => Key.Enter,
            "TAB" => Key.Tab,
            "ESCAPE" or "ESC" => Key.Escape,
            "BACKSPACE" => Key.Backspace,
            "DELETE" or "DEL" => Key.Delete,
            "SPACE" => Key.Space,
            "ARROWLEFT" or "LEFT" => Key.Left,
            "ARROWRIGHT" or "RIGHT" => Key.Right,
            "ARROWUP" or "UP" => Key.Up,
            "ARROWDOWN" or "DOWN" => Key.Down,
            "HOME" => Key.Home,
            "END" => Key.End,
            "PAGEUP" => Key.PageUp,
            "PAGEDOWN" => Key.PageDown,
            "INSERT" => Key.Insert,
            "F1" => Key.F1, "F2" => Key.F2, "F3" => Key.F3, "F4" => Key.F4,
            "F5" => Key.F5, "F6" => Key.F6, "F7" => Key.F7, "F8" => Key.F8,
            "F9" => Key.F9, "F10" => Key.F10, "F11" => Key.F11, "F12" => Key.F12,
            "A" => Key.A, "B" => Key.B, "C" => Key.C, "D" => Key.D,
            "E" => Key.E, "F" => Key.F, "G" => Key.G, "H" => Key.H,
            "I" => Key.I, "J" => Key.J, "K" => Key.K, "L" => Key.L,
            "M" => Key.M, "N" => Key.N, "O" => Key.O, "P" => Key.P,
            "Q" => Key.Q, "R" => Key.R, "S" => Key.S, "T" => Key.T,
            "U" => Key.U, "V" => Key.V, "W" => Key.W, "X" => Key.X,
            "Y" => Key.Y, "Z" => Key.Z,
            "0" => Key.D0, "1" => Key.D1, "2" => Key.D2, "3" => Key.D3,
            "4" => Key.D4, "5" => Key.D5, "6" => Key.D6, "7" => Key.D7,
            "8" => Key.D8, "9" => Key.D9,
            _ => Key.None,
        };
    }
#endif

    private static string HandleCascadeUndo(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        if (App.PerformUndo())
        {
            var desc = App.ActiveUndoStack?.UndoDescription ?? string.Empty;
            return WithPresentation(
                $"{{\"success\":true,\"description\":\"{EscapeJson(desc)}\"}}",
                baselineFrame, GetWaitFrames(parameters));
        }

        if (App.ActiveUndoStack is null)
        {
            return "{\"success\":false,\"error\":\"No undo stack registered. Call App.RegisterUndoStack() first.\"}";
        }

        return "{\"success\":false,\"error\":\"Nothing to undo\"}";
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeRedo(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
        if (App.PerformRedo())
        {
            var desc = App.ActiveUndoStack?.RedoDescription ?? string.Empty;
            return WithPresentation(
                $"{{\"success\":true,\"description\":\"{EscapeJson(desc)}\"}}",
                baselineFrame, GetWaitFrames(parameters));
        }

        if (App.ActiveUndoStack is null)
        {
            return "{\"success\":false,\"error\":\"No undo stack registered. Call App.RegisterUndoStack() first.\"}";
        }

        return "{\"success\":false,\"error\":\"Nothing to redo\"}";
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeScreenshot(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        nint hWnd = nint.Zero;

        if (OperatingSystem.IsWindows() && App.nativeWindow is not null)
        {
            hWnd = App.nativeWindow.Handle;
        }

        if (hWnd == nint.Zero)
        {
            return RawTextContent("No active window to capture");
        }

        // Optional frame synchronization: wait until the requested frame has
        // presented before capturing, so an agent can pair a mutation's
        // presented_frame with the screenshot that shows it. A timeout is
        // reported in the metadata block, and the capture proceeds anyway —
        // the captured_frame field states exactly what the image shows.
        int afterFrame = GetInt(parameters, "after_frame", 0);
        bool afterFrameTimedOut = false;
        if (afterFrame > 0)
        {
            (_, afterFrameTimedOut) = Diagnostics.PresentMonitor.WaitForFrame(afterFrame, PresentWait());
        }
        else
        {
            // No explicit target frame: wait for the render to settle so we never
            // capture the blank initial window or a mid-render-burst intermediate
            // frame (WP-3528 golden-capture flake). ~3 frame intervals of quiet.
            Diagnostics.PresentMonitor.WaitForSettle(TimeSpan.FromMilliseconds(50), PresentWait());
        }

        // CaptureCurrentFrame performs the capture on-demand (request → present →
        // read), so read LastCapturedFrame AFTER it, not before — otherwise the
        // metadata reports the previous capture's frame number.
        ImageData? image = CaptureCurrentFrame();
        long capturedFrame = Diagnostics.PresentMonitor.LastCapturedFrame;

        if (image is null)
        {
            return RawTextContent("Screenshot capture failed");
        }

        // The full device framebuffer dimensions (before any crop). A no-region
        // capture is returned downscaled to fit the vision-API "sweet spot", so
        // the returned image is smaller than this. We expose both in the metadata
        // and use the ratio to map region coordinates (see below).
        int deviceFullWidth = image.Width;
        int deviceFullHeight = image.Height;

        // The scale from device pixels to the pixels of a no-region screenshot.
        // Region coordinates default to that returned-screenshot pixel space so an
        // agent can read a feature off a full screenshot and crop it directly.
        (int returnedFullW, int returnedFullH) = ComputeTargetDimensions(deviceFullWidth, deviceFullHeight);
        double fullScale = deviceFullWidth > 0 ? (double)returnedFullW / deviceFullWidth : 1.0;
        if (fullScale <= 0)
        {
            fullScale = 1.0;
        }

        // Region coordinate space: "screenshot" (default — matches a no-region
        // capture's pixels) or "device" (raw framebuffer pixels, used by the
        // golden harness whose capture region must be machine-independent).
        bool deviceSpaceRegion =
            string.Equals(GetString(parameters, "region_space"), "device", StringComparison.OrdinalIgnoreCase);

        // Apply region crop if specified
        bool screenshotSpaceRegion = false;
        int requestedRegionWidth = 0;
        int requestedRegionHeight = 0;
        if (parameters.ContainsKey("region"))
        {
            var region = parameters["region"]?.AsObject();
            if (region is not null)
            {
                double rx = GetDouble(region, "x", 0);
                double ry = GetDouble(region, "y", 0);
                double rw = GetDouble(region, "width", image.Width);
                double rh = GetDouble(region, "height", image.Height);

                if (!deviceSpaceRegion)
                {
                    // Coordinates are in returned-screenshot pixels — map to the
                    // device framebuffer the crop operates on, and remember the
                    // requested size so the output matches what the agent asked for.
                    screenshotSpaceRegion = true;
                    requestedRegionWidth = (int)Math.Round(rw);
                    requestedRegionHeight = (int)Math.Round(rh);
                    rx /= fullScale;
                    ry /= fullScale;
                    rw /= fullScale;
                    rh /= fullScale;
                }

                var cropped = Win32Screenshot.CropRegion(
                    image, (int)Math.Round(rx), (int)Math.Round(ry),
                    (int)Math.Round(rw), (int)Math.Round(rh));
                if (cropped is null)
                {
                    return RawTextContent("Region is entirely outside window bounds");
                }
                image = cropped;
            }
        }

        // Compute target dimensions: fold user scale into API constraints
        // so we never allocate a massive intermediate image.
        double userScale = GetDouble(parameters, "scale", 1.0);
        userScale = Math.Clamp(userScale, 0.1, 10.0);

        // For a screenshot-space region the output is sized to the requested
        // (screenshot-pixel) rectangle × scale, so the returned image is exactly
        // the area the agent selected (a --scale > 1 is then a pixel-exact zoom).
        int workingWidth = screenshotSpaceRegion
            ? (int)(requestedRegionWidth * userScale)
            : (int)(image.Width * userScale);
        int workingHeight = screenshotSpaceRegion
            ? (int)(requestedRegionHeight * userScale)
            : (int)(image.Height * userScale);

        (int targetWidth, int targetHeight) = ComputeTargetDimensions(workingWidth, workingHeight);

        // Convert raw RGBA bytes to SharpImage frame (original capture size)
        using var sourceFrame = new SharpImage.Image.ImageFrame();
        sourceFrame.Initialize(image.Width, image.Height, SharpImage.Core.ColorspaceType.SRGB, hasAlpha: true);
        for (int y = 0; y < image.Height; y++)
        {
            var row = sourceFrame.GetPixelRowForWrite(y);
            for (int x = 0; x < image.Width; x++)
            {
                int srcOffset = y * image.Stride + x * 4;
                int dstOffset = x * 4;
                row[dstOffset] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset]);
                row[dstOffset + 1] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 1]);
                row[dstOffset + 2] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 2]);
                row[dstOffset + 3] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 3]);
            }
        }

        // Resize and encode, applying the hard 5MB ceiling
        (int finalWidth, int finalHeight, byte[] pngBytes) = ResizeAndEncodeForApi(sourceFrame, targetWidth, targetHeight);

        string base64 = Convert.ToBase64String(pngBytes);

        // Return proper MCP image content with metadata text block
        var sb = new StringBuilder(base64.Length + 256);
        sb.Append("{\"content\":[");
        sb.Append("{\"type\":\"image\",\"data\":\"");
        sb.Append(base64);
        sb.Append("\",\"mimeType\":\"image/png\"},");
        sb.Append("{\"type\":\"text\",\"text\":\"{\\\"width\\\":");
        sb.Append(finalWidth);
        sb.Append(",\\\"height\\\":");
        sb.Append(finalHeight);
        // The full physical framebuffer size. region coordinates default to the
        // returned-screenshot pixel space (a no-region capture is device × the
        // sweet-spot downscale); these let an agent reason about the mapping.
        sb.Append(",\\\"device_width\\\":");
        sb.Append(deviceFullWidth);
        sb.Append(",\\\"device_height\\\":");
        sb.Append(deviceFullHeight);
        sb.Append(",\\\"captured_frame\\\":");
        sb.Append(capturedFrame);
        if (afterFrame > 0)
        {
            sb.Append(",\\\"after_frame_timed_out\\\":");
            sb.Append(BoolStr(afterFrameTimedOut));
        }
        sb.Append("}\"}");
        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    /// <summary>
    /// The factor that converts a coordinate in the returned-screenshot pixel space
    /// to the app's logical (input/event) space: <c>logical = screenshot × factor</c>.
    /// A no-region screenshot is the device framebuffer downscaled to the returned
    /// size, and logical = device ÷ DPI, so the factor is
    /// <c>(deviceFull ÷ dpi) ÷ returnedFull</c>. Pure and uniform for X and Y
    /// (the downscale and DPI are both uniform). Returns 1.0 for degenerate input.
    /// </summary>
    internal static double ScreenshotToLogicalScale(int deviceFull, int returnedFull, float dpi)
    {
        if (deviceFull <= 0 || returnedFull <= 0 || dpi <= 0f)
        {
            return 1.0;
        }
        return deviceFull / (double)dpi / returnedFull;
    }

#if CASCADE_DEVTOOLS
    /// <summary>
    /// Resolves <see cref="ScreenshotToLogicalScale"/> against the live window:
    /// device framebuffer size, the returned (sweet-spot-downscaled) size, and the
    /// current DPI. Returns 1.0 when those are unavailable (treat coords as logical).
    /// </summary>
    private static double LiveScreenshotToLogicalScale()
    {
#if CASCADE_DEVTOOLS
        var device = App.activeBackendProvider?.DeviceSize ?? (Width: 0, Height: 0);
        float dpi = App.ActiveOrchestrator?.PixelRatio ?? 1f;
        (int returnedW, _) = ComputeTargetDimensions(device.Width, device.Height);
        return ScreenshotToLogicalScale(device.Width, returnedW, dpi);
#else
        return 1.0;
#endif
    }

    /// <summary>
    /// Computes target dimensions that satisfy vision API constraints.
    /// Accounts for user scale already being folded into workingWidth/workingHeight.
    /// </summary>
    private static (int width, int height) ComputeTargetDimensions(int workingWidth, int workingHeight)
    {
        const int maxDimension = 2000;
        const int sweetSpotMaxEdge = 1568;
        const double sweetSpotMaxPixels = 1_150_000.0;
        const int minDimension = 200;

        double scale = 1.0;
        double maxEdge = Math.Max(workingWidth, workingHeight);

        if (maxEdge > maxDimension)
        {
            scale = Math.Min(scale, maxDimension / maxEdge);
        }

        if (maxEdge > sweetSpotMaxEdge)
        {
            scale = Math.Min(scale, sweetSpotMaxEdge / maxEdge);
        }

        double totalPixels = (double)workingWidth * workingHeight;
        if (totalPixels > sweetSpotMaxPixels)
        {
            scale = Math.Min(scale, Math.Sqrt(sweetSpotMaxPixels / totalPixels));
        }

        int targetWidth = workingWidth;
        int targetHeight = workingHeight;

        if (scale < 1.0)
        {
            targetWidth = Math.Max(minDimension, (int)(workingWidth * scale));
            targetHeight = Math.Max(minDimension, (int)(workingHeight * scale));
        }

        return (targetWidth, targetHeight);
    }

    /// <summary>
    /// Resizes the source frame to the pre-computed target dimensions and encodes as PNG.
    /// Upscales use nearest-neighbor so zoomed-in captures preserve exact source
    /// pixels (each source pixel becomes an N×N block — required for pixel
    /// inspection); downscales use Lanczos3 for quality.
    /// If the resulting PNG exceeds 5MB, iteratively shrinks until it fits.
    /// </summary>
    private static (int width, int height, byte[] pngBytes) ResizeAndEncodeForApi(
        SharpImage.Image.ImageFrame source, int targetWidth, int targetHeight)
    {
        const long maxSizeBytes = 5L * 1024 * 1024;
        const int minDimension = 200;

        SharpImage.Image.ImageFrame? currentFrame = source;
        SharpImage.Image.ImageFrame? resizedFrame = null;

        if (targetWidth != (int)source.Columns || targetHeight != (int)source.Rows)
        {
            bool isUpscale = targetWidth >= (int)source.Columns && targetHeight >= (int)source.Rows;
            var method = isUpscale
                ? SharpImage.Transform.InterpolationMethod.NearestNeighbor
                : SharpImage.Transform.InterpolationMethod.Lanczos3;
            resizedFrame = SharpImage.Transform.Resize.Apply(source, targetWidth, targetHeight, method);
            currentFrame = resizedFrame;
        }

        try
        {
            using var pngStream = new System.IO.MemoryStream();
            SharpImage.Formats.PngCoder.Write(currentFrame, pngStream);
            byte[] pngBytes = pngStream.ToArray();

            // Hard 5MB limit: if still too large, iteratively shrink
            while (pngBytes.Length > maxSizeBytes && targetWidth > minDimension && targetHeight > minDimension)
            {
                if (resizedFrame is not null)
                {
                    resizedFrame.Dispose();
                }

                targetWidth = Math.Max(minDimension, (int)(targetWidth * 0.9));
                targetHeight = Math.Max(minDimension, (int)(targetHeight * 0.9));
                resizedFrame = SharpImage.Transform.Resize.Apply(
                    source, targetWidth, targetHeight, SharpImage.Transform.InterpolationMethod.Lanczos3);
                currentFrame = resizedFrame;

                pngStream.SetLength(0);
                SharpImage.Formats.PngCoder.Write(currentFrame, pngStream);
                pngBytes = pngStream.ToArray();
            }

            return (targetWidth, targetHeight, pngBytes);
        }
        finally
        {
            if (resizedFrame is not null && resizedFrame != source)
            {
                resizedFrame.Dispose();
            }
        }
    }
#endif

    private static string HandleCascadePixelSample(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        nint hWnd = nint.Zero;
        if (OperatingSystem.IsWindows() && App.nativeWindow is not null)
        {
            hWnd = App.nativeWindow.Handle;
        }

        if (hWnd == nint.Zero)
        {
            return ErrorJson("No active window to capture", null);
        }

        ImageData? image = CaptureCurrentFrame();

        if (image is null)
        {
            return ErrorJson("Screenshot capture failed", null);
        }

        string format = "hex";
        if (parameters.ContainsKey("format"))
        {
            format = parameters["format"]?.ToString() ?? "hex";
        }

        // Region sample
        if (parameters.ContainsKey("region"))
        {
            var region = parameters["region"]?.AsObject();
            if (region is not null)
            {
                int rx = GetInt(region, "x", 0);
                int ry = GetInt(region, "y", 0);
                int rw = GetInt(region, "width", 10);
                int rh = GetInt(region, "height", 10);

                // Clamp to image bounds
                int x1 = Math.Max(0, rx);
                int y1 = Math.Max(0, ry);
                int x2 = Math.Min(image.Width, rx + rw);
                int y2 = Math.Min(image.Height, ry + rh);

                if (x1 >= x2 || y1 >= y2)
                {
                    return ErrorJson("Region is outside window bounds", null);
                }

                if (format == "summary")
                {
                    int minR = 255, minG = 255, minB = 255;
                    int maxR = 0, maxG = 0, maxB = 0;
                    long sumR = 0, sumG = 0, sumB = 0;
                    int count = 0;

                    for (int py = y1; py < y2; py++)
                    {
                        for (int px = x1; px < x2; px++)
                        {
                            int offset = py * image.Stride + px * 4;
                            byte r = image.Pixels[offset];
                            byte g = image.Pixels[offset + 1];
                            byte b = image.Pixels[offset + 2];
                            minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                            minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
                            minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
                            sumR += r; sumG += g; sumB += b;
                            count++;
                        }
                    }

                    int avgR = (int)(sumR / count);
                    int avgG = (int)(sumG / count);
                    int avgB = (int)(sumB / count);

                    return $"{{\"region\":{{\"x\":{x1},\"y\":{y1},\"width\":{x2 - x1},\"height\":{y2 - y1}}}," +
                           $"\"pixel_count\":{count}," +
                           $"\"min\":{{\"r\":{minR},\"g\":{minG},\"b\":{minB},\"hex\":\"#{minR:X2}{minG:X2}{minB:X2}\"}}," +
                           $"\"max\":{{\"r\":{maxR},\"g\":{maxG},\"b\":{maxB},\"hex\":\"#{maxR:X2}{maxG:X2}{maxB:X2}\"}}," +
                           $"\"avg\":{{\"r\":{avgR},\"g\":{avgG},\"b\":{avgB},\"hex\":\"#{avgR:X2}{avgG:X2}{avgB:X2}\"}}}}";
                }

                // Non-summary: return individual pixels (capped at 100 to avoid huge responses)
                var sb = new StringBuilder();
                sb.Append($"{{\"region\":{{\"x\":{x1},\"y\":{y1},\"width\":{x2 - x1},\"height\":{y2 - y1}}},\"pixels\":[");
                int pixelCount = 0;
                int maxPixels = 100;
                bool first = true;

                for (int py = y1; py < y2 && pixelCount < maxPixels; py++)
                {
                    for (int px = x1; px < x2 && pixelCount < maxPixels; px++)
                    {
                        int offset = py * image.Stride + px * 4;
                        byte r = image.Pixels[offset];
                        byte g = image.Pixels[offset + 1];
                        byte b = image.Pixels[offset + 2];
                        byte a = image.Pixels[offset + 3];

                        if (!first)
                        {
                            sb.Append(',');
                        }
                        first = false;

                        if (format == "rgb")
                        {
                            sb.Append($"{{\"x\":{px},\"y\":{py},\"r\":{r},\"g\":{g},\"b\":{b},\"a\":{a}}}");
                        }
                        else
                        {
                            sb.Append($"{{\"x\":{px},\"y\":{py},\"hex\":\"#{r:X2}{g:X2}{b:X2}\",\"a\":{a}}}");
                        }
                        pixelCount++;
                    }
                }

                int totalPixels = (x2 - x1) * (y2 - y1);
                sb.Append($"],\"total_pixels\":{totalPixels}");
                if (pixelCount < totalPixels)
                {
                    sb.Append($",\"truncated\":true,\"shown\":{pixelCount}");
                }
                sb.Append('}');
                return sb.ToString();
            }
        }

        // Single point sample
        if (parameters.ContainsKey("x") && parameters.ContainsKey("y"))
        {
            int x = GetInt(parameters, "x", 0);
            int y = GetInt(parameters, "y", 0);
            var pixel = Win32Screenshot.SamplePixel(image, x, y);
            if (pixel is null)
            {
                return ErrorJson($"Point ({x}, {y}) is outside window bounds ({image.Width}x{image.Height})", null);
            }

            var (r, g, b, a) = pixel.Value;
            if (format == "rgb")
            {
                return $"{{\"x\":{x},\"y\":{y},\"r\":{r},\"g\":{g},\"b\":{b},\"a\":{a}}}";
            }
            return $"{{\"x\":{x},\"y\":{y},\"hex\":\"#{r:X2}{g:X2}{b:X2}\",\"r\":{r},\"g\":{g},\"b\":{b},\"a\":{a}}}";
        }

        return ErrorJson("Specify either x/y for point sample or region for area sample", null);
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeScreenshotDiff(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        string action = parameters["action"]?.ToString() ?? "";

        switch (action)
        {
            case "list":
            {
                var sb = new StringBuilder();
                sb.Append("{\"baselines\":[");
                bool first = true;
                foreach (var kvp in screenshotBaselines)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    sb.Append($"{{\"name\":\"{EscapeJson(kvp.Key)}\",\"width\":{kvp.Value.Width},\"height\":{kvp.Value.Height}}}");
                }
                sb.Append($"],\"count\":{screenshotBaselines.Count}}}");
                return sb.ToString();
            }

            case "clear":
            {
                string? clearName = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(clearName))
                {
                    int count = screenshotBaselines.Count;
                    screenshotBaselines.Clear();
                    return $"{{\"cleared\":\"all\",\"count\":{count}}}";
                }
                bool removed = screenshotBaselines.Remove(clearName);
                return $"{{\"cleared\":\"{EscapeJson(clearName)}\",\"found\":{BoolStr(removed)}}}";
            }

            case "capture_baseline":
            {
                string? name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    return ErrorJson("'name' is required for capture_baseline", null);
                }

                var image = CaptureCurrentFrame();
                if (image is null)
                {
                    return ErrorJson("Screenshot capture failed", null);
                }

                // CaptureCurrentFrame can return a view over the presenter's
                // live capture buffer, which is overwritten on every present —
                // a stored baseline must own its pixels or compare will always
                // report a match against the current frame.
                var pixelsCopy = new byte[image.Pixels.Length];
                Array.Copy(image.Pixels, pixelsCopy, image.Pixels.Length);
                var baseline = new ImageData
                {
                    Pixels = pixelsCopy,
                    Width = image.Width,
                    Height = image.Height,
                    Stride = image.Stride,
                };

                bool existed = screenshotBaselines.ContainsKey(name);
                screenshotBaselines[name] = baseline;
                return $"{{\"action\":\"captured\",\"name\":\"{EscapeJson(name)}\",\"width\":{image.Width},\"height\":{image.Height},\"replaced\":{BoolStr(existed)}}}";
            }

            case "compare":
            {
                string? name = parameters["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    return ErrorJson("'name' is required for compare", null);
                }

                if (!screenshotBaselines.TryGetValue(name, out var baseline))
                {
                    return ErrorJson($"No baseline named '{name}'. Use action='capture_baseline' first.", null);
                }

                var current = CaptureCurrentFrame();
                if (current is null)
                {
                    return ErrorJson("Screenshot capture failed", null);
                }

                int tolerance = GetInt(parameters, "tolerance", 2);
                tolerance = Math.Clamp(tolerance, 0, 255);

                var diff = Win32Screenshot.CompareImages(baseline, current, tolerance);

                var sb = new StringBuilder();
                sb.Append($"{{\"name\":\"{EscapeJson(name)}\",\"tolerance\":{tolerance}");

                if (diff.SizeMismatch)
                {
                    sb.Append($",\"match\":false,\"error\":\"size_mismatch\"");
                    sb.Append($",\"baseline_size\":{{\"width\":{diff.BaselineSize.Width},\"height\":{diff.BaselineSize.Height}}}");
                    sb.Append($",\"current_size\":{{\"width\":{diff.CurrentSize.Width},\"height\":{diff.CurrentSize.Height}}}");
                }
                else
                {
                    bool match = diff.DiffCount == 0;
                    double pct = diff.TotalPixels > 0 ? (double)diff.DiffCount / diff.TotalPixels * 100.0 : 0;
                    sb.Append($",\"match\":{BoolStr(match)}");
                    sb.Append($",\"diff_count\":{diff.DiffCount}");
                    sb.Append($",\"total_pixels\":{diff.TotalPixels}");
                    sb.Append($",\"diff_percent\":{pct:F4}");
                    sb.Append($",\"max_diff\":{diff.MaxDiff}");

                    if (diff.DiffBounds is not null)
                    {
                        var b = diff.DiffBounds.Value;
                        sb.Append($",\"diff_bounds\":{{\"x\":{b.X},\"y\":{b.Y},\"width\":{b.Width},\"height\":{b.Height}}}");
                    }

                    // Per-region rects (8-connected components, largest first)
                    // so an agent can zoom straight to each change with
                    // screenshot --region.
                    sb.Append($",\"diff_rect_count\":{diff.DiffRectCount}");
                    sb.Append(",\"diff_rects\":[");
                    for (int i = 0; i < diff.DiffRects.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(',');
                        }
                        var rect = diff.DiffRects[i];
                        sb.Append($"{{\"x\":{rect.X},\"y\":{rect.Y},\"width\":{rect.Width},\"height\":{rect.Height}}}");
                    }
                    sb.Append(']');
                }

                sb.Append('}');
                return sb.ToString();
            }

            default:
                return ErrorJson($"Unknown action '{action}'. Use: capture_baseline, compare, list, clear", null);
        }
#else
        return DebugOnlyError();
#endif
    }

    // ── Pixel provenance tools (WP-3505) ────────────────────────

#if CASCADE_DEVTOOLS
    /// <summary>
    /// How long capture-enable waits for the first <em>fully tagged</em> snapshot.
    /// Retained layers (e.g. a ScrollView's content) that presented before capture
    /// was enabled carry no provenance, so the first frame after enabling can come
    /// back with uncaptured layers; under load the tagged re-render takes several
    /// frames. This ceiling is generous because it is observability tooling, not a
    /// user-facing latency path — the wait returns the instant a complete frame is
    /// published, so the ceiling only bounds the pathological case (WP-3516).
    /// </summary>
    private static readonly TimeSpan DrawCaptureSettleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Returns the draw snapshot for the last presented frame, enabling capture on
    /// first use. Enabling requires a tagged frame to exist, so this dirties the
    /// retained layers, repaints, and waits until a snapshot is published with no
    /// uncaptured layers — re-invalidating on each idle interval until one lands.
    /// The wait is frame-deterministic: it keys off snapshot completeness
    /// (<see cref="DrawSnapshot.UncapturedLayers"/>), not a fixed delay, so it
    /// returns the moment a fully tagged frame is published and only falls back to
    /// the best partial snapshot if <see cref="DrawCaptureSettleTimeout"/> elapses
    /// under extreme load. After enabling, capture stays on and subsequent queries
    /// read the latest frame directly.
    /// </summary>
    private static DrawSnapshot? EnsureDrawSnapshot(out string? error)
    {
        var provider = App.activeBackendProvider;
        if (provider is null)
        {
            error = "No active render backend";
            return null;
        }

        if (DrawProvenance.CaptureEnabled)
        {
            var existing = provider.GetDrawSnapshot();
            if (existing is null)
            {
                error = "Draw capture is enabled but no frame has presented since — " +
                        "interact with the app once and retry, or check that the backend supports capture.";
                return null;
            }

            error = null;
            return existing;
        }

        DrawProvenance.CaptureEnabled = true;
        if (!TryRequestRepaint(out error))
        {
            return null;
        }

        var deadline = DateTime.UtcNow + DrawCaptureSettleTimeout;
        var nextReinvalidate = DateTime.UtcNow + TimeSpan.FromMilliseconds(300);
        DrawSnapshot? best = null;
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = provider.GetDrawSnapshot();
            if (snapshot is not null)
            {
                best = snapshot;
                if (snapshot.UncapturedLayers == 0)
                {
                    error = null;
                    return snapshot;
                }
            }

            // A retained layer presented before its tagged re-render landed (or the
            // repaint is still in flight under load). Dirty the layers again and
            // repaint until the published snapshot comes back complete.
            if (DateTime.UtcNow >= nextReinvalidate)
            {
                nextReinvalidate = DateTime.UtcNow + TimeSpan.FromMilliseconds(300);
                if (!TryRequestRepaint(out error))
                {
                    return null;
                }
            }

            Thread.Sleep(10);
        }

        // Settle window elapsed without a fully tagged frame. Return the best
        // snapshot seen rather than nothing — the tools surface its
        // uncaptured_layers so the caller knows the answer may be partial.
        if (best is not null)
        {
            error = null;
            return best;
        }

        error = "Draw capture was just enabled but no captured frame was published within " +
                $"{DrawCaptureSettleTimeout.TotalSeconds:F0} s. Interact with the app once and retry.";
        return null;
    }

    private static bool TryRequestRepaint(out string? error)
    {
        bool repaintRequested = false;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Retained ScrollView layers captured before enablement carry
                // no provenance and contribute no shape records — dirty them
                // so the repaint re-emits their content fully tagged.
                DevTools.NodeTreeWalker.InvalidateRetainedLayers();
                repaintRequested = DevTools.NodeTreeWalker.RequestRepaint();
            }).Wait();
        }
        catch (Exception ex)
        {
            error = $"Repaint request failed: {ex.Message}";
            return false;
        }

        if (!repaintRequested)
        {
            error = "Draw capture was just enabled but the repaint path is not wired " +
                    "(app still mounting?). Interact with the app once and retry.";
            return false;
        }

        error = null;
        return true;
    }

    private static void AppendShapeRecordJson(StringBuilder sb, in ShapeDrawRecord shape)
    {
        sb.Append($"{{\"pass\":\"{shape.Pass}\",\"kind\":\"{shape.Kind}\"");
        sb.Append($",\"bounds\":{{\"x\":{shape.MinX:F1},\"y\":{shape.MinY:F1},\"width\":{shape.MaxX - shape.MinX:F1},\"height\":{shape.MaxY - shape.MinY:F1}}}");
        if (shape.Fill is not null)
        {
            sb.Append($",\"fill\":\"{shape.Fill.Value.ToHex()}\"");
        }
        if (shape.Stroke is not null)
        {
            sb.Append($",\"stroke\":\"{shape.Stroke.Value.ToHex()}\"");
        }
        sb.Append(shape.NodeId is not null
            ? $",\"node_id\":\"{EscapeJson(shape.NodeId)}\""
            : ",\"node_id\":null");
        sb.Append($",\"op_index\":{shape.OpIndex}");
        if (shape.LayerHandle != 0)
        {
            sb.Append($",\"layer\":{shape.LayerHandle}");
        }
        sb.Append('}');
    }

    private static void AppendGlyphRecordJson(StringBuilder sb, in GlyphDrawRecord glyph)
    {
        sb.Append($"{{\"pass\":\"{DrawPassNames.Glyph}\",\"category\":\"{glyph.Category}\"");
        sb.Append($",\"glyph_id\":{glyph.GlyphId},\"font_size\":{glyph.FontSize:F1},\"font_handle\":{glyph.FontHandle}");
        sb.Append($",\"bounds\":{{\"x\":{glyph.X:F1},\"y\":{glyph.Y:F1},\"width\":{glyph.Width:F0},\"height\":{glyph.Height:F0}}}");
        sb.Append($",\"atlas\":{{\"u\":{glyph.AtlasU:F0},\"v\":{glyph.AtlasV:F0},\"width\":{glyph.AtlasW:F0},\"height\":{glyph.AtlasH:F0}}}");
        sb.Append($",\"color\":\"{glyph.Color.ToHex()}\"");
        if (glyph.HasClip)
        {
            sb.Append($",\"clip\":{{\"x\":{glyph.ClipMinX:F1},\"y\":{glyph.ClipMinY:F1},\"width\":{glyph.ClipMaxX - glyph.ClipMinX:F1},\"height\":{glyph.ClipMaxY - glyph.ClipMinY:F1}}}");
        }
        if (glyph.IsColorGlyph)
        {
            sb.Append(",\"is_color_glyph\":true");
        }
        sb.Append(glyph.NodeId is not null
            ? $",\"node_id\":\"{EscapeJson(glyph.NodeId)}\""
            : ",\"node_id\":null");
        sb.Append('}');
    }

    private static bool GlyphTouchesPixel(in GlyphDrawRecord glyph, float x, float y)
    {
        if (x < glyph.X || x >= glyph.X + glyph.Width || y < glyph.Y || y >= glyph.Y + glyph.Height)
        {
            return false;
        }

        // The glyph shader discards fragments outside the clip rect — a quad
        // covering the pixel did not draw it if the clip excludes it.
        if (glyph.HasClip &&
            (x < glyph.ClipMinX || x >= glyph.ClipMaxX || y < glyph.ClipMinY || y >= glyph.ClipMaxY))
        {
            return false;
        }

        return true;
    }
#endif

    private static string HandleCascadeWhodrew(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        if (parameters["x"] is null || parameters["y"] is null)
        {
            return ErrorJson("x and y are required", null);
        }

        float x = (float)GetDouble(parameters, "x", 0);
        float y = (float)GetDouble(parameters, "y", 0);

        var snapshot = EnsureDrawSnapshot(out string? error);
        if (snapshot is null)
        {
            return ErrorJson(error ?? "No draw snapshot available", null);
        }

        var sb = new StringBuilder();
        sb.Append($"{{\"x\":{x:F1},\"y\":{y:F1},\"frame\":{snapshot.Frame},\"draws\":[");

        // Emit in GPU execution order: geometry pass, then image pass, then
        // glyph pass — later entries paint over earlier ones.
        int count = 0;
        foreach (var shape in snapshot.Shapes)
        {
            if (!string.Equals(shape.Pass, DrawPassNames.Geometry, StringComparison.Ordinal))
            {
                continue;
            }
            if (x < shape.MinX || x >= shape.MaxX || y < shape.MinY || y >= shape.MaxY)
            {
                continue;
            }
            if (count > 0)
            {
                sb.Append(',');
            }
            AppendShapeRecordJson(sb, in shape);
            count++;
        }

        foreach (var shape in snapshot.Shapes)
        {
            if (!string.Equals(shape.Pass, DrawPassNames.Image, StringComparison.Ordinal))
            {
                continue;
            }
            if (x < shape.MinX || x >= shape.MaxX || y < shape.MinY || y >= shape.MaxY)
            {
                continue;
            }
            if (count > 0)
            {
                sb.Append(',');
            }
            AppendShapeRecordJson(sb, in shape);
            count++;
        }

        foreach (var glyph in snapshot.Glyphs)
        {
            if (!GlyphTouchesPixel(in glyph, x, y))
            {
                continue;
            }
            if (count > 0)
            {
                sb.Append(',');
            }
            AppendGlyphRecordJson(sb, in glyph);
            count++;
        }

        sb.Append($"],\"draw_count\":{count}");
        if (snapshot.UncapturedLayers > 0)
        {
            sb.Append($",\"uncaptured_layers\":{snapshot.UncapturedLayers}");
            sb.Append(",\"warning\":\"Some retained layers were captured before draw capture was enabled — their shape draws are missing from this answer until the layer re-renders.\"");
        }
        sb.Append('}');
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeGlyphInstances(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        float rectX = (float)GetDouble(parameters, "x", 0);
        float rectY = (float)GetDouble(parameters, "y", 0);
        float rectW = (float)GetDouble(parameters, "width", float.MaxValue);
        float rectH = (float)GetDouble(parameters, "height", float.MaxValue);
        int limit = Math.Clamp(GetInt(parameters, "limit", 200), 1, 2000);
        float rectMaxX = rectW >= float.MaxValue ? float.MaxValue : rectX + rectW;
        float rectMaxY = rectH >= float.MaxValue ? float.MaxValue : rectY + rectH;

        var snapshot = EnsureDrawSnapshot(out string? error);
        if (snapshot is null)
        {
            return ErrorJson(error ?? "No draw snapshot available", null);
        }

        var sb = new StringBuilder();
        sb.Append($"{{\"frame\":{snapshot.Frame},\"atlas_dimension\":{snapshot.AtlasDimension}");
        if (snapshot.UncapturedLayers > 0)
        {
            sb.Append($",\"uncaptured_layers\":{snapshot.UncapturedLayers}");
        }
        sb.Append(",\"instances\":[");

        int totalMatches = 0;
        int returned = 0;
        foreach (var glyph in snapshot.Glyphs)
        {
            if (glyph.X + glyph.Width < rectX || glyph.X > rectMaxX ||
                glyph.Y + glyph.Height < rectY || glyph.Y > rectMaxY)
            {
                continue;
            }

            totalMatches++;
            if (returned >= limit)
            {
                continue;
            }
            if (returned > 0)
            {
                sb.Append(',');
            }
            AppendGlyphRecordJson(sb, in glyph);
            returned++;
        }

        sb.Append($"],\"total_matches\":{totalMatches}}}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeAtlas(JsonObject parameters)
    {
#if CASCADE_DEVTOOLS
        var provider = App.activeBackendProvider;
        if (provider is null)
        {
            return RawTextContent("No active render backend");
        }

        int u = GetInt(parameters, "u", 0);
        int v = GetInt(parameters, "v", 0);
        int width = GetInt(parameters, "width", 0);
        int height = GetInt(parameters, "height", 0);

        // The readback submits GPU queue work and maps a staging buffer —
        // marshal to the UI thread like every other GPU-touching tool.
        AtlasRegionCapture? capture = null;
        try
        {
            Dispatcher.InvokeAsync(() =>
            {
                capture = provider.CaptureAtlasRegion(u, v, width, height);
            }).Wait();
        }
        catch (Exception ex)
        {
            return RawTextContent($"Atlas capture failed: {ex.Message}");
        }

        if (capture is null)
        {
            return RawTextContent(
                "Atlas capture failed: no GPU presenter (the CPU fallback has no glyph atlas) or the GPU readback timed out.");
        }

        var image = capture.Image;
        using var sourceFrame = new SharpImage.Image.ImageFrame();
        sourceFrame.Initialize(image.Width, image.Height, SharpImage.Core.ColorspaceType.SRGB, hasAlpha: true);
        for (int row = 0; row < image.Height; row++)
        {
            var pixelRow = sourceFrame.GetPixelRowForWrite(row);
            for (int col = 0; col < image.Width; col++)
            {
                int srcOffset = row * image.Stride + col * 4;
                int dstOffset = col * 4;
                pixelRow[dstOffset] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset]);
                pixelRow[dstOffset + 1] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 1]);
                pixelRow[dstOffset + 2] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 2]);
                pixelRow[dstOffset + 3] = SharpImage.Core.Quantum.ScaleFromByte(image.Pixels[srcOffset + 3]);
            }
        }

        using var pngStream = new System.IO.MemoryStream();
        SharpImage.Formats.PngCoder.Write(sourceFrame, pngStream);
        string base64 = Convert.ToBase64String(pngStream.ToArray());

        var sb = new StringBuilder(base64.Length + 256);
        sb.Append("{\"content\":[");
        sb.Append("{\"type\":\"image\",\"data\":\"");
        sb.Append(base64);
        sb.Append("\",\"mimeType\":\"image/png\"},");
        sb.Append("{\"type\":\"text\",\"text\":\"{\\\"atlas_dimension\\\":");
        sb.Append(capture.AtlasDimension);
        sb.Append(",\\\"u\\\":");
        sb.Append(capture.U);
        sb.Append(",\\\"v\\\":");
        sb.Append(capture.V);
        sb.Append(",\\\"width\\\":");
        sb.Append(capture.Width);
        sb.Append(",\\\"height\\\":");
        sb.Append(capture.Height);
        sb.Append("}\"}");
        sb.Append("]}");
        return sb.ToString();
#else
        return DebugOnlyError();
#endif
    }

    private static string HandleCascadeSetRenderParam(JsonObject parameters)
    {
        string? param = GetString(parameters, "param");
        if (string.IsNullOrEmpty(param))
        {
            return ErrorJson("'param' is required", null);
        }

        var valueNode = parameters["value"];
        if (valueNode is null)
        {
            return ErrorJson("'value' is required", null);
        }

        string valueStr = valueNode.ToString();

        switch (param)
        {
            case "render_mode":
            {
                string normalized = valueStr.ToUpperInvariant();
                if (normalized != "GPU" && normalized != "CPU")
                {
                    return ErrorJson("Invalid render_mode value. Use: gpu, cpu", null);
                }
                long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
                App.activeBackendProvider?.SetRenderParam("render_mode", normalized);
                return WithPresentation(
                    $"{{\"param\":\"render_mode\",\"value\":\"{normalized}\"}}",
                    baselineFrame, GetWaitFrames(parameters));
            }

            case "dpi":
            {
                // Simulate a live OS DPI change (96 = 100%, 120 = 125%, 144 = 150%,
                // 192 = 200%) to exercise the per-monitor-v2 window path end to end:
                // window reposition/resize → swapchain rescale → PixelRatio. A real
                // WM_DPICHANGED can't be forged cross-process (its lParam is a RECT
                // pointer in the app's address space), so the window drives it
                // in-process. Frame-synchronous like the other mutating verbs.
                if (!uint.TryParse(valueStr, out uint newDpi) || newDpi < 48 || newDpi > 960)
                {
                    return ErrorJson("Invalid dpi value. Use an integer 48-960 (96 = 100%).", null);
                }

                var window = App.nativeWindow;
                if (window is null)
                {
                    return ErrorJson("DPI simulation requires the Win32 window; no active window.", null);
                }

                long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
                uint previousDpi = window.Dpi;
                Dispatcher.InvokeAsync(() => { previousDpi = window.SimulateDpiChange(newDpi); }).Wait();

                string scale = (newDpi / 96.0)
                    .ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                return WithPresentation(
                    $"{{\"param\":\"dpi\",\"value\":{newDpi},\"previous_dpi\":{previousDpi},\"scale\":{scale}}}",
                    baselineFrame, GetWaitFrames(parameters));
            }

            case "textgamma":
            {
                // WP-3519 prototype: glyph text-weight gamma. 0 = legacy
                // pure-linear blend; 1.5 = macOS-ish perceptual weight. Lets us
                // A/B the small-text weight live before settling on a default.
                if (!float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gamma) || gamma < 0f || gamma > 4f)
                {
                    return ErrorJson("Invalid textgamma value. Use a float 0-4 (0 = linear, 1.5 = macOS-ish).", null);
                }
                long baselineFrame = Diagnostics.PresentMonitor.PresentedFrames;
                App.activeBackendProvider?.SetRenderParam("textgamma", valueStr);
                // The gamma is a per-present uniform; an idle app never repaints,
                // so force one so the new weight reaches the screen / the next
                // WithPresentation wait observes a fresh frame. NodeTreeWalker is
                // DevTools-only; in a non-DevTools build this handler is unreachable.
#if CASCADE_DEVTOOLS
                Dispatcher.InvokeAsync(() => DevTools.NodeTreeWalker.RequestRepaint()).Wait();
#endif
                string g = gamma.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                return WithPresentation(
                    $"{{\"param\":\"textgamma\",\"value\":{g}}}",
                    baselineFrame, GetWaitFrames(parameters));
            }

            default:
                return ErrorJson($"Unknown param '{param}'. Use: render_mode, dpi, textgamma", null);
        }
    }

    private static ImageData? CaptureCurrentFrame()
    {
        // Try GPU readback first (reads directly from render target — no DWM issues).
        // Capture is on-demand: the full-framebuffer readback stalls the present
        // pipeline, so it only runs when requested (otherwise the app is capped to
        // ~11 fps whenever it presents continuously). Request one, force a present so
        // the readback actually happens, wait for it to land, then read the buffer.
        var provider = App.activeBackendProvider;
        if (provider is not null)
        {
            long baseline = Diagnostics.PresentMonitor.LastCapturedFrame;
            provider.RequestCapture();
            // TryRequestRepaint marshals a repaint to the UI thread so the requested
            // capture actually presents. It routes through NodeTreeWalker, which is
            // DevTools-only; every caller of CaptureCurrentFrame is likewise gated on
            // CASCADE_DEVTOOLS, so this method is unreachable in a non-DevTools build.
#if CASCADE_DEVTOOLS
            TryRequestRepaint(out _); // ignore wiring errors
#endif
            Diagnostics.PresentMonitor.WaitForCapture(baseline, PresentWait());

            var gpuImage = provider.CaptureFrame();
            if (gpuImage is not null)
            {
                return gpuImage;
            }
        }

        // Fall back to Win32 PrintWindow/BitBlt
        nint hWnd = nint.Zero;
        if (OperatingSystem.IsWindows() && App.nativeWindow is not null)
        {
            hWnd = App.nativeWindow.Handle;
        }

        if (hWnd == nint.Zero)
        {
            return null;
        }

        if (OperatingSystem.IsWindows())
        {
            return Win32Screenshot.Capture(hWnd);
        }

        return null;
    }

    private static string HandleCascadeAccessibilityTree(JsonObject parameters)
    {
        int depth = GetInt(parameters, "depth", 5);
        depth = Math.Clamp(depth, 1, 10);

#if CASCADE_DEVTOOLS
        var tree = DevTools.NodeTreeWalker.GetAccessibilityTree();
        return SerializeAccessibilityNode(tree, depth, 0);
#else
        return "{\"root\":{\"id\":\"acc-root\",\"role\":\"Main\"},\"build_mode\":\"release\"}";
#endif
    }

    private static string HandleCascadeFindAccessible(JsonObject parameters)
    {
        string? role = GetString(parameters, "role");
        string? label = GetString(parameters, "label");
        string? labelContains = GetString(parameters, "label_contains");
        int limit = GetInt(parameters, "limit", 20);

#if CASCADE_DEVTOOLS
        var tree = DevTools.NodeTreeWalker.GetAccessibilityTree();
        var matches = new List<DevTools.AccessibleNode>();
        CollectMatchingAccessibleNodes(tree, role, label, labelContains, matches, limit);

        var sb = new StringBuilder();
        sb.Append("{\"nodes\":[");

        for (int i = 0; i < matches.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var node = matches[i];
            sb.Append($"{{\"id\":\"{EscapeJson(node.NodeId)}\"");
            sb.Append($",\"role\":\"{node.Role}\"");
            if (node.Label is not null)
            {
                sb.Append($",\"label\":\"{EscapeJson(node.Label)}\"");
            }
            sb.Append('}');
        }

        sb.Append($"],\"total_matches\":{matches.Count}}}");
        return sb.ToString();
#else
        var sb = new StringBuilder();
        sb.Append("{\"nodes\":[],\"total_matches\":0}");
        return sb.ToString();
#endif
    }

    private static string HandleCascadeWindowInfo(JsonObject parameters)
    {
        var sb = new StringBuilder();
        var bounds = App.Window.Bounds;
        bool isMaximized = App.Window.IsMaximized;
        bool canUndo = App.ActiveUndoStack?.CanUndo ?? false;
        bool canRedo = App.ActiveUndoStack?.CanRedo ?? false;

        string title = "";
        if (App.nativeWindow is not null)
        {
            title = App.nativeWindow.GetTitle();
        }
        else if (App.nativeCocoaWindow is not null)
        {
            title = App.nativeCocoaWindow.GetTitle();
        }
        else if (App.nativeLinuxWindow is not null)
        {
            title = App.nativeLinuxWindow.GetTitle();
        }
        else if (App.nativeLinuxWaylandWindow is not null)
        {
            title = App.nativeLinuxWaylandWindow.GetTitle();
        }

        sb.Append($"{{\"title\":\"{EscapeJson(title)}\"");
        sb.Append($",\"width\":{bounds.Width},\"height\":{bounds.Height}");
        sb.Append($",\"x\":{bounds.X},\"y\":{bounds.Y}");
        sb.Append($",\"focused\":true,\"maximized\":{BoolStr(isMaximized)}");
        sb.Append($",\"can_undo\":{BoolStr(canUndo)},\"can_redo\":{BoolStr(canRedo)}");
#if CASCADE_DEVTOOLS
        sb.Append(",\"build_mode\":\"debug\"");
#else
        sb.Append(",\"build_mode\":\"release\"");
#endif
        sb.Append('}');
        return sb.ToString();
    }

    private static string HandleCascadeListWindows(JsonObject parameters)
    {
        var sb = new StringBuilder();
        sb.Append("{\"windows\":[");

        bool hasWindow = App.nativeWindow is not null
            || App.nativeCocoaWindow is not null
            || App.nativeLinuxWindow is not null
            || App.nativeLinuxWaylandWindow is not null;

        if (hasWindow)
        {
            var bounds = App.Window.Bounds;
            string title = "";
            string platform = "unknown";

            if (App.nativeWindow is not null)
            {
                title = App.nativeWindow.GetTitle();
                platform = "windows";
            }
            else if (App.nativeCocoaWindow is not null)
            {
                title = App.nativeCocoaWindow.GetTitle();
                platform = "macos";
            }
            else if (App.nativeLinuxWindow is not null)
            {
                title = App.nativeLinuxWindow.GetTitle();
                platform = "linux-x11";
            }
            else if (App.nativeLinuxWaylandWindow is not null)
            {
                title = App.nativeLinuxWaylandWindow.GetTitle();
                platform = "linux-wayland";
            }

            sb.Append($"{{\"id\":\"main\",\"title\":\"{EscapeJson(title)}\"");
            sb.Append($",\"platform\":\"{platform}\"");
            sb.Append($",\"width\":{bounds.Width},\"height\":{bounds.Height}");
            sb.Append($",\"pid\":{Environment.ProcessId}}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string HandleCascadeApiIndex(JsonObject parameters)
    {
        string? section = GetString(parameters, "section");
        return McpResources.ReadApiIndexSection(section);
    }

    // ── Serialization helpers ───────────────────────────────────

#if CASCADE_DEVTOOLS
    private static string SerializeTreeSnapshot(DevTools.NodeSnapshot snapshot, int maxDepth)
    {
        var sb = new StringBuilder();
        sb.Append("{\"root\":");
        SerializeNodeSnapshot(sb, snapshot, maxDepth, 0);
        sb.Append('}');
        return sb.ToString();
    }

    private static void SerializeNodeSnapshot(StringBuilder sb, DevTools.NodeSnapshot node, int maxDepth, int currentDepth)
    {
        sb.Append($"{{\"id\":\"{EscapeJson(node.Id)}\"");
        sb.Append($",\"type\":\"{EscapeJson(node.TypeName)}\"");

        if (node.SourceFile is not null)
        {
            sb.Append($",\"source\":{{\"file\":\"{EscapeJson(node.SourceFile)}\",\"line\":{node.SourceLine ?? 0}}}");
        }

        sb.Append($",\"bounds\":{{\"x\":{node.Bounds.X},\"y\":{node.Bounds.Y},\"width\":{node.Bounds.Width},\"height\":{node.Bounds.Height}}}");
        sb.Append($",\"children_count\":{node.Children.Count}");

        if (currentDepth < maxDepth && node.Children.Count > 0)
        {
            sb.Append(",\"children\":[");
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                SerializeNodeSnapshot(sb, node.Children[i], maxDepth, currentDepth + 1);
            }
            sb.Append(']');
        }

        sb.Append('}');
    }

    private static string SerializeNodeDetail(DevTools.NodeDetail detail)
    {
        var sb = new StringBuilder();
        sb.Append($"{{\"id\":\"{EscapeJson(detail.Id)}\"");
        sb.Append($",\"type\":\"{EscapeJson(detail.TypeName)}\"");
        if (detail.SourceLocation is not null)
        {
            sb.Append($",\"source_location\":\"{EscapeJson(detail.SourceLocation)}\"");
        }
        sb.Append($",\"bounds\":{{\"x\":{detail.Bounds.X},\"y\":{detail.Bounds.Y},\"width\":{detail.Bounds.Width},\"height\":{detail.Bounds.Height}}}");
        sb.Append($",\"render_count\":{detail.RenderCount}");
        sb.Append('}');
        return sb.ToString();
    }

    private static string SerializeAccessibilityNode(DevTools.AccessibleNode node, int maxDepth, int currentDepth)
    {
        var sb = new StringBuilder();
        sb.Append("{\"root\":");
        SerializeAccessibleNodeRecursive(sb, node, maxDepth, currentDepth);
        sb.Append(",\"build_mode\":\"debug\"}");
        return sb.ToString();
    }

    private static void SerializeAccessibleNodeRecursive(StringBuilder sb, DevTools.AccessibleNode node, int maxDepth, int currentDepth)
    {
        sb.Append($"{{\"id\":\"{EscapeJson(node.NodeId)}\"");
        sb.Append($",\"role\":\"{node.Role}\"");
        if (node.Label is not null)
        {
            sb.Append($",\"label\":\"{EscapeJson(node.Label)}\"");
        }

        if (currentDepth < maxDepth && node.Children.Count > 0)
        {
            sb.Append(",\"children\":[");
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                SerializeAccessibleNodeRecursive(sb, node.Children[i], maxDepth, currentDepth + 1);
            }
            sb.Append(']');
        }

        sb.Append('}');
    }

    private static void CollectAccessibilityViolations(
        DevTools.AccessibleNode node,
        List<(string severity, string rule, string nodeId, string description, string fix)> violations)
    {
        // Check for missing labels on interactive elements
        if (node.Role is AccessibleRole.Button or AccessibleRole.TextBox or AccessibleRole.Checkbox &&
            string.IsNullOrEmpty(node.Label))
        {
            violations.Add(("error", "interactive-label", node.NodeId,
                $"{node.Role} has no accessible label",
                $"Add .AccessibleLabel() to this {node.Role}."));
        }

        foreach (var child in node.Children)
        {
            CollectAccessibilityViolations(child, violations);
        }
    }

    private static void CollectMatchingAccessibleNodes(
        DevTools.AccessibleNode node,
        string? role, string? label, string? labelContains,
        List<DevTools.AccessibleNode> matches, int limit)
    {
        if (matches.Count >= limit)
        {
            return;
        }

        bool roleMatch = role is null || string.Equals(node.Role.ToString(), role, StringComparison.OrdinalIgnoreCase);
        bool labelMatch = label is null || string.Equals(node.Label, label, StringComparison.Ordinal);
        bool containsMatch = labelContains is null || (node.Label is not null &&
            node.Label.Contains(labelContains, StringComparison.OrdinalIgnoreCase));

        if (roleMatch && labelMatch && containsMatch)
        {
            matches.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectMatchingAccessibleNodes(child, role, label, labelContains, matches, limit);
        }
    }
#endif

    // ── Utility methods ─────────────────────────────────────────

    private static string? GetString(JsonObject? obj, string key)
    {
        if (obj is null)
        {
            return null;
        }
        return obj[key]?.ToString();
    }

    private static bool? GetBoolOrNull(JsonObject? obj, string key)
    {
        var node = obj?[key];
        if (node is null)
        {
            return null;
        }
        return bool.TryParse(node.ToString(), out bool result) ? result : null;
    }

    private static int GetInt(JsonObject? obj, string key, int defaultValue)
    {
        if (obj is null)
        {
            return defaultValue;
        }
        var node = obj[key];
        if (node is null)
        {
            return defaultValue;
        }
        if (int.TryParse(node.ToString(), out int result))
        {
            return result;
        }
        return defaultValue;
    }

    private static double GetDouble(JsonObject? obj, string key, double defaultValue)
    {
        if (obj is null)
        {
            return defaultValue;
        }
        var node = obj[key];
        if (node is null)
        {
            return defaultValue;
        }
        if (double.TryParse(node.ToString(), System.Globalization.CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }
        return defaultValue;
    }

    private static string BoolStr(bool value) => value ? "true" : "false";

    // ── CPU% sampling state ─────────────────────────────────────
    // We measure CPU as the delta of Process.TotalProcessorTime over wall
    // time between successive calls, normalized to 0-100 across all logical
    // CPUs. Cross-platform, AOT-safe, no package dependency. The first call
    // returns 0 because no baseline exists yet.
    private static readonly object cpuSampleLock = new();
    private static long lastCpuSampleTimestamp;
    private static TimeSpan lastCpuSampleTotal;

    private static double SampleProcessCpuPercent(System.Diagnostics.Process proc)
    {
        lock (cpuSampleLock)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            TimeSpan total = proc.TotalProcessorTime;

            if (lastCpuSampleTimestamp == 0)
            {
                lastCpuSampleTimestamp = now;
                lastCpuSampleTotal = total;
                return 0.0;
            }

            double wallSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(lastCpuSampleTimestamp, now).TotalSeconds;
            double cpuSeconds = (total - lastCpuSampleTotal).TotalSeconds;

            lastCpuSampleTimestamp = now;
            lastCpuSampleTotal = total;

            if (wallSeconds <= 0.0 || Environment.ProcessorCount <= 0)
            {
                return 0.0;
            }

            double percent = (cpuSeconds / (wallSeconds * Environment.ProcessorCount)) * 100.0;
            if (percent < 0.0)
            {
                return 0.0;
            }
            if (percent > 100.0)
            {
                return 100.0;
            }
            return percent;
        }
    }

    // ── Frame-synchronous mutation support (WP-3504) ────────────

    /// <summary>
    /// How long a mutating tool waits for the next presented frame before
    /// reporting <c>timed_out: true</c>. A timeout is a structured answer
    /// ("nothing repainted within the window"), never an error.
    /// </summary>
    private static readonly TimeSpan PresentWaitTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The wait window on the CPU software-render fallback. CPU frames take far
    /// longer than the GPU path — seconds, not milliseconds (WP-3514) — so the
    /// 2 s GPU window spuriously reports <c>timed_out</c> under load on that path
    /// (WP-3516). The wait returns the instant the frame presents, so this larger
    /// ceiling costs nothing when a frame does land — it only widens the "nothing
    /// repainted" deadline to match how long a CPU frame legitimately takes.
    /// Callers that may genuinely never repaint on the CPU path (and thus consume
    /// the full window) must use a client timeout larger than this — the
    /// integration bridge's no-repaint test does (see FrameSyncTests).
    /// </summary>
    private static readonly TimeSpan CpuFallbackPresentWaitTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The present-wait window for the current render mode: the generous CPU
    /// ceiling when the last frame came from the software fallback, else the
    /// tight GPU window.
    /// </summary>
    private static TimeSpan PresentWait()
    {
        return Diagnostics.PresentMonitor.CpuRenderActive
            ? CpuFallbackPresentWaitTimeout
            : PresentWaitTimeout;
    }

    /// <summary>
    /// Reads the optional <c>wait_frames</c> tool argument (default 1): how
    /// many new presents a mutating tool waits for before responding, for
    /// UIs that settle over multiple frames.
    /// </summary>
    private static int GetWaitFrames(JsonObject parameters)
    {
        return Math.Clamp(GetInt(parameters, "wait_frames", 1), 1, 240);
    }

    /// <summary>
    /// Appends <c>presented_frame</c>/<c>timed_out</c> to a mutating tool's
    /// JSON response, blocking the MCP handler thread (never the UI thread)
    /// until <paramref name="waitFrames"/> new frames have presented since
    /// <paramref name="baselineFrame"/> or <see cref="PresentWaitTimeout"/>
    /// elapses. Responses reporting a failed or rejected mutation pass
    /// through unchanged — nothing was dispatched, so there is no frame to
    /// wait for. Never forces a present: an idle app that has nothing to
    /// repaint reports <c>timed_out: true</c>, which is exactly the answer
    /// the caller needs.
    /// </summary>
    private static string WithPresentation(string json, long baselineFrame, int waitFrames)
    {
        if (!json.StartsWith('{') || !json.EndsWith('}') ||
            json.StartsWith("{\"error\"", StringComparison.Ordinal) ||
            json.Contains("\"success\":false", StringComparison.Ordinal))
        {
            return json;
        }

        (long presented, bool timedOut) = Diagnostics.PresentMonitor.WaitForFrame(
            baselineFrame + waitFrames, PresentWait());
        return json[..^1] + $",\"presented_frame\":{presented},\"timed_out\":{BoolStr(timedOut)}}}";
    }

    private static string ErrorJson(string message, string? nodeId)
    {
        var sb = new StringBuilder();
        sb.Append($"{{\"error\":\"{EscapeJson(message)}\"");
        if (nodeId is not null)
        {
            sb.Append($",\"node_id\":\"{EscapeJson(nodeId)}\"");
        }
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Returns a pre-formatted MCP text content response for use by RawResponse tools.
    /// Use for error messages or text-only responses from tools that normally return
    /// image content.
    /// </summary>
    private static string RawTextContent(string message)
    {
        return "{\"content\":[{\"type\":\"text\",\"text\":\"" + EscapeJson(message) + "\"}]}";
    }

    private static string DebugOnlyError()
    {
        return "{\"error\":\"This tool is only available in debug builds. " +
               "Use cascade_accessibility_tree or cascade_find_accessible for release-mode inspection.\"}";
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
