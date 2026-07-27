using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Cascade.UI;

/// <summary>
/// Walks the laid-out node tree and issues <see cref="DrawContext"/> calls for each
/// visible node. Reads theme tokens from the active <see cref="CascadeTheme"/> to
/// determine colors, radii, shadows, and typography for each control.
/// </summary>
/// <remarks>
/// The paint pass runs after layout is complete, so every node's
/// <see cref="LayoutNodeData.Bounds"/> is populated. The painter traverses depth-first
/// (parent before children) so backgrounds are painted below content.
/// </remarks>
internal sealed class NodePainter
{
    // TEMP DIAGNOSTIC: per-node-type allocation profiler. Attributes bytes
    // allocated during PaintNode for each Node type, EXCLUSIVE of descendant
    // allocations (child node deltas are subtracted from the parent). Uses a
    // per-painter stack to accumulate child bytes at each depth.
    // Dumped via DumpPerTypeAllocations() on demand.
    private static readonly Dictionary<System.Type, (long Bytes, long Count)> perTypeAllocs = new();
    private static long perTypeFrameCount;
    private static readonly object perTypeLock = new();

    // Stack of "bytes allocated by children of the current node".
    // Each entry on the stack corresponds to one active PaintNode frame.
    private readonly List<long> perTypeChildBytesStack = new(capacity: 64);

    internal static string DumpPerTypeAllocations()
    {
        lock (perTypeLock)
        {
            long frames = perTypeFrameCount;
            if (frames == 0)
            {
                return "no frames sampled";
            }
            var sorted = perTypeAllocs
                .Select(kv => (Name: kv.Key.Name, Bytes: kv.Value.Bytes, Count: kv.Value.Count))
                .OrderByDescending(x => x.Bytes)
                .Take(25)
                .ToList();
            var sb = new System.Text.StringBuilder();
            sb.Append("frames=").Append(frames).AppendLine();
            sb.AppendLine("type, bytes_per_frame, count_per_frame, total_bytes, total_count");
            foreach (var row in sorted)
            {
                sb.Append(row.Name).Append(", ")
                  .Append(row.Bytes / frames).Append(", ")
                  .Append(row.Count / frames).Append(", ")
                  .Append(row.Bytes).Append(", ")
                  .Append(row.Count).AppendLine();
            }
            return sb.ToString();
        }
    }

    internal static void ResetPerTypeAllocations()
    {
        lock (perTypeLock)
        {
            perTypeAllocs.Clear();
            perTypeFrameCount = 0;
        }
    }

    internal static void TickPerTypeFrame()
    {
        lock (perTypeLock)
        {
            perTypeFrameCount++;
            // TEMP: one-shot dump at frame 600 (~15s at 40fps), overwrite ok.
            if (perTypeFrameCount == 600)
            {
                try
                {
                    string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "pertype-allocs.txt");
                    System.IO.File.WriteAllText(path, DumpPerTypeAllocationsUnsafe());
                }
                catch
                {
                    // Ignore dump failures — not critical.
                }
            }
        }
    }

    private static string DumpPerTypeAllocationsUnsafe()
    {
        long frames = perTypeFrameCount;
        if (frames == 0)
        {
            return "no frames sampled";
        }
        var sorted = perTypeAllocs
            .Select(kv => (Name: kv.Key.Name, Bytes: kv.Value.Bytes, Count: kv.Value.Count))
            .OrderByDescending(x => x.Bytes)
            .Take(40)
            .ToList();
        var sb = new System.Text.StringBuilder();
        sb.Append("frames=").Append(frames).AppendLine();
        sb.AppendLine("type, bytes_per_frame, count_per_frame, total_bytes, total_count");
        foreach (var row in sorted)
        {
            sb.Append(row.Name).Append(", ")
              .Append(row.Bytes / frames).Append(", ")
              .Append(row.Count / frames).Append(", ")
              .Append(row.Bytes).Append(", ")
              .Append(row.Count).AppendLine();
        }
        return sb.ToString();
    }

    private readonly DrawContext ctx;
    private CascadeTheme theme;

#if CASCADE_DEVTOOLS
    // Currently active draw-provenance node id (see PaintNode). Tracked here
    // so nested PaintNode calls can restore the parent's id without a stack.
    private string? currentProvenanceId;
#endif

    // Accumulated absolute offset for computing overlay positions.
    // Updated as we recurse through PushTranslate calls.
    private float absoluteX;
    private float absoluteY;

    // Lays out the (detached) custom drag-preview node on demand. Reused across
    // drag frames to avoid a per-frame allocation.
    private LayoutEngine? dragPreviewLayout;

    // Lays out the (detached) curtain node produced by a curtain page transition
    // on demand. Reused across transition frames to avoid a per-frame allocation.
    private LayoutEngine? curtainLayout;

    // Tracks the scroll offset of the currently-painting ScrollView ancestor.
    // Used by separator/gauge animation identity hashing.
    private float activeScrollOffsetY;

    // Screen-space Y bounds of the innermost ScrollView viewport currently
    // being painted. When no ScrollView is active these hold a fully-open
    // range so visibility checks succeed. Updated by PaintScrollView around
    // its recursive paint of the content subtree.
    //
    // These are the single source of truth for "is the node I'm painting
    // right now actually going to appear on screen?" — consulted before
    // setting any HasActive* flag so that off-screen animating controls
    // don't pin the frame loop. Without this, an off-screen Spinner inside
    // a 14,000 px ScrollView kept HelloCascade ticking at 40 fps at idle.
    private float currentViewportTop = float.NegativeInfinity;
    private float currentViewportBottom = float.PositiveInfinity;

    // Deferred overlay paint actions, rendered after the main tree so they
    // appear on top of all sibling content (e.g. dropdown popups).
    private List<Action>? deferredOverlays;

    // Set once per frame in Paint(): true when any popup/overlay is open anywhere in
    // the tree, so ScrollViews fall back to direct paint and don't occlude a popup that
    // spills over them from another pane. See Paint() and PaintScrollView.
    private bool frameHasOpenPopup;

    // Set to true during painting when any Spinner is encountered.
    // Checked by FrameOrchestrator to keep the frame loop running.
    internal static bool HasActiveSpinners { get; private set; }

    // Set to true during painting when any chart entrance animation is in progress.
    // Checked by FrameOrchestrator to keep the frame loop running.
    internal static bool HasActiveChartAnimations { get; private set; }

    // Set to true during painting when active toast notifications are present.
    // Checked by FrameOrchestrator to keep the frame loop running for auto-dismiss.
    internal static bool HasActiveToasts { get; private set; }

    // Set to true during painting when any CanvasNode with an onFrame callback is encountered.
    // Checked by FrameOrchestrator to keep the frame loop running for continuous canvas animations.
    internal static bool HasActiveContinuousCanvases { get; private set; }


    // Frame delta time in seconds, passed from FrameOrchestrator for canvas onFrame callbacks.
    private float deltaTime;

    // Set to true during ScrollView layer capture so entrance animations
    // render at their final state (progress = 1). Without this, charts
    // captured into a retained layer texture are frozen at progress 0
    // and never become visible.
    private bool skipAnimations;

    internal NodePainter(DrawContext ctx, CascadeTheme theme, float deltaTime = 0f)
    {
        this.ctx = ctx;
        this.theme = theme;
        this.deltaTime = deltaTime;
    }

    /// <summary>
    /// Resets per-frame mutable state so the painter can be pooled across
    /// ticks. The <see cref="DrawContext"/> reference itself is stable — its
    /// internals are refreshed via <see cref="DrawContext.BeginFrame"/>.
    /// </summary>
    internal void BeginFrame(CascadeTheme theme, float deltaTime)
    {
        this.theme = theme;
        this.deltaTime = deltaTime;
        absoluteX = 0f;
        absoluteY = 0f;
        activeScrollOffsetY = 0f;
        currentViewportTop = float.NegativeInfinity;
        currentViewportBottom = float.PositiveInfinity;
        skipAnimations = false;
        deferredOverlays?.Clear();
    }

    /// <summary>
    /// The window width in <b>logical</b> pixels. <see cref="DrawContext.Size"/> is in
    /// device pixels, but the painter works in logical coordinates (layout bounds,
    /// trigger rects, node offsets), so viewport math against those must use this — never
    /// raw <c>ctx.Size</c>, which is <c>PixelRatio×</c> too large on scaled displays.
    /// </summary>
    private float ViewportLogicalWidth => ctx.Size.Width / MathF.Max(1f, ctx.PixelRatio);

    /// <summary>
    /// The window height in <b>logical</b> pixels. See <see cref="ViewportLogicalWidth"/>.
    /// </summary>
    private float ViewportLogicalHeight => ctx.Size.Height / MathF.Max(1f, ctx.PixelRatio);

    /// <summary>
    /// True if a node with the given local bounds (as passed into the paint
    /// method) would actually be visible within the innermost enclosing
    /// ScrollView's viewport. When no ScrollView is active this always
    /// returns true.
    ///
    /// This is consulted before setting any <c>HasActive*</c> sentinel so
    /// that off-screen animating controls do not pin the frame loop awake.
    /// Using strict inequality on the top/bottom edges avoids flagging
    /// controls that sit exactly at the viewport boundary with zero
    /// overlap.
    /// </summary>
    private bool IsCurrentlyVisible(Rect localBounds)
    {
        // absoluteY already includes the node's Y offset (PaintRecursive increments
        // it by bounds.Y before calling the paint method), so we must not add
        // localBounds.Y again — doing so would double-count the offset and flag
        // visible nodes as off-screen.
        float nodeTop = absoluteY;
        float nodeBottom = nodeTop + localBounds.Height;

        if (float.IsNegativeInfinity(currentViewportTop))
        {
            // No ScrollView viewport in scope — cull against the window bounds so
            // off-screen top-level content (e.g. a chart below the fold) does not
            // keep the frame loop alive animating (WP-3516).
            return nodeBottom > 0f && nodeTop < ViewportLogicalHeight;
        }

        return nodeBottom > currentViewportTop && nodeTop < currentViewportBottom;
    }

    /// <summary>
    /// Paints the entire node tree starting from the root.
    /// </summary>
    internal void Paint(Node node)
    {
        HasActiveSpinners = false;
        HasActiveChartAnimations = false;
        HasActiveToasts = false;
        HasActiveContinuousCanvases = false;
        ControlStateAnimator.ReducedMotion = theme.Motion.ReducedMotion;
        ControlStateAnimator.BeginFrame();
        ChartAnimationTracker.BeginFrame();
        TickPerTypeFrame();

        // Whether any popup/overlay is open anywhere in the tree this frame. A popup
        // is a deferred overlay drawn last as shapes+glyphs, but a ScrollView's cached
        // layer is composited as an image, which the image pass draws OVER those shapes
        // — so a popup that overlaps a *different* pane's cached ScrollView (e.g. a grid
        // date/select popup spilling over the metadata panel) is occluded. Computed once
        // here (order-independent) so every ScrollView can fall back to direct paint
        // while a popup is open, keeping the popup on top. The CommandPalette overlay is
        // full-screen geometry drawn after the tree, so it hits the same occlusion — its
        // panel background is a cached-layer image drawn over it — and must count too.
        frameHasOpenPopup = HasOpenPopupsInSubtree(node) || CommandPalette.IsOpen;

        Cascade.UI.Diagnostics.DiagnosticsHub.MarkPhase("paint.recursive");
        PaintRecursive(node);
        Cascade.UI.Diagnostics.DiagnosticsHub.MarkPhase("paint.overlays");
        PaintDeferredOverlays();
        if (CommandPalette.IsOpen)
        {
            PaintCommandPaletteOverlay();
        }
        PaintToasts();
        PaintDragDropOverlay();
        Cascade.UI.Diagnostics.DiagnosticsHub.EndPhase();
    }

    private void PaintRecursive(Node node)
    {
        if (node.IsLayoutEmpty || !node.LayoutData.IsVisible)
        {
            return;
        }

        var data = node.LayoutData;
        var bounds = data.Bounds;

        // Skip zero-size nodes
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // Viewport cull (vertical): when we are painting inside a ScrollView
        // whose viewport has been registered in currentViewportTop/Bottom,
        // skip any subtree whose screen-space Y range does not intersect
        // the viewport expanded by a one-viewport-height prefetch margin
        // on each side. This is the central guard that prevents off-screen
        // animating controls (Spinner, ProgressRing indeterminate, chart
        // entrance animations, etc.) from setting HasActive* sentinels
        // that would pin the frame loop awake, AND skips the paint work
        // for the subtree entirely so off-screen content costs near-zero.
        //
        // The generous margin keeps transient popups/overlays near the
        // edge of the viewport working correctly and preserves entrance
        // animations for content scrolling into view. Tighter margins
        // would save more CPU but risk visual glitches.
        //
        // Horizontal culling is intentionally omitted: main-axis scrolling
        // is nearly always vertical in this framework and adding X culling
        // would complicate horizontal toolbars/headers that deliberately
        // extend beyond the viewport left/right for peek affordances.
        if (!float.IsNegativeInfinity(currentViewportTop))
        {
            float nodeTopAbs = absoluteY + bounds.Y;
            float nodeBottomAbs = nodeTopAbs + bounds.Height;
            float viewportHeight = currentViewportBottom - currentViewportTop;
            float cullTop = currentViewportTop - viewportHeight * 0.25f;
            float cullBottom = currentViewportBottom + viewportHeight * 0.25f;
            if (nodeBottomAbs <= cullTop || nodeTopAbs >= cullBottom)
            {
                return;
            }
        }

        // Layout bounds are relative to parent. Push a translate so all
        // drawing within this node uses local coordinates (0, 0, w, h).
        ScopeGuard positionScope = default;
        ScopeGuard translateScope = default;
        ScopeGuard scaleScope = default;
        ScopeGuard rotateScope = default;
        ScopeGuard opacityScope = default;
        ScopeGuard clipScope = default;
        ScopeGuard paddingScope = default;

        // Track accumulated absolute offset for overlay positioning
        absoluteX += bounds.X;
        absoluteY += bounds.Y;

        // Store absolute bounds for drag-and-drop overlay rendering
        if (data.DragData != null)
        {
            data.DragData.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);
        }

        try
        {
            // Translate to this node's layout position within its parent
            if (bounds.X != 0 || bounds.Y != 0)
            {
                positionScope = ctx.PushTranslate(bounds.X, bounds.Y);
            }

            var localBounds = new Rect(0, 0, bounds.Width, bounds.Height);

            // Apply visual-only transforms from layout modifiers
            if (data.TranslateX != 0 || data.TranslateY != 0)
            {
                translateScope = ctx.PushTranslate(data.TranslateX, data.TranslateY);
            }

            if (data.Scale != 1f)
            {
                var center = localBounds.Center;
                scaleScope = ctx.PushScale(data.Scale, data.Scale, center);
            }

            if (data.Rotation != default)
            {
                var center = localBounds.Center;
                rotateScope = ctx.PushRotate(data.Rotation, center);
            }

            if (data.Opacity < 1f)
            {
                opacityScope = ctx.PushOpacity(data.Opacity);
            }

            if (data.ClipContent)
            {
                clipScope = data.CornerRadiusValue.HasValue && data.CornerRadiusValue.Value > 0
                    ? ctx.PushRoundedClip(localBounds, data.CornerRadiusValue.Value)
                    : ctx.PushClip(localBounds);
            }

            // Draw modifier-level background (from .Background() fluent method)
            if (data.BackgroundColor.HasValue)
            {
                ctx.DrawRect(localBounds, data.BackgroundColor.Value,
                    radius: data.CornerRadiusValue ?? 0f);
            }

            // Draw modifier-level border (from .Border() fluent methods)
            PaintModifierBorder(data, localBounds);

            // Push a translate for padding so children are offset into the content area
            var padding = data.Padding;
            if (padding.Left != 0 || padding.Top != 0)
            {
                paddingScope = ctx.PushTranslate(padding.Left, padding.Top);
                absoluteX += padding.Left;
                absoluteY += padding.Top;
            }

            // Dispatch to control-specific or container paint.
            // Pass content-area bounds (after padding) so that leaf controls
            // like Label center text within the actual content area, not the
            // full padded bounds — the translate above already shifted the
            // coordinate origin by the padding.
            var contentBounds = new Rect(0, 0,
                localBounds.Width - padding.Horizontal,
                localBounds.Height - padding.Vertical);
            PaintNode(node, contentBounds);
        }
        finally
        {
            if (paddingScope.IsActive)
            {
                var padding2 = data.Padding;
                absoluteX -= padding2.Left;
                absoluteY -= padding2.Top;
            }
            paddingScope.Dispose();
            clipScope.Dispose();
            opacityScope.Dispose();
            rotateScope.Dispose();
            scaleScope.Dispose();
            translateScope.Dispose();
            positionScope.Dispose();
            absoluteX -= bounds.X;
            absoluteY -= bounds.Y;
        }
    }

    /// <summary>
    /// Paints deferred overlays (e.g. dropdown popups) that must render on top
    /// of the entire tree. Called after the main tree paint pass completes.
    /// Overlays paint in absolute (root-relative) coordinates so they are not
    /// clipped or occluded by parent/sibling transforms.
    /// </summary>
    private void PaintDeferredOverlays()
    {
        if (deferredOverlays is null || deferredOverlays.Count == 0)
        {
            return;
        }

        // Overlays paint at root-level coordinates (no parent transforms active).
        // PushOverlay/PopOverlay ensures their text is rendered on top of the
        // main frame text, preventing underlying controls' text from showing
        // through popup backgrounds.
        foreach (var overlay in deferredOverlays)
        {
            ctx.PushOverlay();
            overlay();
            ctx.PopOverlay();
        }

        deferredOverlays.Clear();
    }

    private void PaintNode(Node node, Rect bounds)
    {
        long __startBytes = GC.GetAllocatedBytesForCurrentThread();
        // Push a zero slot on the child-bytes stack for THIS node's children.
        perTypeChildBytesStack.Add(0L);
        int __myStackIndex = perTypeChildBytesStack.Count - 1;

#if CASCADE_DEVTOOLS
        // Draw-provenance tagging (WP-3505): tell the backend which node is
        // painting so whodrew can attribute draws. Children paint inside this
        // frame, so the parent's id is restored on exit. Free unless an MCP
        // session has enabled capture.
        string? previousProvenance = null;
        bool provenanceSet = false;
        if (DrawProvenance.CaptureEnabled)
        {
            previousProvenance = currentProvenanceId;
            string? nodeProvenance = DevTools.NodeTreeWalker.GetStableId(node) ?? previousProvenance;
            if (!ReferenceEquals(nodeProvenance, previousProvenance))
            {
                currentProvenanceId = nodeProvenance;
                ctx.SetDrawProvenance(nodeProvenance);
                provenanceSet = true;
            }
        }
#endif
        try
        {
            PaintNodeCore(node, bounds);
        }
        finally
        {
#if CASCADE_DEVTOOLS
            if (provenanceSet)
            {
                currentProvenanceId = previousProvenance;
                ctx.SetDrawProvenance(previousProvenance);
            }
#endif
            long __totalDelta = GC.GetAllocatedBytesForCurrentThread() - __startBytes;
            long __childBytes = perTypeChildBytesStack[__myStackIndex];
            perTypeChildBytesStack.RemoveAt(__myStackIndex);
            long __exclusive = __totalDelta - __childBytes;
            if (__exclusive < 0)
            {
                // Shouldn't happen, but guard against profiler-overhead noise.
                __exclusive = 0;
            }

            // Propagate this node's total delta to the parent's child-bytes slot
            // so the parent can subtract it from its own exclusive count.
            int __parentIndex = perTypeChildBytesStack.Count - 1;
            if (__parentIndex >= 0)
            {
                perTypeChildBytesStack[__parentIndex] += __totalDelta;
            }

            var __t = node.GetType();
            lock (perTypeLock)
            {
                if (perTypeAllocs.TryGetValue(__t, out var __v))
                {
                    perTypeAllocs[__t] = (__v.Bytes + __exclusive, __v.Count + 1);
                }
                else
                {
                    perTypeAllocs[__t] = (__exclusive, 1);
                }
            }
        }
    }

    private void PaintNodeCore(Node node, Rect bounds)
    {
        switch (node)
        {
            // Layout containers — recurse into children
            case Row row:
                PaintChildren(row.Children);
                break;

            case Column col:
                PaintChildren(col.Children);
                break;

            case Stack stk:
                PaintChildren(stk.Children);
                break;

            case Grid grid:
                PaintChildren(grid.Children);
                break;

            case Center center:
                PaintRecursive(center.Child);
                break;

            case ScrollView scrollView:
                PaintScrollView(scrollView, bounds);
                break;

            case Spacer:
                break;

            // Leaf controls — paint their visuals
            case Button btn:
                PaintButton(btn, bounds);
                break;

            case Label lbl:
                PaintLabel(lbl, bounds);
                break;

            case TextInput ti:
                PaintTextInput(ti, bounds);
                break;

            case Checkbox cb:
                PaintCheckbox(cb, bounds);
                break;

            case IRadioButton rb:
                PaintRadioButton(rb, bounds);
                break;

            case IRadioGroup rg:
                PaintRecursive(rg.Content);
                break;

            case Toggle tog:
                PaintToggle(tog, bounds);
                break;

            case ProgressBar pb:
                PaintProgressBar(pb, bounds);
                break;

            case Separator sep:
                PaintSeparator(sep, bounds);
                break;

            case Card card:
                PaintCard(card, bounds);
                break;

            case Badge badge:
                PaintBadge(badge, bounds);
                break;

            case FormValidator fv:
                PaintNode(fv.Content, bounds);
                break;

            case KeyHandler kh:
                PaintNode(kh.Content, bounds);
                break;

            case AnimatePresence ap when ap.IsVisible:
                PaintNode(ap.Child, bounds);
                break;
            case AnimatePresence:
                break;

            case Rating rating:
                PaintRating(rating, bounds);
                break;

            case Image img:
                PaintImage(img, bounds);
                break;

            case Spinner spinner:
                PaintSpinner(spinner, bounds);
                break;

            case IconButton ib:
                PaintIconButton(ib, bounds);
                break;

            case IconView iv:
                PaintIconView(iv, bounds);
                break;

            case LinkButton lb:
                PaintLinkButton(lb, bounds);
                break;

            case CanvasNode canvas:
                PaintCanvas(canvas, bounds);
                break;

            case Slider slider:
                PaintSlider(slider, bounds);
                break;

            case ISelectNode select:
                PaintSelect(select, bounds);
                break;

            case IMultiSelectNode ms:
                PaintMultiSelect(ms, bounds);
                break;

            case IComboboxNode cb:
                PaintCombobox(cb, bounds);
                break;

            case SplitButton sb:
                PaintSplitButton(sb, bounds);
                break;

            // Complex controls with child slots
            case SplitView sv:
                PaintSplitView(sv, bounds);
                break;

            case Accordion acc:
                PaintAccordion(acc, bounds);
                break;

            case Expander exp:
                PaintExpander(exp, bounds);
                break;

            case Tag tag:
                PaintTag(tag, bounds);
                break;

            case Avatar av:
                PaintAvatar(av, bounds);
                break;

            case ProgressRing pr:
                PaintProgressRing(pr, bounds);
                break;

            case ISegmentedControl sc:
                PaintSegmentedControl(sc, bounds);
                break;

            case Breadcrumb bc:
                PaintBreadcrumb(bc, bounds);
                break;

            case INumberInput ni:
                PaintNumberInput(ni, bounds);
                break;

            case Gauge gauge:
                PaintGauge(gauge, bounds);
                break;

            case StepIndicator si:
                PaintStepIndicator(si, bounds);
                break;

            case IToggleGroup tg:
                PaintToggleGroup(tg, bounds);
                break;

            case Banner banner:
                PaintBanner(banner, bounds);
                break;

            case Sparkline spark:
                PaintSparkline(spark, bounds);
                break;

            case RangeSlider rs:
                PaintRangeSlider(rs, bounds);
                break;

            case DonutGauge dg:
                PaintDonutGauge(dg, bounds);
                break;

            case Timeline tl:
                PaintTimeline(tl, bounds);
                break;

            case ColorPicker cp:
                PaintColorPicker(cp, bounds);
                break;

            case PinInput pin:
                PaintPinInput(pin, bounds);
                break;

            case StatusBar sb:
                PaintStatusBar(sb, bounds);
                break;

            case ToolBar tb:
                PaintToolBar(tb, bounds);
                break;

            case MenuBar mb:
                PaintMenuBar(mb, bounds);
                break;

            case PropertyGrid pg:
                PaintPropertyGrid(pg, bounds);
                break;

            case NotificationBell nb:
                PaintNotificationBell(nb, bounds);
                break;

            case EmojiPicker ep:
                PaintEmojiPicker(ep, bounds);
                break;

            case QrCode qr:
                PaintQrCode(qr, bounds);
                break;

            case Barcode barcode:
                PaintBarcode(barcode, bounds);
                break;

            case BarChart barChart:
                PaintBarChart(barChart, bounds);
                break;

            case PieChart pieChart:
                PaintPieChart(pieChart, bounds);
                break;

            case LineChart lineChart:
                PaintLineChart(lineChart, bounds);
                break;

            case AreaChart areaChart:
                PaintAreaChart(areaChart, bounds);
                break;

            case HeatMapChart heatMapChart:
                PaintHeatMap(heatMapChart, bounds);
                break;

            case TreeMapChart treeMapChart:
                PaintTreeMapChart(treeMapChart, bounds);
                break;

            case WaterfallChart waterfallChart:
                PaintWaterfallChart(waterfallChart, bounds);
                break;

            case ScatterPlot scatterPlot:
                PaintScatterPlot(scatterPlot, bounds);
                break;

            case ITreeView tv:
                PaintTreeView(tv, bounds);
                break;

            case PasswordInput pwd:
                PaintPasswordInput(pwd, bounds);
                break;

            case TextArea ta:
                PaintTextArea(ta, bounds);
                break;

            case DatePicker dp:
                PaintDatePicker(dp, bounds);
                break;

            case DateTimePicker dtp:
                PaintDateTimePicker(dtp, bounds);
                break;

            case TimePicker tp:
                PaintTimePicker(tp, bounds);
                break;

            case MonthPicker mp:
                PaintMonthPicker(mp, bounds);
                break;

            case TagInput tagIn:
                PaintTagInput(tagIn, bounds);
                break;

            case MentionInput mi:
                PaintMentionInput(mi, bounds);
                break;

            case Markdown md:
                PaintMarkdown(md, bounds);
                break;

            case CommandPalette:
                // Overlay painted directly in Paint() — not through the tree
                break;

            case DateRangePicker drp:
                PaintDateRangePicker(drp, bounds);
                break;

            case Calendar cal:
                PaintCalendar(cal, bounds);
                break;

            case ITabularDataNode tdn:
                PaintTabularData(tdn, (Node)tdn, bounds);
                break;

            case IListViewNode lvn:
                PaintListView(lvn, (Node)lvn, bounds);
                break;

            case NavigationTransitionHost nth:
                PaintNavigationTransition(nth, bounds);
                break;

            case Component comp when comp.RenderedTree is not null:
                PaintRecursive(comp.RenderedTree);
                break;

            default:
                // Unknown node type — do nothing. The modifier-level background
                // and border were already painted above.
                break;
        }
    }

    // ── ScrollView ─────────────────────────────────────────────────────

    /// <summary>
    /// Walks the subtree and returns whether any descendant has active
    /// transient animations and/or active persistent animations. Performs
    /// both checks in a single walk to avoid traversing the tree twice.
    /// </summary>
    private static (bool HasActive, bool HasPersistent) GetDescendantAnimationState(Node root)
    {
        bool hasActive = ControlStateAnimator.HasActiveAnimationsForNode(root);
        bool hasPersistent = ControlStateAnimator.HasActivePersistentAnimationsForNode(root);

        if (hasActive && hasPersistent)
        {
            return (true, true);
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            var (childActive, childPersistent) = GetDescendantAnimationState(child);
            hasActive |= childActive;
            hasPersistent |= childPersistent;

            if (hasActive && hasPersistent)
            {
                return (true, true);
            }
        }

        return (hasActive, hasPersistent);
    }

    /// <summary>
    /// Walks the subtree and returns true if any descendant is a control with an
    /// open popup (dropdown, calendar picker, etc.). Used by PaintScrollView to
    /// decide whether to composite the cached layer or paint directly.
    /// </summary>
    private static bool HasOpenPopupsInSubtree(Node root)
    {
        if (root is ISelectNode { IsOpen: true }
            or IComboboxNode { IsOpen: true }
            or IMultiSelectNode { IsOpen: true }
            or MenuBar { IsOpen: true }
            or DatePicker { IsCalendarOpen: true }
            or DateRangePicker { IsCalendarOpen: true }
            or DateTimePicker { IsCalendarOpen: true }
            or TimePicker { IsPopupOpen: true }
            or MonthPicker { IsPopupOpen: true }
            or ITabularDataNode { IsSelectDropdownOpen: true }
            or ITabularDataNode { IsDatePopupOpen: true })
        {
            return true;
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            if (HasOpenPopupsInSubtree(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Walks the subtree and returns true if any descendant is a continuously
    /// animating control (Spinner, CanvasNode with IsContinuous, or Chart with
    /// active entrance animation). These must be repainted every frame and cannot
    /// be frozen inside a cached ScrollView layer.
    /// </summary>
    private static bool HasContinuousAnimationInSubtree(Node root)
    {
        if (root is Spinner)
        {
            return true;
        }

        if (root is CanvasNode canvas && canvas.IsContinuous)
        {
            return true;
        }

        if (root is BarChart bc && bc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeBarChartHash(bc);
            return ChartAnimationTracker.IsBarChartAnimating(bc, hash, bc.animateTrigger, bc.Series.Sum(s => s.DataPoints.Count));
        }

        if (root is LineChart lc && lc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeLineChartHash(lc);
            return ChartAnimationTracker.IsAnimating(lc, hash, lc.animateTrigger, ChartAnimationTracker.LineDuration);
        }

        if (root is PieChart pc && pc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputePieChartHash(pc);
            return ChartAnimationTracker.IsAnimating(pc, hash, pc.animateTrigger, ChartAnimationTracker.PieDuration);
        }

        if (root is AreaChart ac && ac.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeAreaChartHash(ac);
            return ChartAnimationTracker.IsAnimating(ac, hash, ac.animateTrigger, ChartAnimationTracker.AreaDuration);
        }

        if (root is DonutGauge dg && dg.animateTriggerValue != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeDonutGaugeHash(dg);
            return ChartAnimationTracker.IsAnimating(dg, hash, dg.animateTriggerValue, ChartAnimationTracker.GaugeDuration);
        }

        if (root is HeatMapChart hmc && hmc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeHeatMapChartHash(hmc);
            int totalCells = hmc.cellsList.Count;
            float totalDuration = ChartAnimationTracker.HeatMapDuration + (totalCells - 1) * 30f;
            return ChartAnimationTracker.IsAnimating(hmc, hash, hmc.animateTrigger, totalDuration);
        }

        if (root is TreeMapChart tmc && tmc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeTreeMapChartHash(tmc);
            int totalCells = tmc.nodesList.Count;
            float totalDuration = ChartAnimationTracker.TreeMapDuration + (totalCells - 1) * 30f;
            return ChartAnimationTracker.IsAnimating(tmc, hash, tmc.animateTrigger, totalDuration);
        }

        if (root is WaterfallChart wfc && wfc.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeWaterfallChartHash(wfc);
            int totalItems = wfc.itemsList.Count;
            float totalDuration = ChartAnimationTracker.WaterfallDuration + (totalItems - 1) * 30f;
            return ChartAnimationTracker.IsAnimating(wfc, hash, wfc.animateTrigger, totalDuration);
        }

        if (root is ScatterPlot sp && sp.animateTrigger != AnimateTrigger.None)
        {
            int hash = ChartAnimationTracker.ComputeScatterPlotHash(sp);
            return ChartAnimationTracker.IsAnimating(sp, hash, sp.animateTrigger, ChartAnimationTracker.ScatterDuration);
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            if (HasContinuousAnimationInSubtree(child))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the currently focused element is a keyboard-editable control
    /// (text entry / inline editing) located within the given subtree. Used by
    /// <see cref="PaintScrollView"/> to recapture a cached layer while it hosts an
    /// active edit, so keystrokes and the caret render live instead of compositing
    /// the stale pre-edit bitmap. Evaluated against the live painted tree.
    /// </summary>
    internal static bool SubtreeContainsFocusedEditable(Node root)
    {
        var focused = FocusManager.FocusedElement;
        if (focused is null)
        {
            return false;
        }

        if (focused is not (TextInput or PasswordInput or TextArea or PinInput
            or TagInput or MentionInput or PropertyGrid or ITabularDataNode))
        {
            return false;
        }

        return SubtreeContains(root, focused);
    }

    private static bool SubtreeContains(Node root, Node target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }

        foreach (var child in NodeDiffer.GetChildren(root))
        {
            if (SubtreeContains(child, target))
            {
                return true;
            }
        }

        return false;
    }

    private void PaintScrollView(ScrollView sv, Rect bounds)
    {
        float scrollY = sv.OffsetY;
        float maxScrollY = sv.MaxY;
        float contentHeight = bounds.Height + maxScrollY;

        // If any descendant has active state-transition animations (hover, press,
        // check, toggle, etc.), we cannot composite the cached layer because the
        // animation state advances inside the paint methods and would be frozen at
        // the capture frame. Paint directly while animations are running.
        // Only mark the layer dirty for persistent-state animations (Value, Open,
        // Disabled). Transient animations (Press, Hover, Focus) return the control
        // to its original visual state, so recapturing the layer is wasteful.
        var (hasActiveAnimations, hasPersistentAnimations) = GetDescendantAnimationState(sv.Content);

        if (hasPersistentAnimations)
        {
            sv.IsLayerDirty = true;
        }

        // A focused text-editing control inside this ScrollView mutates its buffer
        // on every keystroke. That mutation changes neither the layer's size, its
        // theme, nor runs a persistent animation, so without this the cached layer
        // keeps compositing the stale pre-edit bitmap — typed text never appears and
        // no caret shows. Deciding this at paint time (against the live painted tree)
        // rather than from the input dispatcher is deliberate: the dispatcher's root
        // and the focus reference can lag the painted tree by a reconcile, so an
        // input-time mark can land on a discarded ScrollView instance. Recapture
        // while the focus lives in this content so edits and the caret render live.
        // Recapture while the focus is in this content, and once more when it
        // leaves (CapturedFocusedEditable was true, now false) so a lingering
        // focus ring / caret from the last focused capture is cleared.
        bool hasFocusedEditable = SubtreeContainsFocusedEditable(sv.Content);
        if (hasFocusedEditable || sv.CapturedFocusedEditable)
        {
            sv.IsLayerDirty = true;
        }

        // Popups (dropdowns, calendar pickers) inside a cached ScrollView layer are
        // invisible because PaintRecursive is skipped during layer compositing — the
        // popup is never added to deferredOverlays. Force direct paint while any
        // descendant has an open popup so the popup renders on top of the content.
        // frameHasOpenPopup extends this to a popup open anywhere in the tree: a cached
        // layer is composited as an image that the image pass draws over the deferred
        // popup shapes, so a popup spilling in from another pane would be occluded —
        // direct-painting this layer while any popup is open keeps the popup on top.
        bool hasOpenPopups = frameHasOpenPopup || HasOpenPopupsInSubtree(sv.Content);

        // Spinners, continuous canvases, and chart entrance animations advance every
        // frame. Freezing them inside a cached layer makes them appear broken.
        bool hasContinuousAnimation = HasContinuousAnimationInSubtree(sv.Content);

        // A theme/dark-mode/high-contrast switch recolours and restyles content
        // without changing its structure or size, so the dirty/size checks below
        // never fire — the layer would composite with the previous theme's baked
        // colours and text styles (e.g. dark text from a light theme, invisible
        // over a dark sidebar). Recapture whenever the theme version has advanced
        // since this layer was last captured.
        bool themeChanged = sv.CapturedThemeVersion != ThemeSwitcher.Version;

        // Determine if we need to re-capture the layer texture.
        // The layer is invalidated when content changes, size changes, the theme
        // changes, or on first paint.
        bool needsRecapture = !hasActiveAnimations && !hasOpenPopups && !hasContinuousAnimation && (
            sv.IsLayerDirty
            || sv.LayerHandle == null
            || themeChanged
            || Math.Abs(sv.LayerWidth - bounds.Width) > 0.5f
            || Math.Abs(sv.LayerHeight - contentHeight) > 0.5f);

        if (needsRecapture)
        {
            // Allocate this ScrollView a stable, unique layer handle once and reuse it
            // for every recapture. Previously the handle was taken from a per-frame
            // counter in recapture order, so when the recapture set changed (e.g. a
            // control animating made only the content layer recapture) a ScrollView
            // could be handed a handle still owned by another — collapsing two layers
            // onto one cache and blanking the sidebar nav.
            sv.LayerHandle ??= ctx.NextLayerHandle();

            // Capture the full scrollable content into a retained layer texture.
            // Content is painted at origin (no scroll offset) so the texture can be
            // reused across scroll frames — only the UV offset changes during compositing.
            using (ctx.PushLayerTexture(sv.LayerHandle.Value, bounds.Width, contentHeight))
            {
                // Adjust absoluteY as if scrollY = 0, matching the original PushTranslate behavior.
                absoluteY -= scrollY;
                float prevScrollOffset = activeScrollOffsetY;
                // Keep the scroll offset in scope so animation-identity hashes
                // (separator/gauge/timeline/tree entrance) resolve to the same
                // content-space position as the direct-paint path below. Using 0
                // here made `absoluteY + activeScrollOffsetY` scroll-dependent, so a
                // separator's hash changed when an open popup forced direct paint —
                // replaying every visible separator's entrance animation.
                activeScrollOffsetY = scrollY;

                float prevViewportTop = currentViewportTop;
                float prevViewportBottom = currentViewportBottom;
                float viewportTop = absoluteY + scrollY;
                float viewportBottom = viewportTop + contentHeight;
                currentViewportTop = viewportTop;
                currentViewportBottom = viewportBottom;

                bool prevSkipAnimations = skipAnimations;
                skipAnimations = true;
                PaintRecursive(sv.Content);
                skipAnimations = prevSkipAnimations;

                currentViewportTop = prevViewportTop;
                currentViewportBottom = prevViewportBottom;
                activeScrollOffsetY = prevScrollOffset;
                absoluteY += scrollY;
            }

            // sv.LayerHandle was allocated above and is reused across recaptures —
            // no need to read it back from the backend.
            sv.LayerWidth = bounds.Width;
            sv.LayerHeight = contentHeight;
            sv.CapturedThemeVersion = ThemeSwitcher.Version;
            sv.CapturedFocusedEditable = hasFocusedEditable;
            sv.IsLayerDirty = false;
        }

        // Composite the layer texture with the current scroll offset.
        // This is the fast path: no child re-rendering, just a textured quad blit.
        // Skip compositing when there are open popups or continuous animations —
        // they must be painted on top / re-rendered every frame.
        if (sv.LayerHandle.HasValue && !hasActiveAnimations && !hasOpenPopups && !hasContinuousAnimation)
        {
            using var clip = ctx.PushClip(new Rect(0, 0, bounds.Width, bounds.Height));
            ctx.DrawLayerTexture(sv.LayerHandle.Value, 0, -scrollY);
        }
        else
        {
            // Fallback if layer handle is not available (non-Etch backend or error),
            // or if active animations require direct painting.
            using var clip = ctx.PushClip(new Rect(0, 0, bounds.Width, bounds.Height));
            using var translate = ctx.PushTranslate(0, -scrollY);
            absoluteY -= scrollY;
            float prevScrollOffset = activeScrollOffsetY;
            activeScrollOffsetY = scrollY;

            float prevViewportTop = currentViewportTop;
            float prevViewportBottom = currentViewportBottom;
            float viewportTop = absoluteY + scrollY;
            float viewportBottom = viewportTop + bounds.Height;
            currentViewportTop = viewportTop;
            currentViewportBottom = viewportBottom;

            PaintRecursive(sv.Content);

            currentViewportTop = prevViewportTop;
            currentViewportBottom = prevViewportBottom;
            activeScrollOffsetY = prevScrollOffset;
            absoluteY += scrollY;
        }

        // Paint scrollbar overlay (fixed to viewport, does not scroll with content)
        if (maxScrollY > 0)
        {
            const float scrollbarWidth = 6f;
            const float scrollbarMargin = 2f;
            const float scrollbarPadV = 4f;
            float scrollbarX = bounds.Width - scrollbarWidth - scrollbarMargin;
            float trackHeight = bounds.Height - scrollbarPadV * 2;

            float thumbRatio = bounds.Height / contentHeight;
            float thumbHeight = Math.Max(24f, trackHeight * thumbRatio);
            float trackRange = trackHeight - thumbHeight;
            float thumbY = scrollbarPadV + (scrollY / maxScrollY) * trackRange;

            // Track (subtle)
            var trackBounds = new Rect(scrollbarX, scrollbarPadV, scrollbarWidth, trackHeight);
            ctx.DrawRect(trackBounds, theme.Colors.Border.Opacity(0.15f), radius: 3f);

            // Thumb
            var thumbBounds = new Rect(scrollbarX, thumbY, scrollbarWidth, thumbHeight);
            ctx.DrawRect(thumbBounds, theme.Colors.Text.Opacity(0.35f), radius: 3f);

            // Store scrollbar geometry on the ScrollView instance for drag support
            sv.ScrollbarTrackBounds = new Rect(
                absoluteX + bounds.X + scrollbarX - scrollbarMargin,
                absoluteY + bounds.Y + scrollbarPadV,
                scrollbarWidth + scrollbarMargin * 2,
                trackHeight);
            sv.ScrollbarThumbHeight = thumbHeight;
        }
        else
        {
            sv.ScrollbarTrackBounds = default;
        }
    }

    // ── Button ─────────────────────────────────────────────────────────

    private void PaintButton(Button btn, Rect bounds)
    {
        var bt = ResolveButtonTheme(btn);

        // Get animation models from theme — use bouncier spring on release for overshoot
        var hoverModel = bt.Hover.EnterTransition?.Model ?? bt.Transition.Model;
        var pressModel = btn.IsPressed
            ? (bt.Pressed.EnterTransition?.Model ?? AnimationModel.Spring.Snappy)
            : (bt.Pressed.ExitTransition?.Model ?? AnimationModel.Spring.Bouncy);

        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);

        // Reconcile animation state with current boolean flags
        bool isDisabled = btn.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, btn)
            && FocusManager.LastFocusWasKeyboard;
        var anim = ControlStateAnimator.Reconcile(
            btn, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;

        // Interpolate scale: default → hover scale → press scale
        // Reduced motion: skip scale effects, preserve color transitions only
        float scale = 1f;
        if (!ControlStateAnimator.ReducedMotion)
        {
            float hoverScale = bt.Hover.Scale ?? 1f;
            float pressScale = bt.Pressed.Scale ?? 1f;
            scale = LerpF(1f, hoverScale, hoverT);
            scale = LerpF(scale, pressScale, pressT);
        }

        Rect paintBounds = bounds;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            float cx = bounds.X + bounds.Width / 2;
            float cy = bounds.Y + bounds.Height / 2;
            float sw = bounds.Width * scale;
            float sh = bounds.Height * scale;
            paintBounds = new Rect(cx - sw / 2, cy - sh / 2, sw, sh);
        }

        // Shadow — interpolate between default and state shadows.
        // Avoids allocating intermediate ShadowSpec instances by lerping
        // drop values inline and painting directly. Common case (1 drop,
        // no inner) stays entirely on the stack.
        var defaultShadow = bt.Shadow;
        var hoverShadowSpec = bt.Hover.Shadow;
        var pressShadowSpec = bt.Pressed.Shadow;

        // Cursor proximity effect: shadow expands and brightens as cursor approaches
        float proximity = ControlStateAnimator.ComputeProximity(paintBounds, 80f);
        float proximityT = (hoverT < 0.5f && !isDisabled && proximity > 0.001f
            && defaultShadow is not null && hoverShadowSpec is not null)
            ? proximity * 0.4f : 0f;

        if (defaultShadow is not null)
        {
            PaintLerpedShadow(
                defaultShadow,
                proximityT > 0f ? hoverShadowSpec : null, proximityT,
                hoverT > 0.001f ? hoverShadowSpec : null, hoverT,
                pressT > 0.001f ? pressShadowSpec : null, pressT,
                paintBounds, bt.Radius);
        }

        // Background — interpolate opacity
        float bgOpacity = 1f;
        if (bt.Hover.BackgroundOpacity.HasValue)
        {
            bgOpacity = LerpF(bgOpacity, bt.Hover.BackgroundOpacity.Value, hoverT);
        }

        if (bt.Pressed.BackgroundOpacity.HasValue)
        {
            bgOpacity = LerpF(bgOpacity, bt.Pressed.BackgroundOpacity.Value, pressT);
        }

        if (bt.Disabled.BackgroundOpacity.HasValue)
        {
            bgOpacity = LerpF(bgOpacity, bt.Disabled.BackgroundOpacity.Value, disabledT);
        }

        // Background brush — use state override or default.
        // If the button has a modifier-level background (from .Background()), the modifier
        // already drew it before PaintButton was called. Skip the theme background to avoid
        // covering the custom color, but still apply hover/press overlays for interactivity.
        bool hasModifierBg = btn.LayoutData.BackgroundColor.HasValue;
        if (!hasModifierBg)
        {
            var bgBrush = bt.Background;
            if (pressT > 0.5f && bt.Pressed.Background is not null)
            {
                bgBrush = bt.Pressed.Background;
            }
            else if (hoverT > 0.5f && bt.Hover.Background is not null)
            {
                bgBrush = bt.Hover.Background;
            }

            PaintBrush(bgBrush, paintBounds, bt.Radius, bgOpacity);
        }

        // Hover/press feedback for transparent variants (outline, ghost): a barely-there
        // NEUTRAL fill, the way macOS highlights a toolbar/list item — a faint light
        // overlay in dark mode, faint dark in light mode (theme-adaptive via Text). No
        // glow (that's Aero/Vista) and no accent wash. Press deepens it a touch; the
        // real tactile feedback is the scale/bounce.
        if (btn.VariantName is "outline" or "ghost")
        {
            float fillAlpha = (0.022f * hoverT + 0.03f * pressT);
            if (fillAlpha > 0.001f && !isDisabled)
            {
                ctx.DrawRect(paintBounds, theme.Colors.Text.ScaleAlpha(fillAlpha), radius: bt.Radius);
            }
        }

        // Overlay color (Fluent hover pattern — fade in/out with hover progress)
        if (bt.Hover.OverlayColor is { } overlay && hoverT > 0.001f)
        {
            var overlayColor = overlay.Opacity(hoverT);
            ctx.DrawRect(paintBounds, fill: overlayColor, radius: bt.Radius);
        }
        else if (hasModifierBg && hoverT > 0.001f)
        {
            // Generic hover overlay for buttons with custom modifier backgrounds
            var hoverOverlay = new ColorValue("#FFFFFF").Opacity(hoverT * 0.12f);
            ctx.DrawRect(paintBounds, fill: hoverOverlay, radius: btn.LayoutData.CornerRadiusValue ?? bt.Radius);
        }

        if (bt.Pressed.OverlayColor is { } pressOverlay && pressT > 0.001f)
        {
            var pressOverlayColor = pressOverlay.Opacity(pressT);
            ctx.DrawRect(paintBounds, fill: pressOverlayColor, radius: bt.Radius);
        }
        else if (hasModifierBg && pressT > 0.001f)
        {
            // Generic press overlay for buttons with custom modifier backgrounds
            var pressOvl = new ColorValue("#000000").Opacity(pressT * 0.15f);
            ctx.DrawRect(paintBounds, fill: pressOvl, radius: btn.LayoutData.CornerRadiusValue ?? bt.Radius);
        }

        // Border
        var border = bt.Border;
        var borderWidth = bt.BorderWidth;
        if (hoverT > 0.5f && bt.Hover.Border is not null)
        {
            border = bt.Hover.Border;
            borderWidth = bt.Hover.BorderWidth ?? borderWidth;
        }

        if (pressT > 0.5f && bt.Pressed.Border is not null)
        {
            border = bt.Pressed.Border;
            borderWidth = bt.Pressed.BorderWidth ?? borderWidth;
        }

        if (border is not null && borderWidth > 0)
        {
            PaintBrushStroke(border, paintBounds, borderWidth, bt.Radius);
        }

        // Focus outline ring — preserved for keyboard navigation.
        // Focus state currently uses the existing boolean approach; will be
        // animated in a subsequent polish pass when focus tracking is wired.
        if (!isDisabled && bt.Focused.OutlineColor is { } outlineColor
            && (bt.Focused.OutlineWidth ?? 0) > 0
            && anim.Focus.Current > 0.001f)
        {
            float focusT = anim.Focus.Current;
            float outlineWidth = bt.Focused.OutlineWidth!.Value * focusT;
            float outlineOffset = bt.Focused.OutlineOffset ?? 2f;
            var outlineRect = new Rect(
                paintBounds.X - outlineOffset,
                paintBounds.Y - outlineOffset,
                paintBounds.Width + outlineOffset * 2,
                paintBounds.Height + outlineOffset * 2);
            PaintBrushStroke(Brush.Solid(outlineColor.Opacity(focusT)), outlineRect,
                outlineWidth, bt.Radius + outlineOffset);
        }

        // Text color — interpolate
        var textColor = bt.TextColor;
        if (bt.Hover.TextColor is { } hoverTextColor && hoverT > 0.001f)
        {
            textColor = ColorValue.Lerp(textColor, hoverTextColor, hoverT);
        }

        if (bt.Pressed.TextColor is { } pressTextColor && pressT > 0.001f)
        {
            textColor = ColorValue.Lerp(textColor, pressTextColor, pressT);
        }

        float textOpacity = 1f;
        if (bt.Hover.TextOpacity.HasValue)
        {
            textOpacity = LerpF(textOpacity, bt.Hover.TextOpacity.Value, hoverT);
        }

        if (bt.Pressed.TextOpacity.HasValue)
        {
            textOpacity = LerpF(textOpacity, bt.Pressed.TextOpacity.Value, pressT);
        }

        if (bt.Disabled.TextOpacity.HasValue)
        {
            textOpacity = LerpF(textOpacity, bt.Disabled.TextOpacity.Value, disabledT);
        }

        if (textOpacity < 1f)
        {
            textColor = textColor.Opacity(textOpacity);
        }

        // Honor a per-button Style() override for the text size/weight. The layout
        // solver already sizes the button using StyleOverride; the renderer must
        // match or the text is drawn at the theme size inside a differently-sized
        // box (e.g. clipping). fontSize 0 falls back to PaintText's default (Body),
        // so buttons without an override render exactly as before.
        PaintText(btn.Label.Resolve(), bounds, bt.PaddingH, textColor,
            fontSize: btn.StyleOverride?.Size ?? 0f,
            alignment: TextAlignment.Center,
            fontWeight: btn.StyleOverride?.Weight ?? bt.TextStyle.Weight);

        // Tooltip overlay (deferred)
        DeferTooltipIfHovered(btn, btn.TooltipText.Resolve(), bounds);
    }

    private ButtonTheme ResolveButtonTheme(Button btn)
    {
        var bt = theme.Button;
        if (btn.VariantName is not null
            && bt.Variants.TryGetValue(btn.VariantName, out var variant))
        {
            return variant;
        }

        return bt;
    }

    // ── SplitButton ────────────────────────────────────────────────────

    private void PaintSplitButton(SplitButton sb, Rect bounds)
    {
        var bt = theme.Button;
        var st = theme.Select;
        float arrowZoneWidth = 36f;
        float dividerX = bounds.X + bounds.Width - arrowZoneWidth;

        // Store arrow zone offset for hit testing
        sb.ArrowZoneX = bounds.Width - arrowZoneWidth;

        // Store absolute bounds for viewport-space hit testing in InputDispatcher
        sb.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Animated state transitions
        bool isDisabled = sb.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, sb)
            && FocusManager.LastFocusWasKeyboard;
        var hoverModel = bt.Hover.EnterTransition?.Model
            ?? AnimationModel.Spring.Snappy;
        var pressModel = sb.IsPressed
            ? (bt.Pressed.EnterTransition?.Model ?? AnimationModel.Spring.Snappy)
            : (bt.Pressed.ExitTransition?.Model ?? AnimationModel.Spring.Bouncy);

        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);

        var anim = ControlStateAnimator.Reconcile(
            sb, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;
        float opacity = LerpF(1f, 0.5f, disabledT);

        // Interpolate button state style properties
        var defaultShadow = bt.Shadow;
        var hoverShadow = bt.Hover.Shadow ?? defaultShadow;
        var pressedShadow = bt.Pressed.Shadow ?? defaultShadow;

        // Shadow — blend between states
        var shadow = defaultShadow;
        if (hoverT > 0.001f && hoverShadow is not null && defaultShadow is not null)
        {
            shadow = ShadowSpec.Lerp(defaultShadow, hoverShadow, hoverT);
        }

        if (pressT > 0.001f && pressedShadow is not null && shadow is not null)
        {
            shadow = ShadowSpec.Lerp(shadow, pressedShadow, pressT);
        }

        if (shadow is not null)
        {
            PaintShadow(shadow, bounds, bt.Radius);
        }

        // Background
        float bgOpacity = 1f;
        if (hoverT > 0.001f && bt.Hover.BackgroundOpacity is { } hoverBgOp)
        {
            bgOpacity = LerpF(bgOpacity, hoverBgOp, hoverT);
        }

        if (pressT > 0.001f && bt.Pressed.BackgroundOpacity is { } pressBgOp)
        {
            bgOpacity = LerpF(bgOpacity, pressBgOp, pressT);
        }

        if (disabledT > 0.001f && bt.Disabled.BackgroundOpacity is { } disBgOp)
        {
            bgOpacity = LerpF(bgOpacity, disBgOp, disabledT);
        }

        var bgBrush = bt.Background;
        PaintBrush(bgBrush, bounds, bt.Radius, bgOpacity * opacity);

        // Overlay — hover and press blended
        if (hoverT > 0.001f && bt.Hover.OverlayColor is { } hoverOverlay)
        {
            float a = hoverOverlay.A * hoverT;
            ctx.DrawRect(bounds, fill: hoverOverlay.Opacity(a), radius: bt.Radius);
        }

        if (pressT > 0.001f && bt.Pressed.OverlayColor is { } pressOverlay)
        {
            float a = pressOverlay.A * pressT;
            ctx.DrawRect(bounds, fill: pressOverlay.Opacity(a), radius: bt.Radius);
        }

        // Border — use default, blend toward active state if needed
        var border = bt.Border;
        var borderWidth = bt.BorderWidth;
        if (border is not null && borderWidth > 0)
        {
            PaintBrushStroke(border, bounds, borderWidth, bt.Radius);
        }

        // Divider line between primary and arrow zones
        var textColor = bt.TextColor.Opacity(opacity);
        var dividerColor = textColor.Opacity(0.3f);
        float dividerPad = 6f;
        ctx.DrawLine(
            new Point(dividerX, bounds.Y + dividerPad),
            new Point(dividerX, bounds.Y + bounds.Height - dividerPad),
            new Stroke(dividerColor, 1f));

        // Primary label text
        var primaryBounds = new Rect(bounds.X, bounds.Y, bounds.Width - arrowZoneWidth, bounds.Height);
        PaintText(sb.Label.Resolve(), primaryBounds, bt.PaddingH, textColor,
            alignment: TextAlignment.Center,
            fontWeight: bt.TextStyle.Weight);

        // Chevron in arrow zone — the 90° Apple chevron used by dropdowns/steppers.
        // PaintChevronGlyph draws the chevron at ~half the box width, so size the box to
        // the arrow zone (not the small ChevronSize) to match the dropdown chevrons.
        float chevBox = MathF.Min(arrowZoneWidth - 8f, bounds.Height - 12f);
        float chevronX = dividerX + (arrowZoneWidth - chevBox) / 2f;
        float chevronY = bounds.Y + (bounds.Height - chevBox) / 2f;
        PaintChevronGlyph(new Rect(chevronX, chevronY, chevBox, chevBox),
            textColor, pointUp: false, strokeWidth: 1.75f);

        // Focus ring when open
        if (sb.IsOpen)
        {
            float outlineOffset = 2f;
            var outlineRect = new Rect(
                bounds.X - outlineOffset,
                bounds.Y - outlineOffset,
                bounds.Width + outlineOffset * 2,
                bounds.Height + outlineOffset * 2);
            ctx.DrawRect(outlineRect, stroke: new Stroke(st.FocusRingColor, st.FocusRingWidth),
                radius: bt.Radius + outlineOffset);
        }

        // Dropdown overlay (deferred)
        if (sb.IsOpen && !sb.IsDisabled && sb.Items.Count > 0)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintSplitButtonDropdown(sb, absTrigger);
            });
        }
        else
        {
            sb.DropdownBounds = default;
        }
    }

    private void PaintSplitButtonDropdown(SplitButton sb, Rect triggerBounds)
    {
        var st = theme.Select;
        float gap = 4f;
        float itemHeight = st.ItemHeight;
        sb.MenuItemHeight = itemHeight;

        // Count non-separator items for sizing; separators are thin
        float separatorHeight = 9f;
        float totalHeight = 0f;
        float maxLabelW = 0f;
        float maxShortcutW = 0f;
        float fontSize = theme.Typography.Body.Size;

        foreach (var item in sb.Items)
        {
            if (item.Label == null)
            {
                totalHeight += separatorHeight;
                continue;
            }

            totalHeight += itemHeight;
            float labelW = ctx.MeasureText(item.Label, fontSize).Width;
            if (labelW > maxLabelW)
            {
                maxLabelW = labelW;
            }

            if (item.Shortcut is { } sc)
            {
                float scW = ctx.MeasureText(sc, 12f).Width;
                if (scW > maxShortcutW)
                {
                    maxShortcutW = scW;
                }
            }
        }

        // Inset the items from the rounded container so text and highlights get
        // breathing room instead of sitting flush against the top/bottom edges and
        // the corner radius — matching how native menus pad their item list.
        const float menuPadV = 6f;
        const float menuInsetH = 6f;

        float dropdownHeight = Math.Min(totalHeight + menuPadV * 2f, st.DropdownMaxHeight);
        float shortcutGap = maxShortcutW > 0 ? 24f : 0f;
        float measuredWidth = (st.ItemPaddingH + menuInsetH) * 2f + maxLabelW + shortcutGap + maxShortcutW;
        float dropdownWidth = Math.Max(Math.Max(triggerBounds.Width, 160f), measuredWidth);

        var dropdownBounds = new Rect(
            triggerBounds.X,
            triggerBounds.Y + triggerBounds.Height + gap,
            dropdownWidth,
            dropdownHeight);

        sb.DropdownBounds = dropdownBounds;

        // Shadow
        PaintShadow(st.DropdownShadow, dropdownBounds, st.DropdownRadius);

        // Background
        ctx.DrawRect(dropdownBounds, st.DropdownBackground, radius: st.DropdownRadius);

        // Border
        if (st.BorderWidth > 0)
        {
            ctx.DrawRect(dropdownBounds,
                stroke: new Stroke(st.BorderColor, st.BorderWidth),
                radius: st.DropdownRadius);
        }

        using var clip = ctx.PushClip(dropdownBounds);

        float itemY = dropdownBounds.Y + menuPadV;
        int highlightedIndex = sb.HighlightedIndex;

        for (int i = 0; i < sb.Items.Count; i++)
        {
            var menuItem = sb.Items[i];

            // Separator
            if (menuItem.Label == null)
            {
                float sepY = itemY + separatorHeight / 2f;
                ctx.DrawLine(
                    new Point(dropdownBounds.X + 8f, sepY),
                    new Point(dropdownBounds.X + dropdownBounds.Width - 8f, sepY),
                    new Stroke(st.BorderColor, 1f));
                itemY += separatorHeight;
                continue;
            }

            var itemBounds = new Rect(dropdownBounds.X, itemY, dropdownBounds.Width, itemHeight);

            // Highlight — inset horizontally with its own rounded corners so it
            // reads as a floating selection rather than a full-bleed bar that
            // collides with the dropdown's rounded corners.
            if (i == highlightedIndex && !menuItem.Disabled)
            {
                var highlightBounds = new Rect(
                    itemBounds.X + menuInsetH,
                    itemBounds.Y,
                    itemBounds.Width - menuInsetH * 2f,
                    itemBounds.Height);
                ctx.DrawRect(highlightBounds, st.ItemHoverBackground,
                    radius: Math.Min(8f, st.DropdownRadius));
            }

            // Text
            float textOpacity = menuItem.Disabled ? 0.4f : 1f;
            var textColor = menuItem.Style == MenuItemStyle.Destructive
                ? theme.Colors.Danger.Opacity(textOpacity)
                : st.TextColor.Opacity(textOpacity);

            PaintText(menuItem.Label, itemBounds, st.ItemPaddingH + menuInsetH, textColor);

            // Shortcut hint on the right
            if (menuItem.Shortcut is { } shortcut)
            {
                var shortcutColor = st.TextColor.Opacity(0.5f * textOpacity);
                PaintText(shortcut, itemBounds, st.ItemPaddingH + menuInsetH, shortcutColor,
                    alignment: TextAlignment.End, fontSize: 12f);
            }

            itemY += itemHeight;
        }
    }

    private void PaintLabel(Label lbl, Rect bounds)
    {
        var textColor = lbl.TextColorOverride ?? theme.Colors.Text;
        var text = lbl.Text ?? lbl.LocText.Resolve();
        var textStyle = lbl.TextStyleOverride ?? theme.Typography.Scale.Body;
        // 1px horizontal padding gives room for glyphs with negative left bearings
        // (e.g., capital J) so they don't get clipped at the label edge.
        PaintText(text, bounds, 1f, textColor,
            fontSize: textStyle.Size,
            fontWeight: textStyle.Weight,
            alignment: lbl.Alignment,
            overflow: lbl.OverflowMode,
            maxLines: lbl.MaxLineCount ?? 0);
    }

    // ── TextInput ──────────────────────────────────────────────────────

    private void PaintTextInput(TextInput ti, Rect bounds)
    {
        var t = theme.TextInput;
        bool disabled = ti.IsDisabled;

        // Set absolute bounds for mouse hit-testing in InputDispatcher
        ti.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // FocusManager.NotifyNodeReplaced transfers focus to the replacement
        // node during reconciliation, so a simple reference check suffices.
        bool focused = ReferenceEquals(FocusManager.FocusedElement, ti);

        // Animated focus/hover/disabled transitions via spring physics
        var hoverModel = t.Transition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        bool isFocused = focused && !disabled;
        var anim = ControlStateAnimator.Reconcile(
            ti, hoverModel, pressModel, isDisabled: disabled, isFocused: isFocused);

        float focusT = anim.Focus.Current;
        float hoverT = anim.Hover.Current;
        float disabledT = anim.Disabled.Current;

        // Animate placeholder visibility: fade out when text present, fade in when empty
        string text = focused && InputDispatcher.ActiveEditBuffer != null
            ? InputDispatcher.ActiveEditBuffer
            : (ti.Value.Value ?? "");

        // Password fields display a masked glyph per character rather than the raw
        // value. Masking here (not just at the draw call) keeps caret placement,
        // selection highlighting, and advance measurement consistent, because the
        // mask is one glyph per source character so all index math stays 1:1.
        // Matches the dedicated PasswordInput control's mask glyph (U+25CF).
        if (ti.InputType == InputType.Password && text.Length > 0)
        {
            text = new string('●', text.Length);
        }

        float placeholderTarget = string.IsNullOrEmpty(text) ? 0f : 1f;
        ControlStateAnimator.ReconcileValue(ti, placeholderTarget, AnimationModel.EaseOut(Duration.Ms(120)));
        float placeholderHidden = anim.Value.Current;

        // Background — the field surface never tints on focus (Apple keeps the
        // control background; emphasis comes from the thin accent border + soft ring).
        var bg = disabled ? t.DisabledBackground : t.Background;
        ctx.DrawRect(bounds, bg, radius: t.Radius);

        // Border — interpolate between default and focus states
        {
            var defaultBorderColor = disabled
                ? t.DisabledBorderColor
                : (hoverT > 0.001f
                    ? ColorValue.Lerp(t.BorderColor, t.FocusBorderColor, hoverT * 0.3f)
                    : t.BorderColor);
            var borderColor = ColorValue.Lerp(defaultBorderColor, t.FocusBorderColor, focusT);
            float borderWidth = LerpF(t.BorderWidth, t.FocusBorderWidth, focusT);
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, borderWidth), radius: t.Radius);

            // Focus ring (outer glow) — fades in with focus progress
            if (t.FocusRingWidth > 0 && focusT > 0.001f)
            {
                float ringOffset = t.FocusBorderWidth;
                var ringRect = new Rect(
                    bounds.X - ringOffset,
                    bounds.Y - ringOffset,
                    bounds.Width + ringOffset * 2,
                    bounds.Height + ringOffset * 2);
                ctx.DrawRect(ringRect,
                    stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), t.FocusRingWidth * focusT),
                    radius: t.Radius + ringOffset);
            }
        }

        // Clip content to control bounds
        using var contentClip = ctx.PushRoundedClip(bounds, t.Radius);

        float fontSize = theme.Typography.Scale.Body.Size;
        float scrollX = focused ? InputDispatcher.TextInputScrollOffsetX : 0f;
        float availableWidth = bounds.Width - t.PaddingH * 2;

        // Placeholder — fades out smoothly when text is entered
        if (placeholderHidden < 0.999f)
        {
            string placeholder = ti.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                float placeholderOpacity = 1f - placeholderHidden;
                PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor.Opacity(placeholderOpacity));
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            var textColor = disabled ? t.DisabledTextColor : t.TextColor;

            // Measure full text advance width (no wrapping)
            var textSize = ctx.MeasureTextAdvance(text, fontSize);
            float textY = bounds.Y + (bounds.Height - textSize.Height) / 2f;
            textY = MathF.Round(textY);

            // Selection highlight
            if (focused && InputDispatcher.TextInputSelectionAnchor != InputDispatcher.TextInputCaretIndex)
            {
                int selStart = Math.Min(InputDispatcher.TextInputSelectionAnchor, InputDispatcher.TextInputCaretIndex);
                int selEnd = Math.Max(InputDispatcher.TextInputSelectionAnchor, InputDispatcher.TextInputCaretIndex);
                selStart = Math.Clamp(selStart, 0, text.Length);
                selEnd = Math.Clamp(selEnd, 0, text.Length);

                string prefix = text[..selStart];
                string selected = text[selStart..selEnd];
                float hlX = bounds.X + t.PaddingH - scrollX +
                    (string.IsNullOrEmpty(prefix) ? 0f : ctx.MeasureTextAdvance(prefix, fontSize).Width);
                float hlW = string.IsNullOrEmpty(selected) ? 0f : ctx.MeasureTextAdvance(selected, fontSize).Width;
                if (hlW > 0)
                {
                    float hlPadY = 4f;
                    ctx.DrawRect(new Rect(hlX, bounds.Y + hlPadY, hlW, bounds.Height - hlPadY * 2),
                        theme.Colors.Primary.Opacity(0.3f));
                }
            }

            // Draw text with infinite maxWidth (never wraps) and apply horizontal scroll
            float textX = MathF.Round(bounds.X + t.PaddingH - scrollX);
            ctx.DrawText(text, textX, textY, fontSize, textColor,
                maxWidth: float.PositiveInfinity, maxLines: 1);
        }

        // Caret when focused — smooth sinusoidal fade blink
        if (focused && !disabled)
        {
            var caret = theme.Caret;
            double blinkMs = caret.BlinkInterval.TotalMilliseconds;
            double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;

            // Caret is fully visible for the first blink cycle after a keystroke,
            // then fades in/out sinusoidally for a smooth, Apple-like pulse.
            float caretOpacity;
            if (elapsed < blinkMs)
            {
                caretOpacity = 1f;
            }
            else
            {
                double phase = (elapsed % blinkMs) / blinkMs * Math.PI * 2.0;
                caretOpacity = (float)(0.5 + 0.5 * Math.Cos(phase));
            }

            if (caretOpacity > 0.01f)
            {
                int caretIdx = Math.Clamp(InputDispatcher.TextInputCaretIndex, 0, text.Length);
                string beforeCaret = text[..caretIdx];
                float caretTextWidth = string.IsNullOrEmpty(beforeCaret)
                    ? 0f
                    : ctx.MeasureTextAdvance(beforeCaret, fontSize).Width;
                float caretX = bounds.X + t.PaddingH + caretTextWidth - scrollX;
                float caretPadY = 6f;
                float caretY = bounds.Y + caretPadY;
                float caretH = bounds.Height - caretPadY * 2;

                ctx.DrawRect(
                    new Rect(caretX, caretY, caret.Width, caretH),
                    caret.Color.Opacity(caretOpacity));

                // Auto-scroll horizontally to keep caret visible
                float caretRelX = caretTextWidth;
                if (caretRelX - scrollX > availableWidth)
                {
                    InputDispatcher.TextInputScrollOffsetX = caretRelX - availableWidth;
                }
                else if (caretRelX < scrollX)
                {
                    InputDispatcher.TextInputScrollOffsetX = caretRelX;
                }
            }
        }
    }

    // ── DatePicker────────────────────────────────────────────────────

    private void PaintDatePicker(DatePicker dp, Rect bounds)
    {
        var t = theme.Select;

        // Background
        ctx.DrawRect(bounds, t.Background, radius: t.Radius);

        // Border — highlight when calendar is open
        if (t.BorderWidth > 0)
        {
            var borderColor = dp.IsCalendarOpen ? theme.Colors.Primary : t.BorderColor;
            ctx.DrawRect(bounds,
                stroke: new Stroke(borderColor, t.BorderWidth),
                radius: t.Radius);
        }

        // Display text — formatted date or placeholder
        DateOnly? value = dp.Value.Value;
        if (value.HasValue)
        {
            string formatted = dp.Format is not null
                ? value.Value.ToString(dp.Format, System.Globalization.CultureInfo.CurrentCulture)
                : value.Value.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
            PaintText(formatted, FieldTextBounds(bounds), t.PaddingH, t.TextColor,
                overflow: TextOverflow.Ellipsis);
        }
        else
        {
            string placeholder = dp.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                PaintText(placeholder, FieldTextBounds(bounds), t.PaddingH, t.PlaceholderColor,
                overflow: TextOverflow.Ellipsis);
            }
        }

        // Calendar icon (right side)
        float iconSize = 14f;
        float iconX = bounds.X + bounds.Width - t.PaddingH - iconSize;
        float iconY = bounds.Y + (bounds.Height - iconSize) / 2f;
        var iconColor = dp.IsCalendarOpen ? theme.Colors.Primary : t.ChevronColor;

        // Calendar outline
        ctx.DrawRect(new Rect(iconX, iconY + 2f, iconSize, iconSize - 2f),
            stroke: new Stroke(iconColor, 1.5f), radius: 1.5f);

        // Two hanging tabs
        float tabW = 1.5f;
        ctx.DrawRect(new Rect(iconX + iconSize * 0.3f, iconY, tabW, 4f), iconColor);
        ctx.DrawRect(new Rect(iconX + iconSize * 0.7f, iconY, tabW, 4f), iconColor);

        // Header line
        float lineY = iconY + 6f;
        ctx.DrawRect(new Rect(iconX + 1f, lineY, iconSize - 2f, 1f), iconColor);

        // Day dots
        float dotSize = 2f;
        float dotSpacing = (iconSize - 4f) / 3f;
        float dotsStartX = iconX + 2f + dotSpacing * 0.5f;
        float dotsStartY = lineY + 3f;
        ctx.DrawRect(new Rect(dotsStartX, dotsStartY, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX + dotSpacing, dotsStartY, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX, dotsStartY + dotSpacing, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX + dotSpacing, dotsStartY + dotSpacing, dotSize, dotSize), iconColor);

        // Calendar popup — deferred to overlay pass so it paints on top of siblings
        // Drive open/close animation
        ControlStateAnimator.ReconcileOpen(dp, dp.IsCalendarOpen,
            AnimationModel.Spring.Snappy);
        float openT = ControlStateAnimator.GetOpenProgress(dp);

        if (dp.IsCalendarOpen || openT > 0.001f)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;
            float capturedOpenT = openT;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintCalendarDropdown(dp, absTrigger, capturedOpenT);
            });
        }
        else
        {
            dp.CalendarBounds = default;
        }
    }

    private void PaintCalendarDropdown(DatePicker dp, Rect triggerBounds, float openT = 1f)
    {
        if (dp.DisplayedMonth < 1 || dp.DisplayedMonth > 12 || dp.DisplayedYear < 1)
        {
            return;
        }

        var t = theme.Select;
        var colors = theme.Colors;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Calendar dimensions
        const float padding = 12f;
        const float headerHeight = 32f;
        const float dayHeaderHeight = 24f;
        const int cols = 7;
        const int rows = 6;
        float cellSize = 32f;
        float calWidth = cellSize * cols + padding * 2;
        float calHeight = padding + headerHeight + dayHeaderHeight + cellSize * rows + padding;
        float gap = 4f;

        // Position below trigger by default, flip above if not enough room
        float calX = triggerBounds.X + (triggerBounds.Width - calWidth) / 2f;
        float calYBelow = triggerBounds.Y + triggerBounds.Height + gap;
        float calYAbove = triggerBounds.Y - gap - calHeight;
        float viewportHeight = ViewportLogicalHeight;
        float viewportWidth = ViewportLogicalWidth;

        // Clamp X so the calendar stays on screen (right edge first, then left).
        if (calX + calWidth > viewportWidth - 4f)
        {
            calX = viewportWidth - calWidth - 4f;
        }
        if (calX < 4f)
        {
            calX = 4f;
        }

        float calY = (calYBelow + calHeight > viewportHeight && calYAbove >= 0)
            ? calYAbove
            : calYBelow;

        var calBounds = new Rect(calX, calY, calWidth, calHeight);
        dp.CalendarBounds = calBounds;
        dp.CalendarCellSize = cellSize;

        // Open animation: scale + opacity + slide
        ScopeGuard opacityScope = default;
        ScopeGuard transformScope = default;
        if (!reducedMotion && openT < 0.999f)
        {
            float scale = LerpF(0.95f, 1f, openT);
            float slideY = LerpF(-4f, 0f, openT);
            opacityScope = ctx.PushOpacity(openT);
            float cx = calBounds.X + calBounds.Width / 2f;
            float cy = calBounds.Y + calBounds.Height / 2f;
            transformScope = ctx.PushScale(scale, scale, new Point(cx, cy));
            calBounds = new Rect(calBounds.X, calBounds.Y + slideY, calBounds.Width, calBounds.Height);
        }

        // Shadow + background
        PaintShadow(t.DropdownShadow, calBounds, t.DropdownRadius);
        ctx.DrawRect(calBounds, t.DropdownBackground, radius: t.DropdownRadius);

        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(calBounds,
                stroke: new Stroke(t.BorderColor, t.BorderWidth),
                radius: t.DropdownRadius);
        }

        using var clip = ctx.PushClip(calBounds);

        float contentX = calX + padding;
        float contentY = calY + padding;
        float contentW = calWidth - padding * 2;
        float arrowSize = 20f;

        // Dispatch to view mode
        switch (dp.ViewMode)
        {
            case CalendarViewMode.Days:
                PaintCalendarDaysView(dp, colors, t, contentX, contentY, contentW, arrowSize,
                    headerHeight, dayHeaderHeight, cellSize, cols, rows);
                break;
            case CalendarViewMode.Months:
                PaintCalendarMonthsView(dp, colors, t, contentX, contentY, contentW, arrowSize,
                    headerHeight, dayHeaderHeight, cellSize, rows);
                break;
            case CalendarViewMode.Years:
                PaintCalendarYearsView(dp, colors, t, contentX, contentY, contentW, arrowSize,
                    headerHeight, dayHeaderHeight, cellSize, rows);
                break;
        }

        transformScope.Dispose();
        opacityScope.Dispose();
    }

    private void PaintCalendarDaysView(DatePicker dp, ColorSet colors, SelectTheme t,
        float contentX, float contentY, float contentW, float arrowSize,
        float headerHeight, float dayHeaderHeight, float cellSize, int cols, int rows)
    {
        // ── Header: < Month Year > ──
        string monthYearText = new DateOnly(dp.DisplayedYear, dp.DisplayedMonth, 1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);

        // Previous month arrow
        var prevBounds = new Rect(contentX, contentY, arrowSize, headerHeight);
        dp.PrevMonthBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        // Next month arrow
        var nextBounds = new Rect(contentX + contentW - arrowSize, contentY, arrowSize, headerHeight);
        dp.NextMonthBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        // Month/year label centered (clickable — switches to month view)
        var headerLabelBounds = new Rect(contentX + arrowSize, contentY,
            contentW - arrowSize * 2, headerHeight);
        dp.HeaderLabelBounds = headerLabelBounds;
        PaintCenteredText(monthYearText, headerLabelBounds, 14f, colors.Primary);

        // ── Day-of-week headers ──
        float headerY = contentY + headerHeight;
        string[] dayAbbrs = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        for (int col = 0; col < cols; col++)
        {
            var cellBounds = new Rect(contentX + col * cellSize, headerY, cellSize, dayHeaderHeight);
            PaintCenteredText(dayAbbrs[col], cellBounds, 11f, colors.TextMuted);
        }

        // ── Day grid ──
        float gridTop = headerY + dayHeaderHeight;
        dp.CalendarGridTop = gridTop;
        dp.CalendarGridLeft = contentX;

        // Compute the first date in the grid (Sunday of the week containing the 1st)
        var firstOfMonth = new DateOnly(dp.DisplayedYear, dp.DisplayedMonth, 1);
        int daysInMonth = DateTime.DaysInMonth(dp.DisplayedYear, dp.DisplayedMonth);
        int startDayOfWeek = (int)firstOfMonth.DayOfWeek; // Sunday = 0
        var gridStartDate = firstOfMonth.AddDays(-startDayOfWeek);
        dp.CalendarGridStartDate = gridStartDate;
        dp.CalendarGridCellCount = rows * cols;

        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly? selectedDate = dp.Value.Value;
        var highlightedDates = dp.HighlightedDatesList is not null
            ? new HashSet<DateOnly>(dp.HighlightedDatesList)
            : null;

        bool reducedMotion2 = ControlStateAnimator.ReducedMotion;

        // Today indicator breathing — use timestamp-based sine wave
        float breathingOpacity = 1f;
        if (!reducedMotion2)
        {
            double elapsed = Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).TotalSeconds;
            breathingOpacity = 0.6f + 0.4f * (float)Math.Sin(elapsed * 2.5);
            ControlStateAnimator.SignalActiveTransition();
        }

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int cellIndex = row * cols + col;
                var date = gridStartDate.AddDays(cellIndex);
                var cellBounds = new Rect(
                    contentX + col * cellSize,
                    gridTop + row * cellSize,
                    cellSize, cellSize);

                bool isCurrentMonth = date.Month == dp.DisplayedMonth && date.Year == dp.DisplayedYear;
                bool isToday = date == today;
                bool isSelected = selectedDate.HasValue && date == selectedDate.Value;
                bool isDisabled = !isCurrentMonth
                    || (dp.Min.HasValue && date < dp.Min.Value)
                    || (dp.Max.HasValue && date > dp.Max.Value)
                    || (dp.DisabledDatesPredicate?.Invoke(date) == true);
                bool isHovered = dp.HighlightedDay == cellIndex && !isDisabled;

                // Hover scale — expand cell slightly
                float cellScale = 1f;
                if (isHovered && !reducedMotion2)
                {
                    cellScale = 1.08f;
                }
                else if (isSelected && !reducedMotion2)
                {
                    cellScale = 0.95f;
                }

                // Cell background
                float inset = 2f;
                float scaledSize = (cellSize - inset * 2) * cellScale;
                float scaleOffset = ((cellSize - inset * 2) - scaledSize) / 2f;
                var innerBounds = new Rect(
                    cellBounds.X + inset + scaleOffset,
                    cellBounds.Y + inset + scaleOffset,
                    scaledSize, scaledSize);

                if (isSelected)
                {
                    ctx.DrawRect(innerBounds, colors.Primary, radius: scaledSize / 2f);
                }
                else if (isHovered)
                {
                    ctx.DrawRect(innerBounds, t.ItemHoverBackground, radius: scaledSize / 2f);
                }
                else if (isToday && isCurrentMonth)
                {
                    // Breathing ring for today
                    ctx.DrawRect(innerBounds,
                        stroke: new Stroke(colors.Primary.Opacity(breathingOpacity), 1.5f),
                        radius: scaledSize / 2f);
                }

                // Day number text
                string dayText = date.Day.ToString();
                ColorValue textColor;
                if (isSelected)
                {
                    textColor = colors.TextOnPrimary;
                }
                else if (isDisabled)
                {
                    textColor = colors.TextMuted.Opacity(0.3f);
                }
                else if (!isCurrentMonth)
                {
                    textColor = colors.TextMuted.Opacity(0.3f);
                }
                else
                {
                    textColor = colors.Text;
                }

                PaintCenteredText(dayText, cellBounds, 13f, textColor);

                // Highlighted date dot
                if (highlightedDates != null && highlightedDates.Contains(date) && isCurrentMonth)
                {
                    float dotR = 2f;
                    float dotX = cellBounds.X + (cellBounds.Width - dotR * 2) / 2f;
                    float dotY = cellBounds.Y + cellBounds.Height - 6f;
                    var dotColor = dp.HighlightedDatesColor == default
                        ? colors.Primary
                        : dp.HighlightedDatesColor;
                    ctx.DrawRect(new Rect(dotX, dotY, dotR * 2, dotR * 2), dotColor, radius: dotR);
                }
            }
        }
    }

    private void PaintCalendarMonthsView(DatePicker dp, ColorSet colors, SelectTheme t,
        float contentX, float contentY, float contentW, float arrowSize,
        float headerHeight, float dayHeaderHeight, float cellSize, int rows)
    {
        // ── Header: < Year > ──
        string yearText = dp.DisplayedYear.ToString();

        var prevBounds = new Rect(contentX, contentY, arrowSize, headerHeight);
        dp.PrevMonthBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        var nextBounds = new Rect(contentX + contentW - arrowSize, contentY, arrowSize, headerHeight);
        dp.NextMonthBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        // Year label (clickable — switches to year view)
        var headerLabelBounds = new Rect(contentX + arrowSize, contentY,
            contentW - arrowSize * 2, headerHeight);
        dp.HeaderLabelBounds = headerLabelBounds;
        PaintCenteredText(yearText, headerLabelBounds, 14f, colors.Primary);

        // ── Month grid: 4 cols × 3 rows ──
        float gridTop = contentY + headerHeight + dayHeaderHeight;
        dp.CalendarGridTop = gridTop;
        dp.CalendarGridLeft = contentX;
        dp.CalendarGridCellCount = 12;

        float monthCellW = contentW / 4f;
        float monthCellH = (cellSize * rows) / 3f;
        string[] monthAbbrs = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                               "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly? selectedDate = dp.Value.Value;

        for (int i = 0; i < 12; i++)
        {
            int col = i % 4;
            int row = i / 4;
            var cellBounds = new Rect(
                contentX + col * monthCellW,
                gridTop + row * monthCellH,
                monthCellW, monthCellH);

            bool isCurrentMonth = dp.DisplayedYear == today.Year && i + 1 == today.Month;
            bool isSelected = selectedDate.HasValue
                && selectedDate.Value.Year == dp.DisplayedYear
                && selectedDate.Value.Month == i + 1;
            bool isHovered = dp.HighlightedDay == i;

            if (isSelected)
            {
                float inset = 4f;
                var selBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(selBounds, colors.Primary, radius: 6f);
            }
            else if (isHovered)
            {
                float inset = 4f;
                var hoverBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(hoverBounds, t.ItemHoverBackground, radius: 6f);
            }
            else if (isCurrentMonth)
            {
                float inset = 4f;
                var todayBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(todayBounds, stroke: new Stroke(colors.Primary, 1f), radius: 6f);
            }

            ColorValue textColor = isSelected ? colors.TextOnPrimary : colors.Text;
            PaintCenteredText(monthAbbrs[i], cellBounds, 13f, textColor);
        }
    }

    private void PaintCalendarYearsView(DatePicker dp, ColorSet colors, SelectTheme t,
        float contentX, float contentY, float contentW, float arrowSize,
        float headerHeight, float dayHeaderHeight, float cellSize, int rows)
    {
        // ── Header: < YearRange > ──
        int startYear = dp.YearGridStart;
        string rangeText = $"{startYear} – {startYear + 11}";

        var prevBounds = new Rect(contentX, contentY, arrowSize, headerHeight);
        dp.PrevMonthBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        var nextBounds = new Rect(contentX + contentW - arrowSize, contentY, arrowSize, headerHeight);
        dp.NextMonthBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        // Range label (not clickable further)
        var headerLabelBounds = new Rect(contentX + arrowSize, contentY,
            contentW - arrowSize * 2, headerHeight);
        dp.HeaderLabelBounds = default;
        PaintCenteredText(rangeText, headerLabelBounds, 14f, colors.Text);

        // ── Year grid: 4 cols × 3 rows ──
        float gridTop = contentY + headerHeight + dayHeaderHeight;
        dp.CalendarGridTop = gridTop;
        dp.CalendarGridLeft = contentX;
        dp.CalendarGridCellCount = 12;

        float yearCellW = contentW / 4f;
        float yearCellH = (cellSize * rows) / 3f;

        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly? selectedDate = dp.Value.Value;

        for (int i = 0; i < 12; i++)
        {
            int year = startYear + i;
            int col = i % 4;
            int row = i / 4;
            var cellBounds = new Rect(
                contentX + col * yearCellW,
                gridTop + row * yearCellH,
                yearCellW, yearCellH);

            bool isCurrentYear = year == today.Year;
            bool isSelected = selectedDate.HasValue && selectedDate.Value.Year == year;
            bool isHovered = dp.HighlightedDay == i;

            if (isSelected)
            {
                float inset = 4f;
                var selBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(selBounds, colors.Primary, radius: 6f);
            }
            else if (isHovered)
            {
                float inset = 4f;
                var hoverBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(hoverBounds, t.ItemHoverBackground, radius: 6f);
            }
            else if (isCurrentYear)
            {
                float inset = 4f;
                var todayBounds = new Rect(cellBounds.X + inset, cellBounds.Y + inset,
                    cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                ctx.DrawRect(todayBounds, stroke: new Stroke(colors.Primary, 1f), radius: 6f);
            }

            ColorValue textColor = isSelected ? colors.TextOnPrimary : colors.Text;
            PaintCenteredText(year.ToString(), cellBounds, 13f, textColor);
        }
    }

    private void PaintCalendarArrow(Rect bounds, ColorValue color, bool isLeft)
    {
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        float size = 6f;

        var path = PathBuilder.Rent();
        if (isLeft)
        {
            path.MoveTo(new Point(cx + size * 0.4f, cy - size));
            path.LineTo(new Point(cx - size * 0.4f, cy));
            path.LineTo(new Point(cx + size * 0.4f, cy + size));
        }
        else
        {
            path.MoveTo(new Point(cx - size * 0.4f, cy - size));
            path.LineTo(new Point(cx + size * 0.4f, cy));
            path.LineTo(new Point(cx - size * 0.4f, cy + size));
        }

        ctx.DrawPath(path.BuildTransient(), stroke: new Stroke(color, 1.5f));
    }

    private void PaintCenteredText(string text, Rect bounds, float fontSize, ColorValue color)
    {
        var textSize = ctx.MeasureText(text, fontSize);
        float textX = bounds.X + (bounds.Width - textSize.Width) / 2f;
        float textY = bounds.Y + (bounds.Height - textSize.Height) / 2f;
        ctx.DrawText(text, textX, textY, fontSize, color);
    }

    // ── DateRangePicker ───────────────────────────────────────────────

    private void PaintDateRangePicker(DateRangePicker drp, Rect bounds)
    {
        var t = theme.Select;
        var colors = theme.Colors;

        if (drp.Layout == DateRangeLayout.TwoFields)
        {
            // Two side-by-side fields with gap
            float gap = 8f;
            float fieldW = (bounds.Width - gap) / 2f;
            var leftBounds = new Rect(bounds.X, bounds.Y, fieldW, bounds.Height);
            var rightBounds = new Rect(bounds.X + fieldW + gap, bounds.Y, fieldW, bounds.Height);

            PaintDateRangeField(drp, leftBounds, t, colors,
                drp.StartBind.Value, drp.StartLabel.Resolve(), "Start date",
                drp.IsCalendarOpen && drp.SelectionPhase == RangeSelectionPhase.SelectingStart);
            PaintDateRangeField(drp, rightBounds, t, colors,
                drp.EndBind.Value, drp.EndLabel.Resolve(), "End date",
                drp.IsCalendarOpen && drp.SelectionPhase == RangeSelectionPhase.SelectingEnd);
        }
        else
        {
            // Single combined field
            ctx.DrawRect(bounds, t.Background, radius: t.Radius);
            if (t.BorderWidth > 0)
            {
                var borderColor = drp.IsCalendarOpen ? colors.Primary : t.BorderColor;
                ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth), radius: t.Radius);
            }

            DateOnly? start = drp.StartBind.Value;
            DateOnly? end = drp.EndBind.Value;
            string displayText;
            ColorValue textColor;

            if (start.HasValue && end.HasValue)
            {
                displayText = $"{start.Value:MMM d, yyyy} — {end.Value:MMM d, yyyy}";
                textColor = t.TextColor;
            }
            else if (start.HasValue)
            {
                displayText = $"{start.Value:MMM d, yyyy} — …";
                textColor = t.TextColor;
            }
            else
            {
                displayText = "Select date range";
                textColor = t.PlaceholderColor;
            }

            PaintText(displayText, FieldTextBounds(bounds), t.PaddingH, textColor,
                overflow: TextOverflow.Ellipsis);

            // Calendar icon (right side)
            PaintCalendarIcon(bounds, t.PaddingH, drp.IsCalendarOpen ? colors.Primary : t.ChevronColor);
        }

        // Calendar popup — deferred to overlay pass
        ControlStateAnimator.ReconcileOpen(drp, drp.IsCalendarOpen,
            AnimationModel.Spring.Snappy);
        float drpOpenT = ControlStateAnimator.GetOpenProgress(drp);

        if (drp.IsCalendarOpen || drpOpenT > 0.001f)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;
            float capturedOpenT = drpOpenT;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintDateRangeDropdown(drp, absTrigger, capturedOpenT);
            });
        }
        else
        {
            drp.CalendarBounds = default;
        }
    }

    private void PaintDateRangeField(DateRangePicker drp, Rect bounds, SelectTheme t,
        ColorSet colors, DateOnly? value, string label, string placeholder, bool isActive)
    {
        ctx.DrawRect(bounds, t.Background, radius: t.Radius);
        if (t.BorderWidth > 0)
        {
            var borderColor = isActive ? colors.Primary : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth), radius: t.Radius);
        }

        // Small caption above the value if a label is provided. The field is measured
        // taller (54px) for this stacked layout so the value doesn't clip.
        if (!string.IsNullOrEmpty(label))
        {
            ctx.DrawText(label, bounds.X + t.PaddingH, bounds.Y + 8f, 11f, colors.TextMuted);

            // Value fills the area below the caption, optically centred by PaintText.
            const float valueSize = 15f;
            float valueTop = bounds.Y + 24f;
            var valueBounds = new Rect(bounds.X, valueTop, bounds.Width, bounds.Y + bounds.Height - valueTop - 4f);
            if (value.HasValue)
            {
                PaintText(value.Value.ToString("MMM d, yyyy"), valueBounds, t.PaddingH, t.TextColor,
                    fontSize: valueSize, overflow: TextOverflow.Ellipsis);
            }
            else
            {
                PaintText(placeholder, valueBounds, t.PaddingH, t.PlaceholderColor,
                    fontSize: valueSize, overflow: TextOverflow.Ellipsis);
            }
        }
        else
        {
            if (value.HasValue)
            {
                PaintText(value.Value.ToString("MMM d, yyyy"), bounds, t.PaddingH, t.TextColor);
            }
            else
            {
                PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor);
            }
        }
    }

    private void PaintCalendarIcon(Rect bounds, float paddingH, ColorValue iconColor)
    {
        float iconSize = 14f;
        float iconX = bounds.X + bounds.Width - paddingH - iconSize;
        float iconY = bounds.Y + (bounds.Height - iconSize) / 2f;

        ctx.DrawRect(new Rect(iconX, iconY + 2f, iconSize, iconSize - 2f),
            stroke: new Stroke(iconColor, 1.5f), radius: 1.5f);

        float tabW = 1.5f;
        ctx.DrawRect(new Rect(iconX + iconSize * 0.3f, iconY, tabW, 4f), iconColor);
        ctx.DrawRect(new Rect(iconX + iconSize * 0.7f, iconY, tabW, 4f), iconColor);

        float lineY = iconY + 6f;
        ctx.DrawRect(new Rect(iconX + 1f, lineY, iconSize - 2f, 1f), iconColor);

        float dotSize = 2f;
        float dotSpacing = (iconSize - 4f) / 3f;
        float dotsStartX = iconX + 2f + dotSpacing * 0.5f;
        float dotsStartY = lineY + 3f;
        ctx.DrawRect(new Rect(dotsStartX, dotsStartY, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX + dotSpacing, dotsStartY, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX, dotsStartY + dotSpacing, dotSize, dotSize), iconColor);
        ctx.DrawRect(new Rect(dotsStartX + dotSpacing, dotsStartY + dotSpacing, dotSize, dotSize), iconColor);
    }

    private void PaintDateRangeDropdown(DateRangePicker drp, Rect triggerBounds, float openT = 1f)
    {
        // Guard against invalid state — can happen during closing animation after
        // reconciliation creates a new node with uninitialized calendar fields.
        if (drp.DisplayedMonth < 1 || drp.DisplayedMonth > 12 || drp.DisplayedYear < 1)
        {
            return;
        }

        var t = theme.Select;
        var colors = theme.Colors;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        const float padding = 12f;
        const float headerHeight = 32f;
        const float dayHeaderHeight = 24f;
        const int cols = 7;
        const int rows = 6;
        float cellSize = 32f;
        float calGap = 16f;
        float singleCalWidth = cellSize * cols;
        float calendarAreaWidth = singleCalWidth * 2 + calGap + padding * 2;

        // Presets sidebar
        float presetsWidth = 0f;
        bool hasPresets = drp.PresetRanges is { Count: > 0 };
        if (hasPresets)
        {
            presetsWidth = 130f + padding;
        }

        float totalWidth = calendarAreaWidth + presetsWidth;
        float calHeight = padding + headerHeight + dayHeaderHeight + cellSize * rows + padding;
        float gap = 4f;

        // Position below trigger, flip above if needed
        float calX = triggerBounds.X + (triggerBounds.Width - totalWidth) / 2f;
        float calYBelow = triggerBounds.Y + triggerBounds.Height + gap;
        float calYAbove = triggerBounds.Y - gap - calHeight;
        float viewportHeight = ViewportLogicalHeight;

        // Clamp X so popup stays on screen
        float viewportWidth = ViewportLogicalWidth;
        if (calX + totalWidth > viewportWidth - 4f)
        {
            calX = viewportWidth - totalWidth - 4f;
        }

        if (calX < 4f)
        {
            calX = 4f;
        }

        float calY = (calYBelow + calHeight > viewportHeight && calYAbove >= 0)
            ? calYAbove
            : calYBelow;

        var calBounds = new Rect(calX, calY, totalWidth, calHeight);
        drp.CalendarBounds = calBounds;
        drp.CalendarCellSize = cellSize;

        // Open animation: scale + opacity + slide
        ScopeGuard opacityScope2 = default;
        ScopeGuard transformScope2 = default;
        if (!reducedMotion && openT < 0.999f)
        {
            float scale2 = LerpF(0.95f, 1f, openT);
            float slideY2 = LerpF(-4f, 0f, openT);
            opacityScope2 = ctx.PushOpacity(openT);
            float cx2 = calBounds.X + calBounds.Width / 2f;
            float cy2 = calBounds.Y + calBounds.Height / 2f;
            transformScope2 = ctx.PushScale(scale2, scale2, new Point(cx2, cy2));
            calBounds = new Rect(calBounds.X, calBounds.Y + slideY2, calBounds.Width, calBounds.Height);
        }

        // Shadow + background
        PaintShadow(t.DropdownShadow, calBounds, t.DropdownRadius);
        ctx.DrawRect(calBounds, t.DropdownBackground, radius: t.DropdownRadius);
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(calBounds, stroke: new Stroke(t.BorderColor, t.BorderWidth), radius: t.DropdownRadius);
        }

        using var clip = ctx.PushClip(calBounds);

        // ── Presets sidebar ──
        float calendarStartX = calX + padding;
        if (hasPresets)
        {
            float presetX = calX + padding;
            float presetY = calY + padding;
            float presetW = presetsWidth - padding;
            var presetBounds = new Rect[drp.PresetRanges!.Count];

            for (int i = 0; i < drp.PresetRanges.Count; i++)
            {
                var preset = drp.PresetRanges[i];
                var rowBounds = new Rect(presetX, presetY, presetW, 28f);
                presetBounds[i] = rowBounds;

                bool isActive = drp.StartBind.Value.HasValue && drp.EndBind.Value.HasValue
                    && drp.StartBind.Value.Value == preset.Start
                    && drp.EndBind.Value.Value == preset.End;
                bool isHovered = drp.HighlightedPreset == i;

                if (isActive)
                {
                    ctx.DrawRect(rowBounds, colors.Primary.Opacity(0.15f), radius: 4f);
                }
                else if (isHovered)
                {
                    ctx.DrawRect(rowBounds, t.ItemHoverBackground, radius: 4f);
                }

                var labelColor = isActive ? colors.Primary : colors.Text;
                PaintText(preset.Label, rowBounds, 8f, labelColor, fontSize: 12f);
                presetY += 30f;
            }

            drp.PresetBounds = presetBounds;

            // Vertical separator line between presets and calendars
            float sepX = calX + presetsWidth - 1f;
            ctx.DrawRect(new Rect(sepX, calY + padding, 1f, calHeight - padding * 2),
                colors.TextMuted.Opacity(0.2f));

            calendarStartX = calX + presetsWidth;
        }
        else
        {
            drp.PresetBounds = [];
        }

        // ── Left calendar header ──
        float leftCalX = calendarStartX;
        float contentY = calY + padding;

        // Navigation arrows: < on far left, > on far right of the whole dual-calendar area
        float arrowSize = 20f;
        float dualCalWidth = singleCalWidth * 2 + calGap;

        var prevBounds = new Rect(leftCalX, contentY, arrowSize, headerHeight);
        drp.PrevMonthBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        var nextBounds = new Rect(leftCalX + dualCalWidth - arrowSize, contentY, arrowSize, headerHeight);
        drp.NextMonthBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        // Left month label
        string leftMonthText = new DateOnly(drp.DisplayedYear, drp.DisplayedMonth, 1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
        var leftHeaderBounds = new Rect(leftCalX + arrowSize, contentY,
            singleCalWidth - arrowSize, headerHeight);
        PaintCenteredText(leftMonthText, leftHeaderBounds, 13f, colors.Text);

        // Right month label
        var (rightMonth, rightYear) = drp.RightMonth();
        string rightMonthText = new DateOnly(rightYear, rightMonth, 1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
        float rightCalX = leftCalX + singleCalWidth + calGap;
        var rightHeaderBounds = new Rect(rightCalX, contentY,
            singleCalWidth - arrowSize, headerHeight);
        PaintCenteredText(rightMonthText, rightHeaderBounds, 13f, colors.Text);

        // ── Day-of-week headers for both calendars ──
        float headerY = contentY + headerHeight;
        string[] dayAbbrs = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        for (int col = 0; col < cols; col++)
        {
            var leftCell = new Rect(leftCalX + col * cellSize, headerY, cellSize, dayHeaderHeight);
            PaintCenteredText(dayAbbrs[col], leftCell, 11f, colors.TextMuted);

            var rightCell = new Rect(rightCalX + col * cellSize, headerY, cellSize, dayHeaderHeight);
            PaintCenteredText(dayAbbrs[col], rightCell, 11f, colors.TextMuted);
        }

        // ── Day grids ──
        float gridTop = headerY + dayHeaderHeight;
        drp.CalendarGridTop = gridTop;
        drp.LeftGridLeft = leftCalX;
        drp.RightGridLeft = rightCalX;

        // Compute grid start dates
        var leftFirst = new DateOnly(drp.DisplayedYear, drp.DisplayedMonth, 1);
        int leftStartDow = (int)leftFirst.DayOfWeek;
        drp.LeftGridStartDate = leftFirst.AddDays(-leftStartDow);

        var rightFirst = new DateOnly(rightYear, rightMonth, 1);
        int rightStartDow = (int)rightFirst.DayOfWeek;
        drp.RightGridStartDate = rightFirst.AddDays(-rightStartDow);

        // Determine the effective range for highlighting
        DateOnly? rangeStart = drp.StartBind.Value;
        DateOnly? rangeEnd = drp.EndBind.Value;
        DateOnly? previewEnd = null;

        if (rangeStart.HasValue && !rangeEnd.HasValue && drp.HoverDate.HasValue)
        {
            previewEnd = drp.HoverDate.Value;
            if (previewEnd < rangeStart)
            {
                // Swap for preview: highlight from hovered to start
                var tempStart = previewEnd;
                previewEnd = rangeStart;
                rangeStart = tempStart;
            }
        }

        DateOnly? highlightStart = rangeStart;
        DateOnly? highlightEnd = rangeEnd ?? previewEnd;
        bool isPreview = !drp.EndBind.Value.HasValue && drp.HoverDate.HasValue;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Paint left calendar days
        PaintDateRangeDaysGrid(drp, leftCalX, gridTop, cellSize, cols, rows,
            drp.LeftGridStartDate, drp.DisplayedMonth, drp.DisplayedYear,
            highlightStart, highlightEnd, isPreview, today, colors, t,
            isLeftCalendar: true);

        // Paint right calendar days
        PaintDateRangeDaysGrid(drp, rightCalX, gridTop, cellSize, cols, rows,
            drp.RightGridStartDate, rightMonth, rightYear,
            highlightStart, highlightEnd, isPreview, today, colors, t,
            isLeftCalendar: false);

        transformScope2.Dispose();
        opacityScope2.Dispose();
    }

    private void PaintDateRangeDaysGrid(DateRangePicker drp, float gridX, float gridTop,
        float cellSize, int cols, int rows, DateOnly gridStartDate,
        int displayedMonth, int displayedYear,
        DateOnly? rangeStart, DateOnly? rangeEnd, bool isPreview,
        DateOnly today, ColorSet colors, SelectTheme t, bool isLeftCalendar)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int cellIndex = row * cols + col;
                var date = gridStartDate.AddDays(cellIndex);
                var cellBounds = new Rect(
                    gridX + col * cellSize,
                    gridTop + row * cellSize,
                    cellSize, cellSize);

                bool isCurrentMonth = date.Month == displayedMonth && date.Year == displayedYear;
                bool isToday = date == today;
                bool isDisabled = !isCurrentMonth
                    || (drp.Min.HasValue && date < drp.Min.Value)
                    || (drp.Max.HasValue && date > drp.Max.Value);

                bool isStart = rangeStart.HasValue && date == rangeStart.Value;
                bool isEnd = rangeEnd.HasValue && date == rangeEnd.Value;
                bool isInRange = rangeStart.HasValue && rangeEnd.HasValue
                    && date > rangeStart.Value && date < rangeEnd.Value;

                int hoveredIndex = isLeftCalendar ? drp.HighlightedDayLeft : drp.HighlightedDayRight;
                bool isHovered = hoveredIndex == cellIndex && !isDisabled;

                float inset = 2f;

                // Range band background (rectangular for in-range, rounded ends for start/end)
                if (isInRange && isCurrentMonth)
                {
                    var bandColor = isPreview
                        ? colors.Primary.Opacity(0.08f)
                        : colors.Primary.Opacity(0.12f);
                    ctx.DrawRect(new Rect(cellBounds.X, cellBounds.Y + inset,
                        cellBounds.Width, cellBounds.Height - inset * 2), bandColor);
                }

                if ((isStart || isEnd) && isCurrentMonth)
                {
                    // Half-band: start gets right half, end gets left half
                    if (isStart && rangeEnd.HasValue && date != rangeEnd.Value)
                    {
                        var bandColor = isPreview
                            ? colors.Primary.Opacity(0.08f)
                            : colors.Primary.Opacity(0.12f);
                        ctx.DrawRect(new Rect(cellBounds.X + cellBounds.Width / 2f, cellBounds.Y + inset,
                            cellBounds.Width / 2f, cellBounds.Height - inset * 2), bandColor);
                    }

                    if (isEnd && rangeStart.HasValue && date != rangeStart.Value)
                    {
                        var bandColor = isPreview
                            ? colors.Primary.Opacity(0.08f)
                            : colors.Primary.Opacity(0.12f);
                        ctx.DrawRect(new Rect(cellBounds.X, cellBounds.Y + inset,
                            cellBounds.Width / 2f, cellBounds.Height - inset * 2), bandColor);
                    }

                    // Circular highlight on start/end date
                    var circleBounds = new Rect(
                        cellBounds.X + inset, cellBounds.Y + inset,
                        cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                    ctx.DrawRect(circleBounds, colors.Primary, radius: cellSize / 2f);
                }
                else if (isHovered)
                {
                    var hoverBounds = new Rect(
                        cellBounds.X + inset, cellBounds.Y + inset,
                        cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                    ctx.DrawRect(hoverBounds, t.ItemHoverBackground, radius: cellSize / 2f);
                }
                else if (isToday && isCurrentMonth)
                {
                    var todayBounds = new Rect(
                        cellBounds.X + inset, cellBounds.Y + inset,
                        cellBounds.Width - inset * 2, cellBounds.Height - inset * 2);
                    ctx.DrawRect(todayBounds,
                        stroke: new Stroke(colors.Primary, 1f),
                        radius: cellSize / 2f);
                }

                // Day number text
                string dayText = date.Day.ToString();
                ColorValue textColor;
                if (isStart || isEnd)
                {
                    textColor = isCurrentMonth ? colors.TextOnPrimary : colors.TextMuted.Opacity(0.3f);
                }
                else if (isDisabled || !isCurrentMonth)
                {
                    textColor = colors.TextMuted.Opacity(0.3f);
                }
                else
                {
                    textColor = colors.Text;
                }

                PaintCenteredText(dayText, cellBounds, 13f, textColor);
            }
        }
    }

    // ── DateTimePicker ────────────────────────────────────────────────

    private void PaintDateTimePicker(DateTimePicker dtp, Rect bounds)
    {
        var t = theme.Select;

        // Background
        ctx.DrawRect(bounds, t.Background, radius: t.Radius);

        // Border — highlight when calendar is open
        if (t.BorderWidth > 0)
        {
            var borderColor = dtp.IsCalendarOpen ? theme.Colors.Primary : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth), radius: t.Radius);
        }

        // Display text — formatted date+time or placeholder
        DateTime? value = dtp.WorkingValue;
        if (value.HasValue)
        {
            string formatStr = dtp.Format ?? dtp.DefaultFormat;
            string formatted = value.Value.ToString(formatStr, System.Globalization.CultureInfo.CurrentCulture);
            PaintText(formatted, FieldTextBounds(bounds), t.PaddingH, t.TextColor,
                overflow: TextOverflow.Ellipsis);
        }
        else
        {
            string placeholder = dtp.PlaceholderText.Resolve();
            if (string.IsNullOrEmpty(placeholder))
            {
                placeholder = "Select date & time";
            }
            PaintText(placeholder, FieldTextBounds(bounds), t.PaddingH, t.PlaceholderColor,
                overflow: TextOverflow.Ellipsis);
        }

        // Calendar icon (right side)
        PaintCalendarIcon(bounds, t.PaddingH, dtp.IsCalendarOpen ? theme.Colors.Primary : t.ChevronColor);

        // Calendar + time popup — deferred to overlay pass
        ControlStateAnimator.ReconcileOpen(dtp, dtp.IsCalendarOpen,
            AnimationModel.Spring.Snappy);
        float dtpOpenT = ControlStateAnimator.GetOpenProgress(dtp);

        if (dtp.IsCalendarOpen || dtpOpenT > 0.001f)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;
            float capturedOpenT = dtpOpenT;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintDateTimeDropdown(dtp, absTrigger, capturedOpenT);
            });
        }
        else
        {
            dtp.CalendarBounds = default;
        }
    }

    private void PaintDateTimeDropdown(DateTimePicker dtp, Rect triggerBounds, float openT = 1f)
    {
        if (dtp.DisplayedMonth < 1 || dtp.DisplayedMonth > 12 || dtp.DisplayedYear < 1)
        {
            return;
        }

        var t = theme.Select;
        var colors = theme.Colors;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Calendar dimensions (same as DatePicker)
        const float padding = 12f;
        const float headerHeight = 32f;
        const float dayHeaderHeight = 24f;
        const int cols = 7;
        const int rows = 6;
        float cellSize = 32f;
        float calWidth = cellSize * cols + padding * 2;

        // Time section height
        const float timeSeparatorHeight = 1f;
        const float timeSectionHeight = 48f;
        float timeHeight = padding + timeSeparatorHeight + timeSectionHeight + padding;

        float calHeight = padding + headerHeight + dayHeaderHeight + cellSize * rows + timeHeight + padding;
        float gap = 4f;

        // Position below trigger
        float calX = triggerBounds.X + (triggerBounds.Width - calWidth) / 2f;
        float calYBelow = triggerBounds.Y + triggerBounds.Height + gap;
        float calYAbove = triggerBounds.Y - gap - calHeight;
        float viewportHeight = ViewportLogicalHeight;
        float viewportWidth = ViewportLogicalWidth;

        // Clamp X
        if (calX + calWidth > viewportWidth - 4f)
        {
            calX = viewportWidth - calWidth - 4f;
        }
        if (calX < 4f)
        {
            calX = 4f;
        }

        float calY = (calYBelow + calHeight > viewportHeight && calYAbove >= 0)
            ? calYAbove
            : calYBelow;

        var calBounds = new Rect(calX, calY, calWidth, calHeight);
        dtp.CalendarBounds = calBounds;
        dtp.CalendarCellSize = cellSize;

        // Open animation: scale + opacity + slide
        ScopeGuard opacityScope3 = default;
        ScopeGuard transformScope3 = default;
        if (!reducedMotion && openT < 0.999f)
        {
            float scale3 = LerpF(0.95f, 1f, openT);
            float slideY3 = LerpF(-4f, 0f, openT);
            opacityScope3 = ctx.PushOpacity(openT);
            float cx3 = calBounds.X + calBounds.Width / 2f;
            float cy3 = calBounds.Y + calBounds.Height / 2f;
            transformScope3 = ctx.PushScale(scale3, scale3, new Point(cx3, cy3));
            calBounds = new Rect(calBounds.X, calBounds.Y + slideY3, calBounds.Width, calBounds.Height);
        }

        // Shadow + background
        PaintShadow(t.DropdownShadow, calBounds, t.DropdownRadius);
        ctx.DrawRect(calBounds, t.DropdownBackground, radius: t.DropdownRadius);
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(calBounds, stroke: new Stroke(t.BorderColor, t.BorderWidth), radius: t.DropdownRadius);
        }

        using var clip = ctx.PushClip(calBounds);

        float contentX = calX + padding;
        float contentY = calY + padding;
        float contentW = calWidth - padding * 2;
        float arrowSize = 20f;

        // ── Calendar header: < Month Year > ──
        string monthYearText = new DateOnly(dtp.DisplayedYear, dtp.DisplayedMonth, 1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);

        var prevBounds = new Rect(contentX, contentY, arrowSize, headerHeight);
        dtp.PrevMonthBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        var nextBounds = new Rect(contentX + contentW - arrowSize, contentY, arrowSize, headerHeight);
        dtp.NextMonthBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        PaintCenteredText(monthYearText,
            new Rect(contentX + arrowSize, contentY, contentW - arrowSize * 2, headerHeight),
            13f, colors.Text);

        // ── Day-of-week headers ──
        float dayHeaderY = contentY + headerHeight;
        string[] dayNames = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        for (int i = 0; i < 7; i++)
        {
            float dx = contentX + i * cellSize;
            PaintCenteredText(dayNames[i],
                new Rect(dx, dayHeaderY, cellSize, dayHeaderHeight),
                11f, colors.TextMuted);
        }

        // ── Day grid ──
        float gridTop = dayHeaderY + dayHeaderHeight;
        dtp.CalendarGridTop = gridTop;
        dtp.CalendarGridLeft = contentX;

        // Compute first day in grid (start on Sunday before the 1st)
        var firstOfMonth = new DateOnly(dtp.DisplayedYear, dtp.DisplayedMonth, 1);
        int startOffset = (int)firstOfMonth.DayOfWeek;
        var gridStart = firstOfMonth.AddDays(-startOffset);
        dtp.CalendarGridStartDate = gridStart;

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly? selectedDate = dtp.Value.Value.HasValue
            ? DateOnly.FromDateTime(dtp.Value.Value.Value)
            : null;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int cellIndex = r * cols + c;
                var date = gridStart.AddDays(cellIndex);
                float cx = contentX + c * cellSize;
                float cy = gridTop + r * cellSize;
                var cellBounds = new Rect(cx, cy, cellSize, cellSize);

                bool isCurrentMonth = date.Month == dtp.DisplayedMonth && date.Year == dtp.DisplayedYear;
                bool isToday = date == today;
                bool isSelected = selectedDate.HasValue && date == selectedDate.Value;
                bool isDisabled = !isCurrentMonth
                    || (dtp.MinDate.HasValue && date < dtp.MinDate.Value)
                    || (dtp.MaxDate.HasValue && date > dtp.MaxDate.Value)
                    || (dtp.DisabledDatesPredicate?.Invoke(date) == true);
                bool isHovered = isCurrentMonth && cellIndex == dtp.HighlightedDay;

                // Background
                if (isSelected)
                {
                    float circleR = cellSize / 2f - 2f;
                    var center = new Point(cx + cellSize / 2f, cy + cellSize / 2f);
                    ctx.DrawCircle(center, circleR, colors.Primary);
                }
                else if (isToday && isCurrentMonth)
                {
                    float circleR = cellSize / 2f - 2f;
                    var center = new Point(cx + cellSize / 2f, cy + cellSize / 2f);
                    ctx.DrawCircle(center, circleR, stroke: new Stroke(colors.Primary, 1.5f));
                }
                else if (isHovered && !isDisabled)
                {
                    float circleR = cellSize / 2f - 2f;
                    var center = new Point(cx + cellSize / 2f, cy + cellSize / 2f);
                    ctx.DrawCircle(center, circleR, colors.Primary.Opacity(0.15f));
                }

                // Text
                var textColor = isSelected ? colors.TextOnPrimary
                    : isDisabled ? colors.TextMuted.Opacity(0.3f)
                    : !isCurrentMonth ? colors.TextMuted.Opacity(0.3f)
                    : colors.Text;

                PaintCenteredText(date.Day.ToString(), cellBounds, 13f, textColor);
            }
        }

        // ── Time section ──
        float timeSepY = gridTop + rows * cellSize + padding;
        ctx.DrawRect(new Rect(calX + padding, timeSepY, calWidth - padding * 2, timeSeparatorHeight),
            colors.TextMuted.Opacity(0.2f));

        float timeY = timeSepY + timeSeparatorHeight + padding * 0.5f;
        float timeCenterX = calX + calWidth / 2f;

        // Time display: [▲] HH : [▲] MM [AM/PM]
        //               [▼]      [▼]
        float spinnerW = 44f;
        float spinnerH = timeSectionHeight;
        float colonW = 16f;
        bool is12h = dtp.TimeFormatValue == TimeFormat.Hour12;
        float ampmW = is12h ? 40f : 0f;
        float totalTimeW = spinnerW * 2 + colonW + (is12h ? 8f + ampmW : 0f);
        float timeStartX = timeCenterX - totalTimeW / 2f;

        // Hour spinner
        float hourX = timeStartX;
        PaintTimeSpinner(dtp, hourX, timeY, spinnerW, spinnerH, dtp.DisplayHour,
            isHour: true, colors);

        // Colon
        float colonX = hourX + spinnerW;
        PaintCenteredText(":",
            new Rect(colonX, timeY, colonW, spinnerH),
            18f, colors.Text);

        // Minute spinner
        float minX = colonX + colonW;
        PaintTimeSpinner(dtp, minX, timeY, spinnerW, spinnerH, dtp.SelectedMinute,
            isHour: false, colors);

        // AM/PM toggle
        if (is12h)
        {
            float ampmX = minX + spinnerW + 8f;
            var ampmBounds = new Rect(ampmX, timeY + 8f, ampmW, spinnerH - 16f);
            dtp.AmPmBounds = ampmBounds;

            string ampmText = dtp.IsPm ? "PM" : "AM";
            ctx.DrawRect(ampmBounds, colors.Primary.Opacity(0.1f), radius: 4f);
            ctx.DrawRect(ampmBounds, stroke: new Stroke(colors.Primary.Opacity(0.3f), 1f), radius: 4f);
            PaintCenteredText(ampmText, ampmBounds, 12f, colors.Primary);
        }

        transformScope3.Dispose();
        opacityScope3.Dispose();
    }

    private void PaintTimeSpinner(DateTimePicker dtp, float x, float y, float w, float h,
        int value, bool isHour, ColorSet colors)
    {
        float arrowH = 10f;
        float gap = 4f;
        float valueH = h - arrowH * 2 - gap * 2;

        // Up arrow
        var upBounds = new Rect(x, y, w, arrowH);
        if (isHour)
        {
            dtp.HourUpBounds = upBounds;
        }
        else
        {
            dtp.MinuteUpBounds = upBounds;
        }

        PaintChevronGlyph(upBounds, colors.TextMuted, pointUp: true);

        // Value
        string valueText = value.ToString(isHour && dtp.TimeFormatValue == TimeFormat.Hour12 ? "0" : "00");
        var valueBounds = new Rect(x, y + arrowH + gap, w, valueH);
        PaintCenteredText(valueText, valueBounds, 20f, colors.Text);

        // Down arrow
        var downBounds = new Rect(x, y + arrowH + gap + valueH + gap, w, arrowH);
        if (isHour)
        {
            dtp.HourDownBounds = downBounds;
        }
        else
        {
            dtp.MinuteDownBounds = downBounds;
        }

        PaintChevronGlyph(downBounds, colors.TextMuted, pointUp: false);
    }

    // ── TimePicker ────────────────────────────────────────────────────

    private void PaintTimePicker(TimePicker tp, Rect bounds)
    {
        var t = theme.Select;
        var colors = theme.Colors;

        // Background
        ctx.DrawRect(bounds, t.Background, radius: t.Radius);

        // Border — highlight when popup is open
        if (t.BorderWidth > 0)
        {
            var borderColor = tp.IsPopupOpen ? colors.Primary : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth), radius: t.Radius);
        }

        // Display text — formatted time or placeholder
        TimeOnly? value = tp.WorkingValue;
        if (value.HasValue)
        {
            string formatted = value.Value.ToString(tp.DefaultFormat, System.Globalization.CultureInfo.CurrentCulture);
            PaintText(formatted, FieldTextBounds(bounds), t.PaddingH, t.TextColor,
                overflow: TextOverflow.Ellipsis);
        }
        else
        {
            string placeholder = tp.PlaceholderText.Resolve();
            if (string.IsNullOrEmpty(placeholder))
            {
                placeholder = "Select time";
            }
            PaintText(placeholder, FieldTextBounds(bounds), t.PaddingH, t.PlaceholderColor,
                overflow: TextOverflow.Ellipsis);
        }

        // Clock icon (right side)
        float iconSize = 14f;
        float iconX = bounds.X + bounds.Width - t.PaddingH - iconSize;
        float iconY = bounds.Y + (bounds.Height - iconSize) / 2f;
        var iconColor = tp.IsPopupOpen ? colors.Primary : t.ChevronColor;
        PaintClockIcon(iconX, iconY, iconSize, iconColor);

        // Time popup — deferred to overlay pass
        ControlStateAnimator.ReconcileOpen(tp, tp.IsPopupOpen,
            AnimationModel.Spring.Snappy);
        float tpOpenT = ControlStateAnimator.GetOpenProgress(tp);

        if (tp.IsPopupOpen || tpOpenT > 0.001f)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;
            float capturedOpenT = tpOpenT;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintTimePickerDropdown(tp, absTrigger, capturedOpenT);
            });
        }
        else
        {
            tp.PopupBounds = default;
        }
    }

    private void PaintClockIcon(float x, float y, float size, ColorValue color)
    {
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float radius = size / 2f - 1f;

        // Circle outline
        ctx.DrawCircle(new Point(cx, cy), radius, stroke: new Stroke(color, 1.5f));

        // Hour hand (shorter, pointing roughly to 10 o'clock)
        float hourLen = radius * 0.5f;
        ctx.DrawLine(new Point(cx, cy), new Point(cx, cy - hourLen), new Stroke(color, 1.5f));

        // Minute hand (longer, pointing to 2 o'clock)
        float minLen = radius * 0.7f;
        float minAngle = 60f * MathF.PI / 180f; // 60 degrees from 12
        ctx.DrawLine(new Point(cx, cy),
            new Point(cx + MathF.Sin(minAngle) * minLen, cy - MathF.Cos(minAngle) * minLen),
            new Stroke(color, 1.2f));
    }

    private void PaintTimePickerDropdown(TimePicker tp, Rect triggerBounds, float openT = 1f)
    {
        var t = theme.Select;
        var colors = theme.Colors;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float padding = 12f;
        float timeSectionHeight = 48f;
        float popupWidth = triggerBounds.Width;
        float popupHeight = padding * 2 + timeSectionHeight;

        float popupX = triggerBounds.X;
        float popupY = triggerBounds.Y + triggerBounds.Height + 4f;

        var popupBounds = new Rect(popupX, popupY, popupWidth, popupHeight);
        tp.PopupBounds = popupBounds;

        // Open animation: scale + opacity + slide
        ScopeGuard opacityScope4 = default;
        ScopeGuard transformScope4 = default;
        if (!reducedMotion && openT < 0.999f)
        {
            float scale4 = LerpF(0.95f, 1f, openT);
            float slideY4 = LerpF(-4f, 0f, openT);
            opacityScope4 = ctx.PushOpacity(openT);
            float cx4 = popupBounds.X + popupBounds.Width / 2f;
            float cy4 = popupBounds.Y + popupBounds.Height / 2f;
            transformScope4 = ctx.PushScale(scale4, scale4, new Point(cx4, cy4));
        }

        // Shadow + background
        ctx.DrawRect(new Rect(popupX + 2, popupY + 2, popupWidth, popupHeight),
            new ColorValue("#000000").Opacity(0.15f), radius: t.Radius);
        ctx.DrawRect(popupBounds, colors.Surface, radius: t.Radius);
        ctx.DrawRect(popupBounds, stroke: new Stroke(colors.Border, 1f), radius: t.Radius);

        // Time spinners centered in the popup
        float spinnerW = 44f;
        float spinnerH = timeSectionHeight;
        float colonW = 16f;
        bool is12h = tp.Format == TimeFormat.Hour12;
        float ampmW = is12h ? 40f : 0f;
        float totalTimeW = spinnerW * 2 + colonW + (is12h ? 8f + ampmW : 0f);
        float timeCenterX = popupX + popupWidth / 2f;
        float timeStartX = timeCenterX - totalTimeW / 2f;
        float timeY = popupY + padding;

        // Hour spinner
        float hourX = timeStartX;
        PaintTimePickerSpinner(tp, hourX, timeY, spinnerW, spinnerH, tp.DisplayHour,
            isHour: true, colors);

        // Colon
        float colonX = hourX + spinnerW;
        PaintCenteredText(":",
            new Rect(colonX, timeY, colonW, spinnerH),
            18f, colors.Text);

        // Minute spinner
        float minX = colonX + colonW;
        PaintTimePickerSpinner(tp, minX, timeY, spinnerW, spinnerH, tp.SelectedMinute,
            isHour: false, colors);

        // AM/PM toggle
        if (is12h)
        {
            float ampmX = minX + spinnerW + 8f;
            var ampmBounds = new Rect(ampmX, timeY + 8f, ampmW, spinnerH - 16f);
            tp.AmPmBounds = ampmBounds;

            string ampmText = tp.IsPm ? "PM" : "AM";
            ctx.DrawRect(ampmBounds, colors.Primary.Opacity(0.1f), radius: 4f);
            ctx.DrawRect(ampmBounds, stroke: new Stroke(colors.Primary.Opacity(0.3f), 1f), radius: 4f);
            PaintCenteredText(ampmText, ampmBounds, 12f, colors.Primary);
        }

        transformScope4.Dispose();
        opacityScope4.Dispose();
    }

    private void PaintTimePickerSpinner(TimePicker tp, float x, float y, float w, float h,
        int value, bool isHour, ColorSet colors)
    {
        float arrowH = 10f;
        float gap = 4f;
        float valueH = h - arrowH * 2 - gap * 2;

        // Up arrow
        var upBounds = new Rect(x, y, w, arrowH);
        if (isHour)
        {
            tp.HourUpBounds = upBounds;
        }
        else
        {
            tp.MinuteUpBounds = upBounds;
        }

        PaintChevronGlyph(upBounds, colors.TextMuted, pointUp: true);

        // Value
        string valueText = value.ToString(isHour && tp.Format == TimeFormat.Hour12 ? "0" : "00");
        var valueBounds = new Rect(x, y + arrowH + gap, w, valueH);
        PaintCenteredText(valueText, valueBounds, 20f, colors.Text);

        // Down arrow
        var downBounds = new Rect(x, y + arrowH + gap + valueH + gap, w, arrowH);
        if (isHour)
        {
            tp.HourDownBounds = downBounds;
        }
        else
        {
            tp.MinuteDownBounds = downBounds;
        }

        PaintChevronGlyph(downBounds, colors.TextMuted, pointUp: false);
    }

    // ── MonthPicker ──────────────────────────────────────────────────

    private void PaintMonthPicker(MonthPicker mp, Rect bounds)
    {
        var t = theme.Select;
        var colors = theme.Colors;

        // Background
        ctx.DrawRect(bounds, t.Background, radius: t.Radius);

        // Border — highlight when popup is open
        if (t.BorderWidth > 0)
        {
            var borderColor = mp.IsPopupOpen ? colors.Primary : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth), radius: t.Radius);
        }

        // Display text — formatted month/year or placeholder
        YearMonth? value = mp.Value.Value;
        if (value.HasValue)
        {
            var date = new DateOnly(value.Value.Year, value.Value.Month, 1);
            string formatted = date.ToString(mp.DefaultFormat, System.Globalization.CultureInfo.CurrentCulture);
            PaintText(formatted, FieldTextBounds(bounds), t.PaddingH, t.TextColor,
                overflow: TextOverflow.Ellipsis);
        }
        else
        {
            string placeholder = mp.PlaceholderText.Resolve();
            if (string.IsNullOrEmpty(placeholder))
            {
                placeholder = "Select month";
            }
            PaintText(placeholder, FieldTextBounds(bounds), t.PaddingH, t.PlaceholderColor,
                overflow: TextOverflow.Ellipsis);
        }

        // Calendar icon (right side)
        float iconSize = 14f;
        float iconX = bounds.X + bounds.Width - t.PaddingH - iconSize;
        float iconY = bounds.Y + (bounds.Height - iconSize) / 2f;
        var iconColor = mp.IsPopupOpen ? colors.Primary : t.ChevronColor;
        PaintCalendarIcon(iconX, iconY, iconSize, iconColor);

        // Month popup — deferred to overlay pass
        ControlStateAnimator.ReconcileOpen(mp, mp.IsPopupOpen,
            AnimationModel.Spring.Snappy);
        float mpOpenT = ControlStateAnimator.GetOpenProgress(mp);

        if (mp.IsPopupOpen || mpOpenT > 0.001f)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;
            float capturedOpenT = mpOpenT;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintMonthPickerDropdown(mp, absTrigger, capturedOpenT);
            });
        }
        else
        {
            mp.PopupBounds = default;
        }
    }

    private void PaintCalendarIcon(float x, float y, float size, ColorValue color)
    {
        // Simple calendar outline
        float inset = 1f;
        var body = new Rect(x + inset, y + inset + 3f, size - inset * 2, size - inset * 2 - 3f);
        ctx.DrawRect(body, stroke: new Stroke(color, 1.2f), radius: 2f);

        // Top bar
        ctx.DrawRect(new Rect(body.X, body.Y, body.Width, 4f), color, radius: 2f);

        // Two "pins" on top
        float pinW = 1.5f;
        float pinH = 4f;
        float pin1X = x + size * 0.3f - pinW / 2f;
        float pin2X = x + size * 0.7f - pinW / 2f;
        float pinY = y + inset;
        ctx.DrawRect(new Rect(pin1X, pinY, pinW, pinH), color);
        ctx.DrawRect(new Rect(pin2X, pinY, pinW, pinH), color);
    }

    private void PaintMonthPickerDropdown(MonthPicker mp, Rect triggerBounds, float openT = 1f)
    {
        var t = theme.Select;
        var colors = theme.Colors;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        const float padding = 16f;
        const float headerHeight = 40f;
        const float gridPadding = 8f;
        const int cols = 4;
        const int rows = 3;

        float popupWidth = 260f;
        float cellW = (popupWidth - padding * 2) / cols;
        float cellH = 40f;
        float popupHeight = padding + headerHeight + gridPadding + rows * cellH + padding;

        float popupX = triggerBounds.X;
        float popupY = triggerBounds.Y + triggerBounds.Height + 4f;

        var popupBounds = new Rect(popupX, popupY, popupWidth, popupHeight);
        mp.PopupBounds = popupBounds;

        // Open animation: scale + opacity + slide
        ScopeGuard opacityScope5 = default;
        ScopeGuard transformScope5 = default;
        if (!reducedMotion && openT < 0.999f)
        {
            float scale5 = LerpF(0.95f, 1f, openT);
            float slideY5 = LerpF(-4f, 0f, openT);
            opacityScope5 = ctx.PushOpacity(openT);
            float cx5 = popupBounds.X + popupBounds.Width / 2f;
            float cy5 = popupBounds.Y + popupBounds.Height / 2f;
            transformScope5 = ctx.PushScale(scale5, scale5, new Point(cx5, cy5));
        }

        // Shadow + background
        ctx.DrawRect(new Rect(popupX + 2, popupY + 2, popupWidth, popupHeight),
            new ColorValue("#000000").Opacity(0.15f), radius: t.Radius);
        ctx.DrawRect(popupBounds, colors.Surface, radius: t.Radius);
        ctx.DrawRect(popupBounds, stroke: new Stroke(colors.Border, 1f), radius: t.Radius);

        float contentX = popupX + padding;
        float contentY = popupY + padding;
        float contentW = popupWidth - padding * 2;
        float arrowSize = 20f;

        // ── Header: < Year > ──
        string yearText = mp.DisplayedYear.ToString();

        var prevBounds = new Rect(contentX, contentY, arrowSize, headerHeight);
        mp.PrevYearBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        var nextBounds = new Rect(contentX + contentW - arrowSize, contentY, arrowSize, headerHeight);
        mp.NextYearBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        var headerLabelBounds = new Rect(contentX + arrowSize, contentY,
            contentW - arrowSize * 2, headerHeight);
        PaintCenteredText(yearText, headerLabelBounds, 14f, colors.Text);

        // ── Month grid: 4 cols × 3 rows ──
        float gridTop = contentY + headerHeight + gridPadding;
        mp.GridTop = gridTop;
        mp.GridLeft = contentX;
        mp.CellWidth = cellW;
        mp.CellHeight = cellH;

        var today = DateTime.Today;
        YearMonth? selectedYm = mp.Value.Value;
        string[] monthAbbrs = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                               "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        for (int i = 0; i < 12; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var cellBounds = new Rect(
                contentX + col * cellW,
                gridTop + row * cellH,
                cellW, cellH);

            int month = i + 1;
            var thisYm = new YearMonth(mp.DisplayedYear, month);
            bool isCurrentMonth = mp.DisplayedYear == today.Year && month == today.Month;
            bool isSelected = selectedYm.HasValue
                && selectedYm.Value.Year == mp.DisplayedYear
                && selectedYm.Value.Month == month;
            bool isDisabled = (mp.MinValue.HasValue && thisYm < mp.MinValue.Value)
                || (mp.MaxValue.HasValue && thisYm > mp.MaxValue.Value);
            bool isHovered = mp.HighlightedMonth == i && !isDisabled;

            // Hover scale + selected spring compress
            float cellScale = 1f;
            if (!reducedMotion)
            {
                if (isHovered)
                {
                    cellScale = 1.06f;
                }
                else if (isSelected)
                {
                    cellScale = 0.95f;
                }
            }

            float inset = 4f;
            float baseW = cellBounds.Width - inset * 2;
            float baseH = cellBounds.Height - inset * 2;
            float scaledW = baseW * cellScale;
            float scaledH = baseH * cellScale;
            float offsetX = (baseW - scaledW) / 2f;
            float offsetY = (baseH - scaledH) / 2f;
            var innerBounds = new Rect(
                cellBounds.X + inset + offsetX,
                cellBounds.Y + inset + offsetY,
                scaledW, scaledH);

            if (isSelected)
            {
                ctx.DrawRect(innerBounds, colors.Primary, radius: 6f);
            }
            else if (isHovered)
            {
                ctx.DrawRect(innerBounds, t.ItemHoverBackground, radius: 6f);
            }
            else if (isCurrentMonth)
            {
                ctx.DrawRect(innerBounds, stroke: new Stroke(colors.Primary, 1f), radius: 6f);
            }

            ColorValue textColor = isSelected
                ? colors.TextOnPrimary
                : isDisabled
                    ? colors.TextMuted
                    : colors.Text;
            PaintCenteredText(monthAbbrs[i], cellBounds, 13f, textColor);
        }

        transformScope5.Dispose();
        opacityScope5.Dispose();
    }

    // ── TagInput ─────────────────────────────────────────────────────

    private void PaintTagInput(TagInput ti, Rect bounds)
    {
        var t = theme.TextInput;
        bool disabled = ti.IsDisabled;

        // Restore focus after re-render (AddTag/RemoveTagAt triggered Invalidate)
        if (TagInput.PendingFocusRestore)
        {
            TagInput.PendingFocusRestore = false;
            ti.IsFocused = true;
            FocusManager.RequestFocus(ti);
        }

        bool focused = ti.IsFocused;

        ti.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Background
        var bg = disabled ? t.DisabledBackground : t.Background;
        ctx.DrawRect(bounds, bg, radius: t.Radius);

        // Border
        if (focused && !disabled)
        {
            ctx.DrawRect(bounds, stroke: new Stroke(t.FocusBorderColor, t.FocusBorderWidth),
                radius: t.Radius);
            if (t.FocusRingWidth > 0)
            {
                float ringOffset = t.FocusBorderWidth;
                var ringRect = new Rect(
                    bounds.X - ringOffset,
                    bounds.Y - ringOffset,
                    bounds.Width + ringOffset * 2,
                    bounds.Height + ringOffset * 2);
                ctx.DrawRect(ringRect, stroke: new Stroke(t.FocusRingColor, t.FocusRingWidth),
                    radius: t.Radius + ringOffset);
            }
        }
        else
        {
            var borderColor = disabled ? t.DisabledBorderColor : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth),
                radius: t.Radius);
        }

        using var clip = ctx.PushRoundedClip(bounds, t.Radius);

        float padding = 8f;
        float tagH = 24f;
        float tagGap = 6f;
        float tagPadH = 8f;
        float removeW = 16f;
        float fontSize = 12f;
        float maxX = bounds.X + bounds.Width - padding;
        float curX = bounds.X + padding;
        float curY = bounds.Y + padding;

        var colors = theme.Colors;
        var tags = ti.CurrentTags;
        ti.TagRemoveBounds.Clear();

        // Paint tag pills
        for (int i = 0; i < tags.Count; i++)
        {
            string tagText = tags[i];
            float textW = ctx.MeasureText(tagText, fontSize).Width;
            float pillW = tagPadH + textW + 6f + removeW + tagPadH;

            // Wrap to next row if needed
            if (curX + pillW > maxX && curX > bounds.X + padding + 1f)
            {
                curX = bounds.X + padding;
                curY += tagH + tagGap;
            }

            // Clamp pill width so close button is always visible
            float availableW = maxX - curX;
            float minPillW = tagPadH + removeW + tagPadH + 10f;
            if (pillW > availableW)
            {
                textW = Math.Max(0f, availableW - tagPadH - 6f - removeW - tagPadH);
                pillW = availableW;
            }
            pillW = Math.Max(pillW, minPillW);

            var pillBounds = new Rect(curX, curY, pillW, tagH);

            // Pill background — neutral gray tint (matches Apple HIG chip style)
            ctx.DrawRect(pillBounds, colors.Text.Opacity(0.10f), radius: tagH / 2f);

            // Tag text (truncated with ellipsis if too long)
            PaintText(tagText, new Rect(curX + tagPadH, curY, textW, tagH), 0f, colors.Text,
                fontSize: fontSize, overflow: TextOverflow.Ellipsis);

            // Remove × button
            float removeBtnX = curX + pillW - tagPadH - removeW;
            var removeBounds = new Rect(
                absoluteX + removeBtnX - bounds.X,
                absoluteY + curY - bounds.Y,
                removeW, tagH);
            ti.TagRemoveBounds.Add(removeBounds);

            bool isRemoveHovered = ti.HoveredRemoveIndex == i;
            float xCenterX = removeBtnX + removeW / 2f;
            float xCenterY = curY + tagH / 2f;
            float xSize = 3.5f;
            var xColor = isRemoveHovered ? colors.Danger : colors.TextMuted;

            ctx.DrawLine(
                new Point(xCenterX - xSize, xCenterY - xSize),
                new Point(xCenterX + xSize, xCenterY + xSize),
                new Stroke(xColor, 1.5f));
            ctx.DrawLine(
                new Point(xCenterX + xSize, xCenterY - xSize),
                new Point(xCenterX - xSize, xCenterY + xSize),
                new Stroke(xColor, 1.5f));

            curX += pillW + tagGap;
        }

        // Input area — remaining space on current row
        float inputMinW = 80f;
        if (curX + inputMinW > maxX && tags.Count > 0)
        {
            curX = bounds.X + padding;
            curY += tagH + tagGap;
        }

        float inputX = curX;
        float inputY = curY;
        float inputW = maxX - curX;
        float inputH = tagH;

        ti.InputAreaBounds = new Rect(
            absoluteX + inputX - bounds.X,
            absoluteY + inputY - bounds.Y,
            inputW, inputH);

        string inputText = ti.InputBuffer;
        if (string.IsNullOrEmpty(inputText) && tags.Count == 0)
        {
            string placeholder = ti.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                PaintText(placeholder, new Rect(inputX, inputY, inputW, inputH), 0f, t.PlaceholderColor, fontSize: fontSize);
            }
        }
        else if (!string.IsNullOrEmpty(inputText))
        {
            PaintText(inputText, new Rect(inputX, inputY, inputW, inputH), 0f, t.TextColor, fontSize: fontSize);
        }

        // Caret
        if (focused && !disabled)
        {
            var caret = theme.Caret;
            double blinkMs = caret.BlinkInterval.TotalMilliseconds;
            double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;
            bool caretVisible = elapsed < blinkMs || (elapsed % blinkMs) < (blinkMs / 2.0);

            if (caretVisible)
            {
                string beforeCaret = inputText.Length > 0 && ti.CaretIndex > 0
                    ? inputText[..Math.Min(ti.CaretIndex, inputText.Length)]
                    : "";
                float caretXOffset = string.IsNullOrEmpty(beforeCaret)
                    ? 0f
                    : ctx.MeasureText(beforeCaret, fontSize).Width;
                float caretX = inputX + caretXOffset;
                float caretPadY = 2f;
                ctx.DrawRect(new Rect(caretX, inputY + caretPadY, caret.Width, inputH - caretPadY * 2),
                    caret.Color);
            }
        }
    }

    // ── MentionInput ─────────────────────────────────────────────────

    private void PaintMentionInput(MentionInput mi, Rect bounds)
    {
        var t = theme.TextInput;
        bool disabled = mi.IsDisabled;

        mi.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        bool focused = ReferenceEquals(FocusManager.FocusedElement, mi);

        // Background
        var bg = disabled ? t.DisabledBackground : t.Background;
        ctx.DrawRect(bounds, bg, radius: t.Radius);

        // Border
        if (focused && !disabled)
        {
            ctx.DrawRect(bounds, stroke: new Stroke(t.FocusBorderColor, t.FocusBorderWidth),
                radius: t.Radius);
            if (t.FocusRingWidth > 0)
            {
                float ringOffset = t.FocusBorderWidth;
                var ringRect = new Rect(
                    bounds.X - ringOffset, bounds.Y - ringOffset,
                    bounds.Width + ringOffset * 2, bounds.Height + ringOffset * 2);
                ctx.DrawRect(ringRect, stroke: new Stroke(t.FocusRingColor, t.FocusRingWidth),
                    radius: t.Radius + ringOffset);
            }
        }
        else
        {
            var borderColor = disabled ? t.DisabledBorderColor : t.BorderColor;
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, t.BorderWidth),
                radius: t.Radius);
        }

        using var contentClip = ctx.PushRoundedClip(bounds, t.Radius);

        string text = focused && InputDispatcher.MentionEditBuffer != null
            ? InputDispatcher.MentionEditBuffer
            : (mi.Value.Value ?? "");
        float fontSize = theme.Typography.Scale.Body.Size;

        if (string.IsNullOrEmpty(text))
        {
            string placeholder = mi.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor);
            }
        }
        else
        {
            var textColor = disabled ? t.DisabledTextColor : t.TextColor;
            var mentionBg = theme.Colors.Primary.Opacity(0.15f);

            // Paint text with mention highlights: scan for @word patterns
            float textX = bounds.X + t.PaddingH;
            var fullTextSize = ctx.MeasureText(text, fontSize);
            float textY = bounds.Y + (bounds.Height - fullTextSize.Height) / 2f;
            int i = 0;
            while (i < text.Length)
            {
                // Check if this position starts a mention (trigger char followed by word chars)
                bool isMention = false;
                foreach (var trigger in mi.Triggers)
                {
                    if (text[i] == trigger.TriggerChar)
                    {
                        int end = i + 1;
                        while (end < text.Length && !char.IsWhiteSpace(text[end]))
                        {
                            end++;
                        }
                        if (end > i + 1)
                        {
                            string mention = text[i..end];
                            var mentionSize = ctx.MeasureText(mention, fontSize);
                            // Draw highlight background. Size it to the measured text
                            // height (the line box the text is drawn into at textY), not
                            // the bare fontSize — fontSize is shorter than the line box, so
                            // a fontSize-tall rect anchored at textY sits too high and clips
                            // the glyphs, making the highlight look vertically off-centre.
                            float hlPad = 2f;
                            ctx.DrawRect(new Rect(textX - hlPad, textY - hlPad,
                                mentionSize.Width + hlPad * 2, mentionSize.Height + hlPad * 2),
                                mentionBg, radius: 3f);
                            ctx.DrawText(mention, textX, textY, fontSize,
                                theme.Colors.Primary);
                            textX += mentionSize.Width;
                            i = end;
                            isMention = true;
                            break;
                        }
                    }
                }

                if (!isMention)
                {
                    // Find next trigger or end of text
                    int segEnd = i + 1;
                    while (segEnd < text.Length)
                    {
                        bool hasTrigger = false;
                        foreach (var trigger in mi.Triggers)
                        {
                            if (text[segEnd] == trigger.TriggerChar)
                            {
                                hasTrigger = true;
                                break;
                            }
                        }
                        if (hasTrigger)
                        {
                            break;
                        }
                        segEnd++;
                    }

                    string segment = text[i..segEnd];
                    ctx.DrawText(segment, textX, textY, fontSize, textColor);
                    textX += ctx.MeasureText(segment, fontSize).Width;
                    i = segEnd;
                }
            }
        }

        // Caret
        if (focused && !disabled)
        {
            var caret = theme.Caret;
            double blinkMs = caret.BlinkInterval.TotalMilliseconds;
            double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;
            bool caretVisible = elapsed < blinkMs || (elapsed % blinkMs) < (blinkMs / 2.0);

            if (caretVisible)
            {
                int caretIdx = Math.Clamp(InputDispatcher.MentionInputCaretIndex, 0, text.Length);
                string beforeCaret = text[..caretIdx];
                float textWidth = string.IsNullOrEmpty(beforeCaret)
                    ? 0f
                    : ctx.MeasureText(beforeCaret, fontSize).Width;
                float caretX = bounds.X + t.PaddingH + textWidth;
                float caretPadY = 6f;
                ctx.DrawRect(
                    new Rect(caretX, bounds.Y + caretPadY, caret.Width, bounds.Height - caretPadY * 2),
                    caret.Color);
            }
        }

        // Suggestion popup overlay — check the focused element (may be the stale node
        // held by FocusManager) since popup state lives on the node that received input,
        // not the freshly-painted node created after re-render.
        var popupSource = FocusManager.FocusedElement as MentionInput;
        if (popupSource is { IsPopupOpen: true, Suggestions.Count: > 0 } && focused)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float bw = bounds.Width;
            float bh = bounds.Height;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                PaintMentionSuggestionPopup(popupSource, absX, absY, bw, bh);
            });
        }
    }

    private void PaintMentionSuggestionPopup(MentionInput mi, float absX, float absY,
        float triggerWidth, float triggerHeight)
    {
        var t = theme.Select;
        float fontSize = theme.Typography.Scale.Body.Size;
        float itemHeight = 28f;
        float padding = 4f;
        int maxVisible = Math.Min(mi.Suggestions.Count, 6);
        float popupHeight = maxVisible * itemHeight + padding * 2;
        float popupWidth = triggerWidth;
        float popupX = absX;
        float popupY = absY + triggerHeight + 2f;

        mi.PopupBounds = new Rect(popupX, popupY, popupWidth, popupHeight);

        // Shadow
        var shadowRect = new Rect(popupX + 2, popupY + 2, popupWidth, popupHeight);
        ctx.DrawRect(shadowRect, new ColorValue("#000000").Opacity(0.2f), radius: 6f);

        // Background
        ctx.DrawRect(mi.PopupBounds, t.Background, radius: 6f);
        ctx.DrawRect(mi.PopupBounds, stroke: new Stroke(t.BorderColor, 1f), radius: 6f);

        mi.SuggestionItemBounds.Clear();
        float y = popupY + padding;
        for (int i = 0; i < maxVisible; i++)
        {
            var itemRect = new Rect(popupX + padding, y, popupWidth - padding * 2, itemHeight);
            mi.SuggestionItemBounds.Add(itemRect);

            // Highlight
            if (i == mi.HighlightedIndex)
            {
                ctx.DrawRect(itemRect, t.ItemHoverBackground, radius: 4f);
            }

            // Display text: strip the trigger char prefix for cleaner display
            string displayText = mi.Suggestions[i];
            if (displayText.Length > 0 && mi.ActiveTrigger != null
                && displayText[0] == mi.ActiveTrigger.TriggerChar)
            {
                displayText = displayText[1..].TrimEnd();
            }

            float textY = y + (itemHeight - fontSize) / 2f;
            ctx.DrawText(displayText, popupX + padding + 8f, textY, fontSize, t.TextColor);

            y += itemHeight;
        }
    }

    // ── Markdown ──────────────────────────────────────────────────────

    private void PaintMarkdown(Markdown md, Rect bounds)
    {
        md.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        var blocks = md.GetParsedBlocks();
        if (blocks.Count == 0)
        {
            return;
        }

        md.CodeBlockCopyButtons.Clear();

        var colors = theme.Colors;
        var typo = theme.Typography.Scale;
        float bodySize = typo.Body.Size;
        float x = bounds.X;
        float y = bounds.Y;
        float maxWidth = bounds.Width;
        const float blockSpacing = 8f;
        const float codeBlockPadding = 12f;
        const float listIndent = 24f;
        const float blockQuotePadding = 16f;
        const float blockQuoteBorderWidth = 3f;

        foreach (var block in blocks)
        {
            if (y > bounds.Y && block.Type != MarkdownBlockType.HorizontalRule)
            {
                y += blockSpacing;
            }

            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                {
                    float fontSize = block.HeadingLevel switch
                    {
                        1 => typo.H1.Size,
                        2 => typo.H2.Size,
                        3 => typo.H3.Size,
                        _ => bodySize + 1f,
                    };
                    FontWeight weight = block.HeadingLevel <= 3 ? FontWeight.Bold : FontWeight.SemiBold;
                    string? fontPath = ctx.DefaultFontPath != null
                        ? ctx.ResolveFontPath(ctx.DefaultFontPath, weight) : null;
                    float headingHeight = EstimateBlockTextHeight(block.Text, fontSize, maxWidth);
                    ctx.DrawText(block.Text, MathF.Round(x), MathF.Round(y), fontSize, colors.Text,
                        fontPath: fontPath, maxWidth: maxWidth);
                    y += headingHeight + 4f;
                    break;
                }

                case MarkdownBlockType.Paragraph:
                {
                    float paraHeight = EstimateBlockTextHeight(block.Text, bodySize, maxWidth);
                    ctx.DrawText(block.Text, MathF.Round(x), MathF.Round(y), bodySize, colors.Text,
                        maxWidth: maxWidth);
                    y += paraHeight;
                    break;
                }

                case MarkdownBlockType.CodeBlock:
                {
                    float codeSize = bodySize - 1f;
                    float codeLineHeight = codeSize * 1.5f;
                    int lineCount = 1;
                    foreach (char c in block.Text)
                    {
                        if (c == '\n')
                        {
                            lineCount++;
                        }
                    }

                    float codeContentHeight = lineCount * codeLineHeight;
                    bool showLangLabel = md.CodeBlockShowLanguageLabel && block.Language != null;
                    float labelHeight = showLangLabel ? 20f : 0f;
                    float totalCodeHeight = codeContentHeight + codeBlockPadding * 2 + labelHeight;
                    float codeTextMaxWidth = maxWidth - codeBlockPadding * 2;

                    var codeRect = new Rect(x, y, maxWidth, totalCodeHeight);
                    var codeBg = new ColorValue("#1e1e1e");
                    ctx.DrawRect(codeRect, codeBg, radius: 6f);

                    float codeY = y + codeBlockPadding;

                    if (showLangLabel)
                    {
                        var captionSize = typo.Caption.Size;
                        ctx.DrawText(block.Language!, x + codeBlockPadding, codeY,
                            captionSize, new ColorValue("#8b949e"),
                            maxWidth: codeTextMaxWidth);
                        codeY += labelHeight;
                    }

                    bool useHighlight = md.CodeBlockSyntaxHighlight &&
                        (block.Language is "csharp" or "cs" or "C#" or "c#");

                    string[] codeLines = block.Text.Split('\n');
                    foreach (string codeLine in codeLines)
                    {
                        if (useHighlight)
                        {
                            float lineX = x + codeBlockPadding;
                            var tokens = TokenizeCSharpLine(codeLine);
                            foreach (var (tokenText, tokenColor) in tokens)
                            {
                                if (string.IsNullOrEmpty(tokenText))
                                {
                                    continue;
                                }
                                float tokenWidth = ctx.MeasureTextAdvance(tokenText, codeSize).Width;
                                if (lineX + tokenWidth > x + maxWidth - codeBlockPadding)
                                {
                                    break;
                                }
                                ctx.DrawText(tokenText, lineX, codeY, codeSize, tokenColor);
                                lineX += tokenWidth;
                            }
                        }
                        else
                        {
                            ctx.DrawText(codeLine, x + codeBlockPadding, codeY, codeSize,
                                new ColorValue("#c9d1d9"),
                                maxWidth: codeTextMaxWidth);
                        }
                        codeY += codeLineHeight;
                    }

                    if (md.CodeBlockShowCopyButton)
                    {
                        float btnSize = 24f;
                        float btnX = x + maxWidth - codeBlockPadding - btnSize;
                        float btnY = y + 8f;
                        var btnRect = new Rect(btnX, btnY, btnSize, btnSize);
                        int btnIndex = md.CodeBlockCopyButtons.Count;

                        // Determine hover directly from mouse position during paint
                        var mousePos = InputDispatcher.CurrentMousePosition;
                        var mdAbs = md.AbsoluteBounds;
                        bool isHovered = mdAbs.Contains(mousePos) && btnRect.Contains(
                            new Point(mousePos.X - mdAbs.X, mousePos.Y - mdAbs.Y));
                        if (isHovered)
                        {
                            md.HoveredCopyButtonIndex = btnIndex;
                        }

                        // Hover background
                        if (isHovered)
                        {
                            ctx.DrawRect(btnRect, new ColorValue("#30363d"), radius: 4f);
                        }
                        else
                        {
                            ctx.DrawRect(btnRect, new ColorValue("#1e1e1e"), radius: 4f);
                        }

                        // Draw copy icon (two overlapping squares)
                        var iconColor = isHovered ? new ColorValue("#c9d1d9") : new ColorValue("#8b949e");
                        float strokeW = 1.25f;
                        var iconStroke = new Stroke(iconColor, strokeW, StrokeCap.Round, StrokeJoin.Round);

                        // Back square (offset up-left)
                        var backRect = new Rect(btnX + 7f, btnY + 4f, 10f, 10f);
                        ctx.DrawRect(backRect, stroke: iconStroke, radius: 1.5f);

                        // Front square (offset down-right), drawn on top
                        var frontRect = new Rect(btnX + 4f, btnY + 7f, 10f, 10f);
                        ctx.DrawRect(frontRect, fill: isHovered ? new ColorValue("#30363d") : new ColorValue("#1e1e1e"), stroke: iconStroke, radius: 1.5f);

                        md.CodeBlockCopyButtons.Add((btnRect, block.Text));
                    }

                    y += totalCodeHeight;
                    break;
                }

                case MarkdownBlockType.BulletList:
                {
                    if (block.Items != null)
                    {
                        foreach (string item in block.Items)
                        {
                            float itemHeight = EstimateBlockTextHeight(item, bodySize, maxWidth - listIndent);
                            float bulletY = y + bodySize * 0.35f;

                            ctx.DrawCircle(new Point(x + 8f, bulletY), 3f, colors.Text.Opacity(0.6f));
                            ctx.DrawText(item, MathF.Round(x + listIndent), MathF.Round(y), bodySize, colors.Text,
                                maxWidth: maxWidth - listIndent);
                            y += itemHeight;
                        }
                    }
                    break;
                }

                case MarkdownBlockType.OrderedList:
                {
                    if (block.Items != null)
                    {
                        for (int i = 0; i < block.Items.Count; i++)
                        {
                            string item = block.Items[i];
                            float itemHeight = EstimateBlockTextHeight(item, bodySize, maxWidth - listIndent);

                            string num = $"{i + 1}.";
                            ctx.DrawText(num, x + 4f, y, bodySize, colors.Text.Opacity(0.7f));
                            ctx.DrawText(item, MathF.Round(x + listIndent), MathF.Round(y), bodySize, colors.Text,
                                maxWidth: maxWidth - listIndent);
                            y += itemHeight;
                        }
                    }
                    break;
                }

                case MarkdownBlockType.BlockQuote:
                {
                    float quoteWidth = maxWidth - blockQuotePadding - blockQuoteBorderWidth;
                    float quoteHeight = EstimateBlockTextHeight(block.Text, bodySize, quoteWidth) + 8f;

                    var borderRect = new Rect(x, y, blockQuoteBorderWidth, quoteHeight);
                    ctx.DrawRect(borderRect, colors.Primary.Opacity(0.5f), radius: 1.5f);

                    float quoteX = x + blockQuoteBorderWidth + blockQuotePadding;
                    ctx.DrawText(block.Text, MathF.Round(quoteX), MathF.Round(y + 4f), bodySize,
                        colors.Text.Opacity(0.7f), maxWidth: quoteWidth);
                    y += quoteHeight;
                    break;
                }

                case MarkdownBlockType.HorizontalRule:
                {
                    y += 8f;
                    float ruleY = y + 0.5f;
                    ctx.DrawRect(new Rect(x, ruleY, maxWidth, 1f), colors.Text.Opacity(0.15f));
                    y += 9f;
                    break;
                }
            }
        }
    }

    private static List<(string Text, ColorValue Color)> TokenizeCSharpLine(string line)
    {
        var result = new List<(string, ColorValue)>();
        if (string.IsNullOrEmpty(line))
        {
            result.Add((line ?? string.Empty, new ColorValue("#c9d1d9")));
            return result;
        }

        string[] csharpKeywords =
        [
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object", "operator",
            "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while", "var",
            "async", "await", "get", "set", "init", "record", "required",
            "when", "yield", "add", "remove", "value", "nameof", "not", "and", "or"
        ];

        var keywordSet = new HashSet<string>(csharpKeywords, StringComparer.Ordinal);
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];

            // Single-line comment
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                result.Add((line[i..], new ColorValue("#8b949e")));
                break;
            }

            // String literal
            if (c == '"')
            {
                int start = i;
                i++;
                while (i < line.Length)
                {
                    if (line[i] == '"' && (i == start + 1 || line[i - 1] != '\\'))
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                int end = i;
                while (i < line.Length && IsCSharpDefaultChar(line[i]))
                {
                    i++;
                }
                result.Add((line[start..i], new ColorValue("#a5d6ff")));
                continue;
            }

            // Verbatim or interpolated string start
            if (c == '@' || c == '$')
            {
                int start = i;
                i++;
                if (i < line.Length && line[i] == '"')
                {
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '"' && (i + 1 >= line.Length || line[i + 1] != '"'))
                        {
                            i++;
                            break;
                        }
                        if (line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                    }
                    int end = i;
                    while (i < line.Length && IsCSharpDefaultChar(line[i]))
                    {
                        i++;
                    }
                    result.Add((line[start..i], new ColorValue("#a5d6ff")));
                    continue;
                }
                else
                {
                    i = start + 1;
                    int end = i;
                    while (i < line.Length && IsCSharpDefaultChar(line[i]))
                    {
                        i++;
                    }
                    result.Add((line[start..i], new ColorValue("#c9d1d9")));
                    continue;
                }
            }

            // Number
            if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                int start = i;
                i++;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'f' || line[i] == 'F' || line[i] == 'd' || line[i] == 'D' || line[i] == 'm' || line[i] == 'M' || line[i] == 'l' || line[i] == 'L' || line[i] == 'u' || line[i] == 'U' || line[i] == 'x' || line[i] == 'X' || line[i] == '_'))
                {
                    i++;
                }
                int end = i;
                while (i < line.Length && IsCSharpDefaultChar(line[i]))
                {
                    i++;
                }
                result.Add((line[start..i], new ColorValue("#79c0ff")));
                continue;
            }

            // Identifier or keyword
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                {
                    i++;
                }
                int end = i;
                while (i < line.Length && IsCSharpDefaultChar(line[i]))
                {
                    i++;
                }
                string word = line[start..end];
                ColorValue color;
                if (keywordSet.Contains(word))
                {
                    color = new ColorValue("#ff7b72");
                }
                else if (char.IsUpper(word[0]))
                {
                    color = new ColorValue("#ffa657");
                }
                else
                {
                    color = new ColorValue("#c9d1d9");
                }
                result.Add((line[start..i], color));
                continue;
            }

            // Whitespace and punctuation - group consecutive default chars
            int defaultStart = i;
            i++;
            while (i < line.Length && IsCSharpDefaultChar(line[i]))
            {
                i++;
            }
            result.Add((line[defaultStart..i], new ColorValue("#c9d1d9")));
        }

        return result;
    }

    private static bool IsCSharpDefaultChar(char c)
    {
        if (c == '/' || c == '"' || c == '@' || c == '$')
        {
            return false;
        }
        if (char.IsDigit(c) || char.IsLetter(c) || c == '_')
        {
            return false;
        }
        return true;
    }

    private float EstimateBlockTextHeight(string text, float fontSize, float maxWidth)
    {
        float lineHeight = fontSize * 1.4f;
        if (string.IsNullOrEmpty(text))
        {
            return lineHeight;
        }

        if (ctx.DefaultFontPath != null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = ctx.DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = maxWidth,
            };
            var result = TextLayoutEngine.Layout(text, options);
            return result.BoundingBox.Height;
        }

        var measured = ctx.MeasureText(text, fontSize);
        if (measured.Width > 0)
        {
            int lineCount = maxWidth > 0 ? (int)Math.Ceiling(measured.Width / maxWidth) : 1;
            return Math.Max(lineCount, 1) * lineHeight;
        }

        float estimatedWidth = text.Length * fontSize * 0.55f;
        int lines = maxWidth > 0 && estimatedWidth > maxWidth
            ? (int)Math.Ceiling(estimatedWidth / maxWidth) : 1;
        return lines * lineHeight;
    }

    private void PaintCommandPaletteOverlay()
    {
        var cp = CommandPalette.Instance;
        if (cp is null)
        {
            return;
        }

        var colors = theme.Colors;
        var typo = theme.Typography.Scale;
        float bodySize = typo.Body.Size;
        bool cpReducedMotion = ControlStateAnimator.ReducedMotion;

        // Full-screen dim background
        float winW = ViewportLogicalWidth;
        float winH = ViewportLogicalHeight;
        var black = new ColorValue("#000000");

        // Entrance animation: backdrop fades, panel scales in
        float entranceT = 1f;
        if (!cpReducedMotion)
        {
            // Quick 200ms ease-out entrance
            long ageMs = Environment.TickCount64 - CommandPalette.OpenTick;
            float t = Math.Min(1f, ageMs / 200f);
            if (t > 0.999f)
            {
                entranceT = 1f;
            }
            else
            {
                entranceT = 1f - (1f - t) * (1f - t);
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        ctx.DrawRect(new Rect(0, 0, winW, winH), black.Opacity(0.5f * entranceT));

        // Centered panel
        float panelWidth = Math.Min(500f, winW - 40f);
        float itemHeight = 36f;
        float searchHeight = 44f;
        int maxVisible = 8;
        int resultCount = cp.FilteredCommands.Count;
        int visibleCount = Math.Min(resultCount, maxVisible);
        float resultsHeight = visibleCount * itemHeight;
        float minPanelHeight = searchHeight + 120f;
        float panelHeight = Math.Max(searchHeight + (resultCount > 0 ? resultsHeight + 1f : 0f), minPanelHeight);
        float panelX = (winW - panelWidth) / 2f;
        float panelY = Math.Min(winH * 0.25f, 180f);

        var panelBounds = new Rect(panelX, panelY, panelWidth, panelHeight);
        cp.OverlayBounds = panelBounds;

        // Panel shadow + background (with entrance scale)
        ScopeGuard cpScaleScope = default;
        if (entranceT < 0.999f)
        {
            float scale = 0.9f + 0.1f * entranceT;
            float panelCX = panelX + panelWidth / 2f;
            float panelCY = panelY + panelHeight / 2f;
            cpScaleScope = ctx.PushScale(scale, scale, new Point(panelCX, panelCY));
        }

        var panelShadow = ShadowSpec.FromDrop(new DropShadow { Blur = 32, OffsetY = 8, Color = black.Opacity(0.4f) });
        PaintShadow(panelShadow, panelBounds, 12f);
        ctx.DrawRect(panelBounds, new ColorValue("#252525"), radius: 12f);
        ctx.DrawRect(panelBounds, stroke: new Stroke(colors.Text.Opacity(0.1f), 1f), radius: 12f);

        // Push overlay so panel text renders on top and underlying DataGrid text
        // in the panel region is cleared (OverlayBounds drives ClearTextRegions).
        ctx.PushOverlay();

        // Expand overlay bounds to the full panel area so ClearTextRegions wipes
        // all underlying text in the panel, even when there are no results.
        ctx.DrawRect(panelBounds, ColorValue.Transparent);

        // Search input area
        float searchY = panelY;
        float searchPadding = 12f;
        float iconSize = 16f;

        // Search text or placeholder
        float textX = panelX + searchPadding + iconSize + 8f;
        float textMaxW = panelWidth - searchPadding * 2 - iconSize - 8f;
        string displayText = string.IsNullOrEmpty(cp.SearchText) ? "Type a command…" : cp.SearchText;
        var textColor = string.IsNullOrEmpty(cp.SearchText) ? colors.Text.Opacity(0.4f) : colors.Text;
        var searchTextSize = ctx.MeasureText(displayText, bodySize);
        float searchTextY = searchY + (searchHeight - searchTextSize.Height) / 2f;
        ctx.DrawText(displayText, MathF.Round(textX), MathF.Round(searchTextY),
            bodySize, textColor, maxWidth: textMaxW);

        // Search icon — vertically centered with the text
        var iconTextSize = ctx.MeasureText("⌘", bodySize);
        float iconY = searchY + (searchHeight - iconTextSize.Height) / 2f;
        ctx.DrawText("⌘", panelX + searchPadding, iconY,
            bodySize, colors.Text.Opacity(0.4f));

        // Cursor with smooth sinusoidal blink (~1s period)
        float caretOpacity = 1f;
        if (!cpReducedMotion)
        {
            double caretMs = Environment.TickCount64;
            caretOpacity = 0.5f + 0.5f * MathF.Cos((float)(caretMs * 2.0 * MathF.PI / 1000.0));
            ControlStateAnimator.SignalActiveTransition();
        }
        if (!string.IsNullOrEmpty(cp.SearchText))
        {
            var cursorTextSize = ctx.MeasureTextAdvance(cp.SearchText, bodySize);
            float cursorX = textX + Math.Min(cursorTextSize.Width, textMaxW);
            float cursorPadY = 6f;
            float cursorTop = searchY + cursorPadY;
            float cursorH = searchHeight - cursorPadY * 2;
            ctx.DrawRect(new Rect(cursorX, cursorTop, 1.5f, cursorH), colors.Text.Opacity(caretOpacity));
        }
        else
        {
            float cursorPadY = 6f;
            float cursorTop = searchY + cursorPadY;
            float cursorH = searchHeight - cursorPadY * 2;
            ctx.DrawRect(new Rect(textX, cursorTop, 1.5f, cursorH), colors.Text.Opacity(caretOpacity));
        }

        if (resultCount == 0)
        {
            // Empty state — left-aligned below the search box
            float emptyY = panelY + searchHeight + 16f;
            ctx.DrawText("No matching commands", panelX + 12f, emptyY,
                bodySize, colors.Text.Opacity(0.4f), maxWidth: panelWidth - 24f);
        }
        else
        {
            // Separator between search and results
        float sepY = searchY + searchHeight;
        ctx.DrawRect(new Rect(panelX, sepY, panelWidth, 1f), colors.Text.Opacity(0.1f));

        // Results list
        float resultsY = sepY + 1f;
        cp.ItemBounds.Clear();

        int scrollOffset = cp.ScrollOffset;
        if (cp.HighlightedIndex >= scrollOffset + maxVisible)
        {
            scrollOffset = cp.HighlightedIndex - maxVisible + 1;
        }
        else if (cp.HighlightedIndex < scrollOffset)
        {
            scrollOffset = cp.HighlightedIndex;
        }
        cp.ScrollOffset = scrollOffset;

        using var clip = ctx.PushClip(new Rect(panelX, resultsY, panelWidth, resultsHeight));

        for (int vi = 0; vi < visibleCount; vi++)
        {
            int idx = scrollOffset + vi;
            if (idx >= resultCount)
            {
                break;
            }

            var cmd = cp.FilteredCommands[idx];
            float itemY = resultsY + vi * itemHeight;
            var itemBounds = new Rect(panelX, itemY, panelWidth, itemHeight);
            cp.ItemBounds.Add(itemBounds);

            // Highlighted item background
            if (idx == cp.HighlightedIndex)
            {
                ctx.DrawRect(new Rect(panelX + 4f, itemY + 2f, panelWidth - 8f, itemHeight - 4f),
                    colors.Primary.Opacity(0.15f), radius: 6f);
            }

            // Category badge
            float labelX = panelX + 12f;
            if (cmd.Category != null)
            {
                var captionSize = typo.Caption.Size;
                var catSize = ctx.MeasureText(cmd.Category, captionSize);
                float badgeW = catSize.Width + 10f;
                float badgeH = catSize.Height + 6f;
                float badgeY = itemY + (itemHeight - badgeH) / 2f;
                // Solid accent chip with on-primary (white) text. A translucent
                // accent fill left the label as blue-on-blue — low contrast and the
                // washed-accent look we avoid elsewhere; a solid chip reads cleanly.
                ctx.DrawRect(new Rect(labelX, badgeY, badgeW, badgeH),
                    colors.Primary, radius: 4f);
                float catTextY = badgeY + (badgeH - catSize.Height) / 2f;
                ctx.DrawText(cmd.Category, labelX + 5f, catTextY,
                    captionSize, colors.TextOnPrimary);
                labelX += badgeW + 8f;
            }

            // Command label
            var labelSize = ctx.MeasureText(cmd.Label, bodySize);
            float labelY = itemY + (itemHeight - labelSize.Height) / 2f;
            float labelMaxW = panelWidth - (labelX - panelX) - 12f;
            if (cmd.Shortcut != null)
            {
                labelMaxW -= 80f;
            }
            ctx.DrawText(cmd.Label, MathF.Round(labelX), MathF.Round(labelY),
                bodySize, colors.Text, maxWidth: labelMaxW);

            // Shortcut badge
            if (cmd.Shortcut != null)
            {
                string shortcutText = cmd.Shortcut.Value.ToString();
                var captionSize = typo.Caption.Size;
                var shortcutSize = ctx.MeasureText(shortcutText, captionSize);
                float scW = shortcutSize.Width + 8f;
                float scH = shortcutSize.Height + 4f;
                float scX = panelX + panelWidth - 12f - scW;
                float scY = itemY + (itemHeight - scH) / 2f;
                ctx.DrawRect(new Rect(scX, scY, scW, scH),
                    colors.Text.Opacity(0.08f), radius: 4f);
                float scTextY = scY + (scH - shortcutSize.Height) / 2f;
                ctx.DrawText(shortcutText, scX + 4f, scTextY,
                    captionSize, colors.Text.Opacity(0.5f));
            }
        }

        }

        ctx.PopOverlay();
        cpScaleScope.Dispose();
    }

    private void PaintCalendar(Calendar cal, Rect bounds)
    {
        cal.EnsureInitialized();
        // Store viewport-space absolute position so InputDispatcher can convert
        // viewport click coords to node-local coords for hit zone checking.
        cal.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);
        var colors = theme.Colors;
        var t = theme.Select;

        const float padding = 16f;
        const float navHeight = 40f;
        const float dayHeaderHeight = 28f;
        const int cols = 7;
        const int rows = 6;

        float cellWidth = (bounds.Width - padding * 2) / cols;
        float cellHeight = (bounds.Height - padding * 2 - navHeight - dayHeaderHeight) / rows;
        cal.CellWidth = cellWidth;
        cal.CellHeight = cellHeight;

        // Background card
        ctx.DrawRect(bounds, colors.SurfaceAlt, radius: 12f);
        ctx.DrawRect(bounds, stroke: new Stroke(colors.Border, 1f), radius: 12f);

        using var clip = ctx.PushClip(bounds);

        float contentX = bounds.X + padding;
        float contentY = bounds.Y + padding;
        float contentW = bounds.Width - padding * 2;

        // ── Navigation header ──
        if (cal.ShowNavigation)
        {
            PaintCalendarNav(cal, colors, contentX, contentY, contentW, navHeight);
        }

        float bodyY = contentY + navHeight;

        // ── Day-of-week headers ──
        string[] dayAbbrs = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        for (int col = 0; col < cols; col++)
        {
            var cellBounds = new Rect(contentX + col * cellWidth, bodyY, cellWidth, dayHeaderHeight);
            PaintCenteredText(dayAbbrs[col], cellBounds, 11f, colors.TextMuted);
        }

        // ── Day grid ──
        float gridTop = bodyY + dayHeaderHeight;
        cal.GridTop = gridTop;
        cal.GridLeft = contentX;

        var firstOfMonth = new DateOnly(cal.DisplayedYear, cal.DisplayedMonth, 1);
        int startDayOfWeek = (int)firstOfMonth.DayOfWeek;
        var gridStartDate = firstOfMonth.AddDays(-startDayOfWeek);
        cal.GridStartDate = gridStartDate;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Build a map of events per date for this visible grid
        var eventsByDate = new Dictionary<DateOnly, List<CalendarEvent>>();
        var gridEnd = gridStartDate.AddDays(rows * cols);
        for (int i = 0; i < cal.Events.Count; i++)
        {
            var evt = cal.Events[i];
            var evtDate = DateOnly.FromDateTime(evt.Start.DateTime);
            if (evtDate >= gridStartDate && evtDate < gridEnd)
            {
                if (!eventsByDate.TryGetValue(evtDate, out var list))
                {
                    list = [];
                    eventsByDate[evtDate] = list;
                }

                list.Add(evt);
            }
        }

        cal.EventHitZones.Clear();

        // Max events per cell based on available height
        float dayNumberHeight = 18f;
        float eventChipHeight = 16f;
        float eventGap = 2f;
        int maxEvents = Math.Max(1, (int)((cellHeight - dayNumberHeight - 4f) / (eventChipHeight + eventGap)));
        cal.MaxEventsPerCell = maxEvents;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int cellIndex = row * cols + col;
                var date = gridStartDate.AddDays(cellIndex);
                var cellBounds = new Rect(
                    contentX + col * cellWidth,
                    gridTop + row * cellHeight,
                    cellWidth, cellHeight);

                bool isCurrentMonth = date.Month == cal.DisplayedMonth && date.Year == cal.DisplayedYear;
                bool isToday = date == today;
                bool isSelected = cal.SelectedDate.HasValue && date == cal.SelectedDate.Value;
                bool isHovered = cal.HighlightedDay == cellIndex;

                // Cell border (grid lines)
                ctx.DrawRect(cellBounds, stroke: new Stroke(colors.Border.Opacity(0.3f), 0.5f));

                // Cell background
                if (isSelected)
                {
                    ctx.DrawRect(cellBounds, colors.Primary.Opacity(0.1f));
                }
                else if (isHovered && isCurrentMonth)
                {
                    ctx.DrawRect(cellBounds, t.ItemHoverBackground.Opacity(0.5f));
                }
                else if (!isCurrentMonth)
                {
                    ctx.DrawRect(cellBounds, colors.Surface.Opacity(0.3f));
                }

                // Day number (top-right of cell)
                string dayText = date.Day.ToString();
                float dayFontSize = 12f;
                var dayTextSize = ctx.MeasureText(dayText, dayFontSize);
                float dayX = cellBounds.X + cellBounds.Width - dayTextSize.Width - 4f;
                float dayY = cellBounds.Y + 2f;

                if (isToday)
                {
                    // Today: number in a solid circle sized to fit the text
                    float circleR = Math.Max(dayTextSize.Width, dayTextSize.Height) / 2f + 4f;
                    float circleCX = dayX + dayTextSize.Width / 2f;
                    float circleCY = dayY + dayTextSize.Height / 2f;
                    ctx.DrawRect(
                        new Rect(circleCX - circleR, circleCY - circleR, circleR * 2, circleR * 2),
                        colors.Primary, radius: circleR);
                    ctx.DrawText(dayText, dayX, dayY, dayFontSize, colors.TextOnPrimary);
                }
                else
                {
                    ColorValue textColor = isCurrentMonth ? colors.Text : colors.TextMuted.Opacity(0.4f);
                    ctx.DrawText(dayText, dayX, dayY, dayFontSize, textColor);
                }

                // Event chips
                if (isCurrentMonth && eventsByDate.TryGetValue(date, out var dayEvents))
                {
                    float chipY = cellBounds.Y + dayNumberHeight + 2f;
                    int shown = Math.Min(dayEvents.Count, maxEvents);
                    for (int ei = 0; ei < shown; ei++)
                    {
                        if (ei == maxEvents - 1 && dayEvents.Count > maxEvents)
                        {
                            // "+N more" indicator
                            string moreText = $"+{dayEvents.Count - ei} more";
                            var moreTextSize = ctx.MeasureText(moreText, 9f);
                            float moreTextY = chipY + (eventChipHeight - moreTextSize.Height) / 2f;
                            ctx.DrawText(moreText, cellBounds.X + 3f, moreTextY, 9f, colors.TextMuted);
                            break;
                        }

                        var evt = dayEvents[ei];
                        var chipColor = cal.GetEventColor(evt);
                        if (chipColor == default)
                        {
                            chipColor = colors.Primary;
                        }

                        var chipBounds = new Rect(
                            cellBounds.X + 2f, chipY,
                            cellBounds.Width - 4f, eventChipHeight);
                        ctx.DrawRect(chipBounds, chipColor.Opacity(0.85f), radius: 3f);

                        // Chip title (clipped to chip bounds, vertically centered)
                        using var chipClip = ctx.PushClip(chipBounds);
                        var chipTextSize = ctx.MeasureText(evt.Title, 10f);
                        float textY = chipBounds.Y + (chipBounds.Height - chipTextSize.Height) / 2f;
                        ctx.DrawText(evt.Title, chipBounds.X + 3f, textY, 10f,
                            new ColorValue("#FFFFFF"));

                        cal.EventHitZones.Add((chipBounds, evt));

                        chipY += eventChipHeight + eventGap;
                    }
                }
            }
        }
    }

    private void PaintCalendarNav(Calendar cal, ColorSet colors,
        float x, float y, float width, float height)
    {
        float arrowSize = 28f;
        float todayBtnWidth = 52f;

        // Previous month arrow
        var prevBounds = new Rect(x, y, arrowSize, height);
        cal.PrevBounds = prevBounds;
        PaintCalendarArrow(prevBounds, colors.TextMuted, isLeft: true);

        // Next month arrow
        var nextBounds = new Rect(x + arrowSize + 4f, y, arrowSize, height);
        cal.NextBounds = nextBounds;
        PaintCalendarArrow(nextBounds, colors.TextMuted, isLeft: false);

        // Month/Year label
        string monthYearText = new DateOnly(cal.DisplayedYear, cal.DisplayedMonth, 1)
            .ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
        float labelX = x + arrowSize * 2 + 12f;
        var labelSize = ctx.MeasureText(monthYearText, 16f);
        float labelY = y + (height - labelSize.Height) / 2f;
        ctx.DrawText(monthYearText, labelX, labelY, 16f, colors.Text);

        // "Today" button (right-aligned)
        float todayX = x + width - todayBtnWidth;
        float todayY = y + (height - 28f) / 2f;
        var todayBounds = new Rect(todayX, todayY, todayBtnWidth, 28f);
        cal.TodayBounds = todayBounds;

        ctx.DrawRect(todayBounds, stroke: new Stroke(colors.Border, 1f), radius: 6f);
        PaintCenteredText("Today", todayBounds, 12f, colors.Text);
    }

    // ── Checkbox ───────────────────────────────────────────────────────

    private void PaintCheckbox(Checkbox cb, Rect bounds)
    {
        var t = theme.Checkbox;
        bool isChecked = cb.BoolValue?.Value ?? (cb.ThreeStateValue == CheckboxValue.Checked);
        bool isIndeterminate = cb.ThreeStateValue == CheckboxValue.Indeterminate;
        bool isDisabled = cb.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, cb)
            && FocusManager.LastFocusWasKeyboard;

        // Reconcile hover/press/focus/disabled animation
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = cb.IsPressed
            ? AnimationModel.Spring.Snappy
            : AnimationModel.Spring.Bouncy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            cb, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        // Reconcile checked value animation (0→1 for check entrance, 1→0 for exit)
        float valueTarget = isChecked ? 1f : 0f;
        var valueModel = GetScrollViewAwareValueModel(t.Transition.Model);
        ControlStateAnimator.ReconcileValue(cb, valueTarget, valueModel);
        float valueT = Math.Clamp(anim.Value.Current, 0f, 1f);

        // Drive Open channel for indeterminate state
        ControlStateAnimator.ReconcileOpen(cb, isIndeterminate,
            AnimationModel.EaseOut(Duration.Ms(150)));
        float indeterminateT = Math.Clamp(anim.Open.Current, 0f, 1f);

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;

        // Checkbox box area (left-aligned within bounds)
        var boxBounds = new Rect(bounds.X, bounds.Y + (bounds.Height - t.Size) / 2f,
            t.Size, t.Size);

        // Press: scale to 0.92x with spring back on release
        float scale = 1f;
        if (!ControlStateAnimator.ReducedMotion)
        {
            scale = LerpF(1f, 0.92f, pressT);
        }

        Rect paintBox = boxBounds;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            float cx = boxBounds.X + boxBounds.Width / 2f;
            float cy = boxBounds.Y + boxBounds.Height / 2f;
            float sw = boxBounds.Width * scale;
            float sh = boxBounds.Height * scale;
            paintBox = new Rect(cx - sw / 2f, cy - sh / 2f, sw, sh);
        }

        ScopeGuard opacity = default;
        try
        {
            // Disabled opacity via animated channel
            float disabledOpacity = LerpF(1f, t.DisabledOpacity, disabledT);
            if (disabledOpacity < 1f)
            {
                opacity = ctx.PushOpacity(disabledOpacity);
            }

            // Determine visual state: unchecked bg vs checked bg vs indeterminate bg
            float filledT = Math.Max(valueT, indeterminateT);

            if (filledT > 0.001f)
            {
                // Background fills with scale-in animation from center
                var bgColor = isIndeterminate ? t.IndeterminateBg : t.CheckedBg;

                // Hover: subtle brightness boost on the checked background (macOS-native style)
                if (hoverT > 0.001f && disabledT < 0.5f)
                {
                    bgColor = ColorValue.Lerp(bgColor, new ColorValue("#FFFFFF"), hoverT * 0.12f);
                }

                if (filledT < 0.999f && !ControlStateAnimator.ReducedMotion)
                {
                    // Scale-in from center: draw the unchecked box first, then overlay filled bg
                    ctx.DrawRect(paintBox, t.Background,
                        stroke: new Stroke(t.BorderColor, t.BorderWidth),
                        radius: t.Radius);

                    float bgScale = filledT;
                    float cx = paintBox.X + paintBox.Width / 2f;
                    float cy = paintBox.Y + paintBox.Height / 2f;
                    float bw = paintBox.Width * bgScale;
                    float bh = paintBox.Height * bgScale;
                    var scaledBox = new Rect(cx - bw / 2f, cy - bh / 2f, bw, bh);
                    ctx.DrawRect(scaledBox, bgColor, radius: t.Radius * bgScale);
                }
                else
                {
                    ctx.DrawRect(paintBox, bgColor, radius: t.Radius);
                }

                // Check mark with progressive draw animation
                if (valueT > 0.001f && !isIndeterminate)
                {
                    PaintCheckMark(paintBox, t.CheckColor, valueT);
                }

                // Indeterminate dash with horizontal slide-in
                if (indeterminateT > 0.001f && isIndeterminate)
                {
                    PaintIndeterminateMark(paintBox, t.IndeterminateColor, indeterminateT);
                }
            }
            else
            {
                // Unchecked state
                // Hover: border brightens, subtle background tint appears
                var borderColor = t.BorderColor;
                if (hoverT > 0.001f)
                {
                    borderColor = ColorValue.Lerp(t.BorderColor, t.CheckedBg, hoverT * 0.3f);
                }

                ctx.DrawRect(paintBox, t.Background,
                    stroke: new Stroke(borderColor, t.BorderWidth),
                    radius: t.Radius);

                // Hover: subtle background tint
                if (hoverT > 0.001f)
                {
                    ctx.DrawRect(paintBox, t.CheckedBg.Opacity(hoverT * 0.06f),
                        radius: t.Radius);
                }
            }

            // Focus ring
            if (anim.Focus.Current > 0.001f)
            {
                float focusT = anim.Focus.Current;
                float outlineWidth = t.FocusRingWidth * focusT;
                float outlineOffset = 2f;
                var focusRect = new Rect(
                    paintBox.X - outlineOffset,
                    paintBox.Y - outlineOffset,
                    paintBox.Width + outlineOffset * 2,
                    paintBox.Height + outlineOffset * 2);
                ctx.DrawRect(focusRect,
                    stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), outlineWidth),
                    radius: t.Radius + outlineOffset);
            }
        }
        finally
        {
            opacity.Dispose();
        }

        // Label text to the right of the box
        string labelText = cb.Label.Resolve();
        if (!string.IsNullOrEmpty(labelText))
        {
            var labelBounds = new Rect(
                boxBounds.Right + t.LabelGap,
                bounds.Y,
                bounds.Width - t.Size - t.LabelGap,
                bounds.Height);
            PaintText(labelText, labelBounds, 0, theme.Colors.Text);
        }
    }

    private void PaintCheckMark(Rect box, ColorValue color, float progress = 1f)
    {
        // Draw a check mark (two lines forming a ✓) with progressive stroke animation.
        // Stroke width scales with box size so it remains visible on all DPIs.
        float pad = box.Width * 0.22f;
        float x0 = box.X + pad;
        float y0 = box.Y + box.Height * 0.55f;
        float x1 = box.X + box.Width * 0.40f;
        float y1 = box.Y + box.Height - pad;
        float x2 = box.Right - pad;
        float y2 = box.Y + pad * 1.2f;

        float strokeWidth = Math.Max(2.5f, box.Width * 0.15f);
        var stroke = new Stroke(color, strokeWidth, StrokeCap.Round, StrokeJoin.Round);

        if (progress >= 0.999f)
        {
            var path = PathBuilder.Rent()
                .MoveTo(new Point(x0, y0))
                .LineTo(new Point(x1, y1))
                .LineTo(new Point(x2, y2))
                .BuildTransient();
            ctx.DrawPath(path, stroke: stroke);
        }
        else
        {
            // Progressive draw: first segment 0→0.4, second segment 0.4→1.0
            float seg1Progress = Math.Clamp(progress / 0.4f, 0f, 1f);
            float seg2Progress = Math.Clamp((progress - 0.4f) / 0.6f, 0f, 1f);

            if (seg1Progress > 0.001f)
            {
                float ex = LerpF(x0, x1, seg1Progress);
                float ey = LerpF(y0, y1, seg1Progress);
                var path = PathBuilder.Rent()
                    .MoveTo(new Point(x0, y0))
                    .LineTo(new Point(ex, ey))
                    .BuildTransient();
                ctx.DrawPath(path, stroke: stroke);
            }

            if (seg2Progress > 0.001f)
            {
                float ex = LerpF(x1, x2, seg2Progress);
                float ey = LerpF(y1, y2, seg2Progress);
                var path = PathBuilder.Rent()
                    .MoveTo(new Point(x1, y1))
                    .LineTo(new Point(ex, ey))
                    .BuildTransient();
                ctx.DrawPath(path, stroke: stroke);
            }
        }
    }

    private void PaintIndeterminateMark(Rect box, ColorValue color, float progress = 1f)
    {
        // Horizontal dash in the center with horizontal slide-in
        float pad = box.Width * 0.25f;
        float y = box.Y + box.Height / 2f;
        float halfStroke = 1.5f;

        float startX = box.X + pad;
        float endX = box.Right - pad;

        if (progress >= 0.999f)
        {
            ctx.DrawRect(new Rect(startX, y - halfStroke, endX - startX, halfStroke * 2f), fill: color);
        }
        else
        {
            // Slide in from center
            float cx = (startX + endX) / 2f;
            float halfLen = (endX - startX) / 2f * progress;
            ctx.DrawRect(new Rect(cx - halfLen, y - halfStroke, halfLen * 2f, halfStroke * 2f), fill: color);
        }
    }

    // ── RadioButton ───────────────────────────────────────────────────

    private void PaintRadioButton(IRadioButton rb, Rect bounds)
    {
        var t = theme.Radio;
        var node = (Node)rb;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;
        bool hasNodeLabel = !rb.NodeLabel.IsLayoutEmpty;
        bool isCard = rb.Style == RadioStyle.Card;

        // Animate hover/press/focus (isDisabled=false — group handles disabled opacity)
        var hoverModel = t.Transition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            node, hoverModel, pressModel, isDisabled: false);
        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;

        // Animate selection with spring (0 = deselected, 1 = selected)
        float selTarget = rb.IsSelected ? 1f : 0f;
        ControlStateAnimator.ReconcileValue(node, selTarget, GetScrollViewAwareValueModel(AnimationModel.Spring.Bouncy));
        float selT = anim.Value.Current;

        // Card style: a bordered surface whose border becomes the accent when selected.
        float cardPad = isCard ? LayoutSolver.RadioCardPadding : 0f;
        if (isCard)
        {
            var cardBorder = ColorValue.Lerp(theme.Colors.Border, t.SelectedColor, selT);
            if (hoverT > 0.01f && selT <= 0.01f)
            {
                cardBorder = ColorValue.Lerp(cardBorder, t.SelectedColor, hoverT * 0.4f);
            }
            float cardBorderWidth = LerpF(1f, 2f, selT);
            ctx.DrawRect(bounds, theme.Colors.Surface,
                stroke: new Stroke(cardBorder, cardBorderWidth),
                radius: 8f);
        }

        // Circle area (left-aligned within the content box, vertically centered)
        float circleX = bounds.X + cardPad;
        float circleY = bounds.Y + (bounds.Height - t.Size) / 2f;
        float size = t.Size;

        // Press scale — shrink to 0.92x with spring
        float pressScale = LerpF(1f, 0.92f, pressT);
        float hoverScale = LerpF(1f, 1.05f, hoverT);
        float scale = pressScale * hoverScale;

        float scaledSize = size * scale;
        float sizeOffset = (size - scaledSize) / 2f;
        var circleBounds = new Rect(
            circleX + sizeOffset, circleY + sizeOffset,
            scaledSize, scaledSize);
        float circleRadius = scaledSize / 2f;

        // Apple radio: unchecked is a surface-filled circle with a thin grey ring;
        // selected is a solid accent-filled circle with a white centre dot.
        // Ring color crossfades grey → accent as the button is selected.
        var ringColor = ColorValue.Lerp(t.BorderColor, t.SelectedColor, selT);

        // Hover (unchecked only): brighten the ring toward the accent color.
        if (hoverT > 0.01f && selT <= 0.01f)
        {
            ringColor = ColorValue.Lerp(ringColor, t.SelectedColor, hoverT * 0.5f);
        }

        // Fill crossfades surface → accent so the dot reads against a filled disc
        // exactly like macOS, instead of painting the whole disc grey when unchecked.
        var bgFill = ColorValue.Lerp(t.Background, t.SelectedColor, selT);

        ctx.DrawRect(circleBounds, bgFill,
            stroke: new Stroke(ringColor, t.BorderWidth),
            radius: circleRadius);

        // Inner dot — scales in from center with spring
        if (selT > 0.01f)
        {
            float dotScale = reducedMotion ? (rb.IsSelected ? 1f : 0f) : selT;
            float dotSize = t.DotSize * dotScale * scale;
            float dotOffset = (scaledSize - dotSize) / 2f;
            var dotBounds = new Rect(
                circleBounds.X + dotOffset,
                circleBounds.Y + dotOffset,
                dotSize, dotSize);
            ctx.DrawRect(dotBounds, t.DotColor, radius: dotSize / 2f);
        }

        // Focus ring
        if (anim.Focus.Current > 0.01f)
        {
            float focusT = anim.Focus.Current;
            float ringOffset = 3f;
            var ringBounds = new Rect(
                circleBounds.X - ringOffset,
                circleBounds.Y - ringOffset,
                circleBounds.Width + ringOffset * 2,
                circleBounds.Height + ringOffset * 2);
            ctx.DrawRect(ringBounds,
                stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), t.FocusRingWidth),
                radius: circleRadius + ringOffset);
        }

        // Rich node label — laid out as a child by MeasureRadioButtonNodeLabel.
        if (hasNodeLabel)
        {
            PaintRecursive(rb.NodeLabel);
            return;
        }

        // Text label to the right of the circle
        string labelText = rb.LabelText;
        if (!string.IsNullOrEmpty(labelText))
        {
            float labelX = bounds.X + t.Size + t.LabelGap;
            var labelBounds = new Rect(
                labelX,
                bounds.Y,
                Math.Max(bounds.Width - t.Size - t.LabelGap, 200f),
                bounds.Height);
            PaintText(labelText, labelBounds, 0, theme.Colors.Text);
        }
    }

    // ── Toggle ─────────────────────────────────────────────────────────

    private void PaintToggle(Toggle tog, Rect bounds)
    {
        var t = theme.Toggle;
        bool isOn = tog.Value.Value;
        bool isDisabled = tog.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, tog)
            && FocusManager.LastFocusWasKeyboard;

        // Reconcile hover/press/focus/disabled animation
        var thumbModel = t.ThumbTransition.Model;
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = tog.IsPressed
            ? AnimationModel.Spring.Snappy
            : AnimationModel.Spring.Bouncy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            tog, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        // Reconcile value animation (thumb slide with spring overshoot)
        float valueTarget = isOn ? 1f : 0f;
        ControlStateAnimator.ReconcileValue(tog, valueTarget, GetScrollViewAwareValueModel(thumbModel));
        float valueT = anim.Value.Current;

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;

        // Track
        var trackBounds = new Rect(
            bounds.X, bounds.Y + (bounds.Height - t.TrackHeight) / 2f,
            t.TrackWidth, t.TrackHeight);

        // Track color crossfades during slide (not instant at midpoint)
        var trackColor = ColorValue.Lerp(t.TrackOffColor, t.TrackOnColor, Math.Clamp(valueT, 0f, 1f));

        ScopeGuard opacity = default;
        try
        {
            // Disabled opacity via animated channel
            float disabledOpacity = LerpF(1f, t.DisabledOpacity, disabledT);
            if (disabledOpacity < 1f)
            {
                opacity = ctx.PushOpacity(disabledOpacity);
            }

            ctx.DrawRect(trackBounds, trackColor, radius: t.TrackRadius);

            // Hover overlay on the track
            if (hoverT > 0.001f && disabledT < 0.5f)
            {
                float overlayOpacity = LerpF(0f, 0.06f, hoverT);
                overlayOpacity = LerpF(overlayOpacity, 0.12f, pressT);
                ctx.DrawRect(trackBounds, theme.Colors.Text.Opacity(overlayOpacity),
                    radius: t.TrackRadius);
            }

            if (t.TrackBorderColor.HasValue && t.TrackBorderWidth > 0)
            {
                // Border fades out as toggle turns on
                float borderOpacity = 1f - Math.Clamp(valueT, 0f, 1f);
                if (borderOpacity > 0.01f)
                {
                    ctx.DrawRect(trackBounds,
                        stroke: new Stroke(t.TrackBorderColor.Value.Opacity(borderOpacity), t.TrackBorderWidth),
                        radius: t.TrackRadius);
                }
            }

            // Thumb position — slides with spring (valueT can overshoot past 0–1)
            float thumbOffset = LerpF(t.ThumbOffsetOff, t.ThumbOffsetOn, valueT);
            float thumbY = trackBounds.Y + (trackBounds.Height - t.ThumbSize) / 2f;

            // Thumb squish on press (wider, shorter — Apple style)
            float thumbW = t.ThumbSize;
            float thumbH = t.ThumbSize;
            if (!ControlStateAnimator.ReducedMotion && pressT > 0.001f)
            {
                float squishAmount = pressT * 0.15f;
                thumbW = t.ThumbSize * (1f + squishAmount);
                thumbH = t.ThumbSize * (1f - squishAmount * 0.5f);
            }

            // Hover: thumb scales up slightly (1.05x)
            if (!ControlStateAnimator.ReducedMotion && hoverT > 0.001f && pressT < 0.5f)
            {
                float hoverScale = LerpF(1f, 1.05f, hoverT);
                thumbW *= hoverScale;
                thumbH *= hoverScale;
            }

            // Center the thumb vertically (accounting for resized height)
            thumbY = trackBounds.Y + (trackBounds.Height - thumbH) / 2f;
            var thumbBounds = new Rect(
                trackBounds.X + thumbOffset - (thumbW - t.ThumbSize) / 2f, thumbY,
                thumbW, thumbH);

            // Thumb shadow — intensifies on press
            var thumbShadow = t.ThumbShadow;
            if (pressT > 0.001f)
            {
                // Create an intensified shadow spec for the pressed state
                var pressedDropBuilder = System.Collections.Immutable.ImmutableArray.CreateBuilder<DropShadow>(
                    Math.Max(1, thumbShadow.Drop.Length));
                foreach (var drop in thumbShadow.Drop)
                {
                    pressedDropBuilder.Add(new DropShadow
                    {
                        OffsetX = drop.OffsetX,
                        OffsetY = drop.OffsetY + 1f,
                        Blur    = drop.Blur + 3f,
                        Spread  = drop.Spread + 1f,
                        Color   = drop.Color.Opacity(Math.Min(drop.Color.A / 255f * 1.5f, 1f)),
                    });
                }

                if (pressedDropBuilder.Count == 0)
                {
                    pressedDropBuilder.Add(new DropShadow
                    {
                        OffsetY = 2f,
                        Blur    = 4f,
                        Spread  = 1f,
                        Color   = new ColorValue("#000000").Opacity(0.24f),
                    });
                }

                var pressedShadow = new ShadowSpec { Drop = pressedDropBuilder.MoveToImmutable() };
                thumbShadow = ShadowSpec.Lerp(thumbShadow, pressedShadow, pressT);
            }

            PaintShadow(thumbShadow, thumbBounds, t.ThumbRadius);
            ctx.DrawRect(thumbBounds, t.ThumbColor, radius: t.ThumbRadius);

            // On state: glow pulse when the spring overshoots past 1.0
            if (!ControlStateAnimator.ReducedMotion && valueT > 1.001f)
            {
                float glowIntensity = (valueT - 1f) * 2f;
                glowIntensity = Math.Clamp(glowIntensity, 0f, 0.4f);
                ctx.DrawCircle(thumbBounds.Center, thumbBounds.Width / 2f + 3f,
                    fill: t.TrackOnColor.Opacity(glowIntensity));
            }

            // Focus ring
            if (anim.Focus.Current > 0.001f)
            {
                float focusT = anim.Focus.Current;
                float outlineWidth = t.FocusRingWidth * focusT;
                float outlineOffset = 2f;
                var focusRect = new Rect(
                    trackBounds.X - outlineOffset,
                    trackBounds.Y - outlineOffset,
                    trackBounds.Width + outlineOffset * 2,
                    trackBounds.Height + outlineOffset * 2);
                ctx.DrawRect(focusRect,
                    stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), outlineWidth),
                    radius: t.TrackRadius + outlineOffset);
            }
        }
        finally
        {
            opacity.Dispose();
        }

        // Label to the right of the track
        string labelText = tog.Label.Resolve();
        if (!string.IsNullOrEmpty(labelText))
        {
            var labelBounds = new Rect(
                trackBounds.Right + theme.Spacing.Sm,
                bounds.Y,
                bounds.Width - t.TrackWidth - theme.Spacing.Sm,
                bounds.Height);
            PaintText(labelText, labelBounds, 0, theme.Colors.Text);
        }
    }

    // ── ProgressBar ────────────────────────────────────────────────────

    private void PaintProgressBar(ProgressBar pb, Rect bounds)
    {
        var t = theme.Progress;
        var trackColor = pb.TrackColorOverride ?? t.TrackColor;
        var fillColor = pb.FillColorOverride ?? t.FillColor;
        float barHeight = pb.HeightOverride ?? t.BarHeight;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Center the bar vertically within bounds
        var trackBounds = new Rect(
            bounds.X,
            bounds.Y + (bounds.Height - barHeight) / 2f,
            bounds.Width,
            barHeight);

        // Track background
        ctx.DrawRect(trackBounds, trackColor, radius: t.BarRadius);

        // Fill
        if (pb.Mode == ProgressMode.Determinate)
        {
            float clampedValue = Math.Clamp(pb.Value, 0f, 1f);

            // Spring-animate the fill width
            ControlStateAnimator.ReconcileValue(pb, clampedValue, AnimationModel.Spring.Snappy);
            float animatedValue = reducedMotion
                ? clampedValue
                : ControlStateAnimator.GetValueProgress(pb);

            if (animatedValue > 0.001f)
            {
                var fillBounds = new Rect(
                    trackBounds.X, trackBounds.Y,
                    trackBounds.Width * animatedValue, trackBounds.Height);
                ctx.DrawRect(fillBounds, fillColor, radius: t.BarRadius);

                // Completion pulse: brief glow when at 100%
                if (clampedValue >= 0.999f && !reducedMotion)
                {
                    double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
                    float pulse = MathF.Max(0, MathF.Sin((float)(elapsedMs * 0.004)));
                    if (pulse > 0.01f)
                    {
                        ctx.DrawRect(fillBounds, fillColor.Opacity(pulse * 0.3f), radius: t.BarRadius);
                        ControlStateAnimator.SignalActiveTransition();
                    }
                }
            }
        }
        else
        {
            // Indeterminate: gradient shimmer slides across
            double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
            float cycle = (float)((elapsedMs % 1500.0) / 1500.0);
            float shimmerCenter = -0.3f + cycle * 1.6f;
            float shimmerWidth = trackBounds.Width * 0.35f;
            float shimmerX = trackBounds.X + trackBounds.Width * shimmerCenter;

            // Clip to track bounds
            float clippedLeft = MathF.Max(shimmerX, trackBounds.X);
            float clippedRight = MathF.Min(shimmerX + shimmerWidth, trackBounds.Right);
            if (clippedRight > clippedLeft)
            {
                var fillBounds = new Rect(
                    clippedLeft, trackBounds.Y,
                    clippedRight - clippedLeft, trackBounds.Height);
                float fadeEdge = MathF.Min(1f, MathF.Min(
                    (shimmerCenter + 0.3f) / 0.3f,
                    (1.3f - shimmerCenter) / 0.3f));
                fadeEdge = MathF.Max(0, fadeEdge);
                ctx.DrawRect(fillBounds, fillColor.Opacity(fadeEdge), radius: t.BarRadius);
            }
            ControlStateAnimator.SignalActiveTransition();
        }
    }

    // ── Separator ──────────────────────────────────────────────────────

    private static readonly HashSet<int> gaugeSeenVisible = new();

    private void PaintSeparator(Separator sep, Rect bounds)
    {
        var color = sep.Color ?? theme.Colors.Border;
        float inset = sep.InsetAmount ?? 0f;

        // Separators are static divider lines — a plain hairline with a short soft
        // inset at each end. There is deliberately no entrance animation: a divider
        // that animates in from the centre freezes half-drawn whenever it sits inside
        // a GPU layer-cached ScrollView (the common case), because the cached layer
        // stops repainting its content after the first few frames. Static lines are
        // consistent everywhere and match the golden examples' plain dividers.
        if (sep.SeparatorOrientation == Orientation.Horizontal)
        {
            float y = bounds.Y + bounds.Height / 2f;
            float left = bounds.X + inset;
            float right = bounds.Right - inset;
            float fadeLen = Math.Min(12f, (right - left) * 0.5f * 0.15f);
            ctx.DrawLine(
                new Point(left + fadeLen, y),
                new Point(right - fadeLen, y),
                new Stroke(color, sep.Thickness));
        }
        else
        {
            float x = bounds.X + bounds.Width / 2f;
            float top = bounds.Y + inset;
            float bottom = bounds.Bottom - inset;
            float fadeLen = Math.Min(12f, (bottom - top) * 0.5f * 0.15f);
            ctx.DrawLine(
                new Point(x, top + fadeLen),
                new Point(x, bottom - fadeLen),
                new Stroke(color, sep.Thickness));
        }
    }

    // ── Card ───────────────────────────────────────────────────────────

    private void PaintCard(Card card, Rect bounds)
    {
        var t = theme.Card;
        float radius = card.CornerRadiusOverride ?? t.Radius;
        bool isClickable = card.ClickHandler != null;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Spring hover/press for clickable cards
        float hoverT = 0f;
        float pressT = 0f;
        if (isClickable)
        {
            var anim = ControlStateAnimator.Reconcile(card,
                AnimationModel.Spring.Bouncy, AnimationModel.Spring.Snappy);
            hoverT = anim.Hover.Current;
            pressT = anim.Press.Current;
            if (reducedMotion)
            {
                hoverT = card.IsHovered ? 1f : 0f;
                pressT = card.IsPressed ? 1f : 0f;
            }
        }

        // Hover lift (up 2px) + press compress (down 1px)
        float liftY = isClickable ? -hoverT * 2f + pressT * 3f : 0f;
        float hoverScale = t.HoverScale ?? 1f;
        float scaleVal = isClickable ? 1f + hoverT * (hoverScale - 1f) - pressT * 0.02f : 1f;

        Rect paintBounds = scaleVal != 1f ? ScaleBounds(bounds, scaleVal) : bounds;
        ShadowSpec shadow = pressT > hoverT ? t.Shadow : (t.HoverShadow ?? t.Shadow);
        if (!isClickable || (hoverT < 0.01f && pressT < 0.01f))
        {
            shadow = t.Shadow;
        }

        // Apply vertical lift
        ScopeGuard lift = default;
        if (MathF.Abs(liftY) > 0.1f)
        {
            lift = ctx.PushTranslate(0, liftY);
        }

        // Shadow
        PaintShadow(shadow, paintBounds, radius);

        // Background
        ctx.DrawRect(paintBounds, t.Background, radius: radius);

        // Border
        if (t.BorderColor.HasValue && t.BorderWidth > 0)
        {
            ctx.DrawRect(paintBounds, stroke: new Stroke(t.BorderColor.Value, t.BorderWidth),
                radius: radius);
        }

        // Paint child slots: Media → Header → Content → Footer
        if (!card.Media.IsLayoutEmpty)
        {
            PaintRecursive(card.Media);
        }

        if (!card.Header.IsLayoutEmpty)
        {
            PaintRecursive(card.Header);
        }

        PaintRecursive(card.Content);

        if (!card.Footer.IsLayoutEmpty)
        {
            PaintRecursive(card.Footer);
        }

        lift.Dispose();
    }

    // ── Badge ──────────────────────────────────────────────────────────

    private void PaintBadge(Badge badge, Rect bounds)
    {
        // Badge is a decorator — paint the child first, then overlay the badge indicator
        PaintRecursive(badge.Child);

        var t = theme.Badge;

        if (badge.IsDot)
        {
            // Dot badge: small colored circle with breathing pulse
            float dotRadius = t.DotSize / 2f;
            var dotCenter = ComputeBadgePosition(bounds, t.DotSize, t.DotSize, badge.Position);
            ctx.DrawCircle(dotCenter, dotRadius, t.DotColor);

            // Breathing pulse ring to draw attention
            if (!ControlStateAnimator.ReducedMotion)
            {
                double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(0).TotalMilliseconds;
                float pulse = MathF.Max(0, MathF.Sin((float)(elapsedMs * 0.0025)));
                if (pulse > 0.01f)
                {
                    ctx.DrawCircle(dotCenter, dotRadius + 1f + pulse * 3f,
                        stroke: new Stroke(t.DotColor.Opacity(pulse * 0.3f), 1f));
                    ControlStateAnimator.SignalActiveTransition();
                }
            }
        }
        else if (badge.Count.HasValue && badge.Count.Value > 0)
        {
            // Count badge: circle for single-digit, pill for multi-digit
            string countText = badge.Count.Value > badge.MaxCount
                ? $"{badge.MaxCount}+"
                : badge.Count.Value.ToString();
            float badgeFontSize = t.TextStyle.Size;
            var textSize = ctx.MeasureText(countText, badgeFontSize);
            float badgeWidth = Math.Max(t.Height, textSize.Width + t.PaddingH * 2);
            float badgeHeight = t.Height;

            var badgeCenter = ComputeBadgePosition(bounds, badgeWidth, badgeHeight, badge.Position);

            if (badgeWidth <= badgeHeight * 1.1f)
            {
                // Near-square — draw as a perfect circle
                float circleRadius = badgeHeight / 2f;
                ctx.DrawCircle(badgeCenter, circleRadius, t.Background);
            }
            else
            {
                // Wider — draw as a pill-shaped rounded rect
                var badgeBounds = new Rect(
                    badgeCenter.X - badgeWidth / 2f,
                    badgeCenter.Y - badgeHeight / 2f,
                    badgeWidth, badgeHeight);
                ctx.DrawRect(badgeBounds, t.Background, radius: badgeHeight / 2f);
            }

            // Draw text centered both horizontally and vertically.
            // Use glyph visual bounds to center the actual glyph shape
            // rather than the em-square (digits have no descenders).
            float textX = MathF.Round(badgeCenter.X - textSize.Width / 2f);
            var glyphBounds = ctx.MeasureGlyphVisualBounds(countText, badgeFontSize);
            float textY = glyphBounds.HasValue
                ? MathF.Round(badgeCenter.Y - glyphBounds.Value.VisualCenterY)
                : MathF.Round(badgeCenter.Y - badgeFontSize / 2f);
            ctx.DrawText(countText, textX, textY, badgeFontSize, t.TextColor);
        }
        else if (!badge.Content.IsLayoutEmpty)
        {
            // Custom content badge
            PaintRecursive(badge.Content);
        }
    }

    private static Point ComputeBadgePosition(Rect parentBounds, float badgeWidth, float badgeHeight,
        BadgePosition position)
    {
        return position switch
        {
            BadgePosition.TopRight => new Point(
                parentBounds.Right - badgeWidth / 4f,
                parentBounds.Top + badgeHeight / 4f),
            BadgePosition.TopLeft => new Point(
                parentBounds.Left + badgeWidth / 4f,
                parentBounds.Top + badgeHeight / 4f),
            BadgePosition.BottomRight => new Point(
                parentBounds.Right - badgeWidth / 4f,
                parentBounds.Bottom - badgeHeight / 4f),
            BadgePosition.BottomLeft => new Point(
                parentBounds.Left + badgeWidth / 4f,
                parentBounds.Bottom - badgeHeight / 4f),
            _ => new Point(parentBounds.Right, parentBounds.Top),
        };
    }

    // ── Rating ─────────────────────────────────────────────────────────

    /// <summary>
    /// Per-rating animation state for star pulse and wave effects.
    /// </summary>
    private sealed class RatingAnimState
    {
        internal float LastValue;
        internal long LastValueChangeTimestamp;
        internal int LastClickedStar = -1;
        internal long LastAccessTimestamp;
    }

    private static readonly Dictionary<int, RatingAnimState> ratingAnimStates = new();

    private void PaintRating(Rating rating, Rect bounds)
    {
        var t = theme.Rating;
        float iconSize = rating.SizeValue ?? t.IconSize;
        float gap = t.Gap;
        int max = rating.Max;
        float currentValue = rating.BoundValue?.Value ?? rating.ReadOnlyValue ?? 0f;
        bool disabled = rating.IsDisabled || rating.IsReadOnly;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        var filledColor = rating.FilledColor ?? t.FilledColor;
        var emptyColor = disabled ? t.DisabledColor : (rating.EmptyColor ?? t.EmptyColor);
        float starRadius = iconSize / 2f;

        // Store absolute bounds for click-to-star mapping in InputDispatcher
        rating.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Animate hover/press
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            rating, hoverModel, pressModel, isDisabled: disabled);
        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;

        // Rating animation state for pulse effects
        int ratingKey = RuntimeHelpers.GetHashCode(rating);
        if (!ratingAnimStates.TryGetValue(ratingKey, out var ratingAnim))
        {
            ratingAnim = new RatingAnimState { LastValue = currentValue };
            ratingAnimStates[ratingKey] = ratingAnim;
        }

        long now = Stopwatch.GetTimestamp();
        ratingAnim.LastAccessTimestamp = now;

        // Detect value change for pulse animation
        bool valueChanged = MathF.Abs(currentValue - ratingAnim.LastValue) > 0.01f;
        if (valueChanged)
        {
            ratingAnim.LastClickedStar = (int)MathF.Ceiling(currentValue) - 1;
            ratingAnim.LastValueChangeTimestamp = now;
            ratingAnim.LastValue = currentValue;
        }

        float pulseElapsed = ratingAnim.LastValueChangeTimestamp > 0
            ? (float)Stopwatch.GetElapsedTime(ratingAnim.LastValueChangeTimestamp, now).TotalSeconds
            : 10f;

        // Center the row of stars vertically within bounds
        float totalWidth = (iconSize * max) + (gap * (max - 1));
        float startX = (bounds.Width - totalWidth) / 2f;
        float centerY = bounds.Height / 2f;

        // Compute hover star index from cursor position (for wave effect)
        int hoverStarIndex = -1;
        if (!disabled && rating.IsHovered && !reducedMotion)
        {
            var mousePos = InputDispatcher.CurrentMousePosition;
            float relX = mousePos.X - absoluteX - startX;
            hoverStarIndex = (int)(relX / (iconSize + gap));
            hoverStarIndex = Math.Clamp(hoverStarIndex, 0, max - 1);
        }

        bool anyAnimating = false;

        for (int i = 0; i < max; i++)
        {
            float cx = startX + (i * (iconSize + gap)) + starRadius;
            float starValue = i + 1;

            // Wave scale on hover — stars scale up sequentially from hovered star
            float waveScale = 1f;
            if (hoverStarIndex >= 0 && !reducedMotion)
            {
                int dist = Math.Abs(i - hoverStarIndex);
                float waveFactor = Math.Max(0f, 1f - dist * 0.3f);
                waveScale = LerpF(1f, 1.12f, waveFactor * hoverT);
            }

            // Press scale
            float starPressScale = disabled ? 1f : LerpF(1f, 0.92f, pressT);

            // Pulse on click — selected star briefly scales to 1.3x
            float pulseScale = 1f;
            if (!reducedMotion && i == ratingAnim.LastClickedStar && pulseElapsed < 0.3f)
            {
                float pulseT2 = pulseElapsed / 0.3f;
                // Overshoot then settle: 1.0 → 1.3 → 1.0
                if (pulseT2 < 0.3f)
                {
                    pulseScale = LerpF(1f, 1.3f, pulseT2 / 0.3f);
                }
                else
                {
                    pulseScale = LerpF(1.3f, 1f, (pulseT2 - 0.3f) / 0.7f);
                }

                anyAnimating = true;
            }

            float effectiveScale = waveScale * starPressScale * pulseScale;
            float radius = starRadius * 0.85f * effectiveScale;

            // Star fill with smooth transition
            if (currentValue >= starValue)
            {
                // Fully filled
                float fillOpacity = 1f;

                // Pulse brightness on recently clicked star
                if (!reducedMotion && i == ratingAnim.LastClickedStar && pulseElapsed < 0.3f)
                {
                    fillOpacity = LerpF(1.2f, 1f, Math.Clamp(pulseElapsed / 0.3f, 0f, 1f));
                }

                ctx.DrawCircle(new Point(cx, centerY), radius,
                    fill: filledColor.Opacity(Math.Min(fillOpacity, 1f)));
            }
            else if (rating.HalfStarsEnabled && currentValue >= starValue - 0.5f)
            {
                // Half star
                ctx.DrawCircle(new Point(cx, centerY), radius, fill: emptyColor);
                var halfRect = new Rect(cx - radius, centerY - radius,
                    radius, radius * 2f);
                ctx.DrawRect(halfRect, filledColor, radius: radius);
            }
            else
            {
                // Empty — dimmed with smooth transition
                float dimOpacity = disabled ? 1f : LerpF(0.5f, 1f, 1f - hoverT * 0.3f);

                // Hover preview: stars at and before cursor show half-intensity fill
                if (hoverStarIndex >= 0 && i <= hoverStarIndex && !disabled)
                {
                    float previewOpacity = 0.35f * hoverT;
                    ctx.DrawCircle(new Point(cx, centerY), radius,
                        fill: filledColor.Opacity(previewOpacity));
                }
                else
                {
                    ctx.DrawCircle(new Point(cx, centerY), radius,
                        fill: emptyColor.Opacity(dimOpacity));
                }
            }
        }

        if (anyAnimating)
        {
            ControlStateAnimator.SignalActiveTransition();
        }
    }

    // ── Image ──────────────────────────────────────────────────────────

    private void PaintImage(Image img, Rect bounds)
    {
        if (img.Source is not null)
        {
            float opacity = img.LayoutData.Opacity;
            ctx.DrawImage(img.Source, bounds, opacity);
        }
        else
        {
            // Placeholder: light gray rect with "No Image" text
            ctx.DrawRect(bounds, new ColorValue("#2C2C2E"), radius: 4f);
            ctx.DrawRect(bounds, stroke: new Stroke(new ColorValue("#3A3A3C"), 1f), radius: 4f);
            PaintText("No Image", bounds, 0, new ColorValue("#636366"),
                fontSize: theme.Typography.Scale.Body.Size,
                alignment: TextAlignment.Center);
        }
    }

    // ── Spinner ────────────────────────────────────────────────────────

    private void PaintSpinner(Spinner spinner, Rect bounds)
    {
        HasActiveSpinners = true;

        var style = spinner.SpinnerStyleOverride ?? theme.Spinner;
        float size = spinner.SpinnerSize ?? Math.Min(bounds.Width, bounds.Height);
        var center = bounds.Center;
        float strokeWidth = style.Thickness > 0 ? style.Thickness : 3f;
        float radius = size / 2f - strokeWidth;

        if (radius <= 0)
        {
            return;
        }

        // Continuous rotation
        float speedMs = (float)style.Speed.TotalMilliseconds;
        if (speedMs <= 0)
        {
            speedMs = 1000f;
        }

        double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
        float rotationDegrees = (float)((elapsedMs / speedMs * 360.0) % 360.0);
        // Draw the active arc with continuous rotation by passing the rotated start angle
        // directly instead of using a transform, avoiding a GPU renderer bug where rotated
        // arcs are not rendered correctly via the arc fast path.
        ctx.DrawArc(center, radius, Angle.Degrees(rotationDegrees), Angle.Degrees(90),
            new Stroke(theme.Colors.Primary, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
    }

    // ── IconButton ─────────────────────────────────────────────────────

    private void PaintIconButton(IconButton ib, Rect bounds)
    {
        var bt = theme.Button;

        // Icon buttons have a circular or rounded background
        float radius = Math.Min(bounds.Width, bounds.Height) / 2f;

        // Animated state transitions — bouncy release for spring-back overshoot
        bool isDisabled = ib.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, ib)
            && FocusManager.LastFocusWasKeyboard;
        var hoverModel = GetScrollViewAwareHoverModel(AnimationModel.Spring.Bouncy);
        var pressModel = GetScrollViewAwarePressModel(ib.IsPressed
            ? AnimationModel.Spring.Snappy
            : AnimationModel.Spring.Bouncy);
        var anim = ControlStateAnimator.Reconcile(
            ib, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;

        // Whole-button scale (press compress/release overshoot)
        float scale = 1f;
        if (!ControlStateAnimator.ReducedMotion)
        {
            float pressScale = bt.Pressed.Scale ?? 0.92f;
            scale = LerpF(1f, pressScale, pressT);
        }
        Rect paintBounds = MathF.Abs(scale - 1f) > 0.001f
            ? ScaleBounds(bounds, scale) : bounds;

        // Background with interpolated opacity
        float bgOpacity = LerpF(1f, bt.Disabled.BackgroundOpacity ?? 0.4f, disabledT);
        if (bgOpacity < 1f)
        {
            using var _ = ctx.PushOpacity(bgOpacity);
            ctx.DrawRect(paintBounds, theme.Colors.SurfaceAlt, radius: radius);
        }
        else
        {
            ctx.DrawRect(paintBounds, theme.Colors.SurfaceAlt, radius: radius);
        }

        // Hover overlay — fades in/out smoothly
        if (hoverT > 0.001f)
        {
            float overlayAlpha = LerpF(0f, 0.06f, hoverT);
            ctx.DrawRect(paintBounds, theme.Colors.Text.Opacity(overlayAlpha), radius: radius);
        }

        // Press overlay — fades in/out smoothly
        if (pressT > 0.001f)
        {
            float pressAlpha = LerpF(0f, 0.12f, pressT);
            ctx.DrawRect(paintBounds, theme.Colors.Text.Opacity(pressAlpha), radius: radius);
        }

        // Draw the icon as stroked lines from its SVG path data
        var icon = ib.Icon;
        if (icon.Paths.Length == 0)
        {
            return;
        }

        float iconSize = ib.Size ?? icon.DefaultSize;
        if (iconSize <= 0)
        {
            iconSize = 20f;
        }

        // Icon-specific scale: grows 1.1x on hover, shrinks 0.9x on press. The scale is
        // applied to the blit, not the rasterization, so the cached bitmap stays stable
        // (and high-res) through the whole animation.
        float iconScale = 1f;
        if (!ControlStateAnimator.ReducedMotion)
        {
            iconScale = LerpF(1f, 1.1f, Math.Clamp(hoverT, 0f, 1f));
            iconScale = LerpF(iconScale, 0.9f, Math.Clamp(pressT, 0f, 1f));
        }

        // Glyph size defaults to half the footprint; an explicit IconSize
        // overrides it (scaled with the press animation so the icon still
        // compresses with the button).
        float baseDrawSize = ib.IconSizeOverride is float glyphSize
            ? glyphSize * (paintBounds.Height / Math.Max(bounds.Height, 0.001f))
            : Math.Min(paintBounds.Width, paintBounds.Height) * 0.5f;
        float iconCx = paintBounds.X + paintBounds.Width / 2f;
        float iconCy = paintBounds.Y + paintBounds.Height / 2f;

        var iconColor = theme.Colors.Text;
        if (disabledT > 0.001f)
        {
            iconColor = ColorValue.Lerp(iconColor, theme.Colors.TextMuted, disabledT);
        }

        // Icon spring wobble: rotation proportional to spring overshoot
        float wobble = (anim.Hover.Current - anim.Hover.Target) * 8f;
        bool hasWobble = MathF.Abs(wobble) > 0.05f && !ControlStateAnimator.ReducedMotion;

        float iconStroke = ib.IconStrokeOverride ?? 2f;
        if (hasWobble)
        {
            using var rotate = ctx.PushRotate(Angle.Degrees(wobble), new Point(iconCx, iconCy));
            PaintIconBitmap(icon, iconCx, iconCy, baseDrawSize, iconScale, iconColor, strokeWidthLogical: iconStroke);
        }
        else
        {
            PaintIconBitmap(icon, iconCx, iconCy, baseDrawSize, iconScale, iconColor, strokeWidthLogical: iconStroke);
        }

        // Focus outline ring
        if (!isDisabled && anim.Focus.Current > 0.001f)
        {
            float focusT = anim.Focus.Current;
            float outlineWidth = (bt.Focused.OutlineWidth ?? 3f) * focusT;
            float outlineOffset = bt.Focused.OutlineOffset ?? 2f;
            var outlineRect = new Rect(
                paintBounds.X - outlineOffset,
                paintBounds.Y - outlineOffset,
                paintBounds.Width + outlineOffset * 2,
                paintBounds.Height + outlineOffset * 2);
            var outlineColor = bt.Focused.OutlineColor ?? theme.Colors.Focus;
            PaintBrushStroke(Brush.Solid(outlineColor.Opacity(focusT)), outlineRect,
                outlineWidth, radius + outlineOffset);
        }

        // Tooltip overlay (deferred)
        DeferTooltipIfHovered(ib, ib.TooltipText.Resolve(), bounds);
    }

    // ── IconView ──────────────────────────────────────────────────────

    private void PaintIconView(IconView iv, Rect bounds)
    {
        var icon = iv.Icon;
        if (icon.Paths.Length == 0)
        {
            return;
        }

        float iconSize = iv.RequestedSize > 0 ? iv.RequestedSize : icon.DefaultSize;
        if (iconSize <= 0)
        {
            iconSize = 24f;
        }

        float drawSize = Math.Min(bounds.Width, bounds.Height);
        float iconCx = bounds.X + bounds.Width / 2f;
        float iconCy = bounds.Y + bounds.Height / 2f;

        var iconColor = iv.ColorOverride ?? theme.Colors.Text;
        float strokeWidth = iv.StrokeOverride ?? Math.Max(1.5f, drawSize / 12f);

        PaintIconBitmap(icon, iconCx, iconCy, drawSize, 1f, iconColor, strokeWidth);
    }

    // ── Icon bitmap cache (Apple/Flutter-style: rasterize once, blit) ──────────

    // Cached anti-aliased icon bitmaps keyed by (icon content, device px, quantized
    // stroke, color). Static so it survives across frames/components; entries are tiny
    // (px²·4 bytes). The cache is what makes this zero-per-frame, unlike re-stroking.
    private static readonly Dictionary<(int Icon, int Px, int Stroke, int Color), ImageSource> iconImageCache = new();

    /// <summary>
    /// Draws <paramref name="icon"/> centered at (<paramref name="centerX"/>,
    /// <paramref name="centerY"/>) as a cached, anti-aliased bitmap. The icon is rasterized
    /// once per (content, device-pixel size, stroke, color) at device resolution — giving
    /// text-quality AA — then blitted. <paramref name="renderScale"/> scales only the blit
    /// (e.g. hover/press), so an animating icon never re-rasterizes.
    /// </summary>
    private void PaintIconBitmap(
        Icon icon, float centerX, float centerY, float baseDrawSize,
        float renderScale, ColorValue color, float strokeWidthLogical)
    {
        if (icon.Paths.Length == 0 || baseDrawSize <= 0f)
        {
            return;
        }

        float ratio = MathF.Max(1f, ctx.PixelRatio);
        int devicePx = Math.Clamp((int)MathF.Round(baseDrawSize * ratio), 1, 1024);
        float strokeDevice = MathF.Max(0.75f, strokeWidthLogical * ratio);

        var image = GetOrCreateIconImage(icon, devicePx, strokeDevice, color);

        float drawn = baseDrawSize * renderScale;
        ctx.DrawImage(image, new Rect(centerX - drawn * 0.5f, centerY - drawn * 0.5f, drawn, drawn));
    }

    private static ImageSource GetOrCreateIconImage(Icon icon, int devicePx, float strokeDevice, ColorValue color)
    {
        int strokeKey = (int)MathF.Round(strokeDevice * 4f);
        int colorKey = HashCode.Combine(color.R, color.G, color.B, color.A);
        var key = (icon.GetHashCode(), devicePx, strokeKey, colorKey);
        if (iconImageCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        float viewW = icon.ViewBox.Width > 0 ? icon.ViewBox.Width : 24f;
        float viewH = icon.ViewBox.Height > 0 ? icon.ViewBox.Height : 24f;
        // Half the stroke plus a pixel of AA fringe must stay inside the bitmap.
        float padding = strokeDevice * 0.5f + 1.5f;
        byte[] rgba = IconRasterizer.Rasterize(icon.Paths, viewW, viewH, devicePx, strokeDevice, color, padding);
        var image = ImageSource.FromBytes(rgba, devicePx, devicePx);
        iconImageCache[key] = image;
        return image;
    }

    /// <summary>
    /// Returns a press animation model suitable for the current paint context.
    /// When inside a ScrollView layer, long Press animations trigger expensive
    /// direct painting of thousands of nodes. This helper returns a very short
    /// curve so the animation completes in ~1 frame, minimizing lag.
    /// </summary>
    private static AnimationModel GetScrollViewAwarePressModel(AnimationModel? preferred)
    {
        // Unlike hover (fires on every mouse-move), a press is a per-click, short-lived
        // spring. Keeping the full bounce — even inside a ScrollView, where it briefly
        // direct-paints the content — is what makes buttons feel alive when clicked. The
        // cost is bounded to the ~300ms of the press, not sustained like hover.
        return preferred ?? AnimationModel.None;
    }

    /// <summary>
    /// When inside a ScrollView layer, Hover animations keep the entire content
    /// in direct-paint mode for ~200ms every time the mouse enters a control.
    /// This forces thousands of nodes to repaint every frame. Returning None
    /// makes hover state change instantly, allowing the cached layer to be used.
    /// </summary>
    private AnimationModel GetScrollViewAwareHoverModel(AnimationModel? preferred)
    {
        if (!float.IsNegativeInfinity(currentViewportTop) && preferred is not null && !preferred.IsNoneModel)
        {
            return AnimationModel.None;
        }
        return preferred ?? AnimationModel.None;
    }

    /// <summary>
    /// When inside a ScrollView layer, Value animations (checkbox checkmark,
    /// radio button dot, toggle thumb slide) keep the entire content in
    /// direct-paint mode for hundreds of milliseconds — up to ~1s for bouncy
    /// springs. At ~140ms per direct-paint frame this causes sustained UI
    /// freezes. Returning None makes the value change instantly, so only a
    /// single layer recapture is needed instead of 5-10 frames of full-tree
    /// direct painting.
    /// </summary>
    private AnimationModel GetScrollViewAwareValueModel(AnimationModel? preferred)
    {
        if (!float.IsNegativeInfinity(currentViewportTop) && preferred is not null && !preferred.IsNoneModel)
        {
            return AnimationModel.None;
        }
        return preferred ?? AnimationModel.None;
    }

    // ── LinkButton ─────────────────────────────────────────────────────

    private void PaintLinkButton(LinkButton lb, Rect bounds)
    {
        // Animated state transitions — bouncy release for spring-back
        bool isDisabled = lb.IsDisabled;
        bool isFocused = ReferenceEquals(FocusManager.FocusedElement, lb)
            && FocusManager.LastFocusWasKeyboard;
        var hoverModel = GetScrollViewAwareHoverModel(AnimationModel.Spring.Snappy);
        var pressModel = GetScrollViewAwarePressModel(lb.IsPressed
            ? AnimationModel.Spring.Snappy
            : AnimationModel.Spring.Bouncy);
        var anim = ControlStateAnimator.Reconcile(
            lb, hoverModel, pressModel, isDisabled: isDisabled, isFocused: isFocused);

        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;
        float disabledT = anim.Disabled.Current;

        // 1px downward shift on press for tactile feedback (skip under reduced motion)
        float pressShift = ControlStateAnimator.ReducedMotion ? 0f : Math.Clamp(pressT, 0f, 1f) * 1f;
        var textBounds = MathF.Abs(pressShift) > 0.01f
            ? new Rect(bounds.X, bounds.Y + pressShift, bounds.Width, bounds.Height)
            : bounds;

        // Interpolate text color: primary → pressed darker → disabled muted
        var textColor = theme.Colors.PrimaryText;
        if (pressT > 0.001f)
        {
            textColor = ColorValue.Lerp(textColor, theme.Colors.PrimaryText.Opacity(0.6f), pressT);
        }

        if (disabledT > 0.001f)
        {
            textColor = ColorValue.Lerp(textColor, theme.Colors.PrimaryText.Opacity(0.4f), disabledT);
        }

        PaintText(lb.Label.Resolve(), textBounds, 0, textColor);

        // Underline — slides in from left on hover, smoothly animated
        float underlineProgress = Math.Max(hoverT, pressT);
        if (underlineProgress > 0.001f && disabledT < 0.99f)
        {
            string text = lb.Label.Resolve();
            float fontSize = theme.Typography.Scale.Body.Size;
            var textSize = ctx.MeasureText(text, fontSize);
            float lineY = textBounds.Y + (textBounds.Height + textSize.Height) / 2f + 1f;
            float lineX = textBounds.X;
            // Underline grows from left based on hover progress
            float lineWidth = textSize.Width * underlineProgress;
            ctx.DrawLine(
                new Point(lineX, lineY),
                new Point(lineX + lineWidth, lineY),
                new Stroke(textColor, 1f));
        }
    }

    // ── Slider ─────────────────────────────────────────────────────────

    private void PaintSlider(Slider slider, Rect bounds)
    {
        var t = theme.Slider;
        float trackHeight = t.TrackHeight;
        float thumbWidth = t.ThumbWidth;
        float thumbHeight = t.ThumbHeight;
        bool disabled = slider.IsDisabled || slider.IsReadOnly;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Animate hover/press/focus state
        var hoverModel = t.ThumbTransition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            slider, hoverModel, pressModel, isDisabled: disabled);
        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;

        // Track
        float trackY = bounds.Y + (bounds.Height - trackHeight) / 2f;
        var trackBounds = new Rect(bounds.X + thumbWidth / 2f, trackY,
            bounds.Width - thumbWidth, trackHeight);

        // Empty track (full width)
        PaintBrush(t.TrackEmpty, trackBounds, t.TrackRadius);

        // Filled portion
        float value = Math.Clamp(slider.Bind.Value, slider.Min, slider.Max);
        float fraction = (slider.Max - slider.Min) > 0
            ? (value - slider.Min) / (slider.Max - slider.Min)
            : 0f;
        float fillWidth = trackBounds.Width * fraction;
        if (fillWidth > 0)
        {
            var fillBounds = new Rect(trackBounds.X, trackBounds.Y,
                fillWidth, trackBounds.Height);
            PaintBrush(t.TrackFill, fillBounds, t.TrackRadius);

            // Track fill glow near thumb — intensifies fill color near the thumb position
            if (!disabled && !reducedMotion && hoverT > 0.01f)
            {
                float glowWidth = 40f;
                float glowX = trackBounds.X + fillWidth - glowWidth / 2f;
                glowX = Math.Max(glowX, trackBounds.X);
                float clippedWidth = Math.Min(glowWidth, fillWidth);
                if (clippedWidth > 0)
                {
                    var glowBounds = new Rect(glowX, trackBounds.Y, clippedWidth, trackBounds.Height);
                    ctx.DrawRect(glowBounds, theme.Colors.Primary.Opacity(0.15f * hoverT),
                        radius: t.TrackRadius);
                }
            }
        }

        // Thumb position
        float thumbX = trackBounds.X + fillWidth - thumbWidth / 2f;
        float thumbY = bounds.Y + (bounds.Height - thumbHeight) / 2f;
        var thumbBounds = new Rect(thumbX, thumbY, thumbWidth, thumbHeight);

        // Spring-based scale: hover 1.15x, press 0.9x with spring back
        float hoverScale = disabled ? 1f : LerpF(1f, 1.15f, hoverT);
        float pressScale = disabled ? 1f : LerpF(1f, 0.9f, pressT);
        float thumbScale = hoverScale * pressScale;

        if (!reducedMotion && MathF.Abs(thumbScale - 1f) > 0.001f)
        {
            float sw = thumbBounds.Width * thumbScale;
            float sh = thumbBounds.Height * thumbScale;
            thumbBounds = new Rect(
                thumbBounds.X + (thumbBounds.Width - sw) / 2f,
                thumbBounds.Y + (thumbBounds.Height - sh) / 2f,
                sw, sh);
        }

        // Proximity glow — shadow expands as cursor approaches
        if (!disabled)
        {
            float glowRadius = Math.Max(thumbBounds.Width, thumbBounds.Height) / 2f;
            float glowOpacity = 0f;

            if (slider.IsPressed)
            {
                glowOpacity = 0.2f;
            }
            else if (!reducedMotion)
            {
                // Compute distance from cursor to thumb center
                var mousePos = InputDispatcher.CurrentMousePosition;
                float thumbCenterX = absoluteX + thumbBounds.X + thumbBounds.Width / 2f;
                float thumbCenterY = absoluteY + thumbBounds.Y + thumbBounds.Height / 2f;
                float dx = mousePos.X - thumbCenterX;
                float dy = mousePos.Y - thumbCenterY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                // Glow starts at 80px distance, maxes at 0
                float maxDist = 80f;
                float proximity = Math.Clamp(1f - dist / maxDist, 0f, 1f);
                glowOpacity = proximity * 0.12f;
            }

            if (glowOpacity > 0.005f)
            {
                float glowPad = LerpF(2f, 6f, glowOpacity / 0.2f);
                ctx.DrawCircle(thumbBounds.Center,
                    glowRadius + glowPad,
                    fill: theme.Colors.Primary.Opacity(glowOpacity));

                if (!slider.IsPressed && glowOpacity > 0.01f)
                {
                    ControlStateAnimator.SignalActiveTransition();
                }
            }
        }

        // Thumb shadow and fill
        var thumbState = disabled ? t.ThumbDisabled : null;
        PaintShadow(thumbState?.Shadow ?? t.ThumbShadow, thumbBounds, t.ThumbRadius);
        PaintBrush(thumbState?.Fill ?? t.ThumbFill, thumbBounds, t.ThumbRadius);

        // Focus ring
        if (!disabled && anim.Focus.Current > 0.01f)
        {
            float focusT2 = anim.Focus.Current;
            float ringOffset = 3f;
            var ringRect = new Rect(
                thumbBounds.X - ringOffset,
                thumbBounds.Y - ringOffset,
                thumbBounds.Width + ringOffset * 2,
                thumbBounds.Height + ringOffset * 2);
            ctx.DrawRect(ringRect,
                stroke: new Stroke(theme.Colors.Focus.Opacity(focusT2), 2f),
                radius: t.ThumbRadius + ringOffset);
        }

        // Value label — springs in above thumb on hover
        if (slider.ShowValueLabelValue && !disabled)
        {
            float labelOpacity = reducedMotion ? (slider.IsHovered || slider.IsPressed ? 1f : 0f) : hoverT;
            if (labelOpacity > 0.01f)
            {
                string fmt = slider.FormatString ?? "F1";
                string labelText = value.ToString(fmt);
                float labelFontSize = 11f;

                var labelSize = ctx.MeasureText(labelText, labelFontSize);
                float labelX = MathF.Round(thumbBounds.X + thumbBounds.Width / 2f - labelSize.Width / 2f);

                // Spring-in: slight Y overshoot + scale
                float labelSpringScale = reducedMotion ? 1f : LerpF(0.8f, 1f, hoverT);
                float labelSlideY = reducedMotion ? 0f : LerpF(4f, 0f, hoverT);
                float labelY = MathF.Round(thumbBounds.Y - labelSize.Height - 6f + labelSlideY);

                if (!reducedMotion && MathF.Abs(labelSpringScale - 1f) > 0.001f)
                {
                    float scaledW = labelSize.Width * labelSpringScale;
                    float scaledH = labelSize.Height * labelSpringScale;
                    labelX += (labelSize.Width - scaledW) / 2f;
                    labelY += (labelSize.Height - scaledH) / 2f;
                }

                ctx.DrawText(labelText, labelX, labelY, labelFontSize,
                    theme.Colors.Text.Opacity(labelOpacity));
            }
        }
    }

    // ── Select / MultiSelect / Combobox shared animation state ──────

    /// <summary>
    /// Per-dropdown animation state for highlight sliding between items.
    /// Keyed by RuntimeHelpers.GetHashCode(node) in a static dictionary.
    /// </summary>
    private sealed class DropdownAnimState
    {
        internal AnimationChannel HighlightY;
        internal int LastHighlightIndex = -1;
        internal long LastAdvanceTimestamp;
        internal long LastAccessTimestamp;

        // Select value crossfade state
        internal string? PreviousDisplayText;
        internal string? CurrentDisplayText;
        internal AnimationChannel CrossfadeT;
        internal bool HasCrossfade;

        internal void AdvanceToNow()
        {
            long now = Stopwatch.GetTimestamp();
            if (LastAdvanceTimestamp == 0)
            {
                LastAdvanceTimestamp = now;
                return;
            }

            float dt = (float)Stopwatch.GetElapsedTime(LastAdvanceTimestamp, now).TotalSeconds;
            LastAdvanceTimestamp = now;
            dt = Math.Min(dt, 0.1f);
            HighlightY.Advance(dt);
            CrossfadeT.Advance(dt);
        }
    }

    /// <summary>
    /// Per-pill animation state for MultiSelect pill add/remove animations.
    /// Keyed by pill label string in a static dictionary.
    /// </summary>
    private sealed class PillAnimState
    {
        internal float ScaleT = 1f;
        internal float OpacityT = 1f;
        internal long AppearTimestamp;
        internal long RemoveTimestamp;
        internal bool IsRemoving;
    }

    private static readonly Dictionary<int, DropdownAnimState> dropdownAnimStates = new();
    private static readonly Dictionary<int, Dictionary<string, PillAnimState>> pillAnimStates = new();

    /// <summary>
    /// Transfers pill animation state from an old MultiSelect node to its
    /// replacement so that pill add/remove animations survive tree rebuilds.
    /// Called by the reconciler during <see cref="Reconciler.TransferInteractiveState"/>.
    /// </summary>
    internal static void TransferPillAnimState(Node from, Node to)
    {
        int oldKey = RuntimeHelpers.GetHashCode(from);
        int newKey = RuntimeHelpers.GetHashCode(to);
        if (oldKey == newKey)
        {
            return;
        }

        if (pillAnimStates.Remove(oldKey, out var states))
        {
            pillAnimStates[newKey] = states;
        }
    }

    /// <summary>
    /// Paints a chevron with optional rotation (0 = pointing down, 180 = pointing up).
    /// </summary>
    private void PaintChevronDown(float x, float y, float size, ColorValue color, float rotationDegrees = 0f)
    {
        float centerX = x + size / 2f;
        float centerY = y + size / 2f;

        if (MathF.Abs(rotationDegrees) > 0.1f)
        {
            using var rotate = ctx.PushRotate(
                Angle.Degrees(rotationDegrees),
                new Point(centerX, centerY));
            PaintChevronPath(x, y, size, color);
        }
        else
        {
            PaintChevronPath(x, y, size, color);
        }
    }

    private void PaintChevronPath(float x, float y, float size, ColorValue color)
    {
        float midX = x + size / 2f;
        float bottomY = y + size * 0.6f;
        var path = PathBuilder.Rent()
            .MoveTo(new Point(x, y + size * 0.3f))
            .LineTo(new Point(midX, bottomY))
            .LineTo(new Point(x + size, y + size * 0.3f))
            .BuildTransient();
        ctx.DrawPath(path, stroke: new Stroke(color, 1.5f, StrokeCap.Round, StrokeJoin.Round));
    }

    /// <summary>
    /// Draws the trailing chevron affordance for Select/MultiSelect/Combobox, honoring
    /// <see cref="SelectTheme.ChevronStyle"/>: either a single rotating caret (default)
    /// or Apple's combo-box accent box with a single white downward chevron.
    /// </summary>
    private void PaintSelectChevron(Rect bounds, float opacity, float openT, bool isOpen)
    {
        var t = theme.Select;

        if (t.ChevronStyle == SelectChevronStyle.ComboBox)
        {
            // Accent rounded square inset from the trailing edge (macOS NSComboBox).
            float inset = 3f;
            float boxH = bounds.Height - inset * 2f;
            float boxW = boxH;
            float boxX = bounds.X + bounds.Width - inset - boxW;
            float boxY = bounds.Y + inset;
            var box = new Rect(boxX, boxY, boxW, boxH);
            float boxRadius = Math.Max(2f, t.Radius - inset);
            ctx.DrawRect(box, t.ChevronBoxColor.Opacity(opacity), radius: boxRadius);

            // A single white downward chevron, centered. Depth ≈ half-width gives a
            // ~90° interior angle — Apple's chevron, not the shallow/wide default.
            float chevW = boxW * 0.46f;
            float chevDepth = chevW * 0.5f;
            float left = boxX + (boxW - chevW) / 2f;
            float topY = boxY + boxH / 2f - chevDepth / 2f;
            var ink = t.ChevronBoxTextColor.Opacity(opacity);
            var path = PathBuilder.Rent()
                .MoveTo(new Point(left, topY))
                .LineTo(new Point(left + chevW / 2f, topY + chevDepth))
                .LineTo(new Point(left + chevW, topY))
                .BuildTransient();
            ctx.DrawPath(path, stroke: new Stroke(ink, 1.75f, StrokeCap.Round, StrokeJoin.Round));
            return;
        }

        float chevronX = bounds.X + bounds.Width - t.PaddingH - t.ChevronSize;
        float chevronY = bounds.Y + (bounds.Height - t.ChevronSize) / 2f;
        float chevronRotation = ControlStateAnimator.ReducedMotion ? (isOpen ? 180f : 0f) : openT * 180f;
        PaintChevronDown(chevronX, chevronY, t.ChevronSize, t.ChevronColor.Opacity(opacity), chevronRotation);
    }

    /// <summary>
    /// Gets or creates dropdown highlight animation state for the given node.
    /// </summary>
    private static DropdownAnimState GetDropdownAnimState(Node node)
    {
        int key = RuntimeHelpers.GetHashCode(node);
        if (!dropdownAnimStates.TryGetValue(key, out var state))
        {
            state = new DropdownAnimState();
            state.LastAdvanceTimestamp = Stopwatch.GetTimestamp();
            dropdownAnimStates[key] = state;
        }

        state.LastAccessTimestamp = Stopwatch.GetTimestamp();
        state.AdvanceToNow();
        return state;
    }

    /// <summary>
    /// Computes per-item stagger opacity and Y-offset for dropdown item animation.
    /// Returns (opacity, slideY) for the item at the given visible index.
    /// </summary>
    private static (float opacity, float slideY) GetItemStagger(float openT, int visibleIndex, bool reducedMotion)
    {
        if (reducedMotion || openT >= 1f)
        {
            return (openT, 0f);
        }

        // Each item starts slightly later: 25ms delay per item (normalized to openT space)
        // The stagger window is the first 60% of the animation, items fade/slide during that
        const float staggerDelay = 0.04f; // per item in openT units
        const float itemDuration = 0.5f;  // each item's animation duration in openT units
        float itemStart = visibleIndex * staggerDelay;
        float itemEnd = itemStart + itemDuration;

        float itemT = Math.Clamp((openT - itemStart) / (itemEnd - itemStart), 0f, 1f);

        // Ease out curve
        float eased = 1f - (1f - itemT) * (1f - itemT);
        float opacity = eased;
        float slideY = LerpF(6f, 0f, eased);

        return (opacity, slideY);
    }

    /// <summary>
    /// Draws a single chevron ("v"/"^") centered in <paramref name="box"/> with the same
    /// ~90° bend used on the dropdown disclosure (depth = half-width). Replaces the solid
    /// ▲/▼ glyphs on steppers and time spinners so every up/down affordance is an Apple
    /// chevron, not a filled triangle.
    /// </summary>
    private void PaintChevronGlyph(Rect box, ColorValue color, bool pointUp, float strokeWidth = 1.5f)
    {
        float w = MathF.Min(box.Width, box.Height * 2f) * 0.5f;
        float depth = w * 0.5f;
        float cx = box.X + box.Width / 2f;
        float cy = box.Y + box.Height / 2f;
        float left = cx - w / 2f;
        float right = cx + w / 2f;
        float yArms = pointUp ? cy + depth / 2f : cy - depth / 2f;
        float yTip = pointUp ? cy - depth / 2f : cy + depth / 2f;
        var path = PathBuilder.Rent()
            .MoveTo(new Point(left, yArms))
            .LineTo(new Point(cx, yTip))
            .LineTo(new Point(right, yArms))
            .BuildTransient();
        ctx.DrawPath(path, stroke: new Stroke(color, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
    }

    /// <summary>
    /// Draws the Select-family field border and, while opening, the soft focus glow.
    /// The border lerps gray → accent with <paramref name="openT"/> so an open dropdown
    /// reads as a glowing accent edge (a crisp line under a tight ring), identical to the
    /// focused TextInput — keeping every bordered input consistent.
    /// </summary>
    private void PaintSelectFieldBorder(Rect bounds, float opacity, float openT)
    {
        var t = theme.Select;

        if (t.BorderWidth > 0)
        {
            var borderColor = ColorValue.Lerp(t.BorderColor, theme.Colors.Primary, openT);
            ctx.DrawRect(bounds,
                stroke: new Stroke(borderColor.Opacity(opacity), t.BorderWidth),
                radius: t.Radius);
        }

        // Soft glow hugs the accent edge (offset = border width) and fades in with open.
        if (openT > 0.001f)
        {
            float ringOffset = t.BorderWidth;
            var ringRect = new Rect(
                bounds.X - ringOffset, bounds.Y - ringOffset,
                bounds.Width + ringOffset * 2, bounds.Height + ringOffset * 2);
            ctx.DrawRect(ringRect,
                stroke: new Stroke(t.FocusRingColor.ScaleAlpha(openT), t.FocusRingWidth),
                radius: t.Radius + ringOffset);
        }
    }

    // ── Select ────────────────────────────────────────────────────────

    private void PaintSelect(ISelectNode select, Rect bounds)
    {
        var t = theme.Select;
        var node = (Node)select;

        float opacity = select.IsNodeDisabled ? t.DisabledOpacity : 1f;

        // Animate open/close state for dropdown. Opening uses a lightly-overshooting
        // spring so the panel "pops" open (closing stays a clean ease-out — see
        // ReconcileOpen). The pop is what makes the control feel alive.
        ControlStateAnimator.ReconcileOpen(node, select.IsOpen, AnimationModel.Spring.Standard);
        float openT = ControlStateAnimator.GetOpenProgress(node);

        // Background
        ctx.DrawRect(bounds, t.Background.Opacity(opacity), radius: t.Radius);

        // Hover overlay
        if (!select.IsNodeDisabled && node.IsHovered && !select.IsOpen)
        {
            ctx.DrawRect(bounds, theme.Colors.SurfaceAlt.Opacity(0.3f), radius: t.Radius);
        }

        // Border + soft focus glow (accent edge on open) — matches focused TextInput.
        PaintSelectFieldBorder(bounds, opacity, select.IsNodeDisabled ? 0f : openT);

        // Text — selected value or placeholder, with crossfade on change
        string? displayText = select.SelectedDisplayText;
        var selectAnimState = GetDropdownAnimState(node);
        bool reducedMotionSelect = ControlStateAnimator.ReducedMotion;

        if (!string.IsNullOrEmpty(displayText))
        {
            // Detect value change for crossfade
            if (displayText != selectAnimState.CurrentDisplayText)
            {
                selectAnimState.PreviousDisplayText = selectAnimState.CurrentDisplayText;
                selectAnimState.CurrentDisplayText = displayText;

                if (!string.IsNullOrEmpty(selectAnimState.PreviousDisplayText) && !reducedMotionSelect)
                {
                    selectAnimState.CrossfadeT.SnapTo(0f);
                    selectAnimState.CrossfadeT.SetTarget(1f, AnimationModel.EaseOut(Duration.Ms(200)));
                    selectAnimState.HasCrossfade = true;
                    ControlStateAnimator.SignalActiveTransition();
                }
                else
                {
                    selectAnimState.HasCrossfade = false;
                }
            }

            float crossfadeProgress = selectAnimState.CrossfadeT.Current;

            // Draw fading-out old text
            if (selectAnimState.HasCrossfade && crossfadeProgress < 0.99f
                && !string.IsNullOrEmpty(selectAnimState.PreviousDisplayText))
            {
                float oldOpacity = opacity * (1f - crossfadeProgress);
                PaintText(selectAnimState.PreviousDisplayText, bounds, t.PaddingH,
                    t.TextColor.Opacity(oldOpacity));

                if (selectAnimState.CrossfadeT.IsAnimating)
                {
                    ControlStateAnimator.SignalActiveTransition();
                }
            }

            // Draw fading-in new text
            float newOpacity = selectAnimState.HasCrossfade
                ? opacity * crossfadeProgress
                : opacity;
            PaintText(displayText, bounds, t.PaddingH, t.TextColor.Opacity(newOpacity));

            if (selectAnimState.HasCrossfade && !selectAnimState.CrossfadeT.IsAnimating)
            {
                selectAnimState.HasCrossfade = false;
            }
        }
        else
        {
            selectAnimState.CurrentDisplayText = null;
            selectAnimState.HasCrossfade = false;

            string placeholder = select.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor.Opacity(opacity));
            }
        }

        // Chevron arrow — rotates with open animation (or Apple popup-button box)
        PaintSelectChevron(bounds, opacity, openT, select.IsOpen);

        // Dropdown panel — deferred to overlay pass so it paints on top of
        // all sibling nodes, not behind them in the Column's paint order.
        if ((select.IsOpen || openT > 0.001f) && !select.IsNodeDisabled && select.OptionCount > 0)
        {
            // Capture absolute position of the trigger for the deferred paint
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintSelectDropdown(select, absTrigger);
            });
        }
        else
        {
            select.DropdownBounds = default;
        }
    }

    /// <summary>
    /// Computes the on-screen bounds and visible-item count for an option dropdown
    /// anchored to a trigger. Clamps the height to the room available in the
    /// viewport and flips the dropdown above the trigger when there is more room
    /// there, so options never fall off the bottom of the window unreachably —
    /// the fitted item count then enables scrolling for the remaining options.
    /// The returned bounds are the unscaled (pre-open-animation) rect used for
    /// hit testing and item layout.
    /// </summary>
    private (Rect Bounds, int VisibleCount) ComputeOptionDropdown(
        Rect triggerBounds, int optionCount, float itemHeight, float maxHeight, float gap)
    {
        const float edgeMargin = 8f;
        float viewportHeight = ViewportLogicalHeight;

        float naturalHeight = Math.Min(optionCount * itemHeight, maxHeight);
        float spaceBelow = viewportHeight - (triggerBounds.Y + triggerBounds.Height + gap) - edgeMargin;
        float spaceAbove = triggerBounds.Y - gap - edgeMargin;

        // Flip above only when it does not fit below and there is genuinely more room up top.
        bool placeAbove = naturalHeight > spaceBelow && spaceAbove > spaceBelow;
        float available = Math.Max(placeAbove ? spaceAbove : spaceBelow, itemHeight);

        int fit = (int)(Math.Min(naturalHeight, available) / itemHeight);
        int visibleCount = Math.Clamp(fit, 1, Math.Max(1, optionCount));
        float height = visibleCount * itemHeight;

        float y = placeAbove
            ? triggerBounds.Y - gap - height
            : triggerBounds.Y + triggerBounds.Height + gap;

        return (new Rect(triggerBounds.X, y, triggerBounds.Width, height), visibleCount);
    }

    private void PaintSelectDropdown(ISelectNode select, Rect triggerBounds)
    {
        var t = theme.Select;
        var node = (Node)select;
        int optionCount = select.OptionCount;
        float itemHeight = t.ItemHeight;

        // Open animation progress
        float openT = ControlStateAnimator.GetOpenProgress(node);
        if (openT < 0.001f)
        {
            select.DropdownBounds = default;
            return;
        }

        // Store item height for InputDispatcher hit testing
        select.DropdownItemHeight = itemHeight;

        float gap = 4f;
        var (dropdownBounds, visibleCount) = ComputeOptionDropdown(
            triggerBounds, optionCount, itemHeight, t.DropdownMaxHeight, gap);
        int maxScrollOffset = Math.Max(0, optionCount - visibleCount);
        select.ScrollOffset = Math.Clamp(select.ScrollOffset, 0, maxScrollOffset);

        // Store bounds for hit testing by InputDispatcher
        select.DropdownBounds = dropdownBounds;

        // Apply open animation: scale from 0.95→1.0, slight Y shift.
        // Background stays fully opaque — fading it makes the dropdown look
        // transparent over controls that are painted later (painter's algorithm).
        float dropdownOpacity = 1.0f;
        float dropdownScale = LerpF(0.93f, 1f, openT);
        float slideY = LerpF(-6f, 0f, openT);

        // Scale around the top-center of the dropdown (anchored to trigger)
        if (MathF.Abs(dropdownScale - 1f) > 0.001f || MathF.Abs(slideY) > 0.01f)
        {
            float anchorX = dropdownBounds.X + dropdownBounds.Width / 2f;
            float anchorY = dropdownBounds.Y;
            float scaledW = dropdownBounds.Width * dropdownScale;
            float scaledH = dropdownBounds.Height * dropdownScale;
            dropdownBounds = new Rect(
                anchorX - scaledW / 2f,
                anchorY + slideY,
                scaledW,
                scaledH);
        }

        // Shadow — deepen with open progress (lerp opacity)
        if (t.DropdownShadow is not null && dropdownOpacity > 0.1f)
        {
            PaintShadow(t.DropdownShadow, dropdownBounds, t.DropdownRadius);
        }

        // Background panel
        ctx.DrawRect(dropdownBounds, t.DropdownBackground.Opacity(dropdownOpacity),
            radius: t.DropdownRadius);

        // Border
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(dropdownBounds,
                stroke: new Stroke(t.BorderColor.Opacity(openT), t.BorderWidth),
                radius: t.DropdownRadius);
        }

        // Clip to dropdown bounds for item rendering
        using var clip = ctx.PushClip(dropdownBounds);

        // Smooth highlight sliding
        var animState = GetDropdownAnimState(node);
        int highlightedIndex = select.HighlightedIndex;
        int scrollOffset = select.ScrollOffset;
        int selectedIndex = select.SelectedIndex;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float highlightTargetY = highlightedIndex >= 0
            ? dropdownBounds.Y + (highlightedIndex - scrollOffset) * itemHeight
            : -1f;

        if (highlightedIndex >= 0 && highlightedIndex != animState.LastHighlightIndex)
        {
            if (animState.LastHighlightIndex < 0)
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }
            else if (!reducedMotion)
            {
                animState.HighlightY.SetTarget(highlightTargetY, AnimationModel.Spring.Snappy);
                ControlStateAnimator.SignalActiveTransition();
            }
            else
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }

            animState.LastHighlightIndex = highlightedIndex;
        }
        else if (highlightedIndex >= 0)
        {
            // Same index but scroll may have changed — update target
            animState.HighlightY.SnapTo(highlightTargetY);
        }

        // Draw sliding highlight background
        if (highlightedIndex >= 0 && animState.HighlightY.Current >= dropdownBounds.Y - itemHeight)
        {
            float hlY = animState.HighlightY.Current;
            var hlBounds = new Rect(dropdownBounds.X, hlY, dropdownBounds.Width, itemHeight);
            float hlRadius = (hlY <= dropdownBounds.Y + 0.5f) ? t.DropdownRadius : 0f;
            ctx.DrawRect(hlBounds, t.ItemHoverBackground.Opacity(openT), radius: hlRadius);

            if (animState.HighlightY.IsAnimating)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        // Render visible options starting from scroll offset
        float itemY = dropdownBounds.Y;

        for (int vi = 0; vi < visibleCount && (scrollOffset + vi) < optionCount; vi++)
        {
            int i = scrollOffset + vi;
            var itemBounds = new Rect(
                dropdownBounds.X,
                itemY,
                dropdownBounds.Width,
                itemHeight);

            // Per-item stagger animation
            var (itemOpacity, itemSlideY) = GetItemStagger(openT, vi, reducedMotion);
            if (itemOpacity < 0.01f)
            {
                itemY += itemHeight;
                continue;
            }

            var staggeredBounds = new Rect(itemBounds.X, itemBounds.Y + itemSlideY,
                itemBounds.Width, itemBounds.Height);

            // Selected item — show a checkmark on the left
            if (i == selectedIndex)
            {
                float checkSize = 14f;
                float checkX = staggeredBounds.X + t.ItemPaddingH;
                float checkY = staggeredBounds.Y + (itemHeight - checkSize) / 2f;
                var checkBounds = new Rect(checkX, checkY, checkSize, checkSize);
                PaintCheckMark(checkBounds, theme.Colors.PrimaryText.Opacity(itemOpacity));
            }

            // Option label text
            float textPadding = t.ItemPaddingH + 20f;
            var textColor = (i == selectedIndex ? theme.Colors.PrimaryText : t.TextColor).Opacity(itemOpacity);
            PaintText(select.GetOptionLabel(i), staggeredBounds, textPadding, textColor);

            itemY += itemHeight;
        }
    }

    // ── MultiSelect ───────────────────────────────────────────────────

    private void PaintMultiSelect(IMultiSelectNode ms, Rect bounds)
    {
        var t = theme.Select;
        var node = (Node)ms;

        float opacity = ms.IsNodeDisabled ? t.DisabledOpacity : 1f;

        // Animate open/close state for dropdown
        ControlStateAnimator.ReconcileOpen(node, ms.IsOpen, AnimationModel.Spring.Standard);
        float openT = ControlStateAnimator.GetOpenProgress(node);

        // Background
        ctx.DrawRect(bounds, t.Background.Opacity(opacity), radius: t.Radius);

        // Hover overlay
        if (!ms.IsNodeDisabled && node.IsHovered && !ms.IsOpen)
        {
            ctx.DrawRect(bounds, theme.Colors.SurfaceAlt.Opacity(0.3f), radius: t.Radius);
        }

        // Border + soft focus glow (accent edge on open) — matches focused TextInput.
        PaintSelectFieldBorder(bounds, opacity, ms.IsNodeDisabled ? 0f : openT);

        // Content area (excluding the chevron). The ComboBox chevron style draws a
        // full-height accent BOX (≈ field height wide), far wider than the plain
        // chevron glyph — reserve for whichever is actually painted, or the trailing
        // pill / "+N" chip slides under the box (see PaintSelectChevron geometry).
        float chevronReserve = t.ChevronStyle == SelectChevronStyle.ComboBox
            ? bounds.Height + 3f
            : t.PaddingH + t.ChevronSize + 4f;
        float contentWidth = bounds.Width - t.PaddingH - chevronReserve;

        if (ms.SelectedCount > 0)
        {
            if (ms.ShowPills)
            {
                PaintMultiSelectPills(ms, bounds, contentWidth, opacity);
            }
            else if (ms.ShowCount)
            {
                string countText = $"{ms.SelectedCount} selected";
                PaintText(countText, bounds, t.PaddingH, t.TextColor.Opacity(opacity));
            }
            else
            {
                // Default: show comma-separated labels
                var labels = ms.SelectedPillLabels;
                string text = string.Join(", ", labels);
                PaintText(text, bounds, t.PaddingH, t.TextColor.Opacity(opacity));
            }
        }
        else
        {
            string placeholder = ms.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor.Opacity(opacity));
            }
        }

        // Chevron — rotates with open animation (or Apple popup-button box)
        PaintSelectChevron(bounds, opacity, openT, ms.IsOpen);

        // Dropdown overlay
        if ((ms.IsOpen || openT > 0.001f) && !ms.IsNodeDisabled && ms.OptionCount > 0)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintMultiSelectDropdown(ms, absTrigger);
            });
        }
        else
        {
            ms.DropdownBounds = default;
        }
    }

    private void PaintMultiSelectPills(IMultiSelectNode ms, Rect bounds, float contentWidth, float opacity)
    {
        var t = theme.Select;
        var node = (Node)ms;
        var labels = ms.SelectedPillLabels;
        int maxPills = ms.MaxPillsVisibleCount ?? labels.Count;
        int pillCount = Math.Min(labels.Count, maxPills);
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float pillHeight = 22f;
        float pillPadH = 8f;
        float pillGap = 4f;
        float pillRadius = 4f;
        float x = bounds.X + t.PaddingH;
        float pillY = bounds.Y + (bounds.Height - pillHeight) / 2f;

        // Get or create pill animation state for this MultiSelect node
        int nodeKey = RuntimeHelpers.GetHashCode(node);
        if (!pillAnimStates.TryGetValue(nodeKey, out var pillStates))
        {
            pillStates = new Dictionary<string, PillAnimState>();
            pillAnimStates[nodeKey] = pillStates;
        }

        long now = Stopwatch.GetTimestamp();

        // Mark new pills that just appeared
        var activeLabels = new HashSet<string>();
        for (int i = 0; i < pillCount; i++)
        {
            string label = labels[i];
            activeLabels.Add(label);
            if (!pillStates.ContainsKey(label))
            {
                pillStates[label] = new PillAnimState
                {
                    AppearTimestamp = now,
                    ScaleT = reducedMotion ? 1f : 0f,
                    OpacityT = reducedMotion ? 1f : 0f,
                };
            }
        }

        // Mark removed pills
        foreach (var kvp in pillStates)
        {
            if (!activeLabels.Contains(kvp.Key) && !kvp.Value.IsRemoving)
            {
                kvp.Value.IsRemoving = true;
                kvp.Value.RemoveTimestamp = now;
            }
        }

        // Clean up fully removed pills
        var toRemove = new List<string>();
        foreach (var kvp in pillStates)
        {
            if (kvp.Value.IsRemoving)
            {
                float elapsed = (float)Stopwatch.GetElapsedTime(kvp.Value.RemoveTimestamp, now).TotalSeconds;
                if (elapsed > 0.3f || reducedMotion)
                {
                    toRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in toRemove)
        {
            pillStates.Remove(key);
        }

        using var clip = ctx.PushClip(new Rect(
            bounds.X + t.PaddingH, bounds.Y,
            contentWidth, bounds.Height));

        // Reserve room for the trailing "+N" chip so it is never clipped or pushed under
        // the chevron: pills lay out within the content area minus the widest chip that
        // could appear (uses the total count so "+12" fits as easily as "+3").
        float contentBoundary = bounds.X + t.PaddingH + contentWidth;
        float moreReserve = pillCount < labels.Count
            ? MeasureTextWidth($"+{labels.Count}") + pillPadH * 2f + pillGap
            : 0f;
        float pillBoundary = contentBoundary - moreReserve;

        bool anyAnimating = false;
        int drawnPills = 0;

        for (int i = 0; i < pillCount; i++)
        {
            string label = labels[i];
            float textWidth = MeasureTextWidth(label);
            float pillWidth = textWidth + pillPadH * 2;

            // Always show at least one pill; otherwise stop once this pill (with the
            // reserved "+N" chip) would overflow.
            if (drawnPills > 0 && x + pillWidth > pillBoundary)
            {
                break;
            }

            // Animate pill appearance
            float pillScale = 1f;
            float pillOpacity = opacity;

            if (pillStates.TryGetValue(label, out var ps) && !reducedMotion)
            {
                float elapsed = (float)Stopwatch.GetElapsedTime(ps.AppearTimestamp, now).TotalSeconds;

                if (ps.IsRemoving)
                {
                    float removeElapsed = (float)Stopwatch.GetElapsedTime(ps.RemoveTimestamp, now).TotalSeconds;
                    float removeT = Math.Clamp(removeElapsed / 0.15f, 0f, 1f);
                    pillOpacity = opacity * (1f - removeT);
                    pillScale = LerpF(1f, 0.8f, removeT);
                    anyAnimating = true;
                }
                else if (elapsed < 0.25f)
                {
                    // Spring-like scale in: overshoot then settle
                    float t2 = Math.Clamp(elapsed / 0.25f, 0f, 1f);
                    float eased = 1f - MathF.Pow(1f - t2, 3f);
                    float overshoot = eased < 0.7f
                        ? LerpF(0.5f, 1.08f, eased / 0.7f)
                        : LerpF(1.08f, 1f, (eased - 0.7f) / 0.3f);
                    pillScale = overshoot;
                    pillOpacity = opacity * Math.Clamp(t2 * 3f, 0f, 1f);
                    anyAnimating = true;
                }
            }

            float scaledWidth = pillWidth * pillScale;
            float scaledHeight = pillHeight * pillScale;
            float offsetX = (pillWidth - scaledWidth) / 2f;
            float offsetY = (pillHeight - scaledHeight) / 2f;

            var pillRect = new Rect(x + offsetX, pillY + offsetY, scaledWidth, scaledHeight);
            var chipBg = theme.Colors.Primary.Opacity(0.2f * (pillOpacity / Math.Max(opacity, 0.01f)));
            ctx.DrawRect(pillRect, chipBg, radius: pillRadius);

            var textBounds = new Rect(x + pillPadH, pillY, textWidth, pillHeight);
            PaintText(label, textBounds, 0, t.TextColor.Opacity(pillOpacity), fontSize: 12f);

            x += pillWidth + pillGap;
            drawnPills++;
        }

        if (anyAnimating)
        {
            ControlStateAnimator.SignalActiveTransition();
        }

        // Show "+N more" for whatever didn't fit (count from the pills actually drawn).
        int remaining = labels.Count - drawnPills;
        if (remaining > 0)
        {
            string moreText = $"+{remaining}";
            float moreWidth = MeasureTextWidth(moreText) + pillPadH * 2;
            var moreBounds = new Rect(x, pillY, moreWidth, pillHeight);
            ctx.DrawRect(moreBounds, theme.Colors.Primary.Opacity(0.15f * opacity), radius: pillRadius);
            PaintText(moreText, moreBounds, pillPadH, t.TextColor.Opacity(opacity * 0.7f), fontSize: 12f);
        }
    }

    private void PaintMultiSelectDropdown(IMultiSelectNode ms, Rect triggerBounds)
    {
        var t = theme.Select;
        var cb = theme.Checkbox;
        var node = (Node)ms;
        int optionCount = ms.OptionCount;
        float itemHeight = t.ItemHeight;

        // Open animation progress
        float openT = ControlStateAnimator.GetOpenProgress(node);
        if (openT < 0.001f)
        {
            ms.DropdownBounds = default;
            return;
        }

        ms.DropdownItemHeight = itemHeight;

        float gap = 4f;
        var (dropdownBounds, visibleCount) = ComputeOptionDropdown(
            triggerBounds, optionCount, itemHeight, t.DropdownMaxHeight, gap);
        int maxScrollOffset = Math.Max(0, optionCount - visibleCount);
        ms.ScrollOffset = Math.Clamp(ms.ScrollOffset, 0, maxScrollOffset);

        ms.DropdownBounds = dropdownBounds;

        // Apply open animation: scale, slight Y shift.
        // Background stays fully opaque — fading it makes the dropdown look
        // transparent over controls that are painted later (painter's algorithm).
        float dropdownOpacity = 1.0f;
        float dropdownScale = LerpF(0.93f, 1f, openT);
        float slideY = LerpF(-6f, 0f, openT);

        if (MathF.Abs(dropdownScale - 1f) > 0.001f || MathF.Abs(slideY) > 0.01f)
        {
            float anchorX = dropdownBounds.X + dropdownBounds.Width / 2f;
            float anchorY = dropdownBounds.Y;
            float scaledW = dropdownBounds.Width * dropdownScale;
            float scaledH = dropdownBounds.Height * dropdownScale;
            dropdownBounds = new Rect(
                anchorX - scaledW / 2f,
                anchorY + slideY,
                scaledW,
                scaledH);
        }

        // Shadow
        if (t.DropdownShadow is not null && dropdownOpacity > 0.1f)
        {
            PaintShadow(t.DropdownShadow, dropdownBounds, t.DropdownRadius);
        }

        // Background panel
        ctx.DrawRect(dropdownBounds, t.DropdownBackground.Opacity(dropdownOpacity),
            radius: t.DropdownRadius);

        // Border
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(dropdownBounds,
                stroke: new Stroke(t.BorderColor.Opacity(openT), t.BorderWidth),
                radius: t.DropdownRadius);
        }

        using var clip = ctx.PushClip(dropdownBounds);

        // Smooth highlight sliding
        var animState = GetDropdownAnimState(node);
        int highlightedIndex = ms.HighlightedIndex;
        int scrollOffset = ms.ScrollOffset;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float highlightTargetY = highlightedIndex >= 0
            ? dropdownBounds.Y + (highlightedIndex - scrollOffset) * itemHeight
            : -1f;

        if (highlightedIndex >= 0 && highlightedIndex != animState.LastHighlightIndex)
        {
            if (animState.LastHighlightIndex < 0)
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }
            else if (!reducedMotion)
            {
                animState.HighlightY.SetTarget(highlightTargetY, AnimationModel.Spring.Snappy);
                ControlStateAnimator.SignalActiveTransition();
            }
            else
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }

            animState.LastHighlightIndex = highlightedIndex;
        }
        else if (highlightedIndex >= 0)
        {
            animState.HighlightY.SnapTo(highlightTargetY);
        }

        // Draw sliding highlight
        if (highlightedIndex >= 0 && animState.HighlightY.Current >= dropdownBounds.Y - itemHeight)
        {
            float hlY = animState.HighlightY.Current;
            var hlBounds = new Rect(dropdownBounds.X, hlY, dropdownBounds.Width, itemHeight);
            float hlRadius = (hlY <= dropdownBounds.Y + 0.5f) ? t.DropdownRadius : 0f;
            ctx.DrawRect(hlBounds, t.ItemHoverBackground.Opacity(openT), radius: hlRadius);

            if (animState.HighlightY.IsAnimating)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        float itemY = dropdownBounds.Y;
        float checkboxSize = 16f;

        for (int vi = 0; vi < visibleCount && (scrollOffset + vi) < optionCount; vi++)
        {
            int i = scrollOffset + vi;
            var itemBounds = new Rect(
                dropdownBounds.X,
                itemY,
                dropdownBounds.Width,
                itemHeight);

            // Per-item stagger
            var (itemOpacity, itemSlideY) = GetItemStagger(openT, vi, reducedMotion);
            if (itemOpacity < 0.01f)
            {
                itemY += itemHeight;
                continue;
            }

            var staggeredBounds = new Rect(itemBounds.X, itemBounds.Y + itemSlideY,
                itemBounds.Width, itemBounds.Height);

            // Checkbox
            bool isSelected = ms.IsItemSelected(i);
            float cbX = staggeredBounds.X + t.ItemPaddingH;
            float cbY = staggeredBounds.Y + (itemHeight - checkboxSize) / 2f;
            var boxBounds = new Rect(cbX, cbY, checkboxSize, checkboxSize);

            if (isSelected)
            {
                ctx.DrawRect(boxBounds, cb.CheckedBg.Opacity(itemOpacity), radius: cb.Radius);
                PaintCheckMark(boxBounds, cb.CheckColor.Opacity(itemOpacity));
            }
            else
            {
                ctx.DrawRect(boxBounds, cb.Background.Opacity(itemOpacity),
                    stroke: new Stroke(cb.BorderColor.Opacity(itemOpacity), cb.BorderWidth),
                    radius: cb.Radius);
            }

            // Option label
            float textPadding = t.ItemPaddingH + checkboxSize + 8f;
            PaintText(
                ms.GetOptionLabel(i),
                staggeredBounds,
                textPadding,
                t.TextColor.Opacity(itemOpacity));

            itemY += itemHeight;
        }
    }

    // ── Combobox ───────────────────────────────────────────────────────

    private void PaintCombobox(IComboboxNode cb, Rect bounds)
    {
        var t = theme.Select;
        var node = (Node)cb;
        float opacity = cb.IsNodeDisabled ? t.DisabledOpacity : 1f;

        // Animate open/close state for dropdown
        ControlStateAnimator.ReconcileOpen(node, cb.IsOpen, AnimationModel.Spring.Standard);
        float openT = ControlStateAnimator.GetOpenProgress(node);

        // Background
        ctx.DrawRect(bounds, t.Background.Opacity(opacity), radius: t.Radius);

        // Hover overlay
        if (!cb.IsNodeDisabled && node.IsHovered && !cb.IsOpen)
        {
            ctx.DrawRect(bounds, theme.Colors.SurfaceAlt.Opacity(0.3f), radius: t.Radius);
        }

        // Border + soft focus glow (accent edge on open) — matches focused TextInput.
        PaintSelectFieldBorder(bounds, opacity, cb.IsNodeDisabled ? 0f : openT);

        // Content: show search text when open, display text when closed
        float chevronReserve = t.PaddingH + t.ChevronSize + 4f;
        if (cb.IsOpen)
        {
            string searchText = cb.SearchText;
            if (!string.IsNullOrEmpty(searchText))
            {
                PaintText(searchText, bounds, t.PaddingH, t.TextColor.Opacity(opacity));
            }
            else
            {
                string placeholder = cb.Placeholder.Resolve();
                if (!string.IsNullOrEmpty(placeholder))
                {
                    PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor.Opacity(opacity));
                }
            }

            // Blinking cursor after text
            string cursorRef = cb.SearchText;
            float textWidth = string.IsNullOrEmpty(cursorRef) ? 0 : MeasureTextWidth(cursorRef, theme.Typography.Body.Size);
            float cursorX = bounds.X + t.PaddingH + textWidth + 1f;
            float cursorY = bounds.Y + 8f;
            float cursorH = bounds.Height - 16f;
            ctx.DrawRect(new Rect(cursorX, cursorY, 1.5f, cursorH), theme.Colors.Focus);
        }
        else
        {
            string? displayText = cb.DisplayText;
            if (!string.IsNullOrEmpty(displayText))
            {
                PaintText(displayText, bounds, t.PaddingH, t.TextColor.Opacity(opacity));
            }
            else
            {
                string placeholder = cb.Placeholder.Resolve();
                if (!string.IsNullOrEmpty(placeholder))
                {
                    PaintText(placeholder, bounds, t.PaddingH, t.PlaceholderColor.Opacity(opacity));
                }
            }
        }

        // Chevron — rotates with open animation (or Apple popup-button box)
        PaintSelectChevron(bounds, opacity, openT, cb.IsOpen);

        // Dropdown overlay (deferred)
        if ((cb.IsOpen || openT > 0.001f) && !cb.IsNodeDisabled && cb.FilteredOptionCount > 0)
        {
            float absX = absoluteX;
            float absY = absoluteY;
            float triggerW = bounds.Width;
            float triggerH = bounds.Height;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var absTrigger = new Rect(absX, absY, triggerW, triggerH);
                PaintComboboxDropdown(cb, absTrigger);
            });
        }
        else
        {
            cb.DropdownBounds = default;
        }
    }

    private void PaintComboboxDropdown(IComboboxNode cb, Rect triggerBounds)
    {
        var t = theme.Select;
        var node = (Node)cb;
        int optionCount = cb.FilteredOptionCount;
        float itemHeight = t.ItemHeight;

        // Open animation progress
        float openT = ControlStateAnimator.GetOpenProgress(node);
        if (openT < 0.001f)
        {
            cb.DropdownBounds = default;
            return;
        }

        cb.DropdownItemHeight = itemHeight;

        float gap = 4f;
        var (dropdownBounds, visibleCount) = ComputeOptionDropdown(
            triggerBounds, optionCount, itemHeight, t.DropdownMaxHeight, gap);
        int maxScrollOffset = Math.Max(0, optionCount - visibleCount);
        cb.ScrollOffset = Math.Clamp(cb.ScrollOffset, 0, maxScrollOffset);

        cb.DropdownBounds = dropdownBounds;

        // Apply open animation: scale, slight Y shift.
        // Background stays fully opaque — fading it makes the dropdown look
        // transparent over controls that are painted later (painter's algorithm).
        float dropdownOpacity = 1.0f;
        float dropdownScale = LerpF(0.93f, 1f, openT);
        float slideY = LerpF(-6f, 0f, openT);

        if (MathF.Abs(dropdownScale - 1f) > 0.001f || MathF.Abs(slideY) > 0.01f)
        {
            float anchorX = dropdownBounds.X + dropdownBounds.Width / 2f;
            float anchorY = dropdownBounds.Y;
            float scaledW = dropdownBounds.Width * dropdownScale;
            float scaledH = dropdownBounds.Height * dropdownScale;
            dropdownBounds = new Rect(
                anchorX - scaledW / 2f,
                anchorY + slideY,
                scaledW,
                scaledH);
        }

        // Shadow
        if (t.DropdownShadow is not null && dropdownOpacity > 0.1f)
        {
            PaintShadow(t.DropdownShadow, dropdownBounds, t.DropdownRadius);
        }

        // Background stays fully opaque — fading it makes the dropdown look
        // transparent over controls that are painted later (painter's algorithm).
        ctx.DrawRect(dropdownBounds, t.DropdownBackground,
            radius: t.DropdownRadius);

        // Border
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(dropdownBounds,
                stroke: new Stroke(t.BorderColor.Opacity(openT), t.BorderWidth),
                radius: t.DropdownRadius);
        }

        // Clip to dropdown
        using var clip = ctx.PushClip(dropdownBounds);

        // Smooth highlight sliding
        var animState = GetDropdownAnimState(node);
        int highlightedIndex = cb.HighlightedIndex;
        int scrollOffset = cb.ScrollOffset;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float highlightTargetY = highlightedIndex >= 0
            ? dropdownBounds.Y + (highlightedIndex - scrollOffset) * itemHeight
            : -1f;

        if (highlightedIndex >= 0 && highlightedIndex != animState.LastHighlightIndex)
        {
            if (animState.LastHighlightIndex < 0)
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }
            else if (!reducedMotion)
            {
                animState.HighlightY.SetTarget(highlightTargetY, AnimationModel.Spring.Snappy);
                ControlStateAnimator.SignalActiveTransition();
            }
            else
            {
                animState.HighlightY.SnapTo(highlightTargetY);
            }

            animState.LastHighlightIndex = highlightedIndex;
        }
        else if (highlightedIndex >= 0)
        {
            animState.HighlightY.SnapTo(highlightTargetY);
        }

        // Draw sliding highlight
        if (highlightedIndex >= 0 && animState.HighlightY.Current >= dropdownBounds.Y - itemHeight)
        {
            float hlY = animState.HighlightY.Current;
            var hlBounds = new Rect(dropdownBounds.X, hlY, dropdownBounds.Width, itemHeight);
            float hlRadius = (hlY <= dropdownBounds.Y + 0.5f) ? t.DropdownRadius : 0f;
            ctx.DrawRect(hlBounds, t.ItemHoverBackground.Opacity(openT), radius: hlRadius);

            if (animState.HighlightY.IsAnimating)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        float itemY = dropdownBounds.Y;

        for (int vi = 0; vi < visibleCount && (scrollOffset + vi) < optionCount; vi++)
        {
            int i = scrollOffset + vi;
            var itemBounds = new Rect(
                dropdownBounds.X,
                itemY,
                dropdownBounds.Width,
                itemHeight);

            // Per-item stagger
            var (itemOpacity, itemSlideY) = GetItemStagger(openT, vi, reducedMotion);
            if (itemOpacity < 0.01f)
            {
                itemY += itemHeight;
                continue;
            }

            var staggeredBounds = new Rect(itemBounds.X, itemBounds.Y + itemSlideY,
                itemBounds.Width, itemBounds.Height);

            // Option label
            var textColor = (i == highlightedIndex ? theme.Colors.PrimaryText : t.TextColor).Opacity(itemOpacity);
            PaintText(
                cb.GetFilteredOptionLabel(i),
                staggeredBounds,
                t.ItemPaddingH,
                textColor);

            itemY += itemHeight;
        }
    }

    // ── Canvas ─────────────────────────────────────────────────────────

    private void PaintCanvas(CanvasNode canvas, Rect bounds)
    {
        // Invoke the frame callback for continuous canvases (animation/physics).
        // This runs before drawing so the updated state is reflected immediately.
        if (canvas.IsContinuous)
        {
            canvas.OnFrame!(deltaTime);
            HasActiveContinuousCanvases = true;
        }

        // Canvas nodes get their own DrawContext with the bounds as the canvas size.
        // The actual draw callback invocation uses the main DrawContext translated
        // to the canvas origin.
        using var _ = ctx.PushTranslate(bounds.X, bounds.Y);
        using var clip = ctx.PushClip(new Rect(0, 0, bounds.Width, bounds.Height));

        // The canvas draws in its own local space, so expose its bounds as the size for
        // the OnDraw callback — then restore, or every popup/overlay painted later this
        // frame (they read ctx.Size for viewport math) would see the canvas size, not the
        // window. ctx.Size is otherwise the device-pixel frame size (see DrawContext.Size).
        var savedSize = ctx.Size;
        ctx.Size = new Size(bounds.Width, bounds.Height);
        try
        {
            canvas.OnDraw(ctx, new Size(bounds.Width, bounds.Height));
        }
        finally
        {
            ctx.Size = savedSize;
        }
    }

    // ── SplitView ──────────────────────────────────────────────────────

    private void PaintSplitView(SplitView sv, Rect bounds)
    {
        const float dividerWidth = 6f;

        // Set absolute bounds for InputDispatcher divider drag calculations
        sv.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Clip each pane to its own bounds. Without this, content wider (or taller)
        // than a pane — e.g. a toolbar row that doesn't fit — paints straight across
        // the divider and over the neighbouring pane. Every other content region in
        // the framework clips its overflow; a SplitView pane must too. Deferred
        // overlays (dropdowns, popups) paint at root level and are unaffected.
        var firstPaneBounds = sv.First.LayoutData.Bounds;
        var secondPaneBounds = sv.Second.LayoutData.Bounds;

        using (ctx.PushClip(firstPaneBounds))
        {
            PaintRecursive(sv.First);
        }

        // Paint divider between panes
        float dividerX, dividerY, dividerW, dividerH;
        if (sv.Orientation == SplitOrientation.Horizontal)
        {
            float firstW = sv.First.LayoutData.Bounds.Width;
            dividerX = bounds.X + firstW;
            dividerY = bounds.Y;
            dividerW = dividerWidth;
            dividerH = bounds.Height;
        }
        else
        {
            float firstH = sv.First.LayoutData.Bounds.Height;
            dividerX = bounds.X;
            dividerY = bounds.Y + firstH;
            dividerW = bounds.Width;
            dividerH = dividerWidth;
        }

        // Proximity brightness: divider brightens as cursor approaches
        float proximity = 0f;
        if (!ControlStateAnimator.ReducedMotion)
        {
            var mousePos = InputDispatcher.CurrentMousePosition;
            float absDivCX = absoluteX + dividerX + dividerW / 2f;
            float absDivCY = absoluteY + dividerY + dividerH / 2f;
            float dist = sv.Orientation == SplitOrientation.Horizontal
                ? MathF.Abs(mousePos.X - absDivCX)
                : MathF.Abs(mousePos.Y - absDivCY);
            proximity = Math.Clamp(1f - dist / 40f, 0f, 1f);
        }

        // Divider background (brightens with proximity) — uses theme border color
        var borderColor = theme.Colors.Border;
        var divBg = ColorValue.Lerp(borderColor, borderColor.Lighten(0.15f), proximity);
        ctx.DrawRect(new Rect(dividerX, dividerY, dividerW, dividerH), divBg);

        // Divider grip dots (scale-in with proximity)
        float gripOpacity = 0.6f + proximity * 0.4f;
        float gripScale = 0.8f + proximity * 0.4f;
        var gripColor = theme.Colors.TextMuted.Opacity(gripOpacity);
        float dotSize = 2f * gripScale;
        if (sv.Orientation == SplitOrientation.Horizontal)
        {
            float cx = dividerX + dividerW / 2f;
            float cy = dividerY + dividerH / 2f;
            for (int i = -1; i <= 1; i++)
            {
                ctx.DrawRect(new Rect(cx - dotSize / 2f, cy + i * 6f - dotSize / 2f, dotSize, dotSize),
                    gripColor, radius: dotSize / 2f);
            }
        }
        else
        {
            float cx = dividerX + dividerW / 2f;
            float cy = dividerY + dividerH / 2f;
            for (int i = -1; i <= 1; i++)
            {
                ctx.DrawRect(new Rect(cx + i * 6f - dotSize / 2f, cy - dotSize / 2f, dotSize, dotSize),
                    gripColor, radius: dotSize / 2f);
            }
        }

        if (proximity > 0.01f)
        {
            ControlStateAnimator.SignalActiveTransition();
        }

        using (ctx.PushClip(secondPaneBounds))
        {
            PaintRecursive(sv.Second);
        }
    }

    // ── Accordion ──────────────────────────────────────────────────────

    private void PaintAccordion(Accordion acc, Rect bounds)
    {
        foreach (var section in acc.Sections)
        {
            PaintRecursive(section);
        }
    }

    // ── Expander ───────────────────────────────────────────────────────

    private void PaintExpander(Expander exp, Rect bounds)
    {
        bool isExpanded = exp.IsExpanded;

        float headerHeight = 40f;

        // Header background with hover/press effects
        var headerBounds = new Rect(0, 0, bounds.Width, headerHeight);
        if (exp.IsPressed)
        {
            ctx.DrawRect(headerBounds, theme.Colors.SurfaceAlt.Opacity(0.15f));
        }
        else if (exp.IsHovered)
        {
            ctx.DrawRect(headerBounds, theme.Colors.SurfaceAlt.Opacity(0.08f));
        }

        // Paint header: custom node or text
        if (!exp.HeaderNode.IsLayoutEmpty)
        {
            PaintRecursive(exp.HeaderNode);
        }
        else
        {
            string headerText = exp.HeaderText.Resolve();
            if (!string.IsNullOrEmpty(headerText))
            {
                var textBounds = new Rect(12f, 0, bounds.Width - 40f, headerHeight);
                PaintText(headerText, textBounds, 0, theme.Colors.Text,
                    fontWeight: FontWeight.SemiBold);
            }
        }

        // Chevron indicator (right side of header) — rotates with spring
        float chevronSize = 10f;
        float chevronX = bounds.Width - 24f;
        float chevronCY = headerHeight / 2f;
        var chevronColor = theme.Colors.TextMuted;
        float chevronStroke = 1.5f;

        // Animate expand state
        ControlStateAnimator.ReconcileOpen(exp, isExpanded,
            AnimationModel.Spring.Snappy);
        float expandT = ControlStateAnimator.GetOpenProgress(exp);
        bool expandReducedMotion = ControlStateAnimator.ReducedMotion;
        float chevronRotation = expandReducedMotion
            ? (isExpanded ? 90f : 0f)
            : (expandT * 90f);

        // Always draw > shape, rotate 0→90° for expanded
        var chevronCenter = new Point(chevronX + chevronSize / 2f, chevronCY);
        var chevronRotateScope = ctx.PushRotate(
            Angle.Degrees(chevronRotation), chevronCenter);
        ctx.DrawLine(
            new Point(chevronX + chevronSize / 4f, chevronCY - chevronSize / 2f),
            new Point(chevronX + chevronSize * 3f / 4f, chevronCY),
            new Stroke(chevronColor, chevronStroke, StrokeCap.Round, StrokeJoin.Round));
        ctx.DrawLine(
            new Point(chevronX + chevronSize * 3f / 4f, chevronCY),
            new Point(chevronX + chevronSize / 4f, chevronCY + chevronSize / 2f),
            new Stroke(chevronColor, chevronStroke, StrokeCap.Round, StrokeJoin.Round));
        chevronRotateScope.Dispose();

        // Separator line under header
        ctx.DrawLine(
            new Point(0, headerHeight),
            new Point(bounds.Width, headerHeight),
            new Stroke(theme.Colors.Border, 0.5f));

        // Paint content if expanded (with opacity fade-in)
        if ((isExpanded || expandT > 0.001f) && !exp.Content.IsLayoutEmpty)
        {
            ScopeGuard contentOpacity = default;
            if (!expandReducedMotion && expandT < 0.999f)
            {
                contentOpacity = ctx.PushOpacity(expandT);
            }
            PaintRecursive(exp.Content);
            contentOpacity.Dispose();
        }
    }

    // ── Tag ───────────────────────────────────────────────────────────

    private void PaintTag(Tag tag, Rect bounds)
    {
        bool isSelected = tag.Selected?.Value ?? false;
        bool isRemovable = tag.OnRemove != null;
        bool isToggleable = tag.OnToggle != null;
        bool isInteractive = isRemovable || isToggleable;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Spring hover/press for interactive tags
        float hoverT = 0f;
        float pressT = 0f;
        if (isInteractive)
        {
            var tagAnim = ControlStateAnimator.Reconcile(tag,
                AnimationModel.Spring.Bouncy, AnimationModel.Spring.Snappy);
            if (!reducedMotion)
            {
                hoverT = tagAnim.Hover.Current;
                pressT = tagAnim.Press.Current;
            }
        }

        // Colors
        var bgColor = isSelected
            ? theme.Colors.Primary.Opacity(0.2f)
            : theme.Colors.SurfaceAlt;
        var textColor = isSelected
            ? theme.Colors.PrimaryText
            : theme.Colors.Text;
        var borderColor = isSelected
            ? theme.Colors.Primary.Opacity(0.5f)
            : theme.Colors.Border;

        // Hover/press color effects (lerp with spring progress)
        if (isInteractive)
        {
            if (pressT > 0.01f)
            {
                var pressColor = isSelected
                    ? theme.Colors.Primary.Opacity(0.35f)
                    : theme.Colors.Text.Opacity(0.12f);
                bgColor = ColorValue.Lerp(bgColor, pressColor, pressT);
            }
            else if (hoverT > 0.01f)
            {
                var hoverColor = isSelected
                    ? theme.Colors.Primary.Opacity(0.28f)
                    : theme.Colors.Text.Opacity(0.06f);
                bgColor = ColorValue.Lerp(bgColor, hoverColor, hoverT);
            }
        }

        // Measure actual text to compute precise pill size
        float fontSize = theme.Typography.Body.Size * 0.85f;
        float textHeight = fontSize * 1.3f;
        float paddingH = 12f;
        float xIconWidth = 10f;
        float xGap = 10f;

        var measuredTextSize = ctx.MeasureText(tag.Label, fontSize);
        // Small buffer to prevent × from kissing the last glyph
        float textWidth = measuredTextSize.Width + 2f;

        // Compute actual pill width from measured text (not layout estimate)
        float actualPillWidth = paddingH + textWidth + paddingH;
        if (isRemovable)
        {
            actualPillWidth = paddingH + textWidth + xGap + xIconWidth + paddingH;
        }

        // Center the correctly-sized pill within the allocated bounds
        float pillX = (bounds.Width - actualPillWidth) / 2f;
        if (pillX < 0f)
        {
            pillX = 0f;
        }

        var pillRect = new Rect(pillX, 0, actualPillWidth, bounds.Height);
        float radius = bounds.Height / 2f;

        // Background pill
        ctx.DrawRect(pillRect, bgColor, radius: radius);

        // Border
        ctx.DrawRect(pillRect, stroke: new Stroke(borderColor, 1f), radius: radius);

        // Label text — positioned within the pill
        float textY = (bounds.Height - textHeight) / 2f;
        float textX = pillX + paddingH;
        var textBounds = new Rect(textX, textY, measuredTextSize.Width, textHeight);
        PaintText(tag.Label, textBounds, 0, textColor, fontSize: fontSize);

        // Remove × button — positioned after measured text within the pill
        if (isRemovable)
        {
            float xSize = 4f;
            float xCenterX = pillX + paddingH + textWidth + xGap + xIconWidth / 2f;
            float xCenterY = bounds.Height / 2f;
            var xColor = theme.Colors.TextMuted;

            // X rotates 90° on hover for visual interest
            float xRotation = hoverT * 90f;
            if (hoverT > 0.01f)
            {
                xColor = ColorValue.Lerp(theme.Colors.TextMuted, theme.Colors.Text, hoverT);
            }

            ScopeGuard rotScope = default;
            if (xRotation > 0.5f)
            {
                rotScope = ctx.PushRotate(Angle.Degrees(xRotation), new Point(xCenterX, xCenterY));
            }

            ctx.DrawLine(
                new Point(xCenterX - xSize, xCenterY - xSize),
                new Point(xCenterX + xSize, xCenterY + xSize),
                new Stroke(xColor, 1.5f, StrokeCap.Round, StrokeJoin.Round));
            ctx.DrawLine(
                new Point(xCenterX + xSize, xCenterY - xSize),
                new Point(xCenterX - xSize, xCenterY + xSize),
                new Stroke(xColor, 1.5f, StrokeCap.Round, StrokeJoin.Round));

            rotScope.Dispose();
        }
    }

    // ── Avatar ────────────────────────────────────────────────────────

    // Consistent avatar background colors derived from name hash.
    private static readonly ColorValue[] AvatarColors =
    [
        new("#E91E63"), // Pink
        new("#9C27B0"), // Purple
        new("#3F51B5"), // Indigo
        new("#2196F3"), // Blue
        new("#00897B"), // Teal
        new("#43A047"), // Green
        new("#F57C00"), // Orange
        new("#5D4037"), // Brown
    ];

    private void PaintAvatar(Avatar av, Rect bounds)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        var center = new Point(bounds.Width / 2f, bounds.Height / 2f);
        float radius = size / 2f;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Spring hover scale
        var avAnim = ControlStateAnimator.Reconcile(av,
            AnimationModel.Spring.Bouncy, AnimationModel.Spring.Snappy);
        float hoverT = reducedMotion ? 0f : avAnim.Hover.Current;
        float avatarScale = 1f + hoverT * 0.05f;

        ScopeGuard scaleScope = default;
        if (avatarScale > 1.001f)
        {
            scaleScope = ctx.PushScale(avatarScale, avatarScale, center);
        }

        // Determine shape
        bool isCircle = av.ShapeValue == AvatarShape.Circle;
        float cornerRadius = av.ShapeValue switch
        {
            AvatarShape.Circle  => radius,
            AvatarShape.Rounded => size * 0.15f,
            _                   => 0f
        };

        // Background color based on name hash
        int colorIndex = av.Name != null
            ? Math.Abs(av.Name.GetHashCode(StringComparison.Ordinal)) % AvatarColors.Length
            : 0;
        var bgColor = av.Name != null
            ? AvatarColors[colorIndex]
            : theme.Colors.TextMuted;

        // Draw background
        if (isCircle)
        {
            ctx.DrawCircle(center, radius, bgColor);
        }
        else
        {
            ctx.DrawRect(bounds, bgColor, radius: cornerRadius);
        }

        // Clip content to avatar shape so nothing overflows
        using (ctx.PushRoundedClip(bounds, isCircle ? radius : cornerRadius))
        {
            // Draw initials or fallback icon
            if (!string.IsNullOrEmpty(av.Initials))
            {
                float fontSize = size * 0.36f;

                // Measure actual text width for precise centering
                string? fontPath = null;
                if (ctx.DefaultFontPath != null)
                {
                    fontPath = ctx.ResolveFontPath(ctx.DefaultFontPath, FontWeight.SemiBold);
                }
                // Centre on the glyph's *rasterized ink* box, not its advance/line
                // box: letters have asymmetric side bearings (e.g. "A" leans right,
                // "C" sits left) and the line box reserves ascent/descent room caps
                // don't fill, so advance/line-box centring reads visibly off in a
                // small circle. Measure the ink at the *device* pixel size the
                // renderer actually rasterises (fontSize × PixelRatio) — hinting is
                // size-dependent, so logical-size ink leaves a per-letter lean at
                // fractional DPI — then scale the visual centre back to logical.
                // Falls back to HarfBuzz outline extents, then the advance box.
                float pr = ctx.PixelRatio > 0f ? ctx.PixelRatio : 1f;
                var ink = ctx.MeasureGlyphInkBounds(av.Initials, fontSize * pr, fontPath);
                float textX, textY;
                if (ink.HasValue)
                {
                    // Horizontal: centre the ink's *mass* (coverage centroid), not its
                    // bounding box — most caps carry more weight on one side than
                    // their box implies, so box-centring reads consistently off (a
                    // "B" leans left). Vertical: keep the box centre; caps are near-
                    // uniform vertically and the mass centroid would over-correct
                    // (an "A" drops low). Both measured at the device pixel size.
                    float opticalCenterX = ink.Value.OpticalCenterX / pr;
                    textX = bounds.Width / 2f - opticalCenterX;
                    textY = bounds.Height / 2f - ink.Value.VisualCenterY / pr;
                }
                else if (ctx.MeasureGlyphVisualBounds(av.Initials, fontSize, fontPath) is { } visual)
                {
                    float visualCenterX = visual.XBearing + visual.VisualWidth / 2f;
                    textX = bounds.Width / 2f - visualCenterX;
                    textY = bounds.Height / 2f - visual.VisualCenterY;
                }
                else
                {
                    var textSize = ctx.MeasureText(av.Initials, fontSize, fontPath);
                    textX = (bounds.Width - textSize.Width) / 2f;
                    textY = (bounds.Height - textSize.Height) / 2f;
                }

                ctx.DrawText(av.Initials, textX, textY, fontSize,
                    new ColorValue("#FFFFFF"), fontPath: fontPath);
            }
            else
            {
                // Anonymous: simple person silhouette (head + shoulders)
                var headColor = new ColorValue("#FFFFFF").Opacity(0.7f);
                float headR = size * 0.15f;
                ctx.DrawCircle(new Point(center.X, center.Y - size * 0.1f), headR, headColor);
                // Shoulders
                float shoulderY = center.Y + size * 0.15f;
                float shoulderW = size * 0.3f;
                float shoulderCY = shoulderY + shoulderW * 0.5f;
                ctx.DrawCircle(new Point(center.X, shoulderCY), shoulderW, headColor);
            }
        }

        // Presence indicator dot
        if (av.PresenceValue is { } presence)
        {
            float dotSize = size * 0.28f;
            float dotRadius = dotSize / 2f;
            float inset = dotRadius * 0.7f;

            // Position based on BadgePosition
            var dotCenter = av.PresencePosition switch
            {
                BadgePosition.TopLeft     => new Point(inset, inset),
                BadgePosition.TopRight    => new Point(bounds.Width - inset, inset),
                BadgePosition.BottomLeft  => new Point(inset, bounds.Height - inset),
                _                         => new Point(bounds.Width - inset, bounds.Height - inset),
            };

            // White ring behind dot
            ctx.DrawCircle(dotCenter, dotRadius + 1.5f, theme.Colors.Surface);

            // Status color
            var statusColor = presence switch
            {
                PresenceStatus.Online       => new ColorValue("#10B981"), // Green
                PresenceStatus.Away         => new ColorValue("#F59E0B"), // Amber
                PresenceStatus.Busy         => new ColorValue("#EF4444"), // Red
                PresenceStatus.DoNotDisturb => new ColorValue("#EF4444"), // Red
                PresenceStatus.Offline      => theme.Colors.TextMuted,
                _                           => theme.Colors.TextMuted,
            };
            ctx.DrawCircle(dotCenter, dotRadius, statusColor);

            // Online presence: breathing pulse ring
            if (presence == PresenceStatus.Online && !reducedMotion)
            {
                double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(0).TotalMilliseconds;
                float breathe = MathF.Max(0, MathF.Sin((float)(elapsedMs * 0.003)));
                if (breathe > 0.01f)
                {
                    ctx.DrawCircle(dotCenter, dotRadius + 2f + breathe * 2f,
                        stroke: new Stroke(statusColor.Opacity(breathe * 0.4f), 1.5f));
                    ControlStateAnimator.SignalActiveTransition();
                }
            }

            // DND: draw minus sign
            if (presence == PresenceStatus.DoNotDisturb)
            {
                float lineHalf = dotRadius * 0.45f;
                ctx.DrawLine(
                    new Point(dotCenter.X - lineHalf, dotCenter.Y),
                    new Point(dotCenter.X + lineHalf, dotCenter.Y),
                    new Stroke(new ColorValue("#FFFFFF"), 1.5f, StrokeCap.Round, StrokeJoin.Round));
            }
        }

        scaleScope.Dispose();
    }

    // ── ProgressRing ──────────────────────────────────────────────────

    private void PaintProgressRing(ProgressRing pr, Rect bounds)
    {
        // Snap to integer pixels to eliminate subpixel anti-aliasing blur
        float size = MathF.Round(Math.Min(bounds.Width, bounds.Height));
        float strokeWidth = pr.StrokeWidthOverride ?? MathF.Max(3f, MathF.Round(size * 0.06f));
        float radius = (size - strokeWidth) / 2f;
        var center = new Point(bounds.Width / 2f, bounds.Height / 2f);

        if (radius <= 0)
        {
            return;
        }

        var trackColor = pr.TrackColorOverride ?? theme.Colors.Border;
        var fillColor = pr.FillColorOverride ?? theme.Colors.Primary;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        if (pr.Mode == ProgressMode.Indeterminate)
        {
            // Continuous rotation
            double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(0).TotalMilliseconds;
            float cycleDuration = 1200f;
            float rotationDegrees = (float)((elapsedMs / cycleDuration * 360.0) % 360.0);

            // Pass the rotated start angle directly instead of using a transform,
            // avoiding a GPU renderer bug where rotated arcs are not rendered correctly.
            ctx.DrawArc(center, radius, Angle.Degrees(rotationDegrees), Angle.Degrees(270),
                new Stroke(fillColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));

            HasActiveSpinners = true;
        }
        else
        {
            float clampedValue = Math.Clamp(pr.Value, 0f, 1f);

            // Spring-animate the arc sweep
            ControlStateAnimator.ReconcileValue(pr, clampedValue, AnimationModel.Spring.Snappy);
            float animatedValue = reducedMotion
                ? clampedValue
                : ControlStateAnimator.GetValueProgress(pr);

            // Fill arc — starts from top (-90°), sweeps clockwise
            if (animatedValue > 0.001f)
            {
                float sweepDegrees = animatedValue * 360f;
                if (sweepDegrees >= 359f)
                {
                    // Full circle — use DrawCircle to avoid arc polyline approximation
                    // and TryDetectArc circumcenter failure when start == end point.
                    ctx.DrawCircle(center, radius,
                        stroke: new Stroke(fillColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
                }
                else
                {
                    ctx.DrawArc(center, radius, Angle.Degrees(-90f), Angle.Degrees(sweepDegrees),
                        new Stroke(fillColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
                }

                // Completion glow when at 100%
                if (clampedValue >= 0.999f && !reducedMotion)
                {
                    double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(0).TotalMilliseconds;
                    float pulse = MathF.Max(0, MathF.Sin((float)(elapsedMs * 0.004)));
                    if (pulse > 0.01f)
                    {
                        ctx.DrawCircle(center, radius,
                            stroke: new Stroke(fillColor.Opacity(pulse * 0.25f), strokeWidth + 2f));
                        ControlStateAnimator.SignalActiveTransition();
                    }
                }
            }
        }

        // Center label
        if (pr.ShowValueEnabled && pr.Mode == ProgressMode.Determinate)
        {
            string label = pr.LabelFormatter != null
                ? pr.LabelFormatter(pr.Value)
                : $"{(int)(pr.Value * 100)}%";
            float labelFontSize = MathF.Max(10f, MathF.Round(size * 0.20f));
            var labelSize = ctx.MeasureText(label, labelFontSize);
            float labelX = MathF.Round((bounds.Width - labelSize.Width) / 2f);
            float labelY = MathF.Round((bounds.Height - labelSize.Height) / 2f);
            ctx.DrawText(label, labelX, labelY, labelFontSize, theme.Colors.Text);
        }
    }

    // ── SegmentedControl ──────────────────────────────────────────────

    private void PaintSegmentedControl(ISegmentedControl sc, Rect bounds)
    {
        // Store absolute bounds for click coordinate mapping
        sc.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        int count = sc.SegmentCount;
        if (count == 0)
        {
            return;
        }

        var node = (Node)sc;
        bool disabled = sc.IsControlDisabled;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float radius = bounds.Height / 2f;
        var bgColor = theme.Colors.SurfaceAlt;
        var borderColor = theme.Colors.Border;
        var selectedBg = theme.Colors.Primary;
        var selectedText = theme.Colors.TextOnPrimary;
        var normalText = theme.Colors.Text;

        // Animate hover/press
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            node, hoverModel, pressModel, isDisabled: disabled);
        float hoverT = anim.Hover.Current;

        // Outer pill background
        ctx.DrawRect(bounds, bgColor, radius: radius);
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor, 1f), radius: radius);

        int selectedIdx = sc.SelectedIndex;
        float fontSize = theme.Typography.Body.Size * 0.85f;
        float paddingH = 16f;

        // Compute variable-width segments matching the layout measurement so every
        // segment has the same horizontal padding regardless of label length.
        float[] segLefts = new float[count];
        float[] segWidths = new float[count];
        float measuredTotal = 0f;
        for (int i = 0; i < count; i++)
        {
            string label = sc.GetSegmentLabel(i);
            float textW = label.Length * fontSize * 0.55f;
            float w = textW + paddingH * 2f;
            segWidths[i] = w;
            measuredTotal += w;
        }

        float scale = measuredTotal > 0f ? bounds.Width / measuredTotal : 1f;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            for (int i = 0; i < count; i++)
            {
                segWidths[i] *= scale;
            }
        }

        float xAcc = 0f;
        for (int i = 0; i < count; i++)
        {
            segLefts[i] = xAcc;
            xAcc += segWidths[i];
        }

        static float EdgeAt(float idx, float[] lefts, float[] widths)
        {
            int n = lefts.Length;
            if (n == 0)
            {
                return 0f;
            }
            if (idx <= 0f)
            {
                return lefts[0] + idx * widths[0];
            }
            if (idx >= n - 1)
            {
                return lefts[n - 1] + (idx - (n - 1)) * widths[n - 1];
            }
            int i = (int)MathF.Floor(idx);
            float t = idx - i;
            return lefts[i] + (lefts[i + 1] - lefts[i]) * t;
        }

        // Animate selection indicator slide with spring
        // ReconcileValue drives the indicator X position as a fraction (selectedIdx)
        if (selectedIdx >= 0)
        {
            ControlStateAnimator.ReconcileValue(node, selectedIdx, AnimationModel.Spring.Bouncy);
            float animatedIdx = anim.Value.Current;

            float selLeft = EdgeAt(animatedIdx, segLefts, segWidths) + 2f;
            float selRight = EdgeAt(animatedIdx + 1f, segLefts, segWidths) - 2f;
            float selX = selLeft;
            float selW = MathF.Max(0f, selRight - selLeft);
            float selY = 2f;
            float selH = bounds.Height - 4f;
            float selRadius = selH / 2f;
            ctx.DrawRect(new Rect(selX, selY, selW, selH), selectedBg, radius: selRadius);
        }

        // Paint segment labels
        for (int i = 0; i < count; i++)
        {
            string label = sc.GetSegmentLabel(i);
            bool isSelected = i == selectedIdx;

            // Text color crossfade — selected text is white, others are normal
            // For animated segments near the indicator, crossfade smoothly
            var textColor = isSelected ? selectedText : normalText;

            // Hover on non-selected: subtle background tint
            if (!isSelected && !disabled && node.IsHovered && !reducedMotion)
            {
                var mousePos = InputDispatcher.CurrentMousePosition;
                float relX = mousePos.X - absoluteX;
                int hoverSegment = 0;
                for (int s = 0; s < count; s++)
                {
                    if (relX < segLefts[s] + segWidths[s])
                    {
                        hoverSegment = s;
                        break;
                    }
                    hoverSegment = s;
                }
                if (hoverSegment == i && hoverT > 0.01f)
                {
                    float hoverBgOpacity = 0.06f * hoverT;
                    float hX = segLefts[i] + 2f;
                    float hW = segWidths[i] - 4f;
                    float hY = 2f;
                    float hH = bounds.Height - 4f;
                    ctx.DrawRect(new Rect(hX, hY, hW, hH),
                        theme.Colors.Primary.Opacity(hoverBgOpacity),
                        radius: hH / 2f);
                }
            }

            // Center text in segment
            var measuredText = ctx.MeasureText(label, fontSize);
            float textX = segLefts[i] + (segWidths[i] - measuredText.Width) / 2f;
            float textY = (bounds.Height - fontSize * 1.3f) / 2f;
            var textBounds = new Rect(textX, textY, measuredText.Width, fontSize * 1.3f);
            PaintText(label, textBounds, 0, textColor, fontSize: fontSize);

            // Dividers between non-selected segments
            if (i > 0 && i != selectedIdx && i - 1 != selectedIdx)
            {
                // Fade dividers near the animated indicator
                float divOpacity = 0.5f;
                if (selectedIdx >= 0 && !reducedMotion)
                {
                    float animIdx = anim.Value.Current;
                    float distFromIndicator = MathF.Min(MathF.Abs(i - animIdx), MathF.Abs(i - 1 - animIdx));
                    if (distFromIndicator < 1.2f)
                    {
                        divOpacity *= Math.Clamp(distFromIndicator - 0.2f, 0f, 1f);
                    }
                }

                float divX = segLefts[i];
                float divTop = bounds.Height * 0.25f;
                float divBottom = bounds.Height * 0.75f;
                ctx.DrawLine(new Point(divX, divTop), new Point(divX, divBottom),
                    new Stroke(borderColor.Opacity(divOpacity), 1f));
            }
        }
    }

    // ── Tooltip overlay ───────────────────────────────────────────────

    private void DeferTooltipIfHovered(Node node, string? tooltipText, Rect bounds)
    {
        if (string.IsNullOrEmpty(tooltipText) || !node.IsHovered)
        {
            return;
        }

        float absX = absoluteX;
        float absY = absoluteY;
        float w = bounds.Width;
        float h = bounds.Height;
        string text = tooltipText;

        deferredOverlays ??= [];
        deferredOverlays.Add(() =>
        {
            PaintTooltipOverlay(text, new Rect(absX, absY, w, h));
        });
    }

    private void PaintTooltipOverlay(string text, Rect targetBounds)
    {
        var tt = theme.Tooltip;
        float fontSize = tt.TextStyle.Size;

        // Measure actual text width for accurate tooltip sizing
        var textSize = ctx.MeasureText(text, fontSize);
        float textWidth = textSize.Width;
        float maxW = tt.MaxWidth;
        if (textWidth > maxW)
        {
            textWidth = maxW;
        }

        float padH = tt.Padding.Left + tt.Padding.Right;
        float padV = tt.Padding.Top + tt.Padding.Bottom;
        float tipW = textWidth + padH;
        float tipH = fontSize * 1.3f + padV;

        // Position above target, centered horizontally
        float tipX = targetBounds.X + (targetBounds.Width - tipW) / 2f;
        float tipY = targetBounds.Y - tipH - tt.ArrowHeight - 4f;

        // Clamp horizontally to the viewport. Clamp the right edge first, then the
        // left, so a tooltip anchored near either screen edge stays fully on screen
        // (the arrow still points at the target, wherever the bubble lands).
        float viewportW = ViewportLogicalWidth;
        if (tipX + tipW > viewportW - 4f)
        {
            tipX = viewportW - 4f - tipW;
        }
        if (tipX < 4f)
        {
            tipX = 4f;
        }
        bool flippedBelow = false;
        if (tipY < 4f)
        {
            tipY = targetBounds.Y + targetBounds.Height + tt.ArrowHeight + 4f;
            flippedBelow = true;
        }

        var tipBounds = new Rect(tipX, tipY, tipW, tipH);

        // Entrance scale + fade from anchor point
        bool tipReducedMotion = ControlStateAnimator.ReducedMotion;
        ScopeGuard tipScaleScope = default;
        ScopeGuard tipOpacityScope = default;
        if (!tipReducedMotion)
        {
            // Use a timestamp-based quick entrance (150ms)
            double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
            float entranceT = Math.Min(1f, (float)((elapsedMs % 60000.0) / 150.0));
            if (entranceT > 0.999f)
            {
                entranceT = 1f;
            }
            else
            {
                ControlStateAnimator.SignalActiveTransition();
            }
            // Scale from anchor: origin at the arrow point
            float anchorX = targetBounds.X + targetBounds.Width / 2f;
            float anchorY = flippedBelow ? tipBounds.Y : tipBounds.Bottom;
            float scale = 0.85f + 0.15f * entranceT;
            if (scale < 0.999f)
            {
                tipScaleScope = ctx.PushScale(scale, scale, new Point(anchorX, anchorY));
                tipOpacityScope = ctx.PushOpacity(entranceT);
            }
        }

        // Shadow (deepens during entrance)
        ctx.DrawBlurredRoundedRect(
            new Rect(tipBounds.X + 1, tipBounds.Y + 2, tipBounds.Width, tipBounds.Height),
            new ColorValue("#000000").Opacity(0.25f), radius: tt.Radius, blurSigma: 4f);

        // Background
        ctx.DrawRect(tipBounds, tt.Background, radius: tt.Radius);

        // Arrow pointing down at target
        float arrowCX = targetBounds.X + targetBounds.Width / 2f;
        float arrowTop = tipBounds.Y + tipBounds.Height;
        float arrowBottom = arrowTop + tt.ArrowHeight;
        ctx.DrawLine(
            new Point(arrowCX - tt.ArrowSize, arrowTop),
            new Point(arrowCX, arrowBottom),
            new Stroke(tt.Background, 2f));
        ctx.DrawLine(
            new Point(arrowCX, arrowBottom),
            new Point(arrowCX + tt.ArrowSize, arrowTop),
            new Stroke(tt.Background, 2f));

        // Text
        var textBounds = new Rect(
            tipBounds.X + tt.Padding.Left,
            tipBounds.Y + tt.Padding.Top,
            tipBounds.Width - padH,
            tipBounds.Height - padV);
        PaintText(text, textBounds, 0, tt.TextColor,
            fontSize: fontSize, alignment: TextAlignment.Center);

        tipOpacityScope.Dispose();
        tipScaleScope.Dispose();
    }

    // ── Breadcrumb ─────────────────────────────────────────────────────

    private void PaintBreadcrumb(Breadcrumb bc, Rect bounds)
    {
        if (bc.Segments.Count == 0)
        {
            return;
        }

        float fontSize = theme.Typography.Scale.Body.Size;
        float separatorPad = 4f;
        float itemPadH = 4f;
        float cursorX = bounds.X;
        float centerY = bounds.Y + bounds.Height / 2f;

        for (int i = 0; i < bc.Segments.Count; i++)
        {
            var seg = bc.Segments[i];
            bool isLast = i == bc.Segments.Count - 1;
            bool isClickable = seg.OnClick != null;

            var textSize = ctx.MeasureText(seg.Label, fontSize);
            float segWidth = textSize.Width + itemPadH * 2;

            // Determine color: last segment is normal text, clickable segments are primary
            var textColor = isLast ? theme.Colors.Text
                : isClickable ? theme.Colors.Primary
                : theme.Colors.TextMuted;

            float textX = MathF.Round(cursorX + itemPadH);
            float textY = MathF.Round(centerY - textSize.Height / 2f);
            ctx.DrawText(seg.Label, textX, textY, fontSize, textColor);

            cursorX += segWidth;

            // Draw separator chevron (except after last segment)
            if (!isLast)
            {
                float sepX = cursorX + separatorPad;
                float chevronSize = fontSize * 0.35f;
                float chevronY = centerY;

                // Draw a simple > chevron
                ctx.DrawLine(
                    new Point(sepX, chevronY - chevronSize),
                    new Point(sepX + chevronSize, chevronY),
                    new Stroke(theme.Colors.TextMuted, 1.5f, StrokeCap.Round, StrokeJoin.Round));
                ctx.DrawLine(
                    new Point(sepX + chevronSize, chevronY),
                    new Point(sepX, chevronY + chevronSize),
                    new Stroke(theme.Colors.TextMuted, 1.5f, StrokeCap.Round, StrokeJoin.Round));

                cursorX += chevronSize + separatorPad * 2 + separatorPad;
            }
        }

        // Store absolute bounds for click handling
        bc.AbsoluteBounds = new Rect(absoluteX + bounds.X, absoluteY + bounds.Y,
            bounds.Width, bounds.Height);
    }

    // ── NumberInput ────────────────────────────────────────────────────

    private void PaintNumberInput(INumberInput ni, Rect bounds)
    {
        ni.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        float fontSize = theme.Typography.Scale.Body.Size;
        float radius = theme.TextInput.Radius;
        float buttonWidth = 28f;
        float paddingH = 8f;
        bool disabled = ni.IsDisabled;

        // Outer border
        var bgColor = disabled ? theme.Colors.SurfaceAlt : theme.Colors.Surface;
        ctx.DrawRect(bounds, bgColor, radius: radius);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border, 1f), radius: radius);

        if (ni.StepperPos == StepperPosition.Split)
        {
            // − button on left
            var leftBtn = new Rect(bounds.X, bounds.Y, buttonWidth, bounds.Height);
            bool leftHover = ni.HoveredStepperButton == 0;
            bool leftPress = ni.PressedStepperButton == 0;
            PaintStepperButton(leftBtn, "−", ni.IsAtMin || disabled, radius, isLeft: true, isHovered: leftHover, isPressed: leftPress);

            // + button on right
            var rightBtn = new Rect(bounds.X + bounds.Width - buttonWidth, bounds.Y, buttonWidth, bounds.Height);
            bool rightHover = ni.HoveredStepperButton == 1;
            bool rightPress = ni.PressedStepperButton == 1;
            PaintStepperButton(rightBtn, "+", ni.IsAtMax || disabled, radius, isLeft: false, isHovered: rightHover, isPressed: rightPress);

            // Value in center
            float valueX = bounds.X + buttonWidth;
            float valueW = bounds.Width - buttonWidth * 2;
            var valueBounds = new Rect(valueX, bounds.Y, valueW, bounds.Height);
            PaintText(ni.DisplayValue, valueBounds, paddingH, theme.Colors.Text,
                fontSize: fontSize, alignment: TextAlignment.Center);
        }
        else if (ni.StepperPos == StepperPosition.Right)
        {
            // Value on left
            float valueW = bounds.Width - buttonWidth;
            var valueBounds = new Rect(bounds.X, bounds.Y, valueW, bounds.Height);
            PaintText(ni.DisplayValue, valueBounds, paddingH, theme.Colors.Text,
                fontSize: fontSize);

            // Stepper stack on right — top half is +, bottom half is −
            float btnX = bounds.X + bounds.Width - buttonWidth;
            float halfH = bounds.Height / 2f;

            var topBtn = new Rect(btnX, bounds.Y, buttonWidth, halfH);
            bool topHover = ni.HoveredStepperButton == 1;
            bool topPress = ni.PressedStepperButton == 1;
            PaintStepperButton(topBtn, "▲", ni.IsAtMax || disabled, 0, isLeft: false, isHovered: topHover, isPressed: topPress);

            var botBtn = new Rect(btnX, bounds.Y + halfH, buttonWidth, halfH);
            bool botHover = ni.HoveredStepperButton == 0;
            bool botPress = ni.PressedStepperButton == 0;
            PaintStepperButton(botBtn, "▼", ni.IsAtMin || disabled, 0, isLeft: false, isHovered: botHover, isPressed: botPress);

            // Divider between stepper buttons
            ctx.DrawLine(
                new Point(btnX, bounds.Y + halfH),
                new Point(btnX + buttonWidth, bounds.Y + halfH),
                new Stroke(theme.Colors.Border, 0.5f));

            // Divider between value and stepper
            ctx.DrawLine(
                new Point(btnX, bounds.Y + 2),
                new Point(btnX, bounds.Y + bounds.Height - 2),
                new Stroke(theme.Colors.Border, 0.5f));
        }
        else
        {
            // No stepper — just display value centered
            PaintText(ni.DisplayValue, bounds, paddingH, theme.Colors.Text,
                fontSize: fontSize, alignment: TextAlignment.Center);
        }
    }

    private void PaintStepperButton(Rect bounds, string symbol, bool disabled, float radius, bool isLeft, bool isHovered = false, bool isPressed = false)
    {
        // Pressed background — stronger than hover
        if (isPressed && !disabled)
        {
            ctx.DrawRect(bounds, theme.Colors.Text.Opacity(0.16f), radius: radius);
        }
        // Hover background highlight
        else if (isHovered && !disabled)
        {
            ctx.DrawRect(bounds, theme.Colors.Text.Opacity(0.08f), radius: radius);
        }

        var textColor = disabled ? theme.Colors.TextMuted : theme.Colors.Text;
        if ((isHovered || isPressed) && !disabled)
        {
            textColor = theme.Colors.Primary;
        }

        // Up/down steppers draw as Apple chevrons (90° bend), not solid ▲/▼ glyphs.
        if (symbol is "▲" or "▼")
        {
            PaintChevronGlyph(bounds, textColor, pointUp: symbol == "▲", strokeWidth: 1.75f);
            return;
        }

        float symSize = theme.Typography.Scale.BodySmall.Size;
        var textSize = ctx.MeasureText(symbol, symSize);
        float x = MathF.Round(bounds.X + (bounds.Width - textSize.Width) / 2f);
        float y = MathF.Round(bounds.Y + (bounds.Height - textSize.Height) / 2f);
        ctx.DrawText(symbol, x, y, symSize, textColor);
    }

    // ── Gauge ──────────────────────────────────────────────────────────

    private void PaintGauge(Gauge gauge, Rect bounds)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        if (gauge.GaugeDisplayStyle == GaugeStyle.Semi)
        {
            size = Math.Min(bounds.Width, bounds.Height * 2f);
        }

        float strokeWidth = gauge.StrokeWidthOverride ?? Math.Max(4f, size * 0.1f);
        float radius = (size - strokeWidth) / 2f;
        if (radius <= 0)
        {
            return;
        }

        float range = gauge.Max - gauge.Min;
        float normalizedValue = range > 0 ? (gauge.Value - gauge.Min) / range : 0f;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Spring-animate the gauge value — only start when visible so the
        // animation plays when the user can actually see it, not off-screen.
        // Use content-space position for stable identity across re-renders.
        float gaugeCenterY = absoluteY + bounds.Height * 0.5f;
        bool gaugeOnScreen = gaugeCenterY > 0 && gaugeCenterY < ViewportLogicalHeight;
        float gaugeContentY = absoluteY + activeScrollOffsetY;
        int gaugeId = HashCode.Combine(
            (int)Math.Round(gaugeContentY * 10f),
            (int)Math.Round(bounds.Width * 10f));
        float animatedValue;
        if (reducedMotion)
        {
            animatedValue = normalizedValue;
        }
        else if (!gaugeOnScreen && !gaugeSeenVisible.Contains(gaugeId))
        {
            animatedValue = normalizedValue;
        }
        else
        {
            if (gaugeOnScreen)
            {
                gaugeSeenVisible.Add(gaugeId);
            }
            ControlStateAnimator.ReconcileValue(gauge, normalizedValue, AnimationModel.Spring.Snappy);
            animatedValue = ControlStateAnimator.GetValueProgress(gauge);
        }

        var trackColor = gauge.TrackColorOverride ?? theme.Colors.Border;
        var fillColor = gauge.FillColorOverride ?? theme.Colors.Primary;

        // Apply segment colors — use animated value for smooth crossfade
        if (gauge.SegmentRanges.Count > 0)
        {
            foreach (var seg in gauge.SegmentRanges)
            {
                float segNormFrom = range > 0 ? (seg.From - gauge.Min) / range : 0f;
                float segNormTo = range > 0 ? (seg.To - gauge.Min) / range : 1f;
                if (animatedValue >= segNormFrom && animatedValue <= segNormTo)
                {
                    fillColor = seg.Color;
                    break;
                }
            }
        }

        if (gauge.GaugeDisplayStyle == GaugeStyle.Semi)
        {
            float centerX = bounds.Width / 2f;
            float centerY = bounds.Height - strokeWidth / 2f;
            var center = new Point(centerX, centerY);

            // Track arc (180°, from left to right across the top)
            ctx.DrawArc(center, radius, Angle.Degrees(-180f), Angle.Degrees(180f),
                new Stroke(trackColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));

            // Fill arc with spring animation
            if (animatedValue > 0.001f)
            {
                float sweepDegrees = animatedValue * 180f;
                ctx.DrawArc(center, radius, Angle.Degrees(-180f), Angle.Degrees(sweepDegrees),
                    new Stroke(fillColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
            }

            // Center label
            if (gauge.ShowValueEnabled || gauge.CenterLabel != null)
            {
                string label = gauge.CenterLabel
                    ?? (gauge.ValueFormat != null
                        ? string.Format($"{{0:{gauge.ValueFormat}}}", gauge.Value)
                        : $"{(int)(normalizedValue * 100)}%");
                float labelFontSize = size * 0.18f;
                var labelSize = ctx.MeasureText(label, labelFontSize);
                float labelX = MathF.Round(centerX - labelSize.Width / 2f);
                // Sit the value at the bottom of the bowl, just above the flat side where
                // the two arc ends stop — with a small gap so it isn't touching the ring.
                float labelY = MathF.Round(centerY - labelFontSize * 1.15f);
                ctx.DrawText(label, labelX, labelY, labelFontSize, theme.Colors.Text);
            }
        }
        else
        {
            // Full 360° gauge
            var center = new Point(bounds.Width / 2f, bounds.Height / 2f);

            // Track circle
            ctx.DrawCircle(center, radius,
                stroke: new Stroke(trackColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));

            // Fill arc with spring animation
            if (animatedValue > 0.001f)
            {
                float sweepDegrees = animatedValue * 360f;
                ctx.DrawArc(center, radius, Angle.Degrees(-90f), Angle.Degrees(sweepDegrees),
                    new Stroke(fillColor, strokeWidth, StrokeCap.Round, StrokeJoin.Round));
            }

            // Center label
            if (gauge.ShowValueEnabled || gauge.CenterLabel != null)
            {
                string label = gauge.CenterLabel
                    ?? (gauge.ValueFormat != null
                        ? string.Format($"{{0:{gauge.ValueFormat}}}", gauge.Value)
                        : $"{(int)(normalizedValue * 100)}%");
                float labelFontSize = size * 0.22f;
                var labelSize = ctx.MeasureText(label, labelFontSize);
                float labelX = MathF.Round(center.X - labelSize.Width / 2f);
                float labelY = MathF.Round(center.Y - labelSize.Height / 2f);
                ctx.DrawText(label, labelX, labelY, labelFontSize, theme.Colors.Text);
            }
        }
    }

    // ── Banner ────────────────────────────────────────────────────────

    private void PaintBanner(Banner banner, Rect bounds)
    {
        banner.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Animate hover/press for dismiss interaction
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            banner, hoverModel, pressModel, isDisabled: false);
        float hoverT = anim.Hover.Current;

        // PaintRecursive already pushes a translate for padding, so the
        // origin (0,0) is the content area top-left.  bounds is the content
        // area (padding already subtracted by PaintRecursive).
        float contentX = 0;
        float contentY = 0;
        float contentW = bounds.Width;
        float contentH = bounds.Height;

        float fontSize = theme.Typography.Body.Size;
        float iconSize = MathF.Round(fontSize * 1.6f);
        float iconRadius = iconSize / 2f;
        float spacing = 10f;

        var accent = banner.AccentColor;
        var textColor = theme.Colors.Text;
        var iconFg = theme.Colors.TextOnPrimary;

        // Icon badge — filled circle with geometric icon, vertically centered in content
        float iconCX = contentX + iconRadius;
        float iconCY = contentY + contentH / 2f;
        var iconRect = new Rect(iconCX - iconRadius, iconCY - iconRadius, iconSize, iconSize);

        // Icon entrance pulse: subtle scale breathing on first render
        bool iconReducedMotion = ControlStateAnimator.ReducedMotion;
        ScopeGuard iconPulseScope = default;
        if (!iconReducedMotion)
        {
            double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
            float pulse = 1f + 0.04f * MathF.Max(0, MathF.Sin((float)(elapsedMs * 0.003)));
            if (MathF.Abs(pulse - 1f) > 0.001f)
            {
                iconPulseScope = ctx.PushScale(pulse, pulse, new Point(iconCX, iconCY));
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        ctx.DrawRect(iconRect, accent, radius: iconRadius);
        PaintBannerIcon(banner.Type, iconCX, iconCY, iconRadius, iconFg, fontSize);
        iconPulseScope.Dispose();

        // Message text — vertically centered in content area
        float textX = contentX + iconSize + spacing;
        float dismissSpace = banner.OnDismiss != null ? iconSize + spacing : 0f;
        float maxTextWidth = MathF.Max(0f, contentW - iconSize - spacing - dismissSpace);

        // Measure with constrained width so multi-line height is correct for centering
        float textHeight;
        string? fontPath = ctx.DefaultFontPath;
        if (fontPath != null)
        {
            var opts = new TextLayoutOptions
            {
                FontPath = fontPath,
                FontSize = fontSize,
                MaxWidth = maxTextWidth,
            };
            textHeight = TextLayoutEngine.Layout(banner.Message, opts).BoundingBox.Height;
        }
        else
        {
            textHeight = fontSize * 1.2f;
        }

        float textY = MathF.Round(contentY + contentH / 2f - textHeight / 2f);
        ctx.DrawText(banner.Message, MathF.Round(textX), textY, fontSize, textColor,
            maxWidth: maxTextWidth,
            overflow: TextOverflow.Ellipsis);

        // Dismiss affordance — geometric × with hover rotation, top-right corner
        if (banner.OnDismiss != null)
        {
            float dismissCX = contentX + contentW - iconRadius - 10f;
            float dismissCY = contentY + iconRadius + 10f;
            float dismissR = iconRadius;
            var dismissRect = new Rect(
                dismissCX - dismissR, dismissCY - dismissR, iconSize, iconSize);

            if (banner.IsHovered)
            {
                ctx.DrawRect(dismissRect, textColor.Opacity(0.1f), radius: dismissR);
            }

            // Rotate X on hover (90° with spring)
            float xRotation = iconReducedMotion ? 0f : (hoverT * 90f);
            ScopeGuard xRotateScope = default;
            if (MathF.Abs(xRotation) > 0.5f)
            {
                xRotateScope = ctx.PushRotate(Angle.Degrees(xRotation),
                    new Point(dismissCX, dismissCY));
            }

            // Draw × using two lines
            float xArm = dismissR * 0.35f;
            float strokeW = MathF.Max(1.5f, fontSize * 0.09f);
            var xStroke = new Stroke(textColor.Opacity(0.6f), strokeW, StrokeCap.Round, StrokeJoin.Round);
            ctx.DrawLine(
                new Point(dismissCX - xArm, dismissCY - xArm),
                new Point(dismissCX + xArm, dismissCY + xArm), xStroke);
            ctx.DrawLine(
                new Point(dismissCX + xArm, dismissCY - xArm),
                new Point(dismissCX - xArm, dismissCY + xArm), xStroke);
            xRotateScope.Dispose();

            banner.DismissHitRect = new Rect(
                absoluteX + dismissCX - dismissR,
                absoluteY + dismissCY - dismissR,
                iconSize, iconSize);
        }
    }

    private void PaintBannerIcon(BannerType type, float cx, float cy, float r, ColorValue color, float fontSize)
    {
        float strokeW = MathF.Max(1.8f, fontSize * 0.11f);
        var stroke = new Stroke(color, strokeW, StrokeCap.Round, StrokeJoin.Round);

        switch (type)
        {
            case BannerType.Info:
            {
                // "i" — dot + vertical line
                float dotR = strokeW * 0.8f;
                float dotY = cy - r * 0.3f;
                ctx.DrawCircle(new Point(cx, dotY), dotR, fill: color);
                float lineTop = cy - r * 0.05f;
                float lineBot = cy + r * 0.4f;
                ctx.DrawLine(new Point(cx, lineTop), new Point(cx, lineBot), stroke);
                break;
            }

            case BannerType.Success:
            {
                // Checkmark ✓ — two line segments
                float arm = r * 0.38f;
                float checkX1 = cx - arm * 0.6f;
                float checkY1 = cy + arm * 0.1f;
                float checkX2 = cx - arm * 0.05f;
                float checkY2 = cy + arm * 0.55f;
                float checkX3 = cx + arm * 0.75f;
                float checkY3 = cy - arm * 0.45f;
                ctx.DrawLine(new Point(checkX1, checkY1), new Point(checkX2, checkY2), stroke);
                ctx.DrawLine(new Point(checkX2, checkY2), new Point(checkX3, checkY3), stroke);
                break;
            }

            case BannerType.Warning:
            {
                // "!" — vertical line + dot
                float lineTop = cy - r * 0.38f;
                float lineBot = cy + r * 0.12f;
                ctx.DrawLine(new Point(cx, lineTop), new Point(cx, lineBot), stroke);
                float dotR = strokeW * 0.8f;
                float dotY = cy + r * 0.38f;
                ctx.DrawCircle(new Point(cx, dotY), dotR, fill: color);
                break;
            }

            case BannerType.Error:
            {
                // × — two crossed lines
                float arm = r * 0.3f;
                ctx.DrawLine(
                    new Point(cx - arm, cy - arm),
                    new Point(cx + arm, cy + arm), stroke);
                ctx.DrawLine(
                    new Point(cx + arm, cy - arm),
                    new Point(cx - arm, cy + arm), stroke);
                break;
            }
        }
    }

    // ── Sparkline ──────────────────────────────────────────────────────

    private void PaintSparkline(Sparkline spark, Rect bounds)
    {
        var data = spark.DataPoints;
        if (data.Count < 2)
        {
            return;
        }

        var primaryColor = spark.colorValue ?? theme.Colors.Primary;
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;
        for (int i = 0; i < data.Count; i++)
        {
            if (data[i] < minVal) { minVal = data[i]; }
            if (data[i] > maxVal) { maxVal = data[i]; }
        }

        double range = maxVal - minVal;
        if (range < 0.0001)
        {
            range = 1.0;
        }

        float padY = bounds.Height * 0.1f;
        float chartH = bounds.Height - padY * 2f;

        // Normal band (shaded reference range)
        if (spark.normalBandLower.HasValue && spark.normalBandUpper.HasValue)
        {
            float bandTop = bounds.Y + padY + chartH * (1f - (float)((spark.normalBandUpper.Value - minVal) / range));
            float bandBot = bounds.Y + padY + chartH * (1f - (float)((spark.normalBandLower.Value - minVal) / range));
            bandTop = Math.Clamp(bandTop, bounds.Y, bounds.Y + bounds.Height);
            bandBot = Math.Clamp(bandBot, bounds.Y, bounds.Y + bounds.Height);
            if (bandBot > bandTop)
            {
                ctx.DrawRect(new Rect(bounds.X, bandTop, bounds.Width, bandBot - bandTop),
                    primaryColor.Opacity(0.08f));
            }
        }

        if (spark.typeValue == SparklineType.Bar || spark.typeValue == SparklineType.WinLoss)
        {
            PaintSparklineBars(spark, bounds, data, minVal, maxVal, range, padY, chartH, primaryColor);
        }
        else
        {
            PaintSparklineLine(spark, bounds, data, minVal, range, padY, chartH, primaryColor);
        }
    }

    private void PaintSparklineLine(Sparkline spark, Rect bounds, IReadOnlyList<double> data,
        double minVal, double range, float padY, float chartH, ColorValue color)
    {
        float stepX = bounds.Width / (data.Count - 1);
        float strokeW = MathF.Max(1.5f, bounds.Height * 0.06f);
        var stroke = new Stroke(color, strokeW, StrokeCap.Round, StrokeJoin.Round);

        for (int i = 0; i < data.Count - 1; i++)
        {
            float x1 = bounds.X + i * stepX;
            float y1 = bounds.Y + padY + chartH * (1f - (float)((data[i] - minVal) / range));
            float x2 = bounds.X + (i + 1) * stepX;
            float y2 = bounds.Y + padY + chartH * (1f - (float)((data[i + 1] - minVal) / range));
            ctx.DrawLine(new Point(x1, y1), new Point(x2, y2), stroke);
        }

        // Endpoint dot
        float lastX = bounds.X + (data.Count - 1) * stepX;
        float lastY = bounds.Y + padY + chartH * (1f - (float)((data[data.Count - 1] - minVal) / range));
        ctx.DrawCircle(new Point(lastX, lastY), strokeW * 1.5f, fill: color);
    }

    private void PaintSparklineBars(Sparkline spark, Rect bounds, IReadOnlyList<double> data,
        double minVal, double maxVal, double range, float padY, float chartH, ColorValue color)
    {
        float gap = 1f;
        float barW = MathF.Max(1f, (bounds.Width - gap * (data.Count - 1)) / data.Count);
        var negColor = spark.negativeColorValue ?? new ColorValue("#EF5350");

        bool isWinLoss = spark.typeValue == SparklineType.WinLoss;
        float baselineY = bounds.Y + padY + chartH * (1f - (float)((0 - minVal) / range));
        if (minVal >= 0)
        {
            baselineY = bounds.Y + bounds.Height - padY;
        }

        for (int i = 0; i < data.Count; i++)
        {
            float x = bounds.X + i * (barW + gap);
            bool isNeg = data[i] < 0;
            var barColor = isNeg ? negColor : color;

            if (isWinLoss)
            {
                float winH = chartH * 0.4f;
                float bY = isNeg ? baselineY + 1f : baselineY - winH - 1f;
                ctx.DrawRect(new Rect(x, bY, barW, winH), barColor, radius: 1f);
            }
            else
            {
                float val = (float)((data[i] - minVal) / range);
                float barH = MathF.Max(1f, chartH * val);
                float bY = bounds.Y + padY + chartH - barH;
                ctx.DrawRect(new Rect(x, bY, barW, barH), barColor, radius: 1f);
            }
        }
    }

    // ── RangeSlider ────────────────────────────────────────────────────

    private void PaintRangeSlider(RangeSlider rs, Rect bounds)
    {
        rs.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        var t = theme.Slider;
        float trackHeight = t.TrackHeight;
        float thumbW = t.ThumbWidth;
        float thumbH = t.ThumbHeight;
        bool disabled = rs.IsDisabled;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Animate hover/press state
        var hoverModel = t.ThumbTransition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            rs, hoverModel, pressModel, isDisabled: disabled);
        float hoverT = anim.Hover.Current;
        float pressT = anim.Press.Current;

        float minVal = Math.Clamp(rs.MinBind.Value, rs.Min, rs.Max);
        float maxVal = Math.Clamp(rs.MaxBind.Value, rs.Min, rs.Max);
        float trackRange = rs.Max - rs.Min;
        float minFrac = trackRange > 0 ? (minVal - rs.Min) / trackRange : 0f;
        float maxFrac = trackRange > 0 ? (maxVal - rs.Min) / trackRange : 1f;

        // Track
        float trackY = bounds.Y + (bounds.Height - trackHeight) / 2f;
        float trackLeft = bounds.X + thumbW / 2f;
        float trackW = bounds.Width - thumbW;
        var trackBounds = new Rect(trackLeft, trackY, trackW, trackHeight);

        // Empty track
        PaintBrush(t.TrackEmpty, trackBounds, t.TrackRadius);

        // Filled portion between the two thumbs
        float fillLeft = trackLeft + trackW * minFrac;
        float fillRight = trackLeft + trackW * maxFrac;
        float fillW = MathF.Max(0f, fillRight - fillLeft);
        if (fillW > 0)
        {
            PaintBrush(t.TrackFill, new Rect(fillLeft, trackY, fillW, trackHeight), t.TrackRadius);

            // Range fill glow on hover
            if (!disabled && !reducedMotion && hoverT > 0.01f)
            {
                ctx.DrawRect(new Rect(fillLeft, trackY, fillW, trackHeight),
                    theme.Colors.Primary.Opacity(0.08f * hoverT), radius: t.TrackRadius);
            }
        }

        // Thumb scale
        float hoverScale = disabled ? 1f : LerpF(1f, 1.15f, hoverT);
        float pressScale2 = disabled ? 1f : LerpF(1f, 0.9f, pressT);
        float thumbScale = hoverScale * pressScale2;

        // Min thumb
        float minThumbX = trackLeft + trackW * minFrac - thumbW / 2f;
        float thumbY = bounds.Y + (bounds.Height - thumbH) / 2f;
        PaintRangeThumbPolished(new Rect(minThumbX, thumbY, thumbW, thumbH), t, disabled,
            thumbScale, reducedMotion);

        // Max thumb
        float maxThumbX = trackLeft + trackW * maxFrac - thumbW / 2f;
        PaintRangeThumbPolished(new Rect(maxThumbX, thumbY, thumbW, thumbH), t, disabled,
            thumbScale, reducedMotion);

        // Focus ring
        if (!disabled && anim.Focus.Current > 0.01f)
        {
            // Draw focus ring around the entire track area
            float focusT2 = anim.Focus.Current;
            float ringOffset = 3f;
            var ringRect = new Rect(
                bounds.X - ringOffset,
                thumbY - ringOffset,
                bounds.Width + ringOffset * 2,
                thumbH + ringOffset * 2);
            ctx.DrawRect(ringRect,
                stroke: new Stroke(theme.Colors.Focus.Opacity(focusT2), 2f),
                radius: t.ThumbRadius + ringOffset);
        }

        // Value labels above thumbs
        if (rs.ShowValueLabelValue && !disabled)
        {
            float labelOpacity = reducedMotion ? (rs.IsHovered ? 1f : 0f) : hoverT;
            if (labelOpacity > 0.01f)
            {
                string fmt = rs.FormatString ?? "F0";
                string minLabel = minVal.ToString(fmt);
                string maxLabel = maxVal.ToString(fmt);
                float labelFontSize = 11f;
                float labelSlideY = reducedMotion ? 0f : LerpF(4f, 0f, hoverT);

                var minLabelSize = ctx.MeasureText(minLabel, labelFontSize);
                ctx.DrawText(minLabel,
                    MathF.Round(minThumbX + thumbW / 2f - minLabelSize.Width / 2f),
                    MathF.Round(thumbY - minLabelSize.Height - 4f + labelSlideY),
                    labelFontSize, theme.Colors.Text.Opacity(labelOpacity));

                var maxLabelSize = ctx.MeasureText(maxLabel, labelFontSize);
                ctx.DrawText(maxLabel,
                    MathF.Round(maxThumbX + thumbW / 2f - maxLabelSize.Width / 2f),
                    MathF.Round(thumbY - maxLabelSize.Height - 4f + labelSlideY),
                    labelFontSize, theme.Colors.Text.Opacity(labelOpacity));
            }
        }
    }

    private void PaintRangeThumbPolished(Rect thumbBounds, SliderTheme t, bool disabled,
        float scale, bool reducedMotion)
    {
        if (!reducedMotion && MathF.Abs(scale - 1f) > 0.001f)
        {
            float sw = thumbBounds.Width * scale;
            float sh = thumbBounds.Height * scale;
            thumbBounds = new Rect(
                thumbBounds.X + (thumbBounds.Width - sw) / 2f,
                thumbBounds.Y + (thumbBounds.Height - sh) / 2f,
                sw, sh);
        }

        if (!disabled)
        {
            PaintShadow(t.ThumbShadow, thumbBounds, t.ThumbRadius);
        }

        var fill = disabled ? (t.ThumbDisabled?.Fill ?? t.ThumbFill) : t.ThumbFill;
        PaintBrush(fill, thumbBounds, t.ThumbRadius);
    }

    // ── DonutGauge ────────────────────────────────────────────────────

    private void PaintDonutGauge(DonutGauge dg, Rect bounds)
    {
        float value = dg.bindableValue?.Value ?? dg.gaugeValue;
        float clampedValue = Math.Clamp(value, 0f, 1f);

        // Entrance animation — arc sweeps from zero
        bool animate = dg.animateTriggerValue != AnimateTrigger.None && !skipAnimations;
        float animProgress = 1f;
        if (animate)
        {
            int animHash = ChartAnimationTracker.ComputeDonutGaugeHash(dg);
            bool onScreen = IsCurrentlyVisible(bounds);
            animProgress = ChartAnimationTracker.GetProgress(dg, animHash, dg.animateTriggerValue, ChartAnimationTracker.GaugeDuration, onScreen);
            if (animProgress < 1f && (animProgress > 0f || onScreen))
            {
                HasActiveChartAnimations = true;
            }
        }

        float animatedValue = clampedValue * animProgress;

        float size = MathF.Min(bounds.Width, bounds.Height);
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        float thickness = dg.thicknessValue;
        float radius = (size - thickness) / 2f;

        if (radius <= 0f)
        {
            return;
        }

        var startAngle = dg.startAngleValue;
        var sweepAngle = dg.sweepAngleValue;

        // Track arc (unfilled background ring)
        var trackColor = dg.trackColorValue ?? theme.Colors.Border;
        ctx.DrawArc(
            new Point(cx, cy), radius, startAngle, sweepAngle,
            new Stroke(trackColor, thickness, StrokeCap.Round, StrokeJoin.Round));

        // Value arc — uses animated value for entrance effect
        if (animatedValue > 0.001f)
        {
            float valueSweepDeg = sweepAngle.InDegrees * animatedValue;
            var fillColor = GetDonutGaugeColor(dg, clampedValue);
            ctx.DrawArc(
                new Point(cx, cy), radius, startAngle, Angle.Degrees(valueSweepDeg),
                new Stroke(fillColor, thickness, StrokeCap.Round, StrokeJoin.Round));
        }

        // Center value text — shows the target value, not the animated value
        string valueText;
        if (dg.formatValue != null)
        {
            if (dg.formatValue.Formatter != null)
            {
                valueText = dg.formatValue.Formatter(value);
            }
            else if (dg.formatValue == GaugeFormat.Number)
            {
                valueText = value.ToString("F1");
            }
            else if (dg.formatValue == GaugeFormat.Currency)
            {
                valueText = value.ToString("C0");
            }
            else
            {
                valueText = $"{clampedValue * 100f:F0}%";
            }
        }
        else
        {
            valueText = $"{clampedValue * 100f:F0}%";
        }

        var textColor = theme.Colors.Text;
        string? labelText = dg.labelText;

        // ── Center text sizing with a guaranteed padding band ───────────────
        // Size the value (and optional label) to fit inside the ring's clear
        // hole with real breathing room, rather than as a blind fraction of the
        // gauge diameter. A fixed fraction lets the text creep right up to the
        // inner stroke edge as the ring gets thicker (smaller hole) or the
        // value/label strings get wider — which is exactly what makes a gauge
        // feel cramped. The block is ink-centred on (cx, cy) below, so its
        // bounding box is centred too; we require that box to fit inside a
        // *padded* inner circle. Fitting the box corners keeps the widest line
        // clear of the ring at the height it actually sits, for any thickness
        // and any string length. Padding is never sacrificed to fit more text —
        // the text shrinks instead.
        float innerRadius = radius - thickness / 2f;            // clear hole radius
        float pad = MathF.Max(3f, innerRadius * 0.08f);         // guaranteed clear band
        float fitRadius = MathF.Max(1f, innerRadius - pad);

        float valueFontSize = size * 0.22f;
        float labelFontSize = labelText != null ? size * 0.11f : 0f;
        // The value-to-label gap is a fixed fraction of the gauge SIZE and is
        // never scaled by the fit below. Every gauge of the same size then shows
        // the same spacing between its number and label, regardless of how much
        // each one's text had to shrink to clear its own ring — a row of gauges
        // reads as one system instead of each drifting to its own rhythm.
        float gap = labelText != null ? size * 0.055f : 0f;

        // Nominal block extents. Width is the advance; height is leading-inclusive
        // (MeasureText) — a deliberate over-estimate of the rendered ink so the
        // padding guarantee never under-pads.
        var valueMetrics = ctx.MeasureText(valueText, valueFontSize);
        float blockW = valueMetrics.Width;
        float lineH = valueMetrics.Height;
        if (labelText != null)
        {
            var labelMetrics = ctx.MeasureText(labelText, labelFontSize);
            blockW = MathF.Max(blockW, labelMetrics.Width);
            lineH += labelMetrics.Height;
        }

        // Shrink only the FONTS (the gap is fixed) until the block's corner sits
        // on fitRadius. With a fixed gap the corner distance is not a plain scale
        // of the block, so solve for the font scale s directly from the corner
        // constraint (blockW·s)² + (lineH·s + gap)² ≤ (2·fitRadius)², i.e.
        // A·s² + B·s + C ≤ 0 — take the positive root and clamp to [0, 1].
        float fitDiameter = 2f * fitRadius;
        float aCoef = blockW * blockW + lineH * lineH;
        float bCoef = 2f * lineH * gap;
        float cCoef = gap * gap - fitDiameter * fitDiameter;
        float fontScale = 1f;
        if (aCoef > 0f)
        {
            float disc = bCoef * bCoef - 4f * aCoef * cCoef;
            if (disc > 0f)
            {
                fontScale = Math.Clamp((-bCoef + MathF.Sqrt(disc)) / (2f * aCoef), 0f, 1f);
            }
        }
        valueFontSize *= fontScale;
        labelFontSize *= fontScale;

        var textSize = ctx.MeasureText(valueText, valueFontSize);
        float textX = cx - textSize.Width / 2f;

        // Lay the value (and optional label) out as one block whose *ink* is
        // centred in the ring. MeasureText's height carries font leading, so
        // centring on it makes the block ride low and shoves the muted label
        // down onto the bottom of the arc. Centre on the visual ink box
        // instead, and give the two lines a real, size-proportional gap so the
        // percent and label never smash together. VisualCenterY is the distance
        // from DrawText's y (line-box top) to the glyph ink centre.
        var valueInk = ctx.MeasureGlyphVisualBounds(valueText, valueFontSize);
        float valueInkH = valueInk?.VisualHeight ?? valueFontSize * 0.7f;
        float valueCenterOffset = valueInk?.VisualCenterY ?? textSize.Height / 2f;

        float valueDrawY;
        float labelDrawY = 0f, labelX = 0f;

        if (labelText != null)
        {
            var labelSize = ctx.MeasureText(labelText, labelFontSize);
            var labelInk = ctx.MeasureGlyphVisualBounds(labelText, labelFontSize);
            float labelInkH = labelInk?.VisualHeight ?? labelFontSize * 0.7f;
            float labelCenterOffset = labelInk?.VisualCenterY ?? labelSize.Height / 2f;

            float blockInkH = valueInkH + gap + labelInkH;

            float valueInkCenterY = cy - blockInkH / 2f + valueInkH / 2f;
            float labelInkCenterY = cy + blockInkH / 2f - labelInkH / 2f;

            valueDrawY = valueInkCenterY - valueCenterOffset;
            labelDrawY = labelInkCenterY - labelCenterOffset;
            labelX = cx - labelSize.Width / 2f;
        }
        else
        {
            valueDrawY = cy - valueCenterOffset;
        }

        // Scale-in center label with entrance animation
        ScopeGuard labelScale = default;
        ScopeGuard labelOpacity = default;
        if (animate && animProgress < 0.999f && !ControlStateAnimator.ReducedMotion)
        {
            float labelT = MathF.Min(1f, animProgress * 1.5f);
            float scale = 0.5f + labelT * 0.5f;
            labelScale = ctx.PushScale(scale, scale, new Point(cx, cy));
            labelOpacity = ctx.PushOpacity(labelT);
        }

        ctx.DrawText(valueText, MathF.Round(textX), MathF.Round(valueDrawY), valueFontSize, textColor);

        // Center label text (below value)
        if (labelText != null)
        {
            var labelColor = theme.Colors.TextMuted;
            ctx.DrawText(labelText, MathF.Round(labelX), MathF.Round(labelDrawY), labelFontSize, labelColor);
        }

        labelOpacity.Dispose();
        labelScale.Dispose();
    }

    private ColorValue GetDonutGaugeColor(DonutGauge dg, float value)
    {
        // Check thresholds (highest matching threshold wins)
        if (dg.thresholdList != null && dg.thresholdList.Count > 0)
        {
            for (int i = dg.thresholdList.Count - 1; i >= 0; i--)
            {
                if (value >= dg.thresholdList[i].Value)
                {
                    return dg.thresholdList[i].Color;
                }
            }
        }

        return dg.colorValue ?? theme.Colors.Primary;
    }

    // ── Timeline ──────────────────────────────────────────────────────

    private static readonly Dictionary<int, long> timelineFirstPaintTick = new();

    private void PaintTimeline(Timeline tl, Rect bounds)
    {
        var events = tl.Events;
        if (events.Count == 0)
        {
            return;
        }

        const float dotRadius = 5f;
        const float spineX = 12f;
        const float contentLeft = 32f;
        const float eventSpacing = 52f;
        const float titleFontSize = 13f;
        const float bodyFontSize = 12f;
        const float timestampFontSize = 11f;

        var spineColor = theme.Colors.Border;
        var titleColor = theme.Colors.Text;
        var bodyColor = theme.Colors.TextMuted;
        var timestampColor = theme.Colors.TextMuted;
        var accentColor = theme.Colors.Primary;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float spineLeft = bounds.X + spineX;
        float textLeft = bounds.X + contentLeft;
        // Reserve space for the right-aligned timestamp so title text doesn't overlap
        const float timestampReserve = 70f;
        float textMaxWidth = bounds.Width - contentLeft - timestampReserve;

        // Entrance stagger animation — use content-space position for stable
        // identity across re-renders (object GetHashCode changes each render).
        float tlContentY = absoluteY + activeScrollOffsetY;
        int tlHash = HashCode.Combine(
            (int)Math.Round(tlContentY * 10f),
            (int)Math.Round(bounds.Width * 10f));
        float centerY = absoluteY + bounds.Height * 0.5f;
        bool onScreen = centerY > 0 && centerY < ViewportLogicalHeight;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        bool animate = !reducedMotion;

        if (animate && onScreen)
        {
            if (!timelineFirstPaintTick.ContainsKey(tlHash))
            {
                timelineFirstPaintTick[tlHash] = now;
            }
        }

        float globalProgress = 1f;
        if (animate && timelineFirstPaintTick.TryGetValue(tlHash, out long startTick))
        {
            double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTick).TotalMilliseconds;
            globalProgress = Math.Clamp((float)(elapsedMs / 800.0), 0f, 1f);
            // Ease out cubic
            globalProgress = 1f - (1f - globalProgress) * (1f - globalProgress) * (1f - globalProgress);
            if (globalProgress < 1f)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        // Draw vertical spine line from first dot to last dot (grows downward during entrance)
        if (events.Count > 1)
        {
            float firstDotY = bounds.Y + dotRadius + 2f;
            float lastDotY = bounds.Y + (events.Count - 1) * eventSpacing + dotRadius + 2f;
            float spineEndY = firstDotY + (lastDotY - firstDotY) * globalProgress;
            float spineOpacity = Math.Clamp(globalProgress * 3f, 0f, 1f);
            ctx.DrawLine(
                new Point(spineLeft, firstDotY),
                new Point(spineLeft, spineEndY),
                new Stroke(spineColor.Opacity(spineOpacity), 2f));
        }

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            float eventY = bounds.Y + i * eventSpacing;
            float dotCenterY = eventY + dotRadius + 2f;

            // Per-item stagger: each item delays 80ms after the previous
            float itemProgress = 1f;
            if (animate && globalProgress < 1f)
            {
                float itemDelay = i * 0.12f;
                itemProgress = Math.Clamp((globalProgress - itemDelay) / 0.3f, 0f, 1f);
                if (itemProgress <= 0f)
                {
                    continue;
                }
            }

            // Dot on the spine (scales in)
            var dotColor = ev.IconColor ?? (i == 0 ? accentColor : spineColor);
            float dotScale = itemProgress;
            ctx.DrawCircle(
                new Point(spineLeft, dotCenterY),
                dotRadius * dotScale,
                fill: dotColor.Opacity(itemProgress));

            // If it's the most recent event (first), draw a ring highlight
            if (i == 0 && itemProgress > 0.5f)
            {
                float ringOpacity = (itemProgress - 0.5f) / 0.5f;
                ctx.DrawCircle(
                    new Point(spineLeft, dotCenterY),
                    dotRadius + 3f,
                    stroke: new Stroke(accentColor.Opacity(ringOpacity), 1.5f));
            }

            // Title (fades + slides in from left)
            float titleY = eventY + 1f;
            float slideX = (1f - itemProgress) * 8f;
            ctx.DrawText(ev.Title, MathF.Round(textLeft + slideX), MathF.Round(titleY),
                titleFontSize, titleColor.Opacity(itemProgress), maxWidth: textMaxWidth);

            // Timestamp (right-aligned)
            string timeStr = FormatTimelineTimestamp(ev.Timestamp);
            var timeSize = ctx.MeasureText(timeStr, timestampFontSize);
            float timeX = bounds.Right - timeSize.Width - 4f;
            ctx.DrawText(timeStr, MathF.Round(timeX), MathF.Round(titleY + 1f),
                timestampFontSize, timestampColor.Opacity(itemProgress));

            // Body text below title
            if (!string.IsNullOrEmpty(ev.Body))
            {
                float bodyY = titleY + titleFontSize + 5f;
                ctx.DrawText(ev.Body, MathF.Round(textLeft + slideX), MathF.Round(bodyY),
                    bodyFontSize, bodyColor.Opacity(itemProgress), maxWidth: textMaxWidth);
            }
        }
    }

    private static string FormatTimelineTimestamp(DateTime timestamp)
    {
        var now = DateTime.Now;
        var diff = now - timestamp;

        if (diff.TotalMinutes < 1)
        {
            return "just now";
        }

        if (diff.TotalHours < 1)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }

        if (diff.TotalDays < 1)
        {
            return $"{(int)diff.TotalHours}h ago";
        }

        if (diff.TotalDays < 7)
        {
            return $"{(int)diff.TotalDays}d ago";
        }

        return timestamp.ToString("MMM d");
    }

    // ── DataGrid / DataTable (ITabularDataNode) ─────────────────────

    private void PaintTabularData(ITabularDataNode tdn, Node node, Rect bounds)
    {
        if (tdn.ColumnCount == 0)
        {
            return;
        }

        // Record absolute bounds for input hit-testing
        tdn.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        float rowHeight = tdn.GetRowHeight();
        float headerHeight = rowHeight + 4f;
        const float pad = 8f;
        const float headerFontSize = 12f;
        const float cellFontSize = 13f;
        const float boolCircleRadius = 5f;

        var headerBg = theme.Colors.SurfaceAlt;
        var headerText = theme.Colors.Text;
        var cellText = theme.Colors.Text;
        var borderColor = theme.Colors.Border;
        var stripeBg = theme.Colors.Surface;
        var stripeAltBg = theme.Colors.SurfaceAlt;
        var primaryColor = theme.Colors.Primary;
        var selectedBg = primaryColor.Opacity(0.15f);
        var hoverBg = primaryColor.Opacity(0.08f);
        var sortIndicatorColor = theme.Colors.TextMuted;
        bool tabReducedMotion = ControlStateAnimator.ReducedMotion;

        // Sort direction animation: 0 = ascending, 1 = descending
        float sortDirTarget = tdn.SortDirectionValue == SortDirection.Descending ? 1f : 0f;
        ControlStateAnimator.ReconcileValue(node, sortDirTarget,
            AnimationModel.Spring.Snappy);
        float sortDirT = ControlStateAnimator.GetValueProgress(node);

        // Edit mode animation
        ControlStateAnimator.ReconcileOpen(node, tdn.IsEditing,
            AnimationModel.Spring.Snappy);
        float editOpenT = ControlStateAnimator.GetOpenProgress(node);

        // Column chooser button takes space from the right side of the header
        const float chooserBtnSize = 24f;
        bool hasChooserBtn = tdn.IsColumnChooserEnabled;

        // Compute column widths (hidden columns get 0 from GetColumnWidth)
        float[] colWidths = new float[tdn.ColumnCount];
        float availWidth = bounds.Width;
        float totalColWidth = 0f;
        // Sort indicator reserve: only added to the actually-sorted column.
        // Reserving on ALL columns inflates total width, worsening proportional scaling.
        const float sortIndicatorReserve = 5f + 3f; // arrowW (2.5*2) + arrowGap (3)
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            colWidths[c] = tdn.GetColumnWidth(c, availWidth);
            if (colWidths[c] > 0f && tdn.IsSortable && tdn.SortColumnIndex == c)
            {
                colWidths[c] += sortIndicatorReserve;
            }
            totalColWidth += colWidths[c];
        }

        // Scale columns proportionally when they exceed available width
        float chooserReserveForScale = hasChooserBtn ? chooserBtnSize + 4f : 0f;
        float usableWidth = availWidth - chooserReserveForScale;
        if (totalColWidth > usableWidth && totalColWidth > 0f)
        {
            float scale = usableWidth / totalColWidth;
            for (int c = 0; c < tdn.ColumnCount; c++)
            {
                colWidths[c] = MathF.Floor(colWidths[c] * scale);
            }
        }

        // Draw outer border
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor, 1f), radius: 4f);

        // Draw header row background
        var headerRect = new Rect(bounds.X, bounds.Y, bounds.Width, headerHeight);
        ctx.DrawRect(headerRect, headerBg, radius: 4f);

        // Draw header text and sort indicators (each cell clipped to its column)
        float colX = bounds.X;
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            if (colWidths[c] <= 0f)
            {
                continue;
            }

            // Clip cell to column width, but never extend past the chooser button area
            float chooserReserve = hasChooserBtn ? chooserBtnSize + 4f : 0f;
            float maxRight = bounds.Right - chooserReserve;
            float cellRight = colX + colWidths[c];
            float clipW = cellRight > maxRight ? maxRight - colX : colWidths[c];
            if (clipW <= 0f)
            {
                colX += colWidths[c];
                continue;
            }
            using var cellClip = ctx.PushClip(new Rect(colX, bounds.Y, clipW, headerHeight));

            var alignment = tdn.GetColumnAlignment(c);
            string headerStr = tdn.GetColumnHeader(c);
            var textSize = ctx.MeasureText(headerStr, headerFontSize);

            // Only reserve sort arrow space on the column that is actually sorted
            bool isSorted = tdn.SortColumnIndex == c;
            float arrowSize = 2.5f;
            float arrowW = arrowSize * 2; // total chevron width (5px)
            float arrowGap = 3f; // gap between text and arrow
            float sortReserve = isSorted ? arrowW + arrowGap : 0f;

            float availTextW = MathF.Max(colWidths[c] - pad - sortReserve, 0f);
            float clampedTextW = MathF.Min(textSize.Width, availTextW);

            float textX = alignment switch
            {
                ColumnAlignment.Right => colX + colWidths[c] - clampedTextW - sortReserve - pad,
                ColumnAlignment.Center => colX + (colWidths[c] - clampedTextW - sortReserve) / 2f,
                _ => colX + pad
            };

            // Clip text to available area (leaving room for sort arrow if sorted)
            float textClipW = colWidths[c] - sortReserve;
            var textClip = ctx.PushClip(new Rect(colX, bounds.Y, MathF.Max(textClipW, 0f), headerHeight));
            ctx.DrawText(headerStr, MathF.Round(textX), MathF.Round(bounds.Y + (headerHeight - headerFontSize) / 2f),
                headerFontSize, headerText);
            textClip.Dispose();

            // Sort indicator chevron — drawn with animated rotation
            if (isSorted)
            {
                float arrowAfterText = textX + clampedTextW + arrowGap;
                float arrowMaxX = colX + colWidths[c] - arrowW - 1f;
                float arrowX = MathF.Min(arrowAfterText, arrowMaxX);
                float arrowY = bounds.Y + headerHeight / 2f;

                // Always draw ascending (up) chevron, rotate 180° for descending
                float rotation = tabReducedMotion
                    ? (sortDirTarget * 180f)
                    : (sortDirT * 180f);
                var arrowCenter = new Point(arrowX + arrowSize, arrowY);
                using var sortRotate = ctx.PushRotate(
                    Angle.Degrees(rotation), arrowCenter);

                ctx.DrawLine(
                    new Point(arrowX, arrowY + arrowSize),
                    new Point(arrowX + arrowSize, arrowY - arrowSize),
                    new Stroke(sortIndicatorColor, 1.5f));
                ctx.DrawLine(
                    new Point(arrowX + arrowSize, arrowY - arrowSize),
                    new Point(arrowX + arrowW, arrowY + arrowSize),
                    new Stroke(sortIndicatorColor, 1.5f));
            }

            colX += colWidths[c];
        }

        // Column chooser button (gear icon) in top-right of header
        if (hasChooserBtn)
        {
            float btnX = bounds.Right - chooserBtnSize - 2f;
            float btnY = bounds.Y + (headerHeight - chooserBtnSize) / 2f;
            var btnRect = new Rect(btnX, btnY, chooserBtnSize, chooserBtnSize);

            // Opaque background to cover any column content bleed
            ctx.DrawRect(new Rect(btnX - 2f, bounds.Y, chooserBtnSize + 4f, headerHeight), headerBg);

            // Store absolute bounds for hit testing
            tdn.ColumnChooserButtonBounds = new Rect(
                absoluteX + bounds.Width - chooserBtnSize - 2f,
                absoluteY + (headerHeight - chooserBtnSize) / 2f,
                chooserBtnSize, chooserBtnSize);

            // Hover background
            if (tdn.IsColumnChooserOpen)
            {
                ctx.DrawRect(btnRect, primaryColor.Opacity(0.15f), radius: 3f);
            }

            // Three horizontal lines (hamburger/column icon)
            float iconPad = 5f;
            float lineLen = chooserBtnSize - iconPad * 2f;
            float lineX = btnX + iconPad;
            float lineSpacing = 4f;
            float firstLineY = btnY + (chooserBtnSize - lineSpacing * 2f) / 2f;
            var lineColor = headerText.Opacity(0.7f);
            for (int li = 0; li < 3; li++)
            {
                float ly = firstLineY + li * lineSpacing;
                ctx.DrawLine(
                    new Point(lineX, ly),
                    new Point(lineX + lineLen, ly),
                    new Stroke(lineColor, 1.5f));
            }
        }

        // Draw resize handle indicator when hovering a column border
        if (tdn.IsNearColumnBorder && tdn.HoveredHeaderCol < 0)
        {
            // IsNearColumnBorder is set but HoveredHeaderCol tracks the cell, not the border.
            // We draw the resize indicator at the hovered border position.
        }

        // Draw column header hover highlight
        if (tdn.HoveredHeaderCol >= 0 && tdn.ResizingColumnIndex < 0 && tdn.ReorderDragIndex < 0)
        {
            float hoverColX = bounds.X;
            for (int hc = 0; hc < tdn.HoveredHeaderCol; hc++)
            {
                hoverColX += colWidths[hc];
            }
            var hoverRect = new Rect(hoverColX, bounds.Y, colWidths[tdn.HoveredHeaderCol], headerHeight);
            ctx.DrawRect(hoverRect, primaryColor.Opacity(0.08f));
        }

        // Draw resize indicator line when actively resizing
        if (tdn.ResizingColumnIndex >= 0)
        {
            float resizeLineX = bounds.X;
            for (int rc = 0; rc <= tdn.ResizingColumnIndex; rc++)
            {
                resizeLineX += colWidths[rc];
            }
            ctx.DrawLine(
                new Point(resizeLineX, bounds.Y),
                new Point(resizeLineX, bounds.Y + headerHeight),
                new Stroke(primaryColor, 2f));
        }

        // Draw column border resize handles on hover (thicker border at column edges)
        if (tdn.IsNearColumnBorder && tdn.ResizingColumnIndex < 0 && tdn.ReorderDragIndex < 0)
        {
            float bdrX = bounds.X;
            for (int bc = 0; bc < tdn.ColumnCount; bc++)
            {
                bdrX += colWidths[bc];
                if (bc < tdn.ColumnCount - 1)
                {
                    // Draw thicker border line at each internal column boundary
                    ctx.DrawLine(
                        new Point(bdrX, bounds.Y + 4f),
                        new Point(bdrX, bounds.Y + headerHeight - 4f),
                        new Stroke(primaryColor.Opacity(0.4f), 2f));
                }
            }
        }

        // Draw header bottom border
        float headerBottom = bounds.Y + headerHeight;
        ctx.DrawLine(
            new Point(bounds.X, headerBottom),
            new Point(bounds.Right, headerBottom),
            new Stroke(borderColor, 1f));

        // ── Filter row ───────────────────────────────────────────────
        const float filterRowHeight = 28f;
        const float filterFontSize = 11f;
        if (tdn.HasFilterRow)
        {
            var filterBg = theme.Colors.Surface;
            var filterInputBg = theme.Colors.SurfaceAlt;
            var filterInputBorder = theme.Colors.Border;
            var filterActiveInputBorder = theme.Colors.Primary;
            var filterTextColor = theme.Colors.Text;
            var filterPlaceholderColor = theme.Colors.TextMuted;
            var clearBtnColor = theme.Colors.TextMuted;

            ctx.DrawRect(new Rect(bounds.X, headerBottom, bounds.Width, filterRowHeight), filterBg);

            float filterColX = bounds.X;
            for (int c = 0; c < tdn.ColumnCount; c++)
            {
                if (colWidths[c] <= 0f)
                {
                    continue;
                }

                float inputX = filterColX + 2f;
                float inputY = headerBottom + 3f;
                float inputW = colWidths[c] - 4f;
                float inputH = filterRowHeight - 6f;
                var inputRect = new Rect(inputX, inputY, inputW, inputH);

                bool isActive = tdn.ActiveFilterCol == c;
                var inputBorderColor = isActive ? filterActiveInputBorder : filterInputBorder;
                ctx.DrawRect(inputRect, filterInputBg, radius: 3f);
                ctx.DrawRect(inputRect, stroke: new Stroke(inputBorderColor, isActive ? 1.5f : 0.5f), radius: 3f);

                string filterText = tdn.GetColumnFilter(c);
                if (filterText.Length > 0)
                {
                    ctx.DrawText(filterText, MathF.Round(inputX + 4f),
                        MathF.Round(inputY + (inputH - filterFontSize) / 2f),
                        filterFontSize, filterTextColor);

                    // Draw cursor if active
                    if (isActive)
                    {
                        string beforeCursor = filterText[..Math.Min(tdn.FilterCursorPos, filterText.Length)];
                        float cursorX = inputX + 4f + ctx.MeasureText(beforeCursor, filterFontSize).Width;
                        ctx.DrawLine(
                            new Point(cursorX, inputY + 3f),
                            new Point(cursorX, inputY + inputH - 3f),
                            new Stroke(filterTextColor, 1f));
                    }

                    // Clear button (X) on right
                    float xBtnX = inputX + inputW - 14f;
                    float xBtnY = inputY + inputH / 2f;
                    float xSize = 3.5f;
                    ctx.DrawLine(
                        new Point(xBtnX - xSize, xBtnY - xSize),
                        new Point(xBtnX + xSize, xBtnY + xSize),
                        new Stroke(clearBtnColor, 1.5f));
                    ctx.DrawLine(
                        new Point(xBtnX + xSize, xBtnY - xSize),
                        new Point(xBtnX - xSize, xBtnY + xSize),
                        new Stroke(clearBtnColor, 1.5f));
                }
                else if (!isActive)
                {
                    ctx.DrawText("Filter...", MathF.Round(inputX + 4f),
                        MathF.Round(inputY + (inputH - filterFontSize) / 2f),
                        filterFontSize, filterPlaceholderColor);
                }
                else
                {
                    // Active but empty — show cursor
                    float cursorX = inputX + 4f;
                    ctx.DrawLine(
                        new Point(cursorX, inputY + 3f),
                        new Point(cursorX, inputY + inputH - 3f),
                        new Stroke(filterTextColor, 1f));
                }

                filterColX += colWidths[c];
            }

            // Filter row bottom border
            float filterBottom = headerBottom + filterRowHeight;
            ctx.DrawLine(
                new Point(bounds.X, filterBottom),
                new Point(bounds.Right, filterBottom),
                new Stroke(borderColor, 0.5f));

            headerBottom = filterBottom;
        }

        // Draw pinned column shadow separators
        if (tdn.HasLeftPinnedColumns)
        {
            float pinX = bounds.X;
            for (int pc = 0; pc < tdn.ColumnCount; pc++)
            {
                if (tdn.GetColumnPin(pc) != ColumnPin.Left)
                {
                    break;
                }
                pinX += colWidths[pc];
            }
            // Draw a subtle shadow line on the right edge of the last pinned column
            var shadowBase = new ColorValue("#000000");
            for (int s = 0; s < 3; s++)
            {
                float opacity = 0.15f - s * 0.05f;
                ctx.DrawLine(
                    new Point(pinX + s, bounds.Y),
                    new Point(pinX + s, bounds.Bottom),
                    new Stroke(shadowBase.Opacity(opacity), 1f));
            }
        }

        if (tdn.HasRightPinnedColumns)
        {
            float pinX = bounds.Right;
            for (int pc = tdn.ColumnCount - 1; pc >= 0; pc--)
            {
                if (tdn.GetColumnPin(pc) != ColumnPin.Right)
                {
                    break;
                }
                pinX -= colWidths[pc];
            }
            var shadowBase2 = new ColorValue("#000000");
            for (int s = 0; s < 3; s++)
            {
                float opacity = 0.15f - s * 0.05f;
                ctx.DrawLine(
                    new Point(pinX - s, bounds.Y),
                    new Point(pinX - s, bounds.Bottom),
                    new Stroke(shadowBase2.Opacity(opacity), 1f));
            }
        }

        // Draw rows
        int visibleRows = 0;
        const float groupHeaderHeight = 32f;
        const float chevronSize = 8f;
        const float expandIndicatorWidth = 24f;
        bool hasRowDetail = tdn.HasRowDetail;
        var detailBg = theme.Colors.SurfaceAlt.Opacity(0.4f);
        var detailTextColor = theme.Colors.TextMuted;
        const float detailFontSize = 12f;
        const float detailPad = 12f;
        float leftOffset = hasRowDetail ? expandIndicatorWidth : 0f;

        // If aggregate row is at top, render it first and shift data down
        float dataStartY = headerBottom;
        bool hasAgg = tdn.HasAggregateRow;
        float aggRowHeight = hasAgg ? tdn.GetAggregateRowHeight() : 0f;
        if (hasAgg && tdn.AggregatePos == AggregatePosition.Top)
        {
            PaintAggregateRow(tdn, headerBottom, aggRowHeight, bounds, colWidths, borderColor, pad, cellFontSize, leftOffset);
            dataStartY = headerBottom + aggRowHeight;
        }

        // ── Virtualization: compute viewport and scroll state ─────────
        float dataAreaBottom = bounds.Bottom;
        if (hasAgg && tdn.AggregatePos == AggregatePosition.Bottom)
        {
            dataAreaBottom -= aggRowHeight;
        }
        float dataAreaHeight = dataAreaBottom - dataStartY;
        tdn.ViewportHeight = dataAreaHeight;
        float scrollY = tdn.ScrollOffsetY;
        // Clamp scroll after viewport is set (MaxScrollOffsetY depends on ViewportHeight)
        if (scrollY > tdn.MaxScrollOffsetY)
        {
            scrollY = tdn.MaxScrollOffsetY;
            tdn.ScrollOffsetY = scrollY;
        }
        float bufferPx = tdn.VirtualizationBufferRows * rowHeight;

        // Frozen row separator tracking
        int frozenCount = tdn.FrozenRowCount;
        float frozenEndY = 0f;

        if (tdn.IsGrouped)
        {
            // ── Grouped rendering with scroll virtualization ───────────
            var groupHeaderBg = theme.Colors.SurfaceAlt.Opacity(0.6f);
            var groupTextColor = theme.Colors.Text;
            var groupCountColor = theme.Colors.TextMuted;
            float currentY = dataStartY - scrollY;

            using var groupClip = ctx.PushClip(new Rect(bounds.X, dataStartY, bounds.Width, dataAreaHeight));

            for (int g = 0; g < tdn.GroupCount; g++)
            {
                // Stop if fully below viewport + buffer
                if (currentY > dataAreaBottom + bufferPx)
                {
                    break;
                }

                bool collapsed = tdn.IsGroupCollapsed(g);
                string groupKey = tdn.GetGroupKey(g);
                int groupRowCount = tdn.GetGroupRowCount(g);

                // Paint group header if visible
                bool groupHeaderVisible = currentY + groupHeaderHeight > dataStartY - bufferPx
                                       && currentY < dataAreaBottom + bufferPx;
                if (groupHeaderVisible)
                {
                    // Draw group header background
                    var ghRect = new Rect(bounds.X + 1f, currentY, bounds.Width - 2f, groupHeaderHeight);
                    ctx.DrawRect(ghRect, groupHeaderBg);

                    // Chevron (▶ collapsed, ▼ expanded)
                    float chevronX = bounds.X + pad;
                    float chevronCenterY = currentY + groupHeaderHeight / 2f;
                    Path chevronPath;
                    if (collapsed)
                    {
                        chevronPath = PathBuilder.Rent()
                            .MoveTo(new Point(chevronX, chevronCenterY - chevronSize / 2f))
                            .LineTo(new Point(chevronX + chevronSize, chevronCenterY))
                            .LineTo(new Point(chevronX, chevronCenterY + chevronSize / 2f))
                            .Close()
                            .BuildTransient();
                    }
                    else
                    {
                        chevronPath = PathBuilder.Rent()
                            .MoveTo(new Point(chevronX, chevronCenterY - chevronSize / 4f))
                            .LineTo(new Point(chevronX + chevronSize, chevronCenterY - chevronSize / 4f))
                            .LineTo(new Point(chevronX + chevronSize / 2f, chevronCenterY + chevronSize / 2f))
                            .Close()
                            .BuildTransient();
                    }
                    ctx.DrawPath(chevronPath, fill: groupTextColor);

                    // Group key text
                    float gTextX = chevronX + chevronSize + 8f;
                    ctx.DrawText(groupKey, MathF.Round(gTextX), MathF.Round(currentY + (groupHeaderHeight - headerFontSize) / 2f),
                        headerFontSize, groupTextColor);

                    // Count badge
                    string countStr = $"({groupRowCount})";
                    var keySize = ctx.MeasureText(groupKey, headerFontSize);
                    ctx.DrawText(countStr, MathF.Round(gTextX + keySize.Width + 6f),
                        MathF.Round(currentY + (groupHeaderHeight - headerFontSize) / 2f),
                        headerFontSize, groupCountColor);

                    // Group header separator
                    ctx.DrawLine(
                        new Point(bounds.X, currentY + groupHeaderHeight),
                        new Point(bounds.Right, currentY + groupHeaderHeight),
                        new Stroke(borderColor, 0.5f));
                }

                currentY += groupHeaderHeight;

                // Draw rows in this group (if expanded)
                if (!collapsed)
                {
                    for (int rowInGroup = 0; rowInGroup < groupRowCount; rowInGroup++)
                    {
                        float rowBottom = currentY + rowHeight;
                        bool aboveViewport = rowBottom <= dataStartY - bufferPx;
                        bool belowViewport = currentY > dataAreaBottom + bufferPx;

                        if (belowViewport)
                        {
                            // Advance past remaining rows in this group
                            currentY += (groupRowCount - rowInGroup) * rowHeight;
                            if (hasRowDetail)
                            {
                                for (int rr = rowInGroup; rr < groupRowCount; rr++)
                                {
                                    int dr = tdn.GetGroupDataRowIndex(g, rr);
                                    if (tdn.IsRowExpanded(dr))
                                    {
                                        currentY += tdn.GetRowDetailHeight(dr);
                                    }
                                }
                            }
                            break;
                        }

                        int r = tdn.GetGroupDataRowIndex(g, rowInGroup);

                        if (!aboveViewport)
                        {
                            visibleRows++;

                            if (hasRowDetail)
                            {
                                PaintRowExpandIndicator(tdn, r, currentY, rowHeight, bounds.X,
                                    expandIndicatorWidth, chevronSize, cellText);
                            }

                            PaintTabularDataRow(tdn, r, currentY, rowHeight, bounds, colWidths,
                                selectedBg, hoverBg, stripeBg, stripeAltBg, cellText, borderColor,
                                pad, cellFontSize, boolCircleRadius, rowInGroup % 2 == 1,
                                r < tdn.RowCount - 1 || rowInGroup < groupRowCount - 1,
                                hasRowDetail ? expandIndicatorWidth : 0f,
                                editOpenT, tabReducedMotion);
                        }

                        currentY += rowHeight;

                        if (hasRowDetail && tdn.IsRowExpanded(r))
                        {
                            float detailH = tdn.GetRowDetailHeight(r);
                            if (!aboveViewport)
                            {
                                PaintRowDetailPanel(tdn, r, currentY, detailH, bounds,
                                    detailBg, detailTextColor, detailFontSize, detailPad, borderColor);
                            }
                            currentY += detailH;
                        }
                    }
                }
            }
        }
        else
        {
            // ── Flat (ungrouped) rendering with scroll virtualization ─
            using var flatClip = ctx.PushClip(new Rect(bounds.X, dataStartY, bounds.Width, dataAreaHeight));

            // O(1) jump for fixed-height rows without row detail
            int startRow = 0;
            float currentY = dataStartY - scrollY;
            if (!hasRowDetail && tdn.RowCount > 0 && rowHeight > 0)
            {
                startRow = Math.Max(0, (int)(scrollY / rowHeight) - tdn.VirtualizationBufferRows);
                currentY = dataStartY + startRow * rowHeight - scrollY;
            }

            for (int r = startRow; r < tdn.RowCount; r++)
            {
                float rowBottom = currentY + rowHeight;
                bool aboveViewport = rowBottom <= dataStartY - bufferPx;
                bool belowViewport = currentY > dataAreaBottom + bufferPx;

                if (belowViewport)
                {
                    break;
                }

                if (!aboveViewport)
                {
                    visibleRows++;

                    // Draw expand indicator if row detail is enabled
                    if (hasRowDetail)
                    {
                        PaintRowExpandIndicator(tdn, r, currentY, rowHeight, bounds.X,
                            expandIndicatorWidth, chevronSize, cellText);
                    }

                    PaintTabularDataRow(tdn, r, currentY, rowHeight, bounds, colWidths,
                        selectedBg, hoverBg, stripeBg, stripeAltBg, cellText, borderColor,
                        pad, cellFontSize, boolCircleRadius, r % 2 == 1,
                        r < tdn.RowCount - 1,
                        leftOffset, editOpenT, tabReducedMotion);
                }

                currentY += rowHeight;

                // Frozen row separator: draw shadow line after the last frozen row
                if (r == frozenCount - 1)
                {
                    frozenEndY = currentY;
                }

                // Draw detail panel if this row is expanded
                if (hasRowDetail && tdn.IsRowExpanded(r))
                {
                    float detailH = tdn.GetRowDetailHeight(r);
                    if (!aboveViewport)
                    {
                        PaintRowDetailPanel(tdn, r, currentY, detailH, bounds,
                            detailBg, detailTextColor, detailFontSize, detailPad, borderColor);
                    }
                    currentY += detailH;
                }
            }
        }

        tdn.VisibleRowCount = visibleRows;

        // ── Frozen row separator ─────────────────────────────────────────
        if (frozenCount > 0 && frozenEndY > 0f)
        {
            var shadowColor = theme.Colors.Border.Opacity(0.6f);
            ctx.DrawLine(
                new Point(bounds.X + 1f, frozenEndY),
                new Point(bounds.Right - 1f, frozenEndY),
                new Stroke(shadowColor, 2f));
        }

        // ── Bottom aggregate row (fixed at bottom of data area) ────────
        if (hasAgg && tdn.AggregatePos == AggregatePosition.Bottom)
        {
            PaintAggregateRow(tdn, dataAreaBottom, aggRowHeight, bounds, colWidths, borderColor, pad, cellFontSize, leftOffset);
        }

        // ── Scroll indicator (thin scrollbar track + thumb) ──────────
        if (tdn.MaxScrollOffsetY > 0)
        {
            const float trackWidth = 4f;
            const float trackMargin = 2f;
            float trackX = bounds.Right - trackWidth - trackMargin;
            float trackTop = dataStartY;
            float trackHeight = dataAreaHeight;

            // Track background
            var trackColor = theme.Colors.Border.Opacity(0.2f);
            ctx.DrawRect(new Rect(trackX, trackTop, trackWidth, trackHeight), trackColor, radius: 2f);

            // Thumb
            float contentH = tdn.TotalContentHeight;
            float thumbHeight = Math.Max(20f, trackHeight * (dataAreaHeight / contentH));
            float thumbRange = trackHeight - thumbHeight;
            float thumbY = trackTop + (tdn.MaxScrollOffsetY > 0
                ? thumbRange * (scrollY / tdn.MaxScrollOffsetY)
                : 0);
            var thumbColor = theme.Colors.TextMuted.Opacity(0.5f);
            ctx.DrawRect(new Rect(trackX, thumbY, trackWidth, thumbHeight), thumbColor, radius: 2f);
        }

        // ── Empty state: no rows to show ─────────────────────────────
        // "No matching rows" when a filter hid everything; a plain "No items"
        // when the grid is genuinely empty (previously nothing was drawn, leaving
        // a blank body). A custom EmptyState(Node) is not yet rendered here — it
        // needs the node laid out — so the default keeps empty grids from looking
        // broken.
        if (tdn.RowCount == 0)
        {
            string emptyMsg = tdn.HasActiveFilter ? "No matching rows" : "No items";
            var emptyColor = theme.Colors.TextMuted;
            var emptySize = ctx.MeasureText(emptyMsg, cellFontSize);
            float emptyX = bounds.X + (bounds.Width - emptySize.Width) / 2f;
            float emptyY = headerBottom + 24f;
            ctx.DrawText(emptyMsg, MathF.Round(emptyX), MathF.Round(emptyY), cellFontSize, emptyColor);
        }

        // ── Deferred overlay rendering for select dropdown and date popup ──
        if (tdn.IsSelectDropdownOpen)
        {
            // Store absolute cell bounds for the dropdown trigger
            int ddCol = tdn.SelectDropdownCol;
            int ddRow = tdn.SelectDropdownRow;
            float cellAbsX = absoluteX;
            for (int c = 0; c < ddCol; c++)
            {
                cellAbsX += colWidths[c];
            }
            float cellAbsY = absoluteY + headerHeight + ddRow * rowHeight;
            float cellW = ddCol < colWidths.Length ? colWidths[ddCol] : 100f;
            var cellAbsBounds = new Rect(cellAbsX, cellAbsY, cellW, rowHeight);
            tdn.SelectDropdownCellBounds = cellAbsBounds;

            float capturedAbsX = cellAbsX;
            float capturedAbsY = cellAbsY;
            float capturedCellW = cellW;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                PaintDataGridSelectDropdown(tdn, new Rect(capturedAbsX, capturedAbsY, capturedCellW, rowHeight));
            });
        }

        if (tdn.IsDatePopupOpen && tdn.DatePopupPicker != null)
        {
            int dpCol = tdn.DatePopupCol;
            int dpRow = tdn.DatePopupRow;
            float cellAbsX = absoluteX;
            for (int c = 0; c < dpCol; c++)
            {
                cellAbsX += colWidths[c];
            }
            float cellAbsY = absoluteY + headerHeight + dpRow * rowHeight;
            float cellW = dpCol < colWidths.Length ? colWidths[dpCol] : 100f;
            var cellAbsBounds = new Rect(cellAbsX, cellAbsY, cellW, rowHeight);
            tdn.DatePopupCellBounds = cellAbsBounds;

            var datePicker = tdn.DatePopupPicker;
            float capturedAbsX = cellAbsX;
            float capturedAbsY = cellAbsY;
            float capturedCellW = cellW;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                var trigger = new Rect(capturedAbsX, capturedAbsY, capturedCellW, rowHeight);
                PaintCalendarDropdown(datePicker, trigger);
            });
        }

        // ── Deferred overlay rendering for column reorder ghost ──
        if (tdn.ReorderDragIndex >= 0 && tdn.ReorderDropIndex >= 0)
        {
            int dragIdx = tdn.ReorderDragIndex;
            int dropIdx = tdn.ReorderDropIndex;
            float ghostX = tdn.ReorderDragX - tdn.ReorderDragWidth / 2f;
            float ghostY = tdn.ReorderHeaderY;
            float ghostW = tdn.ReorderDragWidth;
            float ghostH = tdn.ReorderHeaderHeight;
            string ghostHeader = tdn.GetColumnHeader(dragIdx);

            // Compute drop indicator X position
            float dropLineX = absoluteX;
            for (int di = 0; di < dropIdx; di++)
            {
                dropLineX += colWidths[di];
            }
            // If dropping after the drag position, indicator is on the right edge
            if (dropIdx > dragIdx)
            {
                dropLineX += colWidths[dropIdx];
            }

            float capturedDropLineX = dropLineX;
            float capturedGhostX = ghostX;
            float capturedGhostY = ghostY;
            float capturedGhostW = ghostW;
            float capturedGhostH = ghostH;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                // Draw ghost column header (semi-transparent)
                var ghostBg = headerBg.Opacity(0.7f);
                var ghostRect = new Rect(capturedGhostX, capturedGhostY, capturedGhostW, capturedGhostH);
                ctx.DrawRect(ghostRect, ghostBg, radius: 2f);
                ctx.DrawRect(ghostRect, stroke: new Stroke(primaryColor.Opacity(0.5f), 1f), radius: 2f);

                // Draw ghost header text
                var textSize = ctx.MeasureText(ghostHeader, headerFontSize);
                float textX = capturedGhostX + (capturedGhostW - textSize.Width) / 2f;
                float textY = capturedGhostY + (capturedGhostH - headerFontSize) / 2f;
                ctx.DrawText(ghostHeader, MathF.Round(textX), MathF.Round(textY),
                    headerFontSize, headerText.Opacity(0.7f));

                // Draw drop indicator line
                ctx.DrawLine(
                    new Point(capturedDropLineX, capturedGhostY),
                    new Point(capturedDropLineX, capturedGhostY + capturedGhostH + bounds.Height),
                    new Stroke(primaryColor, 2f));
            });
        }

        // ── Column chooser dropdown overlay ─────────────────────────────
        if (tdn.IsColumnChooserOpen && tdn.IsColumnChooserEnabled)
        {
            float capturedAbsX = absoluteX;
            float capturedAbsY = absoluteY;
            float capturedBoundsW = bounds.Width;
            float capturedHeaderH = headerHeight;
            int colCount = tdn.ColumnCount;

            // Capture column headers and visibility for the overlay lambda
            var colHeaders = new string[colCount];
            var colVisible = new bool[colCount];
            for (int ci = 0; ci < colCount; ci++)
            {
                colHeaders[ci] = tdn.GetColumnHeader(ci);
                colVisible[ci] = tdn.GetColumnVisible(ci);
            }

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                PaintColumnChooserDropdown(tdn, capturedAbsX, capturedAbsY, capturedBoundsW,
                    capturedHeaderH, colCount, colHeaders, colVisible);
            });
        }
        else
        {
            tdn.ColumnChooserBounds = default;
        }
    }

    private void PaintTabularDataRow(
        ITabularDataNode tdn, int r, float rowY, float rowHeight,
        Rect bounds, float[] colWidths,
        ColorValue selectedBg, ColorValue hoverBg, ColorValue stripeBg, ColorValue stripeAltBg,
        ColorValue cellText, ColorValue borderColor,
        float pad, float cellFontSize, float boolCircleRadius,
        bool isAlternateStripe, bool drawSeparator,
        float leftOffset = 0f, float editOpenT = 1f, bool tabReducedMotion = false)
    {
        // Selection highlight (strongest)
        if (tdn.IsRowSelected(r))
        {
            ctx.DrawRect(new Rect(bounds.X + 1f, rowY, bounds.Width - 2f, rowHeight), selectedBg);
        }
        // Hover highlight
        else if (tdn.IsHoverHighlightEnabled && tdn.HoveredRowIndex == r)
        {
            ctx.DrawRect(new Rect(bounds.X + 1f, rowY, bounds.Width - 2f, rowHeight), hoverBg);
        }
        // Stripe alternating rows
        else if (tdn.IsStriped && isAlternateStripe)
        {
            ctx.DrawRect(new Rect(bounds.X + 1f, rowY, bounds.Width - 2f, rowHeight), stripeAltBg);
        }
        // Base background for non-alternate rows (prevents parent bg bleed-through)
        else if (tdn.IsStriped)
        {
            ctx.DrawRect(new Rect(bounds.X + 1f, rowY, bounds.Width - 2f, rowHeight), stripeBg);
        }

        // Draw cell content (shifted right by leftOffset for expand indicator)
        float colX = bounds.X + leftOffset;
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            if (colWidths[c] <= 0f)
            {
                continue;
            }

            // Clip each cell to its column width
            using var dataCellClip = ctx.PushClip(new Rect(colX, rowY, colWidths[c], rowHeight));

            if (tdn.IsCustomColumn(c))
            {
                PaintCustomCellNode(tdn.GetCustomCellNode(r, c), colX, rowY, colWidths[c], rowHeight, pad, cellFontSize, cellText);
            }
            else if (tdn.IsBoolColumn(c))
            {
                bool val = tdn.GetBoolValue(r, c);
                float cx = colX + colWidths[c] / 2f;
                float cy = rowY + rowHeight / 2f;

                if (val)
                {
                    var successColor = new ColorValue("#4CAF50");
                    ctx.DrawCircle(new Point(cx, cy), boolCircleRadius, successColor);
                }
                else
                {
                    var emptyColor = theme.Colors.TextMuted;
                    ctx.DrawCircle(new Point(cx, cy), boolCircleRadius, stroke: new Stroke(emptyColor, 1.5f));
                }
            }
            else
            {
                if (tdn.IsEditing && tdn.EditingRow == r && tdn.EditingCol == c)
                {
                    var editBg = new ColorValue("#FFFFFF");
                    var editBorder = theme.Colors.Primary;
                    var editRect = new Rect(colX + 2, rowY + 2, colWidths[c] - 4, rowHeight - 4);

                    // Animated edit mode border
                    float borderW = tabReducedMotion ? 2f : LerpF(0f, 2f, editOpenT);

                    ctx.DrawRect(editRect, editBg);
                    if (borderW > 0.1f)
                    {
                        ctx.DrawRect(editRect, stroke: new Stroke(editBorder, borderW));
                    }

                    string editText = tdn.EditBuffer;
                    var editTextColor = new ColorValue("#1A1A1A");
                    float textY = MathF.Round(editRect.Y + (editRect.Height - cellFontSize) / 2f);
                    float textX = editRect.X + 4f;

                    if (!string.IsNullOrEmpty(editText))
                    {
                        ctx.DrawText(editText, textX, textY, cellFontSize, editTextColor);
                    }

                    // Smooth sinusoidal caret blink
                    string beforeCursor = editText[..tdn.EditCursorPos];
                    float cursorX = textX + ctx.MeasureText(beforeCursor, cellFontSize).Width;
                    float cursorTop = editRect.Y + 4f;
                    float cursorBot = editRect.Bottom - 4f;
                    float caretOpacity = 1f;
                    if (!tabReducedMotion)
                    {
                        double caretElapsed = Stopwatch.GetElapsedTime(0, Stopwatch.GetTimestamp()).TotalSeconds;
                        caretOpacity = 0.5f + 0.5f * (float)Math.Cos(caretElapsed * 4.0);
                        ControlStateAnimator.SignalActiveTransition();
                    }
                    ctx.DrawLine(
                        new Point(cursorX, cursorTop),
                        new Point(cursorX, cursorBot),
                        new Stroke(editTextColor.Opacity(caretOpacity), 1.5f));
                }
                else
                {
                    string cellStr = tdn.GetCellText(r, c);
                    var alignment = tdn.GetColumnAlignment(c);
                    var textSize = ctx.MeasureText(cellStr, cellFontSize);

                    float textX = alignment switch
                    {
                        ColumnAlignment.Right => colX + colWidths[c] - textSize.Width - pad,
                        ColumnAlignment.Center => colX + (colWidths[c] - textSize.Width) / 2f,
                        _ => colX + pad
                    };

                    ctx.DrawText(cellStr, MathF.Round(textX), MathF.Round(rowY + (rowHeight - cellFontSize) / 2f),
                        cellFontSize, cellText);
                }
            }

            colX += colWidths[c];
        }

        // Draw error indicators (red border + triangle) for cells with validation errors
        float errColX = bounds.X + leftOffset;
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            if (colWidths[c] <= 0f)
            {
                continue;
            }

            if (tdn.HasCellError(r, c))
            {
                var errorColor = new ColorValue("#FF3B30");
                var cellRect = new Rect(errColX + 1, rowY + 1, colWidths[c] - 2, rowHeight - 2);

                // Red border
                ctx.DrawRect(cellRect, stroke: new Stroke(errorColor, 1.5f));

                // Small red triangle in top-right corner
                float triSize = 6f;
                float triX = cellRect.Right - triSize;
                float triY = cellRect.Y;
                var trianglePath = PathBuilder.Rent()
                    .MoveTo(new Point(triX, triY))
                    .LineTo(new Point(triX + triSize, triY))
                    .LineTo(new Point(triX + triSize, triY + triSize))
                    .Close()
                    .BuildTransient();
                ctx.DrawPath(trianglePath, errorColor);

                // Defer error tooltip if this cell is hovered
                if (tdn.HoveredRowIndex == r && tdn.HoveredColIndex == c)
                {
                    string? errorMsg = tdn.GetCellErrorMessage(r, c);
                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        float absErrX = absoluteX + errColX;
                        float absErrY = absoluteY + rowY;
                        float errW = colWidths[c];
                        float errH = rowHeight;
                        string msg = errorMsg;

                        deferredOverlays ??= [];
                        deferredOverlays.Add(() =>
                        {
                            PaintTooltipOverlay(msg, new Rect(absErrX, absErrY, errW, errH));
                        });
                    }
                }
            }

            errColX += colWidths[c];
        }

        // Draw row separator
        if (drawSeparator)
        {
            float sepY = rowY + rowHeight;
            ctx.DrawLine(
                new Point(bounds.X, sepY),
                new Point(bounds.Right, sepY),
                new Stroke(borderColor, 0.5f));
        }
    }

    /// <summary>
    /// Renders a custom column cell Node inline within a DataTable cell.
    /// Supports Sparkline, Label, and Row (of Labels) — the common patterns
    /// used in dashboard tables for badges, indicators, and inline charts.
    /// </summary>
    private void PaintCustomCellNode(
        Node? node, float cellX, float cellY, float cellW, float cellH,
        float pad, float fontSize, ColorValue defaultTextColor)
    {
        if (node is null)
        {
            return;
        }

        if (node is Sparkline spark)
        {
            float sparkW = spark.widthValue;
            float sparkH = Math.Min(spark.heightValue, cellH - 4f);
            float sx = cellX + (cellW - sparkW) / 2f;
            float sy = cellY + (cellH - sparkH) / 2f;
            PaintSparkline(spark, new Rect(sx, sy, sparkW, sparkH));
            return;
        }

        if (node is Label label)
        {
            PaintInlineCellLabel(label, cellX, cellY, cellW, cellH, pad, fontSize, defaultTextColor);
            return;
        }

        if (node is Row row && row.Children is { Count: > 0 })
        {
            float spacing = row.Spacing;
            float startX = cellX + pad;
            float cx = startX;

            foreach (var child in row.Children)
            {
                if (child is Label lbl)
                {
                    var color = lbl.TextColorOverride ?? defaultTextColor;
                    float fs = lbl.TextStyleOverride?.Size ?? fontSize;
                    string text = lbl.Text ?? "";
                    float ty = cellY + (cellH - fs) / 2f;
                    ctx.DrawText(text, MathF.Round(cx), MathF.Round(ty), fs, color);
                    cx += ctx.MeasureText(text, fs).Width + spacing;
                }
            }
            return;
        }
    }

    private void PaintInlineCellLabel(
        Label label, float cellX, float cellY, float cellW, float cellH,
        float pad, float fontSize, ColorValue defaultTextColor)
    {
        var color = label.TextColorOverride ?? defaultTextColor;
        float fs = label.TextStyleOverride?.Size ?? fontSize;
        string text = label.Text ?? "";
        var textSize = ctx.MeasureText(text, fs);
        float tx = cellX + pad;
        float ty = cellY + (cellH - fs) / 2f;
        ctx.DrawText(text, MathF.Round(tx), MathF.Round(ty), fs, color);
    }

    private void PaintRowExpandIndicator(
        ITabularDataNode tdn, int row, float rowY, float rowHeight,
        float boundsX, float indicatorWidth, float chevronSize, ColorValue color)
    {
        bool expanded = tdn.IsRowExpanded(row);
        float chevronX = boundsX + (indicatorWidth - chevronSize) / 2f;
        float chevronCenterY = rowY + rowHeight / 2f;
        float halfChev = chevronSize / 2f;

        Path chevronPath;
        if (expanded)
        {
            // Down-pointing triangle ▼
            chevronPath = PathBuilder.Rent()
                .MoveTo(new Point(chevronX, chevronCenterY - halfChev / 2f))
                .LineTo(new Point(chevronX + chevronSize, chevronCenterY - halfChev / 2f))
                .LineTo(new Point(chevronX + halfChev, chevronCenterY + halfChev))
                .Close()
                .BuildTransient();
        }
        else
        {
            // Right-pointing triangle ▶
            chevronPath = PathBuilder.Rent()
                .MoveTo(new Point(chevronX, chevronCenterY - halfChev))
                .LineTo(new Point(chevronX + chevronSize, chevronCenterY))
                .LineTo(new Point(chevronX, chevronCenterY + halfChev))
                .Close()
                .BuildTransient();
        }
        ctx.DrawPath(chevronPath, fill: color.Opacity(0.5f));
    }

    private void PaintRowDetailPanel(
        ITabularDataNode tdn, int row, float panelY, float panelHeight,
        Rect bounds, ColorValue detailBg, ColorValue detailTextColor,
        float detailFontSize, float detailPad, ColorValue borderColor)
    {
        // Background
        var panelRect = new Rect(bounds.X + 1f, panelY, bounds.Width - 2f, panelHeight);
        ctx.DrawRect(panelRect, detailBg);

        // Left accent bar
        var accentColor = theme.Colors.Primary.Opacity(0.4f);
        ctx.DrawLine(
            new Point(bounds.X + 4f, panelY + 4f),
            new Point(bounds.X + 4f, panelY + panelHeight - 4f),
            new Stroke(accentColor, 2f));

        // Detail text (multi-line)
        string text = tdn.GetRowDetailText(row);
        string[] lines = text.Split('\n');
        float textX = bounds.X + detailPad + 8f;
        float textY = panelY + 8f;
        float lineHeight = 18f;

        for (int i = 0; i < lines.Length; i++)
        {
            if (textY + detailFontSize > panelY + panelHeight)
            {
                break;
            }
            ctx.DrawText(lines[i], MathF.Round(textX), MathF.Round(textY),
                detailFontSize, detailTextColor);
            textY += lineHeight;
        }

        // Bottom separator
        ctx.DrawLine(
            new Point(bounds.X, panelY + panelHeight),
            new Point(bounds.Right, panelY + panelHeight),
            new Stroke(borderColor, 0.5f));
    }

    private void PaintAggregateRow(
        ITabularDataNode tdn, float rowY, float rowHeight, Rect bounds,
        float[] colWidths, ColorValue borderColor, float pad, float fontSize,
        float leftOffset)
    {
        // Distinct background (darker than normal rows)
        var aggBg = theme.Colors.SurfaceAlt.Opacity(0.8f);
        var aggRect = new Rect(bounds.X + 1f, rowY, bounds.Width - 2f, rowHeight);
        ctx.DrawRect(aggRect, aggBg);

        // Top separator
        ctx.DrawLine(
            new Point(bounds.X, rowY),
            new Point(bounds.Right, rowY),
            new Stroke(borderColor, 1f));

        // Draw aggregate values per column
        var aggTextColor = theme.Colors.Text;
        float colX = bounds.X + leftOffset;
        for (int c = 0; c < tdn.ColumnCount && c < colWidths.Length; c++)
        {
            float colW = colWidths[c];
            if (colW <= 0f)
            {
                continue;
            }
            string text = tdn.GetAggregateText(c);
            if (text.Length > 0)
            {
                var textSize = ctx.MeasureText(text, fontSize);
                float textX;
                if (tdn.GetColumnAlignment(c) == ColumnAlignment.Right)
                {
                    textX = colX + colW - pad - textSize.Width;
                }
                else if (tdn.GetColumnAlignment(c) == ColumnAlignment.Center)
                {
                    textX = colX + (colW - textSize.Width) / 2f;
                }
                else
                {
                    textX = colX + pad;
                }
                float textY = rowY + (rowHeight - textSize.Height) / 2f;
                string? boldFont = ctx.DefaultFontPath != null
                    ? ctx.ResolveFontPath(ctx.DefaultFontPath, FontWeight.SemiBold)
                    : null;
                ctx.DrawText(text, MathF.Round(textX), MathF.Round(textY), fontSize, aggTextColor, fontPath: boldFont);
            }
            colX += colW;
        }

        // Bottom separator
        ctx.DrawLine(
            new Point(bounds.X, rowY + rowHeight),
            new Point(bounds.Right, rowY + rowHeight),
            new Stroke(borderColor, 0.5f));
    }

    private void PaintColumnChooserDropdown(
        ITabularDataNode tdn, float absX, float absY, float boundsWidth,
        float headerHeight, int colCount, string[] colHeaders, bool[] colVisible)
    {
        const float itemHeight = 28f;
        const float pad = 8f;
        const float fontSize = 12f;
        const float gap = 2f;
        const float checkboxSize = 14f;

        float ddWidth = Math.Min(180f, boundsWidth);
        float ddHeight = colCount * itemHeight;
        float maxHeight = 300f;
        if (ddHeight > maxHeight)
        {
            ddHeight = maxHeight;
        }

        // Position below the header on the right side
        float ddX = absX + boundsWidth - ddWidth;
        float ddY = absY + headerHeight + gap;

        var ddBounds = new Rect(ddX, ddY, ddWidth, ddHeight);
        tdn.ColumnChooserBounds = ddBounds;

        // Shadow
        PaintShadow(theme.Select.DropdownShadow, ddBounds, 4f);

        // Background
        ctx.DrawRect(ddBounds, theme.Colors.Surface, radius: 4f);
        ctx.DrawRect(ddBounds, stroke: new Stroke(theme.Colors.Border, 1f), radius: 4f);

        // Clip to dropdown bounds
        using var clip = ctx.PushClip(ddBounds);

        float itemY = ddBounds.Y;
        int hoverIdx = tdn.ColumnChooserHoverIndex;

        for (int i = 0; i < colCount && itemY + itemHeight <= ddBounds.Bottom; i++)
        {
            var itemRect = new Rect(ddBounds.X, itemY, ddBounds.Width, itemHeight);

            // Hover highlight
            if (i == hoverIdx)
            {
                ctx.DrawRect(itemRect, theme.Colors.Primary.Opacity(0.1f));
            }

            // Checkbox
            float cbX = ddBounds.X + pad;
            float cbY = itemY + (itemHeight - checkboxSize) / 2f;
            var cbRect = new Rect(cbX, cbY, checkboxSize, checkboxSize);
            ctx.DrawRect(cbRect, stroke: new Stroke(theme.Colors.Border, 1f), radius: 2f);

            if (colVisible[i])
            {
                // Draw checkmark
                float checkPad = 3f;
                ctx.DrawLine(
                    new Point(cbX + checkPad, cbY + checkboxSize / 2f),
                    new Point(cbX + checkboxSize / 2f, cbY + checkboxSize - checkPad),
                    new Stroke(theme.Colors.Primary, 2f));
                ctx.DrawLine(
                    new Point(cbX + checkboxSize / 2f, cbY + checkboxSize - checkPad),
                    new Point(cbX + checkboxSize - checkPad, cbY + checkPad),
                    new Stroke(theme.Colors.Primary, 2f));
            }

            // Column name
            float textX = cbX + checkboxSize + 8f;
            float textY = itemY + (itemHeight - fontSize) / 2f;
            ctx.DrawText(colHeaders[i], MathF.Round(textX), MathF.Round(textY),
                fontSize, theme.Colors.Text);

            itemY += itemHeight;
        }
    }

    private void PaintDataGridSelectDropdown(ITabularDataNode tdn, Rect cellBounds)
    {
        var options = tdn.GetSelectOptions(tdn.SelectDropdownCol);
        if (options == null || options.Count == 0)
        {
            return;
        }

        var t = theme.Select;
        const float itemHeight = 32f;
        const float pad = 8f;
        const float fontSize = 13f;
        const float gap = 4f;

        float ddWidth = Math.Max(cellBounds.Width, 120f);
        float ddHeight = options.Count * itemHeight;
        float maxHeight = 240f;
        if (ddHeight > maxHeight)
        {
            ddHeight = maxHeight;
        }

        // Position below cell by default, flip above if not enough room
        float ddX = cellBounds.X;
        float ddYBelow = cellBounds.Bottom + gap;
        float ddYAbove = cellBounds.Y - gap - ddHeight;
        float viewportHeight = ViewportLogicalHeight;

        float ddY = (ddYBelow + ddHeight > viewportHeight && ddYAbove >= 0)
            ? ddYAbove
            : ddYBelow;

        var ddBounds = new Rect(ddX, ddY, ddWidth, ddHeight);
        tdn.SelectDropdownBounds = ddBounds;

        // Shadow + background
        PaintShadow(t.DropdownShadow, ddBounds, t.DropdownRadius);
        ctx.DrawRect(ddBounds, t.DropdownBackground, radius: t.DropdownRadius);
        if (t.BorderWidth > 0)
        {
            ctx.DrawRect(ddBounds, stroke: new Stroke(t.BorderColor, t.BorderWidth), radius: t.DropdownRadius);
        }

        using var clip = ctx.PushClip(ddBounds);

        // Draw options
        int hoverIdx = tdn.SelectDropdownHoverIndex;
        for (int i = 0; i < options.Count; i++)
        {
            float optY = ddY + i * itemHeight;
            if (optY + itemHeight < ddY || optY > ddY + ddHeight)
            {
                continue;
            }

            var optRect = new Rect(ddX, optY, ddWidth, itemHeight);

            // Hover highlight
            if (i == hoverIdx)
            {
                ctx.DrawRect(optRect, theme.Colors.Primary.Opacity(0.12f));
            }

            // Option text
            string text = options[i]?.ToString() ?? "";
            ctx.DrawText(text, MathF.Round(ddX + pad), MathF.Round(optY + (itemHeight - fontSize) / 2f),
                fontSize, i == hoverIdx ? theme.Colors.Primary : theme.Colors.Text);
        }
    }

    // ── ListView (IListViewNode) ──────────────────────────────────────

    private void PaintListView(IListViewNode lvn, Node node, Rect bounds)
    {
        float itemHeight = lvn.GetItemHeight();
        const float pad = 12f;
        const float fontSize = 13f;
        const float sectionFontSize = 11f;

        var bg = theme.Colors.Surface;
        var textColor = theme.Colors.Text;
        var borderColor = theme.Colors.Border;
        var selectedBg = theme.Colors.Primary;
        var selectedText = theme.Colors.TextOnPrimary;
        var sectionBg = theme.Colors.SurfaceAlt;
        var sectionText = theme.Colors.TextMuted;

        // Outer border
        ctx.DrawRect(bounds, bg, radius: 4f);
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor, 1f), radius: 4f);

        float currentY = bounds.Y;

        if (lvn.SectionCount > 0)
        {
            for (int s = 0; s < lvn.SectionCount; s++)
            {
                if (currentY > bounds.Bottom)
                {
                    break;
                }

                // Section header
                float sectionHeaderHeight = 28f;
                ctx.DrawRect(new Rect(bounds.X + 1f, currentY, bounds.Width - 2f, sectionHeaderHeight), sectionBg);
                ctx.DrawText(lvn.GetSectionKey(s).ToUpperInvariant(), MathF.Round(bounds.X + pad),
                    MathF.Round(currentY + (sectionHeaderHeight - sectionFontSize) / 2f),
                    sectionFontSize, sectionText);
                currentY += sectionHeaderHeight;

                // Section items
                for (int i = 0; i < lvn.GetSectionItemCount(s); i++)
                {
                    if (currentY + itemHeight > bounds.Bottom)
                    {
                        break;
                    }

                    string text = lvn.GetSectionItemText(s, i);
                    ctx.DrawText(text, MathF.Round(bounds.X + pad),
                        MathF.Round(currentY + (itemHeight - fontSize) / 2f),
                        fontSize, textColor);

                    currentY += itemHeight;

                    // Separator
                    ctx.DrawLine(
                        new Point(bounds.X + pad, currentY),
                        new Point(bounds.Right - pad, currentY),
                        new Stroke(borderColor, 0.5f));
                }
            }
        }
        else
        {
            for (int i = 0; i < lvn.ItemCount; i++)
            {
                float itemY = bounds.Y + i * itemHeight;
                if (itemY + itemHeight > bounds.Bottom)
                {
                    break;
                }

                bool isSelected = lvn.IsItemSelected(i);
                string text = lvn.GetItemText(i);

                if (isSelected)
                {
                    ctx.DrawRect(new Rect(bounds.X + 1f, itemY, bounds.Width - 2f, itemHeight), selectedBg);
                    ctx.DrawText(text, MathF.Round(bounds.X + pad),
                        MathF.Round(itemY + (itemHeight - fontSize) / 2f),
                        fontSize, selectedText);
                }
                else
                {
                    ctx.DrawText(text, MathF.Round(bounds.X + pad),
                        MathF.Round(itemY + (itemHeight - fontSize) / 2f),
                        fontSize, textColor);
                }

                // Separator
                if (i < lvn.ItemCount - 1)
                {
                    float sepY = itemY + itemHeight;
                    ctx.DrawLine(
                        new Point(bounds.X + pad, sepY),
                        new Point(bounds.Right - pad, sepY),
                        new Stroke(borderColor, 0.5f));
                }
            }
        }
    }

    // ── ColorPicker ───────────────────────────────────────────────────

    private void PaintColorPicker(ColorPicker cp, Rect bounds)
    {
        cp.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);
        var currentColor = cp.Value.Value;
        var t = theme.TextInput;

        // Initialize HSB from the current color on first paint
        if (!cp.HsbInitialized)
        {
            var (h, s, b) = ColorToHsb(currentColor);
            cp.Hue = h;
            cp.Saturation = s;
            cp.Brightness = b;
            cp.HsbInitialized = true;
        }

        // Outer container background
        ctx.DrawRect(bounds, theme.Colors.Surface, radius: t.Radius);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border, 1f), radius: t.Radius);

        const float pad = 8f;
        float innerX = bounds.X + pad;
        float innerY = bounds.Y + pad;
        float innerW = bounds.Width - pad * 2;
        const float canvasHeight = 130f;
        const float hueBarH = 14f;

        // ── Saturation/Brightness canvas ──────────────────────────────
        var canvasBounds = new Rect(innerX, innerY, innerW, canvasHeight);

        // Paint a 2D grid of solid HSB colors — each cell is a single opaque color.
        // This avoids alpha compositing artifacts that cause visible banding.
        const float cellSize = 2f;
        int cols = (int)MathF.Ceiling(innerW / cellSize);
        int rows = (int)MathF.Ceiling(canvasHeight / cellSize);
        float currentHue = cp.Hue;

        for (int row = 0; row < rows; row++)
        {
            float brightness = 1f - (row + 0.5f) / rows;
            float cy = innerY + row * cellSize;
            float ch = (row == rows - 1) ? (canvasBounds.Bottom - cy) : cellSize;

            for (int col = 0; col < cols; col++)
            {
                float saturation = (col + 0.5f) / cols;
                float cx = innerX + col * cellSize;
                float cw = (col == cols - 1) ? (canvasBounds.Right - cx) : cellSize;

                // HSB to sRGB inline
                float c = brightness * saturation;
                float hp = currentHue / 60f;
                float x = c * (1f - MathF.Abs(hp % 2f - 1f));
                float m = brightness - c;
                float cr, cg, cb;
                if (hp < 1f) { cr = c + m; cg = x + m; cb = m; }
                else if (hp < 2f) { cr = x + m; cg = c + m; cb = m; }
                else if (hp < 3f) { cr = m; cg = c + m; cb = x + m; }
                else if (hp < 4f) { cr = m; cg = x + m; cb = c + m; }
                else if (hp < 5f) { cr = x + m; cg = m; cb = c + m; }
                else { cr = c + m; cg = m; cb = x + m; }

                ctx.DrawRect(new Rect(cx, cy, cw, ch), ColorValue.FromRgba(cr, cg, cb, 1f));
            }
        }

        // Canvas border
        ctx.DrawRect(canvasBounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f),
            radius: t.Radius);

        // SB indicator circle
        float indicatorX = innerX + cp.Saturation * innerW;
        float indicatorY = innerY + (1f - cp.Brightness) * canvasHeight;
        ctx.DrawCircle(new Point(indicatorX, indicatorY), 7f,
            fill: currentColor,
            stroke: new Stroke(ColorValue.FromRgba(1f, 1f, 1f, 1f), 2f));
        ctx.DrawCircle(new Point(indicatorX, indicatorY), 9f,
            stroke: new Stroke(ColorValue.FromRgba(0f, 0f, 0f, 0.3f), 1f));

        // ── Hue bar below the canvas ──────────────────────────────────
        float hueBarY = innerY + canvasHeight + 8f;
        var hueBarBounds = new Rect(innerX, hueBarY, innerW, hueBarH);
        PaintHueBar(hueBarBounds, hueBarH / 2f);

        // Hue indicator circle
        float hueFrac = cp.Hue / 360f;
        float hueIndX = innerX + hueFrac * innerW;
        float hueIndCY = hueBarY + hueBarH / 2f;
        ctx.DrawCircle(new Point(hueIndX, hueIndCY), 7f,
            fill: HueToColor(cp.Hue),
            stroke: new Stroke(ColorValue.FromRgba(1f, 1f, 1f, 1f), 2f));
        ctx.DrawCircle(new Point(hueIndX, hueIndCY), 9f,
            stroke: new Stroke(ColorValue.FromRgba(0f, 0f, 0f, 0.3f), 1f));

        // ── Hex label below hue bar ───────────────────────────────────
        float labelY = hueBarY + hueBarH + 10f;
        string hexStr = currentColor.ToHex();
        float labelFontSize = 13f;
        var labelSize = ctx.MeasureText(hexStr, labelFontSize);
        float labelX = innerX + (innerW - labelSize.Width) / 2f;
        ctx.DrawText(hexStr, MathF.Round(labelX), MathF.Round(labelY),
            labelFontSize, theme.Colors.Text);
    }

    private static (float h, float s, float b) ColorToHsb(ColorValue color)
    {
        if (color.A <= 0f)
        {
            return (0f, 0f, 0f);
        }

        // Unpremultiply and convert linear → sRGB
        float invA = 1f / color.A;
        float sR = PainterLinearToSrgb(color.R * invA);
        float sG = PainterLinearToSrgb(color.G * invA);
        float sB = PainterLinearToSrgb(color.B * invA);

        float max = MathF.Max(sR, MathF.Max(sG, sB));
        float min = MathF.Min(sR, MathF.Min(sG, sB));
        float delta = max - min;

        float h = 0f;
        float s = max > 0f ? delta / max : 0f;
        float b = max;

        if (delta > 0.001f)
        {
            if (max == sR)
            {
                h = 60f * (((sG - sB) / delta) % 6f);
            }
            else if (max == sG)
            {
                h = 60f * (((sB - sR) / delta) + 2f);
            }
            else
            {
                h = 60f * (((sR - sG) / delta) + 4f);
            }

            if (h < 0f)
            {
                h += 360f;
            }
        }

        return (h, s, b);
    }

    private static float PainterLinearToSrgb(float linear)
    {
        linear = MathF.Max(0f, MathF.Min(1f, linear));
        if (linear <= 0.0031308f)
        {
            return linear * 12.92f;
        }

        return 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;
    }

    private void PaintHueBar(Rect bounds, float radius)
    {
        // Draw a hue spectrum bar using 24 segments for smooth appearance
        const int segments = 24;
        float segW = bounds.Width / segments;

        for (int i = 0; i < segments; i++)
        {
            var midColor = HueToColor(360f * (i + 0.5f) / segments);
            float sx = bounds.X + i * segW;
            float sw = (i == segments - 1) ? (bounds.Right - sx) : segW;
            ctx.DrawRect(new Rect(sx, bounds.Y, sw, bounds.Height), midColor);
        }

        // Round corners via border overlay
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.2f), 1f), radius: radius);
    }

    private static ColorValue HueToColor(float hue)
    {
        // Convert hue (0-360) to RGB with full saturation and brightness
        float h = hue / 60f;
        float c = 1f;
        float x = 1f - MathF.Abs(h % 2f - 1f);

        float r, g, b;
        if (h < 1f) { r = c; g = x; b = 0f; }
        else if (h < 2f) { r = x; g = c; b = 0f; }
        else if (h < 3f) { r = 0f; g = c; b = x; }
        else if (h < 4f) { r = 0f; g = x; b = c; }
        else if (h < 5f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return ColorValue.FromRgba(r, g, b, 1f);
    }

    // ── PinInput ──────────────────────────────────────────────────────

    // Per-pin-input state: tracks active cell index and per-cell fill timestamps
    // for scale-in animations when digits are entered.
    private static readonly Dictionary<int, PinAnimState> pinAnimStates = new();

    private sealed class PinAnimState
    {
        internal int LastActiveCell = -1;
        internal int LastTextLength;
        internal long ActiveCellChangedTimestamp;
        internal long[] CellFilledTimestamps = [];
        internal long LastAccessTimestamp;

        internal void EnsureCellCount(int count)
        {
            if (CellFilledTimestamps.Length < count)
            {
                CellFilledTimestamps = new long[count];
            }
        }
    }

    private void PaintPinInput(PinInput pin, Rect bounds)
    {
        pin.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);
        var t = theme.TextInput;
        int length = pin.Length;
        bool disabled = pin.IsDisabled;

        const float cellWidth = 40f;
        const float cellHeight = 48f;
        const float gap = 8f;
        const float separatorExtra = 12f;
        float fontSize = 20f;

        bool focused = ReferenceEquals(FocusManager.FocusedElement, pin);

        // Animated focus/disabled transitions
        var hoverModel = t.Transition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        bool isFocused = focused && !disabled;
        var anim = ControlStateAnimator.Reconcile(
            pin, hoverModel, pressModel, isDisabled: disabled, isFocused: isFocused);
        float focusT = anim.Focus.Current;
        float disabledT = anim.Disabled.Current;

        // Use the dispatcher's buffer when focused (it's always up-to-date),
        // fall back to the Bindable's value when not focused.
        string value = focused && InputDispatcher.PinEditBuffer != null
            ? InputDispatcher.PinEditBuffer
            : (pin.Value.Value ?? "");

        // Per-cell animation state
        int pinKey = RuntimeHelpers.GetHashCode(pin);
        if (!pinAnimStates.TryGetValue(pinKey, out var pinAnim))
        {
            pinAnim = new PinAnimState { LastTextLength = value.Length };
            pinAnimStates[pinKey] = pinAnim;
        }

        pinAnim.EnsureCellCount(length);
        pinAnim.LastAccessTimestamp = Stopwatch.GetTimestamp();
        long now = Stopwatch.GetTimestamp();

        // Detect active cell change for scale animation
        int activeCell = focused ? InputDispatcher.PinActiveCellIndex : -1;
        if (activeCell != pinAnim.LastActiveCell)
        {
            pinAnim.ActiveCellChangedTimestamp = now;
            pinAnim.LastActiveCell = activeCell;
        }

        // Detect new digits entered for scale-in animation
        if (value.Length > pinAnim.LastTextLength)
        {
            for (int i = pinAnim.LastTextLength; i < value.Length && i < length; i++)
            {
                pinAnim.CellFilledTimestamps[i] = now;
            }
        }

        pinAnim.LastTextLength = value.Length;

        float x = bounds.X;
        float y = bounds.Y + (bounds.Height - cellHeight) / 2f;

        for (int i = 0; i < length; i++)
        {
            // Check if we need a separator before this cell
            if (pin.SeparatorPositions.Contains(i) && i > 0)
            {
                // Draw a separator dash
                float sepX = x + 2f;
                float sepY = y + cellHeight / 2f;
                ctx.DrawLine(
                    new Point(sepX, sepY),
                    new Point(sepX + 8f, sepY),
                    new Stroke(theme.Colors.TextMuted, 2f));
                x += separatorExtra;
            }

            var cellBounds = new Rect(x, y, cellWidth, cellHeight);
            bool isActiveCell = focused && i == activeCell;

            // Active cell scale-up animation (1.08x over ~200ms spring)
            float cellScale = 1f;
            if (!ControlStateAnimator.ReducedMotion && isActiveCell && focusT > 0.5f)
            {
                float elapsed = (float)Stopwatch.GetElapsedTime(pinAnim.ActiveCellChangedTimestamp).TotalSeconds;
                // Critically damped spring approximation: overshoot then settle
                float springT = 1f - MathF.Exp(-8f * elapsed) * (1f + 8f * elapsed * 0.3f);
                springT = Math.Clamp(springT, 0f, 1.15f);
                cellScale = LerpF(1f, 1.08f, springT);
                if (elapsed < 0.4f)
                {
                    ControlStateAnimator.SignalActiveTransition();
                }
            }

            Rect paintCellBounds = cellBounds;
            if (MathF.Abs(cellScale - 1f) > 0.001f)
            {
                float cx = cellBounds.X + cellWidth / 2f;
                float cy = cellBounds.Y + cellHeight / 2f;
                float sw = cellWidth * cellScale;
                float sh = cellHeight * cellScale;
                paintCellBounds = new Rect(cx - sw / 2f, cy - sh / 2f, sw, sh);
            }

            // Cell background
            var bg = disabled ? t.DisabledBackground : t.Background;
            ctx.DrawRect(paintCellBounds, bg, radius: t.Radius);

            // Cell border — interpolate between default and focus for active cell
            if (isActiveCell)
            {
                float cellFocusT = focusT;
                var borderColor = ColorValue.Lerp(t.BorderColor, t.FocusBorderColor, cellFocusT);
                float borderWidth = LerpF(t.BorderWidth, t.FocusBorderWidth, cellFocusT);
                ctx.DrawRect(paintCellBounds, stroke: new Stroke(borderColor, borderWidth),
                    radius: t.Radius);

                // Focus ring — fades in with focus
                if (t.FocusRingWidth > 0 && cellFocusT > 0.001f)
                {
                    float ringOff = t.FocusBorderWidth;
                    var ringRect = new Rect(
                        paintCellBounds.X - ringOff, paintCellBounds.Y - ringOff,
                        paintCellBounds.Width + ringOff * 2, paintCellBounds.Height + ringOff * 2);
                    ctx.DrawRect(ringRect,
                        stroke: new Stroke(t.FocusRingColor.Opacity(cellFocusT), t.FocusRingWidth * cellFocusT),
                        radius: t.Radius + ringOff);
                }
            }
            else
            {
                var borderColor = disabled ? t.DisabledBorderColor : t.BorderColor;
                ctx.DrawRect(paintCellBounds, stroke: new Stroke(borderColor, t.BorderWidth),
                    radius: t.Radius);
            }

            // Character in cell — with scale-in spring when newly entered
            if (i < value.Length)
            {
                char ch = value[i];
                string displayChar = pin.IsMasked ? "\u2022" : ch.ToString();
                var textColor = disabled ? t.DisabledTextColor : t.TextColor;

                // Scale-in animation for newly entered digit
                float charScale = 1f;
                if (!ControlStateAnimator.ReducedMotion && pinAnim.CellFilledTimestamps[i] > 0)
                {
                    float fillElapsed = (float)Stopwatch.GetElapsedTime(pinAnim.CellFilledTimestamps[i]).TotalSeconds;
                    if (fillElapsed < 0.35f)
                    {
                        // Spring from 0→1 with overshoot
                        float springT = 1f - MathF.Exp(-10f * fillElapsed) * MathF.Cos(12f * fillElapsed);
                        charScale = Math.Clamp(springT, 0f, 1.2f);
                        ControlStateAnimator.SignalActiveTransition();
                    }
                }

                var charSize = ctx.MeasureText(displayChar, fontSize * charScale);
                float charX = paintCellBounds.X + (paintCellBounds.Width - charSize.Width) / 2f;
                float charY = paintCellBounds.Y + (paintCellBounds.Height - charSize.Height) / 2f;
                ctx.DrawText(displayChar, MathF.Round(charX), MathF.Round(charY),
                    fontSize * charScale, textColor);
            }
            else if (isActiveCell)
            {
                // Smooth caret blink in active cell
                const double blinkMs = 530.0;
                double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;
                float caretOpacity;
                if (elapsed < blinkMs)
                {
                    caretOpacity = 1f;
                }
                else
                {
                    double phase = (elapsed % blinkMs) / blinkMs * Math.PI * 2.0;
                    caretOpacity = (float)(0.5 + 0.5 * Math.Cos(phase));
                }

                if (caretOpacity > 0.01f)
                {
                    float caretX = paintCellBounds.X + paintCellBounds.Width / 2f;
                    float caretTop = paintCellBounds.Y + 10f;
                    float caretBottom = paintCellBounds.Y + paintCellBounds.Height - 10f;
                    ctx.DrawLine(
                        new Point(caretX, caretTop),
                        new Point(caretX, caretBottom),
                        new Stroke(theme.Caret.Color.Opacity(caretOpacity), theme.Caret.Width));
                }
            }

            x += cellWidth + gap;
        }
    }

    // ── StepIndicator ─────────────────────────────────────────────────

    private void PaintStepIndicator(StepIndicator si, Rect bounds)
    {
        si.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        int stepCount = si.Steps.Count;
        if (stepCount == 0)
        {
            return;
        }

        int currentStep = si.CurrentStep.Value;
        float circleSize = 28f;
        float circleRadius = circleSize / 2f;
        float lineHeight = 2f;
        float labelGap = 6f;
        float fontSize = theme.Typography.Body.Size * 0.75f;

        var completedColor = theme.Colors.Primary;
        var currentColor = theme.Colors.Primary;
        var pendingColor = theme.Colors.Border;
        var textColor = theme.Colors.Text;
        var completedTextColor = theme.Colors.TextOnPrimary;

        // Calculate spacing: circles are evenly distributed across the width
        float totalCircleWidth = stepCount * circleSize;
        float availableGap = bounds.Width - totalCircleWidth;
        float gap = stepCount > 1 ? availableGap / (stepCount - 1) : 0;

        for (int i = 0; i < stepCount; i++)
        {
            float cx = i * (circleSize + gap) + circleRadius;
            float cy = circleRadius;
            bool isCompleted = i < currentStep;
            bool isCurrent = i == currentStep;

            // Draw connecting line to next step
            if (i < stepCount - 1)
            {
                float lineStartX = cx + circleRadius + 2f;
                float lineEndX = (i + 1) * (circleSize + gap);
                float lineY = cy;
                var lineColor = isCompleted ? completedColor : pendingColor;
                ctx.DrawLine(
                    new Point(lineStartX, lineY),
                    new Point(lineEndX, lineY),
                    new Stroke(lineColor, lineHeight));
            }

            // Draw circle
            var circleRect = new Rect(cx - circleRadius, cy - circleRadius, circleSize, circleSize);
            if (isCompleted || isCurrent)
            {
                ctx.DrawRect(circleRect, isCompleted ? completedColor : currentColor,
                    radius: circleRadius);

                // Active step: no glow — solid circle only for clean, professional look
            }
            else
            {
                ctx.DrawRect(circleRect, pendingColor.Opacity(0.3f), radius: circleRadius);
                ctx.DrawRect(circleRect, stroke: new Stroke(pendingColor, 1.5f), radius: circleRadius);
            }

            // Draw step content: checkmark for completed, number for others
            if (isCompleted)
            {
                // Draw checkmark (✓) inside completed circle
                float checkSize = circleSize * 0.35f;
                float checkStroke = 2f;
                var checkColor = completedTextColor;
                // Checkmark path: short downstroke then longer upstroke
                ctx.DrawLine(
                    new Point(cx - checkSize * 0.5f, cy),
                    new Point(cx - checkSize * 0.1f, cy + checkSize * 0.4f),
                    new Stroke(checkColor, checkStroke, StrokeCap.Round, StrokeJoin.Round));
                ctx.DrawLine(
                    new Point(cx - checkSize * 0.1f, cy + checkSize * 0.4f),
                    new Point(cx + checkSize * 0.5f, cy - checkSize * 0.35f),
                    new Stroke(checkColor, checkStroke, StrokeCap.Round, StrokeJoin.Round));
            }
            else
            {
                string numberText = (i + 1).ToString();
                var numColor = isCurrent ? completedTextColor : textColor;
                float numFontSize = circleSize * 0.45f;
                var numSize = ctx.MeasureText(numberText, numFontSize);
                float numX = MathF.Round(cx - numSize.Width / 2f);
                float numY = MathF.Round(cy - numSize.Height / 2f + numFontSize * 0.06f);
                ctx.DrawText(numberText, numX, numY, numFontSize, numColor);
            }

            // Draw label below
            string label = si.Steps[i].Label;
            var labelSize = ctx.MeasureText(label, fontSize);
            float labelX = MathF.Round(cx - labelSize.Width / 2f);
            float labelY = circleSize + labelGap;
            ctx.DrawText(label, labelX, labelY, fontSize,
                (isCompleted || isCurrent) ? textColor : textColor.Opacity(0.6f));
        }
    }

    // ── ToggleGroup ───────────────────────────────────────────────────

    private void PaintToggleGroup(IToggleGroup tg, Rect bounds)
    {
        tg.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        int count = tg.OptionCount;
        if (count == 0)
        {
            return;
        }

        var node = (Node)tg;
        bool disabled = tg.IsControlDisabled;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        float outerRadius = bounds.Height / 2f;
        var bgColor = theme.Colors.SurfaceAlt;
        var borderColor = theme.Colors.Border;
        var selectedBg = theme.Colors.Primary;
        var selectedText = theme.Colors.TextOnPrimary;
        var normalText = theme.Colors.Text;
        float fontSize = theme.Typography.Body.Size * 0.85f;

        // Animate hover/press
        var hoverModel = AnimationModel.Spring.Snappy;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        var anim = ControlStateAnimator.Reconcile(
            node, hoverModel, pressModel, isDisabled: disabled);
        float hoverT = anim.Hover.Current;

        // Outer container
        ctx.DrawRect(bounds, bgColor, radius: outerRadius);
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor, 1f), radius: outerRadius);

        int selectedIdx = tg.SelectedIndex;

        // Compute variable-width buttons matching the layout measurement so every
        // item has the same horizontal padding regardless of label length. Must match
        // MeasureToggleGroup's paddingH or the buttons rescale and lose the padding.
        float paddingH = 24f;
        float[] buttonLefts = new float[count];
        float[] buttonWidths = new float[count];
        float measuredTotal = 0f;
        for (int i = 0; i < count; i++)
        {
            string label = tg.GetOptionLabel(i);
            float textW = label.Length * fontSize * 0.55f;
            float w = textW + paddingH * 2f;
            buttonWidths[i] = w;
            measuredTotal += w;
        }

        float scale = measuredTotal > 0f ? bounds.Width / measuredTotal : 1f;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            for (int i = 0; i < count; i++)
            {
                buttonWidths[i] *= scale;
            }
        }

        float xAcc = 0f;
        for (int i = 0; i < count; i++)
        {
            buttonLefts[i] = xAcc;
            xAcc += buttonWidths[i];
        }

        static float EdgeAt(float idx, float[] lefts, float[] widths)
        {
            int n = lefts.Length;
            if (n == 0)
            {
                return 0f;
            }
            if (idx <= 0f)
            {
                return lefts[0] + idx * widths[0];
            }
            if (idx >= n - 1)
            {
                return lefts[n - 1] + (idx - (n - 1)) * widths[n - 1];
            }
            int i = (int)MathF.Floor(idx);
            float t = idx - i;
            return lefts[i] + (lefts[i + 1] - lefts[i]) * t;
        }

        // Animated selection indicator slide with spring overshoot
        if (selectedIdx >= 0)
        {
            ControlStateAnimator.ReconcileValue(node, selectedIdx, AnimationModel.Spring.Bouncy);
            float animatedIdx = anim.Value.Current;

            float selLeft = EdgeAt(animatedIdx, buttonLefts, buttonWidths) + 2f;
            float selRight = EdgeAt(animatedIdx + 1f, buttonLefts, buttonWidths) - 2f;
            float selX = selLeft;
            float selW = MathF.Max(0f, selRight - selLeft);
            float selY = 2f;
            float selH = bounds.Height - 4f;
            float selRadius = selH / 2f;

            // Selected button: press depth — subtle scale 0.98x inward
            if (!disabled && !reducedMotion)
            {
                float pressDepth = 0.98f;
                float depthW = selW * pressDepth;
                float depthH = selH * pressDepth;
                float depthX = selX + (selW - depthW) / 2f;
                float depthY = selY + (selH - depthH) / 2f;
                ctx.DrawRect(new Rect(depthX, depthY, depthW, depthH), selectedBg, radius: selRadius);
            }
            else
            {
                ctx.DrawRect(new Rect(selX, selY, selW, selH), selectedBg, radius: selRadius);
            }
        }

        // Paint button labels
        for (int i = 0; i < count; i++)
        {
            string label = tg.GetOptionLabel(i);
            bool isSelected = i == selectedIdx;
            var textColor = disabled
                ? (isSelected ? selectedText.Opacity(0.5f) : normalText.Opacity(0.4f))
                : (isSelected ? selectedText : normalText);

            // Deselected buttons: subtle shadow increase (lift effect) on hover
            if (!isSelected && !disabled && !reducedMotion && node.IsHovered)
            {
                var mousePos = InputDispatcher.CurrentMousePosition;
                float relX = mousePos.X - absoluteX;
                int hoverButton = 0;
                for (int b = 0; b < count; b++)
                {
                    if (relX < buttonLefts[b] + buttonWidths[b])
                    {
                        hoverButton = b;
                        break;
                    }
                    hoverButton = b;
                }
                if (hoverButton == i && hoverT > 0.01f)
                {
                    float hX = buttonLefts[i] + 2f;
                    float hW = buttonWidths[i] - 4f;
                    float hY = 2f;
                    float hH = bounds.Height - 4f;
                    ctx.DrawRect(new Rect(hX, hY, hW, hH),
                        theme.Colors.Primary.Opacity(0.05f * hoverT),
                        radius: hH / 2f);
                }
            }

            var measuredText = ctx.MeasureText(label, fontSize);
            float textX = buttonLefts[i] + (buttonWidths[i] - measuredText.Width) / 2f;
            float textY = (bounds.Height - fontSize * 1.3f) / 2f;
            var textBounds = new Rect(textX, textY, measuredText.Width, fontSize * 1.3f);
            PaintText(label, textBounds, 0, textColor, fontSize: fontSize);

            // Dividers between non-selected buttons — fade near animated indicator
            if (i > 0 && i != selectedIdx && i - 1 != selectedIdx)
            {
                float divOpacity = 0.5f;
                if (selectedIdx >= 0 && !reducedMotion)
                {
                    float animIdx = anim.Value.Current;
                    float distFromIndicator = MathF.Min(MathF.Abs(i - animIdx), MathF.Abs(i - 1 - animIdx));
                    if (distFromIndicator < 1.2f)
                    {
                        divOpacity *= Math.Clamp(distFromIndicator - 0.2f, 0f, 1f);
                    }
                }

                float divX = buttonLefts[i];
                float divTop = bounds.Height * 0.25f;
                float divBottom = bounds.Height * 0.75f;
                ctx.DrawLine(new Point(divX, divTop), new Point(divX, divBottom),
                    new Stroke(borderColor.Opacity(divOpacity), 1f));
            }
        }
    }

    private void PaintModifierBorder(LayoutNodeData data, Rect bounds)
    {
        // Gradient border: fill the rounded rect with the gradient, then punch the
        // interior back to the node's own background so only a ring of the given
        // width shows. (The background was already painted above; redrawing the
        // inset keeps the ring crisp.) Runs before content, which draws on top.
        if (data.BorderGradient is { } gradient && data.BorderWidth > 0)
        {
            float r = data.BorderRadiusValue;
            ctx.DrawRect(bounds, gradient, radius: r);

            float w = data.BorderWidth;
            if (data.BackgroundColor is { } fill && bounds.Width > 2 * w && bounds.Height > 2 * w)
            {
                var inner = new Rect(bounds.X + w, bounds.Y + w,
                    bounds.Width - 2 * w, bounds.Height - 2 * w);
                ctx.DrawRect(inner, fill, radius: MathF.Max(0f, r - w));
            }
            return;
        }

        // Uniform border
        if (data.BorderColor.HasValue && data.BorderWidth > 0)
        {
            ctx.DrawRect(bounds,
                stroke: new Stroke(data.BorderColor.Value, data.BorderWidth),
                radius: data.BorderRadiusValue);
        }

        // Per-side borders (top, bottom, left, right)
        if (data.BorderTopColor.HasValue && (data.BorderTopWidth ?? 0) > 0)
        {
            ctx.DrawLine(
                new Point(bounds.Left, bounds.Top),
                new Point(bounds.Right, bounds.Top),
                new Stroke(data.BorderTopColor.Value, data.BorderTopWidth!.Value));
        }

        if (data.BorderBottomColor.HasValue && (data.BorderBottomWidth ?? 0) > 0)
        {
            ctx.DrawLine(
                new Point(bounds.Left, bounds.Bottom),
                new Point(bounds.Right, bounds.Bottom),
                new Stroke(data.BorderBottomColor.Value, data.BorderBottomWidth!.Value));
        }

        if (data.BorderLeftColor.HasValue && (data.BorderLeftWidth ?? 0) > 0)
        {
            ctx.DrawLine(
                new Point(bounds.Left, bounds.Top),
                new Point(bounds.Left, bounds.Bottom),
                new Stroke(data.BorderLeftColor.Value, data.BorderLeftWidth!.Value));
        }

        if (data.BorderRightColor.HasValue && (data.BorderRightWidth ?? 0) > 0)
        {
            ctx.DrawLine(
                new Point(bounds.Right, bounds.Top),
                new Point(bounds.Right, bounds.Bottom),
                new Stroke(data.BorderRightColor.Value, data.BorderRightWidth!.Value));
        }
    }

    private void PaintBrush(Brush brush, Rect bounds, float radius, float opacity = 1f)
    {
        switch (brush.Kind)
        {
            case BrushKind.Solid:
                var color = opacity < 1f ? brush.Color.Opacity(opacity) : brush.Color;
                ctx.DrawRect(bounds, color, radius: radius);
                break;

            case BrushKind.LinearGradient:
            case BrushKind.RadialGradient:
            case BrushKind.SweepGradient:
                if (brush.Gradient is not null)
                {
                    if (opacity < 1f)
                    {
                        using var _ = ctx.PushOpacity(opacity);
                        ctx.DrawRect(bounds, brush.Gradient, radius: radius);
                    }
                    else
                    {
                        ctx.DrawRect(bounds, brush.Gradient, radius: radius);
                    }
                }
                break;

            case BrushKind.Image:
                if (brush.ImageSource is not null)
                {
                    ctx.DrawImage(brush.ImageSource, bounds, opacity);
                }
                break;
        }
    }

    private void PaintBrushStroke(Brush brush, Rect bounds, float width, float radius)
    {
        // For stroke, we only support solid color currently
        var color = brush.Kind == BrushKind.Solid ? brush.Color : theme.Colors.Border;
        ctx.DrawRect(bounds, stroke: new Stroke(color, width), radius: radius);
    }

    private void PaintShadow(ShadowSpec shadow, Rect bounds, float radius)
    {
        foreach (var drop in shadow.Drop)
        {
            if (drop.Blur <= 0 && drop.Spread <= 0)
            {
                continue;
            }

            var shadowBounds = new Rect(
                bounds.X + drop.OffsetX - drop.Spread,
                bounds.Y + drop.OffsetY - drop.Spread,
                bounds.Width + drop.Spread * 2,
                bounds.Height + drop.Spread * 2);

            // CSS blur radius is the full diameter; Gaussian sigma = radius / 2.
            float sigma = drop.Blur / 2f;
            if (sigma > 0)
            {
                ctx.DrawBlurredRoundedRect(shadowBounds, drop.Color, radius: radius, blurSigma: sigma);
            }
            else
            {
                ctx.DrawRect(shadowBounds, drop.Color, radius: radius);
            }
        }
    }

    /// <summary>
    /// Paints a shadow that is a sequence of up to three lerps from a base
    /// ShadowSpec. Avoids allocating intermediate ShadowSpec/Builder instances
    /// by computing lerped DropShadow values inline (DropShadow is a struct).
    ///
    /// Each (target, t) pair, when target is non-null and t > 0, blends the
    /// current value toward target by t. When all targets are null/zero, the
    /// base shadow is painted unchanged.
    /// </summary>
    private void PaintLerpedShadow(
        ShadowSpec baseShadow,
        ShadowSpec? targetA, float tA,
        ShadowSpec? targetB, float tB,
        ShadowSpec? targetC, float tC,
        Rect bounds, float radius)
    {
        var baseDrops = baseShadow.Drop;
        // Determine effective drop count across all stages.
        int dropCount = baseDrops.Length;
        if (targetA is not null && tA > 0f)
        {
            dropCount = Math.Max(dropCount, targetA.Drop.Length);
        }
        if (targetB is not null && tB > 0f)
        {
            dropCount = Math.Max(dropCount, targetB.Drop.Length);
        }
        if (targetC is not null && tC > 0f)
        {
            dropCount = Math.Max(dropCount, targetC.Drop.Length);
        }

        for (int i = 0; i < dropCount; i++)
        {
            var current = i < baseDrops.Length ? baseDrops[i] : DropShadow.None;
            current = LerpDropAt(current, targetA, tA, i);
            current = LerpDropAt(current, targetB, tB, i);
            current = LerpDropAt(current, targetC, tC, i);

            if (current.Blur <= 0 && current.Spread <= 0)
            {
                continue;
            }

            var shadowBounds = new Rect(
                bounds.X + current.OffsetX - current.Spread,
                bounds.Y + current.OffsetY - current.Spread,
                bounds.Width + current.Spread * 2,
                bounds.Height + current.Spread * 2);

            float sigma = current.Blur / 2f;
            if (sigma > 0)
            {
                ctx.DrawBlurredRoundedRect(shadowBounds, current.Color, radius: radius, blurSigma: sigma);
            }
            else
            {
                ctx.DrawRect(shadowBounds, current.Color, radius: radius);
            }
        }
    }

    private static DropShadow LerpDropAt(DropShadow current, ShadowSpec? target, float t, int index)
    {
        if (target is null || t <= 0f)
        {
            return current;
        }
        if (t >= 1f)
        {
            return index < target.Drop.Length ? target.Drop[index] : DropShadow.None;
        }
        var b = index < target.Drop.Length ? target.Drop[index] : DropShadow.None;
        return new DropShadow
        {
            Blur = current.Blur + (b.Blur - current.Blur) * t,
            Spread = current.Spread + (b.Spread - current.Spread) * t,
            OffsetX = current.OffsetX + (b.OffsetX - current.OffsetX) * t,
            OffsetY = current.OffsetY + (b.OffsetY - current.OffsetY) * t,
            Color = ColorValue.Lerp(current.Color, b.Color, t),
        };
    }

    /// <summary>Linear interpolation between two float values.</summary>
    private static float LerpF(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// Measures the width of text at the pill font size (12px) for layout calculations.
    /// </summary>
    private float MeasureTextWidth(string text, float fontSize = 12f)
    {
        return ctx.MeasureText(text, fontSize).Width;
    }

    /// <summary>
    /// Paints text within the given bounds using the text rendering pipeline.
    /// Uses HarfBuzz shaping via TextLayoutEngine for accurate glyph positioning,
    /// with a colored-bar fallback when no font is available (headless mode).
    /// </summary>
    /// <summary>
    /// Shrinks a picker field's bounds to leave room for the trailing affordance
    /// icon (calendar / clock) plus a small gap, so value or placeholder text never
    /// paints underneath the icon. The 14px icon size matches each picker's draw.
    /// </summary>
    private static Rect FieldTextBounds(Rect bounds)
    {
        const float iconSize = 14f;
        const float gap = 6f;
        return bounds with { Width = MathF.Max(0f, bounds.Width - iconSize - gap) };
    }

    /// <summary>
    /// Decides whether text drawn by <see cref="PaintText"/> should be centred as a
    /// single line (on its glyph visual box) or laid out as flowing multi-line text.
    /// </summary>
    /// <remarks>
    /// <paramref name="maxLines"/> == 0 is the "unlimited lines" sentinel a plain
    /// <see cref="Label"/> passes when no <c>MaxLines</c> is set — it is NOT
    /// single-line. Only <c>maxLines == 1</c> guarantees one line. Otherwise the
    /// text spans multiple lines if it contains an explicit newline or is wider than
    /// the available width (so it wraps). Treating an unlimited multi-line label as
    /// single-line placed its first line at the vertical centre of the full text
    /// box, leaving a large gap above it.
    /// </remarks>
    internal static bool ShouldCenterAsSingleLine(
        int maxLines, string text, float singleLineWidth, float availableWidth)
    {
        if (maxLines == 1)
        {
            return true;
        }

        return !text.Contains('\n', StringComparison.Ordinal) && singleLineWidth <= availableWidth;
    }

    private void PaintText(string? text, Rect bounds, float horizontalPadding,
        ColorValue color, float fontSize = 0f,
        TextAlignment alignment = TextAlignment.Start,
        TextOverflow overflow = TextOverflow.Clip,
        int maxLines = 1,
        FontWeight fontWeight = FontWeight.Regular)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        float effectiveFontSize = fontSize > 0 ? fontSize : theme.Typography.Scale.Body.Size;
        float availableWidth = bounds.Width - horizontalPadding * 2;
        if (availableWidth <= 0)
        {
            return;
        }

        // Resolve weight-specific font path
        string? fontPath = null;
        if (fontWeight is not (FontWeight.Regular or FontWeight.None) && ctx.DefaultFontPath != null)
        {
            fontPath = ctx.ResolveFontPath(ctx.DefaultFontPath, fontWeight);
        }

        float x = bounds.X + horizontalPadding;
        float y;

        // Decide single- vs multi-line. maxLines == 0 is the "unlimited" sentinel,
        // NOT single-line — a plain Label (no .MaxLines) arrives here with
        // maxLines == 0 and may legitimately wrap or contain '\n'. Only maxLines
        // == 1 is guaranteed single-line. The single-line width measure is cheap,
        // so use it to short-circuit the common (fits-on-one-line) case and only
        // pay for a full wrapping layout when the text can actually span lines.
        Size singleLineSize = ctx.MeasureText(text, effectiveFontSize, fontPath);
        bool singleLine = ShouldCenterAsSingleLine(
            maxLines, text, singleLineSize.Width, availableWidth);

        if (singleLine)
        {
            // Single-line text: centre the glyph *visual* box in the content box,
            // not the font's line box. The line box reserves ascent/descent room
            // that most glyphs (digits, caps) don't fill, so line-box centring
            // leaves visible text sitting high or low with an asymmetric gap —
            // visibly off in badges/pills/chips/buttons, where the control's
            // symmetric padding then centres this (already off-centre) line box.
            // Because a single line's ink height never exceeds the line height,
            // visual centring never pushes the glyph past the box edges.
            var visual = ctx.MeasureGlyphVisualBounds(text, effectiveFontSize, fontPath);
            y = visual.HasValue
                ? bounds.Y + bounds.Height / 2f - visual.Value.VisualCenterY
                : bounds.Y + (bounds.Height - singleLineSize.Height) / 2f;
        }
        else
        {
            // Multi-line: measure the wrapped text against the real width so the
            // block is positioned by its true height. Centring by slack keeps a
            // text-fitted label top-aligned (slack ≈ 0) while still centring when
            // the box is deliberately taller than the text. Using the single-line
            // height here (the old maxLines <= 1 path) dropped the first line to
            // the vertical middle of the full multi-line box.
            Size textSize = singleLineSize;
            string? measureFont = fontPath ?? ctx.DefaultFontPath;
            if (!string.IsNullOrEmpty(measureFont))
            {
                var measureOptions = new TextLayoutOptions
                {
                    FontPath = measureFont,
                    FontSize = effectiveFontSize,
                    MaxWidth = availableWidth,
                    MaxLines = maxLines,
                    Overflow = overflow,
                };
                textSize = TextLayoutEngine.Layout(text, measureOptions).BoundingBox;
            }

            y = bounds.Y + (bounds.Height - textSize.Height) / 2f;
        }

        // Snap to pixel grid for crisp rendering, especially at small sizes
        x = MathF.Round(x);
        y = MathF.Round(y);

        ctx.DrawText(text, x, y, effectiveFontSize, color,
            fontPath: fontPath,
            alignment: alignment,
            overflow: overflow,
            maxWidth: availableWidth,
            maxLines: maxLines);
    }

    private static Rect ScaleBounds(Rect bounds, float scale)
    {
        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        float sw = bounds.Width * scale;
        float sh = bounds.Height * scale;
        return new Rect(cx - sw / 2f, cy - sh / 2f, sw, sh);
    }

    private void PaintChildren(IReadOnlyList<Node> children)
    {
        for (int i = 0; i < children.Count; i++)
        {
            PaintRecursive(children[i]);
        }
    }

    // ── StatusBar ──────────────────────────────────────────────────────

    private void PaintStatusBar(StatusBar sb, Rect bounds)
    {
        float fontSize = theme.Typography.Body.Size * 0.85f;
        var bg = theme.Colors.Surface.Opacity(0.8f);
        var textColor = theme.Colors.TextMuted;
        var borderColor = theme.Colors.Border;
        float paddingH = 12f;

        // Background
        ctx.DrawRect(bounds, bg);

        // Top border
        ctx.DrawLine(
            new Point(bounds.X, bounds.Y),
            new Point(bounds.X + bounds.Width, bounds.Y),
            new Stroke(borderColor, 1f));

        // Use first character to measure vertical centering
        float textY = bounds.Y + bounds.Height / 2f;
        var probeBounds = ctx.MeasureGlyphVisualBounds("M", fontSize);
        if (probeBounds.HasValue)
        {
            textY = MathF.Round(bounds.Y + bounds.Height / 2f - probeBounds.Value.VisualCenterY);
        }
        else
        {
            textY = MathF.Round(bounds.Y + bounds.Height / 2f - fontSize * 0.35f);
        }

        // Left zone — left-aligned
        if (sb.Left is Label leftLabel)
        {
            string leftText = leftLabel.Text ?? "";
            if (leftText.Length > 0)
            {
                ctx.DrawText(leftText, bounds.X + paddingH, textY, fontSize, textColor);
            }
        }

        // Center zone — centered
        if (sb.Center is Label centerLabel)
        {
            string centerText = centerLabel.Text ?? "";
            if (centerText.Length > 0)
            {
                var textSize = ctx.MeasureText(centerText, fontSize);
                float textX = MathF.Round(bounds.X + bounds.Width / 2f - textSize.Width / 2f);
                ctx.DrawText(centerText, textX, textY, fontSize, textColor);
            }
        }

        // Right zone — right-aligned
        if (sb.Right is Label rightLabel)
        {
            string rightText = rightLabel.Text ?? "";
            if (rightText.Length > 0)
            {
                var textSize = ctx.MeasureText(rightText, fontSize);
                float textX = MathF.Round(bounds.X + bounds.Width - paddingH - textSize.Width);
                ctx.DrawText(rightText, textX, textY, fontSize, textColor);
            }
        }
    }

    // ── ToolBar ────────────────────────────────────────────────────────

    private void PaintToolBar(ToolBar tb, Rect bounds)
    {
        const float buttonSize = 32f;
        const float gap = 4f;
        const float separatorWidth = 12f;

        tb.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        var bg = theme.Colors.SurfaceAlt;
        var hoverBg = theme.Colors.Text.Opacity(0.08f);
        var pressBg = theme.Colors.Text.Opacity(0.14f);
        var borderColor = theme.Colors.Border;
        float radius = 6f;

        // Background with subtle rounded container
        ctx.DrawRect(bounds, bg, radius: radius);
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor.Opacity(0.3f), 1f), radius: radius);

        // Center items vertically when bounds are taller than intrinsic height
        float x = bounds.X;
        float y = bounds.Y + (bounds.Height - buttonSize) / 2f;

        for (int i = 0; i < tb.Items.Count; i++)
        {
            var item = tb.Items[i];

            if (i > 0)
            {
                x += gap;
            }

            if (item.IsSeparator)
            {
                // Subtle separator with rounded ends
                float sepX = MathF.Round(x + separatorWidth / 2f);
                float sepTop = y + buttonSize * 0.25f;
                float sepBot = y + buttonSize * 0.75f;
                ctx.DrawLine(
                    new Point(sepX, sepTop),
                    new Point(sepX, sepBot),
                    new Stroke(borderColor.Opacity(0.5f), 1f, StrokeCap.Round, StrokeJoin.Round));
                x += separatorWidth;
                continue;
            }

            var btnRect = new Rect(x, y, buttonSize, buttonSize);
            var iconColor = item.Enabled ? theme.Colors.Text : theme.Colors.TextMuted;

            // Per-button hover/press highlighting
            if (tb.PressedItemIndex == i && tb.IsPressed)
            {
                ctx.DrawRect(btnRect, pressBg, radius: radius - 1f);
                // Inner shadow for pressed/depressed feel
                ctx.DrawLine(
                    new Point(btnRect.X + 2f, btnRect.Y + 1f),
                    new Point(btnRect.X + btnRect.Width - 2f, btnRect.Y + 1f),
                    new Stroke(new ColorValue("#000000").Opacity(0.10f), 1f));
            }
            else if (tb.HoveredItemIndex == i && tb.IsHovered)
            {
                ctx.DrawRect(btnRect, hoverBg, radius: radius - 1f);
            }

            // Toggle state — filled background for active toggles
            if (item.ToggleValue.Value)
            {
                ctx.DrawRect(btnRect, theme.Colors.Primary.Opacity(0.15f), radius: radius - 1f);
                iconColor = theme.Colors.Primary;
            }

            // Draw icon as a cached, anti-aliased bitmap (rasterized once per size/color).
            var icon = item.Icon;
            if (icon.Paths.Length > 0)
            {
                float drawSize = buttonSize * 0.58f;
                float viewW = icon.ViewBox.Width > 0 ? icon.ViewBox.Width : 24f;
                // Lucide draws a 2-unit stroke in a 24-unit view box.
                float strokeLogical = 2f * drawSize / viewW;
                PaintIconBitmap(icon,
                    btnRect.X + buttonSize / 2f, btnRect.Y + buttonSize / 2f,
                    drawSize, 1f, iconColor, strokeLogical);
            }

            x += buttonSize;
        }
    }

    // ── MenuBar ───────────────────────────────────────────────────────

    private void PaintMenuBar(MenuBar mb, Rect bounds)
    {
        const float barHeight = 30f;
        const float labelPadH = 12f;
        const float fontSize = 13f;

        mb.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Track dropdown open state for entrance animation
        // Use absolute bounds position for stable identity across re-renders.
        int mbKey = HashCode.Combine(
            (int)Math.Round(absoluteY * 10f),
            (int)Math.Round(bounds.Width * 10f));
        if (!mb.IsOpen)
        {
            menuBarOpenTick.Remove(mbKey);
        }
        else if (!menuBarOpenTick.ContainsKey(mbKey))
        {
            menuBarOpenTick[mbKey] = Environment.TickCount64;
        }

        var bgColor = theme.Colors.SurfaceAlt;
        var textColor = theme.Colors.Text;
        var mutedColor = theme.Colors.TextMuted;
        var hoverBg = theme.Colors.Text.Opacity(0.08f);
        // An open top-level menu reads like macOS: a solid accent-filled pill with
        // white-on-accent text, not a translucent wash with tinted text (which looks
        // like a Windows classic menu). Corners are rounded to match the dropdown.
        var activeBg = theme.Colors.Primary;
        var borderColor = theme.Colors.Border;

        // Bar background
        ctx.DrawRect(bounds, bgColor);
        // Bottom border line
        ctx.DrawLine(
            new Point(bounds.X, bounds.Y + bounds.Height),
            new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            new Stroke(borderColor.Opacity(0.3f), 1f));

        // Ensure label bounds array is the right size
        if (mb.MenuLabelBounds.Length != mb.Menus.Count)
        {
            mb.MenuLabelBounds = new Rect[mb.Menus.Count];
        }

        // Center menu items vertically when bounds are taller than intrinsic height
        float barY = bounds.Y + (bounds.Height - barHeight) / 2f;
        float x = bounds.X;
        float absX = absoluteX;
        float absY = absoluteY;

        for (int i = 0; i < mb.Menus.Count; i++)
        {
            var menu = mb.Menus[i];
            // Measure the real text width so the highlight box has equal padding on
            // both sides — a char-count × average-width estimate is wrong for labels
            // with wide glyphs (e.g. "View") and leaves no padding on the right.
            float labelW = ctx.MeasureText(menu.Label, fontSize).Width + labelPadH * 2f;
            var labelRect = new Rect(x, barY, labelW, barHeight);

            // Store absolute bounds for hit testing
            mb.MenuLabelBounds[i] = new Rect(absX + x - bounds.X, absY, labelW, barHeight);

            // Highlight: open menu (solid accent pill) or hovered (subtle wash).
            // Inset vertically so the fill reads as a pill inside the bar, not a
            // full-height block.
            var highlightRect = new Rect(
                labelRect.X,
                labelRect.Y + 3f,
                labelRect.Width,
                labelRect.Height - 6f);
            if (mb.OpenMenuIndex == i)
            {
                ctx.DrawRect(highlightRect, activeBg, radius: 5f);
            }
            else if (mb.HoveredMenuIndex == i)
            {
                ctx.DrawRect(highlightRect, hoverBg, radius: 5f);
            }

            // Draw label text — white-on-accent for the open menu, normal otherwise
            var labelColor = mb.OpenMenuIndex == i ? theme.Colors.TextOnPrimary : textColor;
            PaintText(menu.Label, labelRect, labelPadH, labelColor, fontSize: fontSize);

            x += labelW;
        }

        // Dropdown overlay (deferred)
        if (mb.IsOpen && mb.OpenMenuIndex < mb.Menus.Count)
        {
            float capturedAbsX = absoluteX;
            float capturedAbsY = absoluteY;

            deferredOverlays ??= [];
            deferredOverlays.Add(() =>
            {
                PaintMenuBarDropdown(mb, capturedAbsX, capturedAbsY);
            });
        }
        else
        {
            mb.DropdownBounds = default;
        }
    }

    private void PaintMenuBarDropdown(MenuBar mb, float barAbsX, float barAbsY)
    {
        var menu = mb.Menus[mb.OpenMenuIndex];
        var items = menu.Items;
        if (items.Count == 0)
        {
            return;
        }

        const float itemHeight = 28f;
        const float separatorHeight = 9f;
        const float headerHeight = 24f;
        const float padH = 12f;
        const float fontSize = 13f;
        const float iconW = 20f;
        const float shortcutGap = 24f;
        const float submenuArrowW = 16f;
        const float gap = 4f;
        float radius = 6f;

        mb.MenuItemHeight = itemHeight;

        // Calculate dropdown size
        float totalHeight = 8f; // top padding
        float maxLabelW = 0f;
        float maxShortcutW = 0f;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Label == null && item.CustomContent == Node.Empty)
            {
                totalHeight += separatorHeight;
            }
            else if (!item.Enabled && item.OnClick == null && item.ToggleValue.OnChange is null && item.Items == null)
            {
                totalHeight += headerHeight;
            }
            else
            {
                totalHeight += itemHeight;
                // Measure the real glyph width so wide labels (e.g. "Sort by Modified")
                // aren't clipped by a char-count × average-width underestimate.
                float labelLen = string.IsNullOrEmpty(item.Label)
                    ? 0f
                    : ctx.MeasureText(item.Label, fontSize).Width;
                if (labelLen > maxLabelW)
                {
                    maxLabelW = labelLen;
                }
                if (item.Shortcut != null)
                {
                    float shortcutLen = ctx.MeasureText(item.Shortcut.Value.ToString(), fontSize - 1f).Width;
                    if (shortcutLen > maxShortcutW)
                    {
                        maxShortcutW = shortcutLen;
                    }
                }
            }
        }
        totalHeight += 8f; // bottom padding

        float dropdownWidth = padH + iconW + maxLabelW + shortcutGap + maxShortcutW + submenuArrowW + padH;
        dropdownWidth = Math.Max(dropdownWidth, 180f);

        // Position below the menu label
        var labelBounds = mb.MenuLabelBounds[mb.OpenMenuIndex];
        float dropX = labelBounds.X;
        float dropY = labelBounds.Y + labelBounds.Height + gap;

        var dropdownBounds = new Rect(dropX, dropY, dropdownWidth, totalHeight);
        mb.DropdownBounds = dropdownBounds;

        // Entrance animation: scale from top + opacity fade
        // Use the same content-position key as PaintMenuBar for consistent identity.
        bool mbReducedMotion = ControlStateAnimator.ReducedMotion;
        float openT = 1f;
        int mbDropKey = HashCode.Combine(
            (int)Math.Round(barAbsY * 10f),
            (int)Math.Round(mb.AbsoluteBounds.Width * 10f));
        if (!mbReducedMotion && menuBarOpenTick.TryGetValue(mbDropKey, out long openTick))
        {
            float elapsedMs = (float)(Environment.TickCount64 - openTick);
            openT = Math.Clamp(elapsedMs / 150f, 0f, 1f);
            openT = 1f - (1f - openT) * (1f - openT); // ease-out
        }

        ScopeGuard mbScaleScope = default;
        ScopeGuard mbOpacityScope = default;
        if (!mbReducedMotion && openT < 0.999f)
        {
            float scale = 0.92f + 0.08f * openT;
            mbScaleScope = ctx.PushScale(scale, scale, new Point(dropX + dropdownWidth / 2f, dropY));
            mbOpacityScope = ctx.PushOpacity(openT);
            ControlStateAnimator.SignalActiveTransition();
        }

        // Shadow
        var shadowBounds = new Rect(dropX + 2, dropY + 2, dropdownWidth, totalHeight);
        ctx.DrawRect(shadowBounds, new ColorValue("#000000").Opacity(0.15f), radius: radius);

        // Background
        ctx.DrawRect(dropdownBounds, theme.Colors.Surface, radius: radius);
        ctx.DrawRect(dropdownBounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: radius);

        // Items
        float y = dropY + 4f; // top padding
        var textColor = theme.Colors.Text;
        var mutedColor = theme.Colors.TextMuted;
        var hoverBg = theme.Colors.Text.Opacity(0.08f);
        var checkColor = theme.Colors.Primary;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            // Per-item stagger: each item fades in with slight delay
            ScopeGuard itemStaggerScope = default;
            if (!mbReducedMotion && openT < 0.999f)
            {
                float itemDelay = i * 20f;
                float elapsedForItem = menuBarOpenTick.TryGetValue(mbDropKey, out long ot)
                    ? (float)(Environment.TickCount64 - ot) - itemDelay : 1000f;
                float itemT = Math.Clamp(elapsedForItem / 100f, 0f, 1f);
                itemT = 1f - (1f - itemT) * (1f - itemT);
                if (itemT < 0.999f)
                {
                    itemStaggerScope = ctx.PushOpacity(itemT);
                }
            }

            // Separator
            if (item.Label == null && item.CustomContent == Node.Empty)
            {
                float sepY = MathF.Round(y + separatorHeight / 2f);
                ctx.DrawLine(
                    new Point(dropX + padH, sepY),
                    new Point(dropX + dropdownWidth - padH, sepY),
                    new Stroke(theme.Colors.Border.Opacity(0.3f), 1f));
                y += separatorHeight;
                itemStaggerScope.Dispose();
                continue;
            }

            // Header (non-interactive disabled label)
            if (!item.Enabled && item.OnClick == null && item.ToggleValue.OnChange is null && item.Items == null)
            {
                var headerRect = new Rect(dropX + padH, y, dropdownWidth - padH * 2, headerHeight);
                PaintText(item.Label ?? "", headerRect, 0f, mutedColor, fontSize: fontSize - 1f);
                y += headerHeight;
                itemStaggerScope.Dispose();
                continue;
            }

            var itemRect = new Rect(dropX + 4f, y, dropdownWidth - 8f, itemHeight);

            // Highlight
            if (mb.HighlightedItemIndex == i && item.Enabled)
            {
                ctx.DrawRect(itemRect, hoverBg, radius: 4f);
            }

            float ix = dropX + padH;

            // Checkmark for toggles
            if (item.ToggleValue.OnChange is not null)
            {
                if (item.ToggleValue.Value)
                {
                    var checkBounds = new Rect(ix, y, iconW, itemHeight);
                    PaintText("✓", checkBounds, 0f, checkColor, fontSize: fontSize);
                }
                ix += iconW;
            }
            else
            {
                // Icon space (even if no icon, for alignment)
                ix += iconW;
            }

            // Label
            var labelColor = item.Enabled ? textColor : mutedColor;
            var labelBoundsItem = new Rect(ix, y, maxLabelW + 4f, itemHeight);
            PaintText(item.Label ?? "", labelBoundsItem, 0f, labelColor, fontSize: fontSize);

            // Shortcut
            if (item.Shortcut != null)
            {
                float shortcutX = dropX + dropdownWidth - padH - maxShortcutW - submenuArrowW;
                var shortcutBounds = new Rect(shortcutX, y, maxShortcutW + submenuArrowW, itemHeight);
                PaintText(item.Shortcut.Value.ToString(), shortcutBounds, 0f, mutedColor, fontSize: fontSize - 1f);
            }

            // Submenu arrow
            if (item.Items != null && item.Items.Count > 0)
            {
                float arrowX = dropX + dropdownWidth - padH - 8f;
                float arrowY2 = y + itemHeight / 2f;
                float arrowSize = 4f;
                ctx.DrawLine(
                    new Point(arrowX, arrowY2 - arrowSize),
                    new Point(arrowX + arrowSize, arrowY2),
                    new Stroke(mutedColor, 1.5f));
                ctx.DrawLine(
                    new Point(arrowX + arrowSize, arrowY2),
                    new Point(arrowX, arrowY2 + arrowSize),
                    new Stroke(mutedColor, 1.5f));
            }

            y += itemHeight;
            itemStaggerScope.Dispose();
        }

        mbScaleScope.Dispose();
        mbOpacityScope.Dispose();
    }

    // ── PropertyGrid ──────────────────────────────────────────────────

    private static readonly Dictionary<int, float> propertyGridChevronProgress = new();

    private void PaintPropertyGrid(PropertyGrid pg, Rect bounds)
    {
        const float groupHeaderH = 32f;
        const float rowH = 28f;
        const float labelRatio = 0.4f;
        const float padH = 10f;
        const float fontSize = 12f;
        const float chevronSize = 5f;

        pg.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);
        pg.RowHeight = rowH;
        pg.GroupHeaderHeight = groupHeaderH;

        var bgColor = theme.Colors.Surface;
        var altBgColor = theme.Colors.SurfaceAlt;
        var textColor = theme.Colors.Text;
        var mutedColor = theme.Colors.TextMuted;
        var borderColor = theme.Colors.Border.Opacity(0.3f);
        var headerBg = theme.Colors.SurfaceAlt;
        var hoverBg = theme.Colors.Text.Opacity(0.05f);
        var primaryColor = theme.Colors.Primary;
        float radius = 6f;

        // Outer border
        ctx.DrawRect(bounds, bgColor, radius: radius);
        ctx.DrawRect(bounds, stroke: new Stroke(borderColor, 1f), radius: radius);

        float labelW = bounds.Width * labelRatio;
        float editorW = bounds.Width * (1f - labelRatio);
        float y = bounds.Y;
        int flatRow = 0;

        for (int gi = 0; gi < pg.Groups.Count; gi++)
        {
            var group = pg.Groups[gi];
            if (group.Visible != null && !group.Visible())
            {
                continue;
            }

            bool collapsed = pg.CollapsedGroups.Contains(gi);

            // Group header
            var headerRect = new Rect(bounds.X, y, bounds.Width, groupHeaderH);
            ctx.DrawRect(headerRect, headerBg);
            // Header bottom border
            ctx.DrawLine(
                new Point(bounds.X, y + groupHeaderH),
                new Point(bounds.X + bounds.Width, y + groupHeaderH),
                new Stroke(borderColor, 1f));

            // Chevron (rotates 0→90° for expanded with spring)
            float chevX = bounds.X + padH;
            float chevY = y + groupHeaderH / 2f;
            var chevronCenter = new Point(chevX + chevronSize / 2f, chevY);

            // Use content-space position for per-group animation tracking
            // (object GetHashCode changes each re-render).
            float pgContentY = absoluteY + activeScrollOffsetY + y;
            int groupKey = HashCode.Combine(
                (int)Math.Round(pgContentY * 10f),
                gi);
            var groupNode = pg as Node;
            float expandTarget = collapsed ? 0f : 1f;
            // Draw single > chevron, rotated 0→90°
            float chevRot;
            if (ControlStateAnimator.ReducedMotion)
            {
                chevRot = collapsed ? 0f : 90f;
            }
            else
            {
                // Track per-group collapse state using a simple lerp approach
                if (!propertyGridChevronProgress.TryGetValue(groupKey, out float currentRot))
                {
                    currentRot = collapsed ? 0f : 90f;
                }
                float targetRot = collapsed ? 0f : 90f;
                float newRot = currentRot + (targetRot - currentRot) * 0.15f;
                if (MathF.Abs(newRot - targetRot) < 0.5f)
                {
                    newRot = targetRot;
                }
                else
                {
                    ControlStateAnimator.SignalActiveTransition();
                }
                propertyGridChevronProgress[groupKey] = newRot;
                chevRot = newRot;
            }

            using (ctx.PushRotate(Angle.Degrees(chevRot), chevronCenter))
            {
                // Draw > shape (pointing right)
                ctx.DrawLine(new Point(chevX, chevY - chevronSize),
                    new Point(chevX + chevronSize, chevY),
                    new Stroke(mutedColor, 1.5f));
                ctx.DrawLine(new Point(chevX + chevronSize, chevY),
                    new Point(chevX, chevY + chevronSize),
                    new Stroke(mutedColor, 1.5f));
            }

            // Group name
            var headerTextRect = new Rect(bounds.X + padH + chevronSize + 8f, y, bounds.Width - padH * 2 - chevronSize - 8f, groupHeaderH);
            PaintText(group.Name, headerTextRect, 0f, textColor, fontSize: fontSize + 1f);

            y += groupHeaderH;

            if (collapsed)
            {
                continue;
            }

            // Property rows
            for (int pi = 0; pi < group.Properties.Count; pi++)
            {
                var prop = group.Properties[pi];
                var rowRect = new Rect(bounds.X, y, bounds.Width, rowH);

                // Alternate row background
                if (pi % 2 == 1)
                {
                    ctx.DrawRect(rowRect, altBgColor);
                }

                // Hover highlight
                if (pg.HoveredRow == flatRow)
                {
                    ctx.DrawRect(rowRect, hoverBg);
                }

                // Row bottom border
                ctx.DrawLine(
                    new Point(bounds.X, y + rowH),
                    new Point(bounds.X + bounds.Width, y + rowH),
                    new Stroke(borderColor, 0.5f));

                // Label column
                var labelRect = new Rect(bounds.X + padH, y, labelW - padH * 2, rowH);
                PaintText(prop.Name, labelRect, 0f, mutedColor, fontSize: fontSize);

                // Column divider
                float divX = bounds.X + labelW;
                ctx.DrawLine(
                    new Point(divX, y),
                    new Point(divX, y + rowH),
                    new Stroke(borderColor, 0.5f));

                // Editor column
                float editorX = divX + padH;
                float editorAvailW = editorW - padH * 2;
                var editorRect = new Rect(editorX, y, editorAvailW, rowH);

                PaintPropertyEditor(pg, prop, editorRect, fontSize, textColor, mutedColor, primaryColor);

                y += rowH;
                flatRow++;
            }
        }
    }

    private void PaintPropertyEditor(
        PropertyGrid pg, PropertyDefinition prop, Rect editorRect, float fontSize,
        ColorValue textColor, ColorValue mutedColor, ColorValue primaryColor)
    {
        string valueText;

        switch (prop.EditorKind)
        {
            case PropertyEditorKind.String:
            {
                var getter = (Func<string>)prop.Getter!;
                valueText = getter();
                if (InputDispatcher.PropertyGridEditingProperty == prop)
                {
                    PaintPropertyInlineEditor(editorRect, fontSize, textColor, primaryColor);
                }
                else
                {
                    PaintText(valueText, editorRect, 0f, textColor, fontSize: fontSize);
                }
                break;
            }

            case PropertyEditorKind.Float:
            {
                var getter = (Func<float>)prop.Getter!;
                float val = getter();
                string fmt = prop.FormatString ?? "F1";
                valueText = val.ToString(fmt);
                if (InputDispatcher.PropertyGridEditingProperty == prop)
                {
                    PaintPropertyInlineEditor(editorRect, fontSize, textColor, primaryColor);
                }
                else
                {
                    PaintText(valueText, editorRect, 0f, textColor, fontSize: fontSize);
                }
                break;
            }

            case PropertyEditorKind.Int:
            {
                var getter = (Func<int>)prop.Getter!;
                valueText = getter().ToString();
                if (InputDispatcher.PropertyGridEditingProperty == prop)
                {
                    PaintPropertyInlineEditor(editorRect, fontSize, textColor, primaryColor);
                }
                else
                {
                    PaintText(valueText, editorRect, 0f, textColor, fontSize: fontSize);
                }
                break;
            }

            case PropertyEditorKind.Bool:
            {
                var getter = (Func<bool>)prop.Getter!;
                bool val = getter();

                // Small toggle switch
                float toggleW = 32f;
                float toggleH = 16f;
                float toggleY = editorRect.Y + (editorRect.Height - toggleH) / 2f;
                var toggleRect = new Rect(editorRect.X, toggleY, toggleW, toggleH);
                float toggleR = toggleH / 2f;

                var trackColor = val ? primaryColor : theme.Colors.Border;
                ctx.DrawRect(toggleRect, trackColor, radius: toggleR);

                // Thumb
                float thumbD = toggleH - 4f;
                float thumbX = val ? toggleRect.X + toggleW - thumbD - 2f : toggleRect.X + 2f;
                float thumbY2 = toggleY + 2f;
                ctx.DrawRect(new Rect(thumbX, thumbY2, thumbD, thumbD),
                    new ColorValue("#FFFFFF"), radius: thumbD / 2f);
                break;
            }

            case PropertyEditorKind.Enum:
            {
                var getter = prop.Getter!;
                var val = getter.DynamicInvoke();
                valueText = val?.ToString() ?? "";
                PaintText(valueText, editorRect, 0f, textColor, fontSize: fontSize);

                // Small dropdown indicator
                float chevX = editorRect.X + editorRect.Width - 10f;
                float chevY = editorRect.Y + editorRect.Height / 2f;
                PaintChevronDown(chevX, chevY, 3f, mutedColor);
                break;
            }

            case PropertyEditorKind.Color:
            {
                var getter = (Func<ColorValue>)prop.Getter!;
                var color = getter();

                // Color swatch
                float swatchSize = 18f;
                float swatchY = editorRect.Y + (editorRect.Height - swatchSize) / 2f;
                var swatchRect = new Rect(editorRect.X, swatchY, swatchSize, swatchSize);
                ctx.DrawRect(swatchRect, color, radius: 3f);
                ctx.DrawRect(swatchRect, stroke: new Stroke(theme.Colors.Border, 1f), radius: 3f);

                // Hex text
                var hexRect = new Rect(editorRect.X + swatchSize + 6f, editorRect.Y,
                    editorRect.Width - swatchSize - 6f, editorRect.Height);
                PaintText(color.ToString(), hexRect, 0f, mutedColor, fontSize: fontSize);
                break;
            }

            case PropertyEditorKind.Date:
            {
                var getter = (Func<DateOnly>)prop.Getter!;
                valueText = getter().ToString("yyyy-MM-dd");
                PaintText(valueText, editorRect, 0f, textColor, fontSize: fontSize);
                break;
            }

            case PropertyEditorKind.ReadOnly:
            {
                var getter = (Func<object>)prop.Getter!;
                valueText = getter()?.ToString() ?? "";
                PaintText(valueText, editorRect, 0f, mutedColor, fontSize: fontSize);
                break;
            }

            default:
            {
                valueText = "(unsupported)";
                PaintText(valueText, editorRect, 0f, mutedColor, fontSize: fontSize);
                break;
            }
        }
    }

    private void PaintPropertyInlineEditor(
        Rect editorRect, float fontSize,
        ColorValue textColor, ColorValue primaryColor)
    {
        // Draw a subtle input background
        var inputRect = new Rect(editorRect.X - 2f, editorRect.Y + 2f,
            editorRect.Width + 4f, editorRect.Height - 4f);
        ctx.DrawRect(inputRect, theme.Colors.Surface, radius: 3f);
        ctx.DrawRect(inputRect, stroke: new Stroke(primaryColor, 1f), radius: 3f);

        // Draw the edit buffer text
        string text = InputDispatcher.PropertyGridEditBuffer;
        PaintText(text, editorRect, 0f, textColor, fontSize: fontSize);

        // Draw caret (blinking)
        const double blinkMs = 530.0;
        double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;
        bool caretVisible = elapsed < blinkMs || (elapsed % blinkMs) < (blinkMs / 2.0);
        if (caretVisible)
        {
            int caretPos = Math.Clamp(InputDispatcher.PropertyGridEditCaret, 0, text.Length);
            string beforeCaret = text[..caretPos];
            var beforeSize = ctx.MeasureText(beforeCaret, fontSize);
            float caretX = editorRect.X + beforeSize.Width;
            float caretY1 = editorRect.Y + (editorRect.Height - fontSize) / 2f;
            float caretY2 = caretY1 + fontSize;
            ctx.DrawLine(new Point(caretX, caretY1), new Point(caretX, caretY2),
                new Stroke(primaryColor, 1.5f));
        }
    }

    // ── EmojiPicker ──────────────────────────────────────────────────

    private void PaintEmojiPicker(EmojiPicker ep, Rect bounds)
    {
        const float cellSize = 36f;
        const float spacing = 2f;
        const int columns = 8;
        const float tabHeight = 36f;
        const float pad = 8f;
        float radius = 8f;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        ep.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Background panel
        ctx.DrawRect(bounds, theme.Colors.Surface, radius: radius);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: radius);

        // Tab indicator spring slide
        float tabW = (bounds.Width - pad * 2f) / EmojiPicker.CategoryIcons.Length;
        float indicatorTarget = ep.SelectedCategoryIndex / (float)Math.Max(1, EmojiPicker.CategoryIcons.Length - 1);
        ControlStateAnimator.ReconcileValue(ep, indicatorTarget, AnimationModel.Spring.Bouncy);
        float indicatorT = reducedMotion ? indicatorTarget : ControlStateAnimator.GetValueProgress(ep);
        float indicatorX = bounds.X + pad + indicatorT * (EmojiPicker.CategoryIcons.Length - 1) * tabW;

        // Draw sliding indicator with gap below so it doesn't touch the separator
        ctx.DrawRect(new Rect(indicatorX, bounds.Y + tabHeight - 5f, tabW, 2f),
            theme.Colors.Primary, radius: 1f);

        // Category tabs
        for (int t = 0; t < EmojiPicker.CategoryIcons.Length; t++)
        {
            float tabX = bounds.X + pad + t * tabW;
            float tabY = bounds.Y;

            // Tab hover
            if (ep.HoveredIndex == -(100 + t))
            {
                ctx.DrawRect(new Rect(tabX + 2f, tabY + 2f, tabW - 4f, tabHeight - 6f),
                    new ColorValue("#FFFFFF").Opacity(0.06f), radius: 4f);
            }

            // Tab icon
            float iconFontSize = 18f;
            var tabColor = t == ep.SelectedCategoryIndex ? theme.Colors.Text : theme.Colors.TextMuted;
            PaintEmojiCentered(EmojiPicker.CategoryIcons[t], tabX, tabY, tabW, tabHeight, iconFontSize, tabColor);
        }

        // Separator below tabs
        ctx.DrawRect(new Rect(bounds.X, bounds.Y + tabHeight, bounds.Width, 1f),
            theme.Colors.Border.Opacity(0.3f));

        // Emoji grid
        var emojis = EmojiPicker.EmojiData[ep.SelectedCategoryIndex];
        float gridY = bounds.Y + tabHeight + 4f;
        float gridX = bounds.X + pad;

        for (int i = 0; i < emojis.Length; i++)
        {
            int col = i % columns;
            int row = i / columns;
            float cellX = gridX + col * (cellSize + spacing);
            float cellY = gridY + row * (cellSize + spacing);

            if (cellY + cellSize > bounds.Y + bounds.Height)
            {
                break;
            }

            bool isHovered = i == ep.HoveredIndex;

            // Hover highlight
            if (isHovered)
            {
                ctx.DrawRect(new Rect(cellX, cellY, cellSize, cellSize),
                    new ColorValue("#FFFFFF").Opacity(0.1f), radius: 6f);
            }

            // Emoji character — scales up on hover
            float emojiScale = isHovered && !reducedMotion ? 1.15f : 1f;
            ScopeGuard scaleScope = default;
            if (emojiScale > 1.001f)
            {
                var cellCenter = new Point(cellX + cellSize / 2f, cellY + cellSize / 2f);
                scaleScope = ctx.PushScale(emojiScale, emojiScale, cellCenter);
            }

            // Use a warm amber for the grid so monochrome fallback emoji
            // (newer glyphs without COLR layers) look like yellow faces
            // instead of white ghosts.  Colored emoji ignore this parameter.
            var emojiGridColor = new ColorValue("#FFC107");
            PaintEmojiCentered(emojis[i], cellX, cellY, cellSize, cellSize, 22f, emojiGridColor);

            scaleScope.Dispose();
        }
    }

    /// <summary>
    /// Draws an emoji character centered in a cell using the system emoji font
    /// so COLR color layers are used. If the glyph is wider or taller than the
    /// cell, the font size is scaled down so it fits without overlapping.
    /// </summary>
    private void PaintEmojiCentered(string emoji, float cellX, float cellY,
        float cellW, float cellH, float fontSize, ColorValue color)
    {
        string? emojiFont = FontFallback.GetEmojiFontPath();

        // Measure with the requested size first
        var bounds = ctx.MeasureGlyphVisualBounds(emoji, fontSize, emojiFont);
        float visualW = bounds?.VisualWidth ?? fontSize;
        float visualH = bounds?.VisualHeight ?? fontSize;

        // COLR color glyphs may have layers (tears, hearts, sweat drops, etc.)
        // that extend beyond the base glyph bounds reported by FreeType's
        // glyph extents. Use a conservative estimate for scaling so we don't
        // overflow the cell, but center using the reported visual height.
        float conservativeH = MathF.Max(visualH, fontSize * 1.25f);
        float conservativeW = MathF.Max(visualW, fontSize * 1.25f);

        // If the glyph is too big for the cell, scale down proportionally
        float scale = 1f;
        if (conservativeW > cellW || conservativeH > cellH)
        {
            scale = MathF.Min(cellW / conservativeW, cellH / conservativeH);
            fontSize *= scale;

            // Re-measure at the smaller size
            bounds = ctx.MeasureGlyphVisualBounds(emoji, fontSize, emojiFont);
            visualW = bounds?.VisualWidth ?? fontSize;
            visualH = bounds?.VisualHeight ?? fontSize;
            conservativeH = MathF.Max(visualH, fontSize * 1.25f);
            conservativeW = MathF.Max(visualW, fontSize * 1.25f);
        }

        float xBearing = bounds?.XBearing ?? 0f;

        float x = MathF.Round(cellX + (cellW - visualW) / 2f - xBearing);

        // DrawText's y is the top of the line box, not the baseline.
        // VisualCenterY gives the distance from that y to the glyph's
        // visual center: Ascent - YBearing + VisualHeight/2.
        float y = bounds.HasValue
            ? MathF.Round(cellY + cellH / 2f - bounds.Value.VisualCenterY)
            : MathF.Round(cellY + (cellH - visualH) / 2f);

        ctx.DrawText(emoji, x, y, fontSize, color, fontPath: emojiFont);
    }

    // ── NotificationBell ──────────────────────────────────────────────

    private void PaintNotificationBell(NotificationBell nb, Rect bounds)
    {
        var notifications = nb.Notifications.Value;
        int unreadCount = 0;
        if (notifications != null)
        {
            for (int i = 0; i < notifications.Count; i++)
            {
                if (!notifications[i].IsRead)
                {
                    unreadCount++;
                }
            }
        }

        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + bounds.Height / 2f;
        bool bellReducedMotion = ControlStateAnimator.ReducedMotion;

        // Store absolute bounds for input handling
        nb.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        // Hover highlight circle
        if (nb.HoveredIndex == -2)
        {
            ctx.DrawRect(bounds, new ColorValue("#FFFFFF").Opacity(0.08f), radius: bounds.Width / 2f);
        }

        // Bell icon — subtle ring animation when unread notifications exist
        var iconColor = nb.IsOpen ? theme.Colors.Primary : theme.Colors.Text;
        float bellW = 16f;
        float bellH = 18f;
        float bellX = cx - bellW / 2f;
        float bellY = cy - bellH / 2f;

        ScopeGuard bellRingScope = default;
        if (unreadCount > 0 && !bellReducedMotion)
        {
            double elapsedMs = Stopwatch.GetElapsedTime(0).TotalMilliseconds;
            float ringAngle = 3f * MathF.Sin((float)(elapsedMs * 0.005));
            if (MathF.Abs(ringAngle) > 0.1f)
            {
                bellRingScope = ctx.PushRotate(Angle.Degrees(ringAngle), new Point(cx, bellY));
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        // Bell dome (rounded rect as approximation)
        ctx.DrawRect(new Rect(bellX + 2f, bellY, bellW - 4f, bellH - 4f), iconColor, radius: 4f);
        // Bell base (wider)
        ctx.DrawRect(new Rect(bellX, bellY + bellH - 6f, bellW, 4f), iconColor, radius: 2f);
        // Clapper (small circle at bottom)
        float clapperR = 2f;
        ctx.DrawRect(new Rect(cx - clapperR, bellY + bellH - 1f, clapperR * 2f, clapperR * 2f),
            iconColor, radius: clapperR);
        bellRingScope.Dispose();

        // Badge — rendered as deferred overlay with spring scale-in
        if (unreadCount > 0)
        {
            string badgeText = unreadCount > 99 ? "99+" : unreadCount.ToString();
            var bt = theme.Badge;
            float badgeFontSize = bt.TextStyle.Size;
            var capturedTextSize = ctx.MeasureText(badgeText, badgeFontSize);
            float badgeW = Math.Max(bt.Height, capturedTextSize.Width + bt.PaddingH * 2);
            float badgeH = bt.Height;
            // Position: top-right of bell icon (not control bounds, which may be stretched)
            float bellCenterX = absoluteX + bounds.Width / 2f;
            float bellCenterY = absoluteY + bounds.Height / 2f;
            float capturedCX = bellCenterX + bellW / 2f;
            float capturedCY = bellCenterY - bellH / 2f;
            var capturedBg = theme.Colors.Danger;
            var capturedTxt = bt.TextColor;

            // Animate badge scale via ReconcileOpen
            ControlStateAnimator.ReconcileOpen(nb, true, AnimationModel.Spring.Bouncy);
            float badgeScale = bellReducedMotion ? 1f : ControlStateAnimator.GetOpenProgress(nb);

            deferredOverlays ??= [];
            float capturedBadgeScale = badgeScale;
            deferredOverlays.Add(() =>
            {
                ScopeGuard badgeScaleScope = default;
                if (capturedBadgeScale < 0.999f)
                {
                    badgeScaleScope = ctx.PushScale(capturedBadgeScale, capturedBadgeScale,
                        new Point(capturedCX, capturedCY));
                }

                if (badgeW <= badgeH * 1.1f)
                {
                    ctx.DrawCircle(new Point(capturedCX, capturedCY), badgeH / 2f, capturedBg);
                }
                else
                {
                    ctx.DrawRect(new Rect(capturedCX - badgeW / 2f, capturedCY - badgeH / 2f,
                        badgeW, badgeH), capturedBg, radius: badgeH / 2f);
                }
                // Center text within badge bounds using glyph visual bounds
                float badgeLeft = capturedCX - badgeW / 2f;
                var glyphBounds = ctx.MeasureGlyphVisualBounds(badgeText, badgeFontSize);
                float ty = glyphBounds.HasValue
                    ? MathF.Round(capturedCY - glyphBounds.Value.VisualCenterY)
                    : MathF.Round(capturedCY - badgeFontSize * 0.5f);
                ctx.DrawText(badgeText, badgeLeft, ty, badgeFontSize, capturedTxt,
                    alignment: TextAlignment.Center, maxWidth: badgeW);

                badgeScaleScope.Dispose();
            });
        }

        // Deferred dropdown overlay
        if (nb.IsOpen)
        {
            float capturedAbsX = absoluteX;
            float capturedAbsY = absoluteY;
            float capturedBoundsH = bounds.Height;
            deferredOverlays ??= [];
            deferredOverlays.Add(() => PaintNotificationBellDropdown(nb, capturedAbsX, capturedAbsY, capturedBoundsH));
        }
    }

    private void PaintNotificationBellDropdown(NotificationBell nb, float anchorAbsX, float anchorAbsY, float anchorH)
    {
        var notifications = nb.Notifications.Value;
        int count = notifications?.Count ?? 0;
        int visibleCount = Math.Min(count, nb.MaxVisibleCount);

        float dropdownW = 320f;
        float headerH = nb.HeaderHeight;
        float itemH = nb.ItemRowHeight;
        float maxDropH = 400f;
        float dropdownH = headerH + Math.Min(visibleCount * itemH, maxDropH - headerH);
        if (visibleCount == 0)
        {
            dropdownH = headerH + 60f;
        }

        // Position below bell, right-aligned to bell icon
        float dropX = anchorAbsX + 36f - dropdownW;
        float dropY = anchorAbsY + anchorH + 4f;

        // Keep on screen
        if (dropX < 4f)
        {
            dropX = 4f;
        }

        nb.DropdownBounds = new Rect(dropX, dropY, dropdownW, dropdownH);

        // Shadow
        ctx.DrawRect(new Rect(dropX + 2f, dropY + 2f, dropdownW, dropdownH),
            new ColorValue("#000000").Opacity(0.3f), radius: 8f);

        // Background
        ctx.DrawRect(new Rect(dropX, dropY, dropdownW, dropdownH),
            theme.Colors.Surface, radius: 8f);

        // Border
        ctx.DrawRect(new Rect(dropX, dropY, dropdownW, dropdownH),
            stroke: new Stroke(theme.Colors.Border, 1f), radius: 8f);

        // Header: "Notifications" title + unread count
        int unreadCount = 0;
        if (notifications != null)
        {
            for (int i = 0; i < notifications.Count; i++)
            {
                if (!notifications[i].IsRead)
                {
                    unreadCount++;
                }
            }
        }

        float headerPad = 12f;
        PaintText("Notifications", new Rect(dropX + headerPad, dropY, dropdownW * 0.5f, headerH), 0f,
            theme.Colors.Text, fontSize: 14f, fontWeight: FontWeight.Bold);

        if (unreadCount > 0 && nb.OnReadAll != null)
        {
            PaintText("Mark all read", new Rect(dropX + dropdownW * 0.5f, dropY, dropdownW * 0.5f - headerPad, headerH), 0f,
                theme.Colors.Primary, fontSize: 12f, alignment: TextAlignment.End);
        }

        // Separator under header
        ctx.DrawRect(new Rect(dropX, dropY + headerH - 1f, dropdownW, 1f), theme.Colors.Border);

        // Notification items
        float itemStartY = dropY + headerH;
        if (visibleCount == 0)
        {
            PaintText("No notifications", new Rect(dropX, itemStartY, dropdownW, 60f), 0f,
                theme.Colors.TextMuted, fontSize: 13f, alignment: TextAlignment.Center);
        }
        else
        {
            for (int i = 0; i < visibleCount; i++)
            {
                var notif = notifications![i];
                float rowY = itemStartY + i * itemH;

                if (rowY + itemH > dropY + dropdownH)
                {
                    break;
                }

                // Hover highlight
                if (i == nb.HoveredIndex)
                {
                    ctx.DrawRect(new Rect(dropX + 1f, rowY, dropdownW - 2f, itemH),
                        new ColorValue("#FFFFFF").Opacity(0.06f));
                }

                // Unread indicator dot
                if (!notif.IsRead)
                {
                    float dotR = 4f;
                    ctx.DrawRect(new Rect(dropX + 10f, rowY + itemH / 2f - dotR, dotR * 2f, dotR * 2f),
                        theme.Colors.Primary, radius: dotR);
                }

                // Title
                float textLeft = dropX + 24f;
                float textWidth = dropdownW - 36f;
                PaintText(notif.Title, new Rect(textLeft, rowY + 8f, textWidth, 20f), 0f,
                    notif.IsRead ? theme.Colors.TextMuted : theme.Colors.Text,
                    fontSize: 13f, fontWeight: notif.IsRead ? FontWeight.Regular : FontWeight.Bold);

                // Body
                if (notif.Body != null)
                {
                    PaintText(notif.Body, new Rect(textLeft, rowY + 28f, textWidth, 18f), 0f,
                        theme.Colors.TextMuted, fontSize: 12f);
                }

                // Timestamp
                string timeAgo = FormatTimeAgo(notif.Timestamp);
                PaintText(timeAgo, new Rect(textLeft, rowY + 48f, textWidth, 16f), 0f,
                    theme.Colors.TextMuted.Opacity(0.6f), fontSize: 11f);

                // Separator between items
                if (i < visibleCount - 1)
                {
                    ctx.DrawRect(new Rect(dropX + 12f, rowY + itemH - 1f, dropdownW - 24f, 1f),
                        theme.Colors.Border.Opacity(0.3f));
                }
            }
        }
    }

    private static string FormatTimeAgo(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }
        if (elapsed.TotalMinutes < 60)
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }
        if (elapsed.TotalHours < 24)
        {
            return $"{(int)elapsed.TotalHours}h ago";
        }
        if (elapsed.TotalDays < 30)
        {
            return $"{(int)elapsed.TotalDays}d ago";
        }
        return timestamp.ToString("MMM d");
    }

    // ── BarChart ───────────────────────────────────────────────────────

    private void PaintBarChart(BarChart chart, Rect bounds)
    {
        if (chart.Series.Count == 0)
        {
            return;
        }

        var series = chart.Series[0];
        var points = series.DataPoints;
        if (points.Count == 0)
        {
            return;
        }

        var chartTheme = ChartTheme.Default(theme);
        var palette = chartTheme.Palette;

        // Entrance animation progress
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        int animHash = 0;
        bool onScreen = false;
        float animProgress = 1f;
        if (animate)
        {
            animHash = ChartAnimationTracker.ComputeBarChartHash(chart);
            onScreen = IsCurrentlyVisible(bounds);
            animProgress = ChartAnimationTracker.GetProgress(chart, animHash, chart.animateTrigger, ChartAnimationTracker.BarDuration, onScreen);
        }

        const float leftPad = 40f;
        const float bottomPad = 28f;
        const float topPad = 8f;
        const float rightPad = 8f;

        float plotX = bounds.X + leftPad;
        float plotY = bounds.Y + topPad;
        float plotW = bounds.Width - leftPad - rightPad;
        float plotH = bounds.Height - topPad - bottomPad;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        // Compute value range
        double maxVal = 0;
        foreach (var pt in points)
        {
            if (pt.Y > maxVal)
            {
                maxVal = pt.Y;
            }
        }

        if (maxVal <= 0)
        {
            maxVal = 1;
        }

        // Round up to nice ceiling
        double ceil = RoundUpNice(maxVal);

        // Draw background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        // Draw horizontal gridlines (4 lines) — staggered during entrance
        int gridCount = 4;
        for (int i = 0; i <= gridCount; i++)
        {
            float frac = (float)i / gridCount;
            float gy = plotY + plotH * (1f - frac);

            // Grid lines fade in with stagger during entrance
            float gridOpacity = 1f;
            if (animate)
            {
                float gridT = Math.Clamp(animProgress * 2.5f - i * 0.2f, 0f, 1f);
                gridOpacity = gridT;
                if (gridT < 0.01f)
                {
                    continue;
                }
            }

            ctx.DrawLine(
                new Point(plotX, MathF.Round(gy)),
                new Point(plotX + plotW, MathF.Round(gy)),
                new Stroke(chartTheme.GridLine.Opacity(0.3f * gridOpacity), chartTheme.GridLineWidth));

            // Y-axis label (fades with grid line)
            double val = ceil * frac;
            string label = val >= 1000 ? $"{val / 1000:F0}k" : $"{val:F0}";
            var labelSize = ctx.MeasureText(label, 10f);
            ctx.DrawText(label,
                MathF.Round(plotX - labelSize.Width - 4f),
                MathF.Round(gy - labelSize.Height / 2f),
                10f, theme.Colors.TextMuted.Opacity(gridOpacity));
        }

        // Draw bars
        int n = points.Count;
        float barGroupW = plotW / n;
        float barW = barGroupW * chart.barWidthFraction;
        float barGap = (barGroupW - barW) / 2f;
        float barRadius = Math.Min(chartTheme.BarRadius, barW / 2f);

        for (int i = 0; i < n; i++)
        {
            var pt = points[i];
            float frac = (float)(pt.Y / ceil);

            // Apply staggered entrance animation — bars grow from baseline
            if (animate)
            {
                float barProgress = ChartAnimationTracker.GetBarProgress(chart, animHash, chart.animateTrigger, i, n, onScreen);
                frac *= barProgress;
                if (barProgress < 1f && (barProgress > 0f || onScreen))
                {
                    HasActiveChartAnimations = true;
                }
            }

            float barH = plotH * frac;

            float bx = plotX + i * barGroupW + barGap;
            float by = plotY + plotH - barH;

            var barColor = palette.GetColor(i);
            ctx.DrawRect(new Rect(bx, by, barW, barH), barColor, radius: barRadius);

            // X-axis label — fades in with bar stagger
            float xLabelOpacity = 1f;
            if (animate)
            {
                float barProgress2 = ChartAnimationTracker.GetBarProgress(chart, animHash, chart.animateTrigger, i, n, onScreen);
                xLabelOpacity = Math.Clamp(barProgress2 * 2f - 0.5f, 0f, 1f);
            }
            string xLabel = pt.X?.ToString() ?? $"{i}";
            var xSize = ctx.MeasureText(xLabel, 10f);
            ctx.DrawText(xLabel,
                MathF.Round(bx + barW / 2f - xSize.Width / 2f),
                MathF.Round(plotY + plotH + 6f),
                10f, theme.Colors.TextMuted.Opacity(xLabelOpacity));
        }
    }

    private static double RoundUpNice(double value)
    {
        if (value <= 0)
        {
            return 1;
        }

        double exp = Math.Floor(Math.Log10(value));
        double frac = value / Math.Pow(10, exp);

        double nice;
        if (frac <= 1.0)
        {
            nice = 1.0;
        }
        else if (frac <= 2.0)
        {
            nice = 2.0;
        }
        else if (frac <= 5.0)
        {
            nice = 5.0;
        }
        else
        {
            nice = 10.0;
        }

        return nice * Math.Pow(10, exp);
    }

    // ── PieChart ──────────────────────────────────────────────────────

    private void PaintPieChart(PieChart chart, Rect bounds)
    {
        var slices = chart.Slices;
        if (slices.Count == 0)
        {
            return;
        }

        var chartTheme = ChartTheme.Default(theme);
        var palette = chartTheme.Palette;

        // Entrance animation progress
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        float animProgress = 1f;
        if (animate)
        {
            int animHash = ChartAnimationTracker.ComputePieChartHash(chart);
            bool onScreen = IsCurrentlyVisible(bounds);
            animProgress = ChartAnimationTracker.GetProgress(chart, animHash, chart.animateTrigger, ChartAnimationTracker.PieDuration, onScreen);
            if (animProgress < 1f && (animProgress > 0f || onScreen))
            {
                HasActiveChartAnimations = true;
            }
        }

        // Compute total
        double total = 0;
        foreach (var s in slices)
        {
            total += Math.Max(0, s.Value);
        }

        if (total <= 0)
        {
            return;
        }

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        // Reserve space at bottom for legend so chart and legend don't overlap
        const float legendAreaH = 32f;
        bool hasLegend = slices.Count > 0;
        float chartBottomPad = hasLegend ? legendAreaH : 0f;

        float cx = bounds.X + bounds.Width / 2f;
        float cy = bounds.Y + (bounds.Height - chartBottomPad) / 2f;
        float maxRadius = MathF.Min(bounds.Width, bounds.Height - chartBottomPad) / 2f - 16f;

        if (maxRadius <= 0)
        {
            return;
        }

        float holeRadius = chart.holeRadiusValue;
        float innerR = maxRadius * holeRadius;

        float startDeg = chart.startAngleValue.InDegrees;

        // All slices grow proportionally from 0 to full size so no slice
        // is completely hidden during the entrance animation.
        for (int i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            double frac = Math.Max(0, slice.Value) / total;
            float sweepDeg = (float)(frac * 360.0);
            float actualSweep = sweepDeg * animProgress;

            if (actualSweep < 0.1f)
            {
                startDeg += sweepDeg;
                continue;
            }

            var color = slice.colorOverride ?? palette.GetColor(i);
            float startRad = startDeg * MathF.PI / 180f;
            float sweepRad = actualSweep * MathF.PI / 180f;
            ctx.DrawSector(new Point(cx, cy), maxRadius, innerR, startRad, sweepRad, color);

            startDeg += sweepDeg;
        }

        // Donut label — only show when animation is complete or nearly so
        if (chart.donutLabelValue.HasValue && holeRadius > 0.2f && animProgress > 0.8f)
        {
            float labelOpacity = (animProgress - 0.8f) / 0.2f;
            string labelText = $"{chart.donutLabelValue.Value:F0}";
            float labelFontSize = maxRadius * 0.35f;
            var textSize = ctx.MeasureText(labelText, labelFontSize);

            using var opScope = labelOpacity < 1f ? ctx.PushOpacity(labelOpacity) : default;
            ctx.DrawText(labelText,
                MathF.Round(cx - textSize.Width / 2f),
                MathF.Round(cy - textSize.Height / 2f - (chart.donutSubLabel != null ? labelFontSize * 0.2f : 0)),
                labelFontSize, theme.Colors.Text);

            if (chart.donutSubLabel != null)
            {
                float subFontSize = maxRadius * 0.16f;
                var subSize = ctx.MeasureText(chart.donutSubLabel, subFontSize);
                ctx.DrawText(chart.donutSubLabel,
                    MathF.Round(cx - subSize.Width / 2f),
                    MathF.Round(cy + textSize.Height / 2f - labelFontSize * 0.1f),
                    subFontSize, theme.Colors.TextMuted);
            }
        }

        // Legend below chart — stagger fade-in after slices mostly complete
        const float legendFontSize = 11f;
        float legendTextH = ctx.MeasureText("X", legendFontSize).Height;
        float legendY = bounds.Y + bounds.Height - legendAreaH / 2f - legendTextH / 2f;
        float legendX = bounds.X + 12f;
        for (int i = 0; i < slices.Count && legendX < bounds.X + bounds.Width - 20f; i++)
        {
            float legendItemT = 1f;
            if (animate)
            {
                legendItemT = Math.Clamp((animProgress - 0.6f - i * 0.06f) / 0.3f, 0f, 1f);
                if (legendItemT < 0.01f)
                {
                    // Still need to advance legendX for layout
                    string skipLabel = slices[i].Label;
                    var skipSize = ctx.MeasureText(skipLabel, legendFontSize);
                    legendX += 12f + skipSize.Width + 10f;
                    continue;
                }
            }

            ScopeGuard legendOpScope = legendItemT < 1f ? ctx.PushOpacity(legendItemT) : default;

            var color = slices[i].colorOverride ?? palette.GetColor(i);
            // Vertically center the color square with the text
            float squareSize = 8f;
            float squareY = legendY + (legendTextH - squareSize) / 2f;
            ctx.DrawRect(new Rect(legendX, squareY, squareSize, squareSize), color, radius: 2f);
            legendX += 12f;

            string label = slices[i].Label;
            var labelSize = ctx.MeasureText(label, legendFontSize);
            ctx.DrawText(label, legendX, legendY, legendFontSize, theme.Colors.TextMuted);
            legendX += labelSize.Width + 10f;

            legendOpScope.Dispose();
        }
    }

    /// <summary>
    /// Builds a closed path for an annular sector (ring segment).
    /// The path consists of an outer arc, a radial line to the inner arc,
    /// the inner arc traversed in reverse, and a closing radial line.
    /// This produces a true donut slice without needing a hole punch-out.
    /// </summary>
    private static Path BuildAnnularSectorPath(
        Point center,
        float outerRadius,
        float innerRadius,
        Angle startAngle,
        Angle sweepAngle)
    {
        float sweepRad = sweepAngle.InRadians;
        if (MathF.Abs(sweepRad) < 0.001f)
        {
            return Path.Arc(center, outerRadius, startAngle, sweepAngle);
        }

        var builder = PathBuilder.Rent();

        float startRad = startAngle.InRadians;
        float endRad = startRad + sweepRad;

        // --- Outer arc (forward) ---
        float outerStartX = center.X + outerRadius * MathF.Cos(startRad);
        float outerStartY = center.Y + outerRadius * MathF.Sin(startRad);
        builder.MoveTo(new Point(outerStartX, outerStartY));

        float remaining = sweepRad;
        float current = startRad;

        while (MathF.Abs(remaining) > 0.001f)
        {
            float maxSegment = MathF.PI / 2f;
            float segment = remaining > 0
                ? MathF.Min(remaining, maxSegment)
                : MathF.Max(remaining, -maxSegment);

            float halfSegment = segment / 2f;
            float kFactor = 4f / 3f * MathF.Tan(halfSegment / 2f);

            float cos0 = MathF.Cos(current);
            float sin0 = MathF.Sin(current);
            float cos1 = MathF.Cos(current + segment);
            float sin1 = MathF.Sin(current + segment);

            float x0 = center.X + outerRadius * cos0;
            float y0 = center.Y + outerRadius * sin0;
            float x1 = center.X + outerRadius * cos1;
            float y1 = center.Y + outerRadius * sin1;

            float cp1x = x0 - kFactor * outerRadius * sin0;
            float cp1y = y0 + kFactor * outerRadius * cos0;
            float cp2x = x1 + kFactor * outerRadius * sin1;
            float cp2y = y1 - kFactor * outerRadius * cos1;

            builder.CubicTo(
                new Point(cp1x, cp1y),
                new Point(cp2x, cp2y),
                new Point(x1, y1));

            current += segment;
            remaining -= segment;
        }

        // --- Radial line from outer end to inner end ---
        float innerEndX = center.X + innerRadius * MathF.Cos(endRad);
        float innerEndY = center.Y + innerRadius * MathF.Sin(endRad);
        builder.LineTo(new Point(innerEndX, innerEndY));

        // --- Inner arc (reverse direction) ---
        remaining = -sweepRad;
        current = endRad;

        while (MathF.Abs(remaining) > 0.001f)
        {
            float maxSegment = MathF.PI / 2f;
            float segment = remaining > 0
                ? MathF.Min(remaining, maxSegment)
                : MathF.Max(remaining, -maxSegment);

            float halfSegment = segment / 2f;
            float kFactor = 4f / 3f * MathF.Tan(halfSegment / 2f);

            float cos0 = MathF.Cos(current);
            float sin0 = MathF.Sin(current);
            float cos1 = MathF.Cos(current + segment);
            float sin1 = MathF.Sin(current + segment);

            float x0 = center.X + innerRadius * cos0;
            float y0 = center.Y + innerRadius * sin0;
            float x1 = center.X + innerRadius * cos1;
            float y1 = center.Y + innerRadius * sin1;

            float cp1x = x0 - kFactor * innerRadius * sin0;
            float cp1y = y0 + kFactor * innerRadius * cos0;
            float cp2x = x1 + kFactor * innerRadius * sin1;
            float cp2y = y1 - kFactor * innerRadius * cos1;

            builder.CubicTo(
                new Point(cp1x, cp1y),
                new Point(cp2x, cp2y),
                new Point(x1, y1));

            current += segment;
            remaining -= segment;
        }

        // Close back to outer start (implicit radial line via Close).
        builder.Close();

        return builder.Build();
    }

    // ── LineChart ──────────────────────────────────────────────────────

    private void PaintLineChart(LineChart chart, Rect bounds)
    {
        if (chart.Series.Count == 0)
        {
            return;
        }

        var chartTheme = ChartTheme.Default(theme);
        var palette = chartTheme.Palette;

        // Entrance animation progress (left-to-right reveal)
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        float animProgress = 1f;
        if (animate)
        {
            int animHash = ChartAnimationTracker.ComputeLineChartHash(chart);
            bool onScreen = IsCurrentlyVisible(bounds);
            animProgress = ChartAnimationTracker.GetProgress(chart, animHash, chart.animateTrigger, ChartAnimationTracker.LineDuration, onScreen);
            if (animProgress < 1f && (animProgress > 0f || onScreen))
            {
                HasActiveChartAnimations = true;
            }
        }

        bool hasLegend = chart.Series.Count > 1;
        const float leftPad = 40f;
        float bottomPad = hasLegend ? 44f : 28f;
        const float topPad = 8f;
        const float rightPad = 24f;

        float plotX = bounds.X + leftPad;
        float plotY = bounds.Y + topPad;
        float plotW = bounds.Width - leftPad - rightPad;
        float plotH = bounds.Height - topPad - bottomPad;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        // Compute global min/max across all series
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;
        int maxPoints = 0;
        for (int s = 0; s < chart.Series.Count; s++)
        {
            var pts = chart.Series[s].DataPoints;
            if (pts.Count > maxPoints)
            {
                maxPoints = pts.Count;
            }

            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i].Y < minVal) { minVal = pts[i].Y; }
                if (pts[i].Y > maxVal) { maxVal = pts[i].Y; }
            }
        }

        if (maxPoints < 2 || maxVal <= minVal)
        {
            return;
        }

        double ceil = RoundUpNice(maxVal);
        double floor = minVal >= 0 ? 0 : -RoundUpNice(-minVal);
        double range = ceil - floor;
        if (range <= 0)
        {
            range = 1;
        }

        // Draw background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        // Draw horizontal gridlines — staggered during entrance
        int gridCount = 4;
        for (int i = 0; i <= gridCount; i++)
        {
            float frac = (float)i / gridCount;
            float gy = plotY + plotH * (1f - frac);

            float gridOpacity = 1f;
            if (animate)
            {
                float gridT = Math.Clamp(animProgress * 2.5f - i * 0.2f, 0f, 1f);
                gridOpacity = gridT;
                if (gridT < 0.01f)
                {
                    continue;
                }
            }

            ctx.DrawLine(
                new Point(plotX, MathF.Round(gy)),
                new Point(plotX + plotW, MathF.Round(gy)),
                new Stroke(chartTheme.GridLine.Opacity(0.3f * gridOpacity), chartTheme.GridLineWidth));

            double val = floor + range * frac;
            string label = val >= 1000 ? $"{val / 1000:F0}k" : $"{val:F0}";
            var labelSize = ctx.MeasureText(label, 10f);
            ctx.DrawText(label,
                MathF.Round(plotX - labelSize.Width - 4f),
                MathF.Round(gy - labelSize.Height / 2f),
                10f, theme.Colors.TextMuted.Opacity(gridOpacity));
        }
        float revealWidth = plotW * animProgress;
        ScopeGuard animClip = animProgress < 1f
            ? ctx.PushClip(new Rect(plotX, plotY - 4f, revealWidth + 4f, plotH + 8f))
            : default;

        // Draw each series
        for (int s = 0; s < chart.Series.Count; s++)
        {
            var series = chart.Series[s];
            var points = series.DataPoints;
            if (points.Count < 2)
            {
                continue;
            }

            var lineColor = series.colorOverride ?? palette.GetColor(s);
            float lineWidth = series.lineWidthValue;
            var lineStroke = new Stroke(lineColor, lineWidth, StrokeCap.Round, StrokeJoin.Round);

            float stepX = plotW / (points.Count - 1);

            // Draw line segments
            for (int i = 0; i < points.Count - 1; i++)
            {
                float x1 = plotX + i * stepX;
                float y1 = plotY + plotH * (1f - (float)((points[i].Y - floor) / range));
                float x2 = plotX + (i + 1) * stepX;
                float y2 = plotY + plotH * (1f - (float)((points[i + 1].Y - floor) / range));
                ctx.DrawLine(new Point(x1, y1), new Point(x2, y2), lineStroke);
            }

            // Draw data point markers — scale in with stagger after line reveal
            bool showPoints = chart.pointDisplay == PointDisplay.Always
                || (chart.pointDisplay == PointDisplay.Auto && points.Count <= 20);
            if (showPoints)
            {
                float dotRadius = lineWidth * 1.8f;
                for (int i = 0; i < points.Count; i++)
                {
                    float px = plotX + i * stepX;
                    float py = plotY + plotH * (1f - (float)((points[i].Y - floor) / range));

                    // Scale in: dot appears after the reveal line passes its X position
                    float dotScale = 1f;
                    if (animate && animProgress < 1f)
                    {
                        float pointFrac = points.Count > 1 ? (float)i / (points.Count - 1) : 0f;
                        float pointT = Math.Clamp((animProgress - pointFrac) * 5f, 0f, 1f);
                        dotScale = pointT;
                    }

                    if (dotScale > 0.01f)
                    {
                        ctx.DrawCircle(new Point(px, py), dotRadius * dotScale,
                            fill: lineColor.Opacity(dotScale));
                    }
                }
            }

            // X-axis labels (only for first series) — with thinning to avoid overlap
            if (s == 0 && points.Count > 0)
            {
                var sampleLabel = points[0].X?.ToString() ?? "0";
                float sampleW = ctx.MeasureText(sampleLabel, 10f).Width;
                float minSpacing = sampleW + 12f;
                int labelStep = stepX > 0.001f ? Math.Max(1, (int)MathF.Ceiling(minSpacing / stepX)) : 1;

                for (int i = 0; i < points.Count; i += labelStep)
                {
                    string xLabel = points[i].X?.ToString() ?? $"{i}";
                    var xSize = ctx.MeasureText(xLabel, 10f);
                    float lx = plotX + i * stepX - xSize.Width / 2f;
                    ctx.DrawText(xLabel,
                        MathF.Round(lx),
                        MathF.Round(plotY + plotH + 6f),
                        10f, theme.Colors.TextMuted);
                }

                // Always draw last label if it wasn't drawn by the step
                int lastIdx = points.Count - 1;
                if (lastIdx % labelStep != 0)
                {
                    string lastLabel = points[lastIdx].X?.ToString() ?? $"{lastIdx}";
                    var lastSize = ctx.MeasureText(lastLabel, 10f);
                    float lastLx = plotX + lastIdx * stepX - lastSize.Width / 2f;
                    ctx.DrawText(lastLabel,
                        MathF.Round(lastLx),
                        MathF.Round(plotY + plotH + 6f),
                        10f, theme.Colors.TextMuted);
                }
            }
        }

        animClip.Dispose();

        // Legend (if multiple series)
        if (hasLegend)
        {
            float legendY = plotY + plotH + 22f;
            float legendX = bounds.X + leftPad;
            for (int s = 0; s < chart.Series.Count && legendX < bounds.X + bounds.Width - 20f; s++)
            {
                var color = chart.Series[s].colorOverride ?? palette.GetColor(s);
                ctx.DrawRect(new Rect(legendX, legendY, 8f, 8f), color, radius: 2f);
                legendX += 12f;

                string name = chart.Series[s].SeriesName;
                var nameSize = ctx.MeasureText(name, 11f);
                ctx.DrawText(name, legendX, legendY - 1f, 11f, theme.Colors.TextMuted);
                legendX += nameSize.Width + 14f;
            }
        }
    }

    // ── AreaChart ──────────────────────────────────────────────────────

    private void PaintAreaChart(AreaChart chart, Rect bounds)
    {
        if (chart.Series.Count == 0)
        {
            return;
        }

        var chartTheme = ChartTheme.Default(theme);
        var palette = chartTheme.Palette;

        // Entrance animation (left-to-right reveal)
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        float animProgress = 1f;
        if (animate)
        {
            int animHash = ChartAnimationTracker.ComputeAreaChartHash(chart);
            bool onScreen = IsCurrentlyVisible(bounds);
            animProgress = ChartAnimationTracker.GetProgress(chart, animHash, chart.animateTrigger, ChartAnimationTracker.AreaDuration, onScreen);
            if (animProgress < 1f && (animProgress > 0f || onScreen))
            {
                HasActiveChartAnimations = true;
            }
        }

        bool hasLegend = chart.Series.Count > 1;
        const float leftPad = 40f;
        float bottomPad = hasLegend ? 44f : 28f;
        const float topPad = 8f;
        const float rightPad = 24f;

        float plotX = bounds.X + leftPad;
        float plotY = bounds.Y + topPad;
        float plotW = bounds.Width - leftPad - rightPad;
        float plotH = bounds.Height - topPad - bottomPad;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        // Compute global min/max (handle stacking)
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;
        int maxPoints = 0;

        if (chart.stackedEnabled && chart.Series.Count > 1)
        {
            // For stacked: find max of cumulative sums at each X
            for (int s = 0; s < chart.Series.Count; s++)
            {
                var pts = chart.Series[s].DataPoints;
                if (pts.Count > maxPoints) { maxPoints = pts.Count; }
            }

            for (int i = 0; i < maxPoints; i++)
            {
                double cumulative = 0;
                for (int s = 0; s < chart.Series.Count; s++)
                {
                    var pts = chart.Series[s].DataPoints;
                    if (i < pts.Count)
                    {
                        cumulative += pts[i].Y;
                    }
                }

                if (cumulative > maxVal) { maxVal = cumulative; }
                if (cumulative < minVal) { minVal = cumulative; }
            }

            minVal = Math.Min(minVal, 0);
        }
        else
        {
            for (int s = 0; s < chart.Series.Count; s++)
            {
                var pts = chart.Series[s].DataPoints;
                if (pts.Count > maxPoints) { maxPoints = pts.Count; }
                for (int i = 0; i < pts.Count; i++)
                {
                    if (pts[i].Y < minVal) { minVal = pts[i].Y; }
                    if (pts[i].Y > maxVal) { maxVal = pts[i].Y; }
                }
            }
        }

        if (maxPoints < 2 || maxVal <= minVal)
        {
            return;
        }

        double ceil = RoundUpNice(maxVal);
        double floor = minVal >= 0 ? 0 : -RoundUpNice(-minVal);
        double range = ceil - floor;
        if (range <= 0) { range = 1; }

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        // Gridlines — staggered during entrance.
        // Cache the 5 gridline value labels on the chart, keyed on (ceil, floor).
        // These strings are pure functions of the data extremes so they change
        // only when data or plot range changes.
        int gridCount = 4;
        if (chart.gridLabelCache == null
            || chart.gridLabelCache.Length != gridCount + 1
            || chart.gridCeilKey != ceil
            || chart.gridFloorKey != floor)
        {
            chart.gridLabelCache = new string?[gridCount + 1];
            chart.gridCeilKey = ceil;
            chart.gridFloorKey = floor;
        }
        var gridLabels = chart.gridLabelCache;

        for (int i = 0; i <= gridCount; i++)
        {
            float frac = (float)i / gridCount;
            float gy = plotY + plotH * (1f - frac);

            float gridOpacity = 1f;
            if (animate)
            {
                float gridT = Math.Clamp(animProgress * 2.5f - i * 0.2f, 0f, 1f);
                gridOpacity = gridT;
                if (gridT < 0.01f)
                {
                    continue;
                }
            }

            ctx.DrawLine(
                new Point(plotX, MathF.Round(gy)),
                new Point(plotX + plotW, MathF.Round(gy)),
                new Stroke(chartTheme.GridLine.Opacity(0.3f * gridOpacity), chartTheme.GridLineWidth));

            string? label = gridLabels[i];
            if (label == null)
            {
                double val = floor + range * frac;
                label = val >= 1000 ? $"{val / 1000:F0}k" : $"{val:F0}";
                gridLabels[i] = label;
            }
            var labelSize = ctx.MeasureText(label, 10f);
            ctx.DrawText(label,
                MathF.Round(plotX - labelSize.Width - 4f),
                MathF.Round(gy - labelSize.Height / 2f),
                10f, theme.Colors.TextMuted.Opacity(gridOpacity));
        }

        // Animation clip (left-to-right reveal)
        float revealWidth = plotW * animProgress;
        ScopeGuard animClip = animProgress < 1f
            ? ctx.PushClip(new Rect(plotX, plotY - 4f, revealWidth + 4f, plotH + 8f))
            : default;

        float stepX = plotW / (maxPoints - 1);

        // For stacking, track cumulative baselines. Reuse a pooled buffer
        // on the chart to avoid per-frame allocation. Size can only grow
        // — maxPoints is stable across re-renders of the same series.
        float[] prevBaseline = chart.prevBaselinePool ?? [];
        if (prevBaseline.Length < maxPoints)
        {
            prevBaseline = new float[maxPoints];
            chart.prevBaselinePool = prevBaseline;
        }
        for (int i = 0; i < maxPoints; i++)
        {
            prevBaseline[i] = plotY + plotH; // start at X-axis
        }

        // Draw each series (back-to-front for stacking; reversed for overlaid)
        int seriesCount = chart.Series.Count;
        for (int s = 0; s < seriesCount; s++)
        {
            var series = chart.Series[s];
            var points = series.DataPoints;
            if (points.Count < 2)
            {
                continue;
            }

            var lineColor = series.colorOverride ?? palette.GetColor(s);
            float fillOpacity = chart.fillOpacityValue;

            // Compute Y positions for this series. Reuse pooled buffer.
            float[] yPositions = chart.yPositionsPool ?? [];
            if (yPositions.Length < points.Count)
            {
                yPositions = new float[points.Count];
                chart.yPositionsPool = yPositions;
            }
            for (int i = 0; i < points.Count; i++)
            {
                double yVal = points[i].Y;
                if (chart.stackedEnabled && s > 0)
                {
                    // Add previous cumulative values
                    double cumPrev = 0;
                    for (int ps = 0; ps < s; ps++)
                    {
                        var prevPts = chart.Series[ps].DataPoints;
                        if (i < prevPts.Count) { cumPrev += prevPts[i].Y; }
                    }
                    yVal += cumPrev;
                }

                yPositions[i] = plotY + plotH * (1f - (float)((yVal - floor) / range));
            }

            // Build filled area path: line top + baseline bottom.
            // Uses a thread-local pooled PathBuilder + transient Path:
            // zero managed allocations for the path backing buffers — the
            // builder's arrays are reused across frames, and the Path is
            // consumed synchronously by DrawPath before the next Reset.
            var areaPath = PathBuilder.Rent();
            areaPath.MoveTo(new Point(plotX, yPositions[0]));
            for (int i = 1; i < points.Count; i++)
            {
                float px = plotX + i * stepX;
                areaPath.LineTo(new Point(px, yPositions[i]));
            }

            // Close back along the baseline (previous series top or X-axis)
            for (int i = points.Count - 1; i >= 0; i--)
            {
                float px = plotX + i * stepX;
                float baseY = chart.stackedEnabled ? prevBaseline[i] : (plotY + plotH);
                areaPath.LineTo(new Point(px, baseY));
            }

            areaPath.Close();
            ctx.DrawPath(areaPath.BuildTransient(), fill: lineColor.Opacity(fillOpacity));

            // Draw the line on top
            float lineWidth = series.lineWidthValue;
            var lineStroke = new Stroke(lineColor, lineWidth, StrokeCap.Round, StrokeJoin.Round);
            for (int i = 0; i < points.Count - 1; i++)
            {
                float x1 = plotX + i * stepX;
                float x2 = plotX + (i + 1) * stepX;
                ctx.DrawLine(new Point(x1, yPositions[i]), new Point(x2, yPositions[i + 1]), lineStroke);
            }

            // Data point markers — scale in with stagger after reveal
            bool showPoints = chart.pointDisplay == PointDisplay.Always
                || (chart.pointDisplay == PointDisplay.Auto && points.Count <= 20);
            if (showPoints)
            {
                float dotRadius = lineWidth * 1.8f;
                for (int i = 0; i < points.Count; i++)
                {
                    float px = plotX + i * stepX;

                    float dotScale = 1f;
                    if (animate && animProgress < 1f)
                    {
                        float pointFrac = points.Count > 1 ? (float)i / (points.Count - 1) : 0f;
                        float pointT = Math.Clamp((animProgress - pointFrac) * 5f, 0f, 1f);
                        dotScale = pointT;
                    }

                    if (dotScale > 0.01f)
                    {
                        ctx.DrawCircle(new Point(px, yPositions[i]), dotRadius * dotScale,
                            fill: lineColor.Opacity(dotScale));
                    }
                }
            }

            // X-axis labels (first series only) — with thinning to avoid overlap.
            // Per-point label strings are cached on the chart so
            // points[i].X?.ToString() runs once per unique index.
            if (s == 0 && points.Count > 0)
            {
                // Ensure per-series label array exists and is the right size.
                var xCache = chart.xLabelCache;
                if (xCache == null || xCache.Length < chart.Series.Count)
                {
                    xCache = new string?[chart.Series.Count][];
                    chart.xLabelCache = xCache;
                }
                var seriesXLabels = xCache[s];
                if (seriesXLabels == null || seriesXLabels.Length != points.Count)
                {
                    seriesXLabels = new string?[points.Count];
                    xCache[s] = seriesXLabels;
                }

                string sampleLabel = seriesXLabels[0] ?? (seriesXLabels[0] = points[0].X?.ToString() ?? "0");
                float sampleW = ctx.MeasureText(sampleLabel, 10f).Width;
                float minSpacing = sampleW + 12f;
                int labelStep = stepX > 0.001f ? Math.Max(1, (int)MathF.Ceiling(minSpacing / stepX)) : 1;

                for (int i = 0; i < points.Count; i += labelStep)
                {
                    string xLabel = seriesXLabels[i] ?? (seriesXLabels[i] = points[i].X?.ToString() ?? i.ToString());
                    var xSize = ctx.MeasureText(xLabel, 10f);
                    float lx = plotX + i * stepX - xSize.Width / 2f;
                    ctx.DrawText(xLabel,
                        MathF.Round(lx),
                        MathF.Round(plotY + plotH + 6f),
                        10f, theme.Colors.TextMuted);
                }

                // Always draw last label if it wasn't drawn by the step
                int lastIdx = points.Count - 1;
                if (lastIdx % labelStep != 0)
                {
                    string lastLabel = seriesXLabels[lastIdx]
                        ?? (seriesXLabels[lastIdx] = points[lastIdx].X?.ToString() ?? lastIdx.ToString());
                    var lastSize = ctx.MeasureText(lastLabel, 10f);
                    float lastLx = plotX + lastIdx * stepX - lastSize.Width / 2f;
                    ctx.DrawText(lastLabel,
                        MathF.Round(lastLx),
                        MathF.Round(plotY + plotH + 6f),
                        10f, theme.Colors.TextMuted);
                }
            }

            // Update stacking baseline for next series
            if (chart.stackedEnabled)
            {
                for (int i = 0; i < points.Count && i < maxPoints; i++)
                {
                    prevBaseline[i] = yPositions[i];
                }
            }
        }

        animClip.Dispose();

        // Legend
        if (hasLegend)
        {
            float legendY = plotY + plotH + 22f;
            float legendX = bounds.X + leftPad;
            for (int s = 0; s < chart.Series.Count && legendX < bounds.X + bounds.Width - 20f; s++)
            {
                var color = chart.Series[s].colorOverride ?? palette.GetColor(s);
                ctx.DrawRect(new Rect(legendX, legendY, 8f, 8f), color, radius: 2f);
                legendX += 12f;

                string name = chart.Series[s].SeriesName;
                var nameSize = ctx.MeasureText(name, 11f);
                ctx.DrawText(name, legendX, legendY - 1f, 11f, theme.Colors.TextMuted);
                legendX += nameSize.Width + 14f;
            }
        }
    }

    // ── Animation state for MenuBar dropdown entrance ─────────────────
    private static readonly Dictionary<int, long> menuBarOpenTick = new();

    // ── TreeView ───────────────────────────────────────────────────────

    private static readonly Dictionary<long, float> treeChevronProgress = new();

    private void PaintTreeView(ITreeView tree, Rect bounds)
    {
        // Store absolute bounds for hit testing
        tree.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        var items = tree.GetFlattenedDisplay();
        if (items.Count == 0)
        {
            return;
        }

        var paths = tree.FlattenedPaths;
        string? selectedPath = TreeViewInteractionState.SelectedPath;
        int hoveredRow = TreeViewInteractionState.HoveredRow;

        bool treeReducedMotion = ControlStateAnimator.ReducedMotion;

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        const float rowHeight = 28f;
        const float indentWidth = 20f;
        const float iconSize = 8f;
        const float leftPad = 8f;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float rowY = bounds.Y + i * rowHeight;

            if (rowY + rowHeight < bounds.Y || rowY > bounds.Y + bounds.Height)
            {
                continue;
            }

            // Row hover / selection highlight — a slightly inset rounded pill, so the
            // tree reads as interactive (a selected row uses a solid accent fill; a
            // hovered row a subtle neutral wash). Draw before the row content.
            bool isSelected = selectedPath != null && i < paths.Count && paths[i] == selectedPath;
            bool isHovered = i == hoveredRow;
            if (isSelected || isHovered)
            {
                var rowRect = new Rect(bounds.X + 4f, rowY + 1f, bounds.Width - 8f, rowHeight - 2f);
                var rowFill = isSelected ? theme.Colors.Primary : theme.Colors.Text.Opacity(0.06f);
                ctx.DrawRect(rowRect, rowFill, radius: 5f);
            }

            float indentX = bounds.X + leftPad + item.Depth * indentWidth;

            // Draw expand/collapse chevron or leaf dot
            if (item.HasChildren)
            {
                float chevronX = indentX + 2f;
                float chevronCY = rowY + rowHeight / 2f;
                var chevronColor = isSelected ? theme.Colors.TextOnPrimary : theme.Colors.TextMuted;
                float cStroke = 1.5f;

                // Animate chevron rotation per item
                // Use content-space position for stable identity across re-renders.
                float targetRotation = item.IsExpanded ? 90f : 0f;
                float treeContentY = absoluteY + activeScrollOffsetY;
                long itemKey = (long)HashCode.Combine(
                    (int)Math.Round(treeContentY * 10f),
                    (int)Math.Round(bounds.Width * 10f),
                    i);
                if (treeReducedMotion)
                {
                    treeChevronProgress[itemKey] = targetRotation;
                }
                else
                {
                    if (!treeChevronProgress.TryGetValue(itemKey, out float current))
                    {
                        current = targetRotation;
                    }
                    float diff = targetRotation - current;
                    if (MathF.Abs(diff) > 0.5f)
                    {
                        current += diff * 0.2f;
                        ControlStateAnimator.SignalActiveTransition();
                    }
                    else
                    {
                        current = targetRotation;
                    }
                    treeChevronProgress[itemKey] = current;
                }
                float rotation = treeChevronProgress[itemKey];

                // Always draw > shape, rotate 0→90° for expanded
                var chevCenter = new Point(chevronX + iconSize / 2f, chevronCY);
                using var treeChevRotate = ctx.PushRotate(
                    Angle.Degrees(rotation), chevCenter);
                ctx.DrawLine(
                    new Point(chevronX + iconSize / 4f, chevronCY - iconSize / 2f),
                    new Point(chevronX + iconSize * 3f / 4f, chevronCY),
                    new Stroke(chevronColor, cStroke, StrokeCap.Round, StrokeJoin.Round));
                ctx.DrawLine(
                    new Point(chevronX + iconSize * 3f / 4f, chevronCY),
                    new Point(chevronX + iconSize / 4f, chevronCY + iconSize / 2f),
                    new Stroke(chevronColor, cStroke, StrokeCap.Round, StrokeJoin.Round));
            }
            else
            {
                // Leaf node dot
                float dotX = indentX + iconSize / 2f + 2f;
                float dotY = rowY + rowHeight / 2f;
                var dotColor = isSelected
                    ? theme.Colors.TextOnPrimary.Opacity(0.7f)
                    : theme.Colors.TextMuted.Opacity(0.5f);
                ctx.DrawCircle(new Point(dotX, dotY), 2f, fill: dotColor);
            }

            // Draw label text, optically centred on the row so it lines up with the
            // chevron / leaf dot (both drawn at the row centre). DrawText's y is the
            // line-box top; the visible glyph box sits ~0.3·fontSize below it, so its
            // optical centre is ~0.8·fontSize down — place that on the row centre.
            float textX = indentX + iconSize + 8f;
            float fontSize = item.Depth == 0 ? 13f : 12f;
            float textY = rowY + rowHeight / 2f - fontSize * 0.8f;
            var textColor = isSelected
                ? theme.Colors.TextOnPrimary
                : (item.HasChildren ? theme.Colors.Text : theme.Colors.TextMuted);
            ctx.DrawText(item.Label, textX, textY, fontSize, textColor);
        }
    }

    // ── PasswordInput ─────────────────────────────────────────────────

    private void PaintPasswordInput(PasswordInput pwd, Rect bounds)
    {
        var t = theme.TextInput;
        bool disabled = pwd.IsDisabled;
        float inputHeight = 36f;
        var inputBounds = new Rect(bounds.X, bounds.Y, bounds.Width, inputHeight);

        // Store bounds for input hit-testing (absolute viewport coordinates)
        pwd.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, inputHeight);

        bool focused = ReferenceEquals(FocusManager.FocusedElement, pwd);

        // Animated focus/hover/disabled transitions
        var hoverModel = t.Transition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        bool isFocused = focused && !disabled;
        var anim = ControlStateAnimator.Reconcile(
            pwd, hoverModel, pressModel, isDisabled: disabled, isFocused: isFocused);

        float focusT = anim.Focus.Current;
        float hoverT = anim.Hover.Current;

        // Animate reveal toggle state (Open channel: 0=masked, 1=revealed)
        bool revealed = InputDispatcher.PasswordRevealed;
        ControlStateAnimator.ReconcileOpen(
            pwd, revealed, AnimationModel.EaseOut(Duration.Ms(200)));
        float revealT = ControlStateAnimator.GetOpenProgress(pwd);

        // Background — the field surface never tints on focus (consistent with
        // TextInput; emphasis is the thin accent border + soft ring, not a blue fill).
        var bg = disabled ? t.DisabledBackground : t.Background;
        ctx.DrawRect(inputBounds, bg, radius: t.Radius);

        // Border — interpolate between default and focus states
        {
            var defaultBorderColor = disabled
                ? t.DisabledBorderColor
                : (hoverT > 0.001f
                    ? ColorValue.Lerp(t.BorderColor, t.FocusBorderColor, hoverT * 0.3f)
                    : t.BorderColor);
            var borderColor = ColorValue.Lerp(defaultBorderColor, t.FocusBorderColor, focusT);
            float borderWidth = LerpF(t.BorderWidth, t.FocusBorderWidth, focusT);
            ctx.DrawRect(inputBounds, stroke: new Stroke(borderColor, borderWidth), radius: t.Radius);

            // Focus ring — fades in
            if (t.FocusRingWidth > 0 && focusT > 0.001f)
            {
                float ringOffset = t.FocusBorderWidth;
                var ringRect = new Rect(
                    inputBounds.X - ringOffset,
                    inputBounds.Y - ringOffset,
                    inputBounds.Width + ringOffset * 2,
                    inputBounds.Height + ringOffset * 2);
                ctx.DrawRect(ringRect,
                    stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), t.FocusRingWidth * focusT),
                    radius: t.Radius + ringOffset);
            }
        }

        // Get text from buffer (when focused) or bindable
        string text = focused && InputDispatcher.PasswordEditBuffer != null
            ? InputDispatcher.PasswordEditBuffer
            : (pwd.Value.Value ?? "");

        bool masked = !revealed;

        float fontSize = theme.Typography.Scale.Body.Size;
        float toggleBtnW = pwd.ShowToggleButton ? 32f : 0f;
        float toggleMargin = pwd.ShowToggleButton ? 4f : 0f;
        float scrollX = focused ? InputDispatcher.PasswordScrollOffsetX : 0f;
        float availableTextWidth = inputBounds.Width - t.PaddingH * 2 - toggleBtnW - toggleMargin;

        // Clip text content to input area excluding the toggle button
        var textClipBounds = new Rect(
            inputBounds.X, inputBounds.Y,
            inputBounds.Width - toggleBtnW - toggleMargin, inputBounds.Height);
        var contentClip = ctx.PushRoundedClip(textClipBounds, t.Radius);

        // Animate placeholder visibility
        float placeholderTarget = string.IsNullOrEmpty(text) ? 0f : 1f;
        ControlStateAnimator.ReconcileValue(pwd, placeholderTarget, AnimationModel.EaseOut(Duration.Ms(120)));
        float placeholderHidden = anim.Value.Current;

        if (placeholderHidden < 0.999f)
        {
            string placeholder = pwd.Placeholder.Resolve();
            if (!string.IsNullOrEmpty(placeholder))
            {
                float placeholderOpacity = 1f - placeholderHidden;
                PaintText(placeholder, textClipBounds, t.PaddingH, t.PlaceholderColor.Opacity(placeholderOpacity));
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            string displayText = masked ? new string('●', text.Length) : text;
            var textColor = disabled ? t.DisabledTextColor : t.TextColor;

            // Measure full text advance width (no wrapping)
            var textSize = ctx.MeasureTextAdvance(displayText, fontSize);
            float textY = inputBounds.Y + (inputHeight - textSize.Height) / 2f;
            textY = MathF.Round(textY);

            // Draw text with infinite maxWidth (never wraps) and apply horizontal scroll
            float textX = MathF.Round(inputBounds.X + t.PaddingH - scrollX);
            ctx.DrawText(displayText, textX, textY, fontSize, textColor,
                maxWidth: float.PositiveInfinity, maxLines: 1);
        }

        // Smooth caret blink — inside clip so caret doesn't overlap the eye icon
        if (focused && !disabled)
        {
            var caret = theme.Caret;
            double blinkMs = caret.BlinkInterval.TotalMilliseconds;
            double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;

            float caretOpacity;
            if (elapsed < blinkMs)
            {
                caretOpacity = 1f;
            }
            else
            {
                double phase = (elapsed % blinkMs) / blinkMs * Math.PI * 2.0;
                caretOpacity = (float)(0.5 + 0.5 * Math.Cos(phase));
            }

            if (caretOpacity > 0.01f)
            {
                string displayText = masked ? new string('●', text.Length) : text;
                float caretTextWidth = string.IsNullOrEmpty(displayText)
                    ? 0f
                    : ctx.MeasureTextAdvance(displayText, fontSize).Width;
                float caretX = inputBounds.X + t.PaddingH + caretTextWidth - scrollX;
                float caretPadY = 6f;
                float caretY = inputBounds.Y + caretPadY;
                float caretH = inputHeight - caretPadY * 2;
                ctx.DrawRect(new Rect(caretX, caretY, caret.Width, caretH),
                    caret.Color.Opacity(caretOpacity));

                // Auto-scroll horizontally to keep caret visible
                float caretRelX = caretTextWidth;
                if (caretRelX - scrollX > availableTextWidth)
                {
                    InputDispatcher.PasswordScrollOffsetX = caretRelX - availableTextWidth;
                }
                else if (caretRelX < scrollX)
                {
                    InputDispatcher.PasswordScrollOffsetX = caretRelX;
                }
            }
        }

        // Dispose clip before painting the eye icon (which sits outside the text area)
        contentClip.Dispose();

        // Show/hide toggle button (eye icon) — rotates 180° on toggle
        if (pwd.ShowToggleButton && !disabled)
        {
            float btnX = inputBounds.X + inputBounds.Width - toggleBtnW - 4f;
            float btnY = inputBounds.Y + (inputHeight - 16f) / 2f;
            float iconCenterX = btnX + 12f;
            float iconCenterY = btnY + 8f;
            var eyeColor = theme.Colors.TextMuted;

            // Rotation from reveal animation: 0° (masked) → 180° (revealed)
            float rotationDeg = revealT * 180f;
            bool drawAsOpen = revealT > 0.5f;

            if (!ControlStateAnimator.ReducedMotion && MathF.Abs(rotationDeg) > 0.1f
                && MathF.Abs(rotationDeg - 180f) > 0.1f)
            {
                // Mid-transition: scale + rotate the icon for polish
                float midScale = 1f - 0.15f * MathF.Sin(revealT * MathF.PI);
                var iconOrigin = new Point(iconCenterX, iconCenterY);
                using var scale = ctx.PushScale(midScale, midScale, iconOrigin);
                using var rotate = ctx.PushRotate(Angle.Degrees(rotationDeg), iconOrigin);

                PaintEyeIcon(iconCenterX, iconCenterY, eyeColor, drawAsOpen);
            }
            else
            {
                PaintEyeIcon(iconCenterX, iconCenterY, eyeColor, !masked);
            }
        }

        // Segmented strength indicator bar (4 segments with stagger fill)
        if (pwd.UseStrengthIndicator && !string.IsNullOrEmpty(text))
        {
            float barY = inputBounds.Y + inputHeight + 4f;
            float barH = 4f;
            float barW = inputBounds.Width;
            const int segmentCount = 4;
            float segGap = 3f;
            float segW = (barW - segGap * (segmentCount - 1)) / segmentCount;

            // Evaluate strength
            var evaluator = pwd.CustomStrengthEvaluator ?? PasswordStrengthEvaluator.Evaluate;
            var strength = evaluator(text);
            int score = PasswordStrengthEvaluator.CalculateScore(text);

            // Determine how many segments should be filled (1-4)
            int filledCount = strength switch
            {
                PasswordStrength.Weak => 1,
                PasswordStrength.Fair => 2,
                PasswordStrength.Good => 3,
                PasswordStrength.Strong => 4,
                _ => 1
            };

            // Segment colors based on fill position
            ColorValue[] segColors =
            [
                theme.Colors.Danger,    // red
                theme.Colors.Warning,   // orange
                theme.Colors.Primary,   // blue/teal
                theme.Colors.Success,   // green
            ];

            for (int i = 0; i < segmentCount; i++)
            {
                float segX = inputBounds.X + i * (segW + segGap);

                // Track (always visible)
                ctx.DrawRect(new Rect(segX, barY, segW, barH),
                    theme.Colors.Border.Opacity(0.2f), radius: 2f);

                // Filled segment with stagger delay
                if (i < filledCount)
                {
                    // Color for this segment position based on total strength
                    var segColor = segColors[Math.Min(filledCount - 1, segColors.Length - 1)];

                    // Partial fill within the segment based on exact score
                    float segFillFrac = 1f;
                    if (i == filledCount - 1)
                    {
                        float segScoreBase = i * 25f;
                        segFillFrac = Math.Clamp((score - segScoreBase) / 25f, 0.15f, 1f);
                    }

                    ctx.DrawRect(new Rect(segX, barY, segW * segFillFrac, barH),
                        segColor, radius: 2f);
                }
            }
        }
    }

    /// <summary>Paints an eye icon centered at the given position.</summary>
    private void PaintEyeIcon(float cx, float cy, ColorValue color, bool isOpen)
    {
        if (isOpen)
        {
            ctx.DrawArc(new Point(cx, cy), 8f,
                Angle.Degrees(0), Angle.Degrees(360),
                new Stroke(color, 1.5f));
            ctx.DrawRect(new Rect(cx - 3f, cy - 3f, 6f, 6f), color, radius: 3f);
        }
        else
        {
            ctx.DrawLine(
                new Point(cx - 10f, cy),
                new Point(cx + 10f, cy),
                new Stroke(color, 1.5f, StrokeCap.Round, StrokeJoin.Miter));
            ctx.DrawArc(new Point(cx, cy), 6f,
                Angle.Degrees(0), Angle.Degrees(180),
                new Stroke(color, 1.5f, StrokeCap.Round, StrokeJoin.Miter));
        }
    }

    // ── TextArea ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the index of the laid-out line that begins exactly at the given
    /// character offset, or -1 if none does (e.g. a caret on a not-yet-emitted
    /// trailing empty line after a final newline).
    /// </summary>
    private static int LayoutLineStartingAt(TextLayoutResult layout, int offset)
    {
        var lines = layout.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TextStart == offset)
            {
                return i;
            }
        }
        return -1;
    }

    private void PaintTextArea(TextArea ta, Rect bounds)
    {
        var t = theme.TextInput;
        bool disabled = ta.IsDisabled;

        // Set absolute bounds for mouse hit-testing in InputDispatcher
        ta.AbsoluteBounds = new Rect(absoluteX, absoluteY, bounds.Width, bounds.Height);

        bool focused = ReferenceEquals(FocusManager.FocusedElement, ta);

        // Keep TextAreaAbsoluteBounds current for the active TextArea (used by scroll handler)
        if (focused)
        {
            InputDispatcher.TextAreaAbsoluteBounds = ta.AbsoluteBounds;
        }

        // Animated focus/hover/disabled transitions
        var hoverModel = t.Transition.Model;
        var pressModel = AnimationModel.Spring.Snappy;
        hoverModel = GetScrollViewAwareHoverModel(hoverModel);
        pressModel = GetScrollViewAwarePressModel(pressModel);
        bool isFocused = focused && !disabled;
        var anim = ControlStateAnimator.Reconcile(
            ta, hoverModel, pressModel, isDisabled: disabled, isFocused: isFocused);

        float focusT = anim.Focus.Current;
        float hoverT = anim.Hover.Current;

        // Background — the field surface never tints on focus (Apple keeps the
        // control background; emphasis comes from the thin accent border + soft
        // ring, matching TextInput. An earlier 8% focus-colour inner glow made the
        // surface read as translucent light blue instead of the control colour.)
        var bg = disabled ? t.DisabledBackground : t.Background;
        ctx.DrawRect(bounds, bg, radius: t.Radius);

        // Border — interpolate between default and focus states
        {
            var defaultBorderColor = disabled
                ? t.DisabledBorderColor
                : (hoverT > 0.001f
                    ? ColorValue.Lerp(t.BorderColor, t.FocusBorderColor, hoverT * 0.3f)
                    : t.BorderColor);
            var borderColor = ColorValue.Lerp(defaultBorderColor, t.FocusBorderColor, focusT);
            float borderWidth = LerpF(t.BorderWidth, t.FocusBorderWidth, focusT);
            ctx.DrawRect(bounds, stroke: new Stroke(borderColor, borderWidth), radius: t.Radius);

            // Focus ring — fades in
            if (t.FocusRingWidth > 0 && focusT > 0.001f)
            {
                float ringOffset = t.FocusBorderWidth;
                var ringRect = new Rect(
                    bounds.X - ringOffset,
                    bounds.Y - ringOffset,
                    bounds.Width + ringOffset * 2,
                    bounds.Height + ringOffset * 2);
                ctx.DrawRect(ringRect,
                    stroke: new Stroke(t.FocusRingColor.ScaleAlpha(focusT), t.FocusRingWidth * focusT),
                    radius: t.Radius + ringOffset);
            }
        }

        // Clip content to control bounds to prevent text/caret overflow
        using var contentClip = ctx.PushRoundedClip(bounds, t.Radius);

        // Get text from buffer (when focused) or bindable
        string text = focused && InputDispatcher.TextAreaEditBuffer != null
            ? InputDispatcher.TextAreaEditBuffer
            : (ta.Value.Value ?? "");

        float fontSize = theme.Typography.Scale.Body.Size;
        float paddingH = t.PaddingH;
        float paddingV = 8f;

        // Reserve a strip at the bottom for the floating character count so the last
        // line of text never runs underneath it. (Text glyphs are drawn in a pass above
        // shapes, so a background behind the count can't mask them — the content area
        // simply must not extend into the count's row.)
        float countReserve = ta.CharacterCountStyle.HasValue
            ? paddingV + ctx.MeasureText("0/0", fontSize - 2f).Height + 2f
            : 0f;

        // Use font metrics for line height instead of arbitrary constant
        float lineHeight = ctx.MeasureText("Xg", fontSize).Height;

        float scrollY = focused ? InputDispatcher.TextAreaScrollOffsetY : 0f;

        float contentLeft = bounds.X + paddingH;
        float contentTop = bounds.Y + paddingV;
        float contentBottom = bounds.Y + bounds.Height - countReserve;
        float contentWidth = bounds.Width - paddingH * 2;

        // Soft word-wrap: lay the text out to the content width via the shared text
        // layout engine so long lines wrap to the next visual line instead of running
        // the caret off the right edge. The same layout drives the caret, selection,
        // and — via the parameters stamped below — the input dispatcher's caret
        // hit-testing and vertical navigation, so painter and input never disagree on
        // where lines wrap. The engine caches on (text, options), so building the same
        // options in both places is a cache hit, not a recompute.
        string? fontPath = ctx.DefaultFontPath;
        TextLayoutResult? layout = null;
        if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(fontPath) && contentWidth > 0)
        {
            var layoutOptions = new TextLayoutOptions
            {
                FontPath = fontPath,
                FontSize = fontSize,
                MaxWidth = contentWidth,
                MaxLines = 0,
                Overflow = TextOverflow.Clip,
            };
            layout = TextLayoutEngine.Layout(text, layoutOptions);
        }

        if (focused)
        {
            InputDispatcher.TextAreaFontPath = fontPath;
            InputDispatcher.TextAreaFontSize = fontSize;
            InputDispatcher.TextAreaContentWidth = contentWidth;
            InputDispatcher.TextAreaPaddingH = paddingH;
            InputDispatcher.TextAreaPaddingV = paddingV;
        }

        // Animate placeholder visibility
        float placeholderTarget = string.IsNullOrEmpty(text) ? 0f : 1f;
        ControlStateAnimator.ReconcileValue(ta, placeholderTarget, AnimationModel.EaseOut(Duration.Ms(120)));
        float placeholderHidden = anim.Value.Current;

        if (layout is null)
        {
            InputDispatcher.TextAreaMaxScrollY = 0f;
            InputDispatcher.TextAreaScrollbarTrackBounds = default;

            if (placeholderHidden < 0.999f)
            {
                string placeholder = ta.Placeholder.Resolve();
                if (!string.IsNullOrEmpty(placeholder))
                {
                    float placeholderOpacity = 1f - placeholderHidden;
                    // Top-aligned, matching how the first text line is drawn, so the
                    // placeholder and typed text share a baseline.
                    ctx.DrawText(placeholder, contentLeft, contentTop, fontSize,
                        t.PlaceholderColor.Opacity(placeholderOpacity), fontPath: fontPath);
                }
            }
        }
        else
        {
            var textColor = disabled ? t.DisabledTextColor : t.TextColor;

            // Clamp scroll offset so content can't scroll past the end of the
            // wrapped text. A trailing newline adds an (unemitted) empty final line
            // the caret can sit on, so reserve a row for it too.
            float totalContentHeight = layout.BoundingBox.Height;
            if (text[^1] == '\n' && layout.Lines.Count > 0)
            {
                totalContentHeight += layout.Lines[^1].Height;
            }
            float viewportHeight = bounds.Height - paddingV * 2 - countReserve;
            float maxScroll = Math.Max(0f, totalContentHeight - viewportHeight);
            InputDispatcher.TextAreaMaxScrollY = maxScroll;
            if (scrollY > maxScroll)
            {
                scrollY = maxScroll;
                if (focused)
                {
                    InputDispatcher.TextAreaScrollOffsetY = scrollY;
                }
            }

            // Compute selection range for highlight rendering
            int selStart = -1;
            int selEnd = -1;
            if (focused && InputDispatcher.TextAreaSelectionAnchor != InputDispatcher.TextAreaCaretIndex)
            {
                selStart = Math.Min(InputDispatcher.TextAreaSelectionAnchor, InputDispatcher.TextAreaCaretIndex);
                selEnd = Math.Max(InputDispatcher.TextAreaSelectionAnchor, InputDispatcher.TextAreaCaretIndex);
            }

            var lines = layout.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                float textY = contentTop + line.Y - scrollY;

                if (textY + line.Height <= contentTop)
                {
                    continue; // above viewport
                }
                if (textY >= contentBottom)
                {
                    break; // below viewport (lines are ordered top-to-bottom)
                }

                int lineStart = line.TextStart;
                int lineEnd = line.TextStart + line.TextLength;

                // Selection highlight for the selected portion of this visual line
                if (selStart >= 0 && selEnd > lineStart && selStart <= lineEnd)
                {
                    int hlStart = Math.Max(selStart, lineStart);
                    int hlEnd = Math.Min(selEnd, lineEnd);
                    if (hlEnd > hlStart)
                    {
                        string prefix = text.Substring(lineStart, hlStart - lineStart);
                        string selected = text.Substring(hlStart, hlEnd - hlStart);
                        float hlX = contentLeft +
                            (prefix.Length == 0 ? 0f : ctx.MeasureTextAdvance(prefix, fontSize).Width);
                        float hlW = ctx.MeasureTextAdvance(selected, fontSize).Width;
                        if (hlW > 0)
                        {
                            ctx.DrawRect(new Rect(hlX, textY, hlW, line.Height),
                                theme.Colors.Primary.Opacity(0.3f));
                        }
                    }
                    else if (selStart < lineEnd && selEnd > lineEnd)
                    {
                        // Selection continues past the end of this line (spans a line
                        // break) — a thin trailing marker signals the included newline.
                        // The `selStart < lineEnd` guard is essential: a line's range
                        // includes its trailing '\n', so lineEnd equals the NEXT line's
                        // start. Without it, a selection that begins exactly at the next
                        // line (e.g. Ctrl+Shift+End from a line start) would draw a
                        // spurious marker on the line above, reading as "1 char selected"
                        // from that line. When selStart == lineEnd the break is not in
                        // the selection, so no marker.
                        ctx.DrawRect(new Rect(contentLeft + line.Width, textY, fontSize * 0.4f, line.Height),
                            theme.Colors.Primary.Opacity(0.3f));
                    }
                }

                if (line.TextLength > 0)
                {
                    string lineText = text.Substring(lineStart, line.TextLength);
                    // Draw at the engine's line top (DrawText places the baseline at
                    // top + ascent, uniform for every line). PaintText is wrong here:
                    // its single-line *visual centring* shifts each line by its own ink
                    // box, so lines with tall ascenders/descenders crowd or spread and
                    // the wrapped spacing looks ragged.
                    ctx.DrawText(lineText, contentLeft, textY, fontSize, textColor, fontPath: fontPath);
                }
            }
        }

        // Smooth caret blink
        if (focused && !disabled)
        {
            var caret = theme.Caret;
            double blinkMs = caret.BlinkInterval.TotalMilliseconds;
            double elapsed = Stopwatch.GetElapsedTime(InputDispatcher.CaretResetTimestamp).TotalMilliseconds;

            float caretOpacity;
            if (elapsed < blinkMs)
            {
                caretOpacity = 1f;
            }
            else
            {
                double phase = (elapsed % blinkMs) / blinkMs * Math.PI * 2.0;
                caretOpacity = (float)(0.5 + 0.5 * Math.Cos(phase));
            }

            if (caretOpacity > 0.01f)
            {
                // Caret geometry from the same wrapped layout so it lands exactly at
                // the visual position of the character index (soft-wrap aware).
                string caretBuf = text ?? "";
                int caretIdx = Math.Clamp(InputDispatcher.TextAreaCaretIndex, 0, caretBuf.Length);
                float caretRelX;
                float caretRelY;
                float caretH;
                if (layout is not null && layout.Lines.Count > 0)
                {
                    // A caret right after a newline always belongs at the START of the
                    // next row (downstream affinity). GetCaretInfo can't express this:
                    // the previous line's TextLength includes the '\n', so its range
                    // overlaps the next line's start and GetCaretInfo returns the
                    // previous line — leaving the caret stuck at the end of the old
                    // line after pressing Enter until a character is typed. Handle it
                    // explicitly: jump to the line that starts at the caret, or (for a
                    // trailing '\n' the engine emits no line for) a fresh row below.
                    int lineAtCaret = caretIdx > 0 && caretBuf[caretIdx - 1] == '\n'
                        ? LayoutLineStartingAt(layout, caretIdx)
                        : -2;
                    if (lineAtCaret >= 0)
                    {
                        var startLine = layout.Lines[lineAtCaret];
                        caretRelX = 0f;
                        caretRelY = startLine.Y;
                        caretH = startLine.Height;
                    }
                    else if (lineAtCaret == -1)
                    {
                        var prevLine = layout.Lines[layout.GetLineIndexForOffset(caretIdx - 1)];
                        caretRelX = 0f;
                        caretRelY = prevLine.Y + prevLine.Height;
                        caretH = prevLine.Height;
                    }
                    else
                    {
                        var caretInfo = layout.GetCaretInfo(caretIdx);
                        caretRelX = caretInfo.X;
                        caretRelY = caretInfo.Y;
                        caretH = caretInfo.Height;
                    }
                }
                else
                {
                    caretRelX = 0f;
                    caretRelY = 0f;
                    caretH = lineHeight;
                }

                float caretX = contentLeft + caretRelX;
                float caretY = contentTop + caretRelY - scrollY;

                // Only draw if caret is within viewport
                if (caretY + caretH > bounds.Y && caretY < bounds.Y + bounds.Height)
                {
                    ctx.DrawRect(
                        new Rect(caretX, caretY, caret.Width, caretH),
                        caret.Color.Opacity(caretOpacity));
                }

                // Auto-scroll to keep caret visible — only when caret has moved
                // (prevents mouse wheel scroll from being undone by auto-scroll)
                if (caretIdx != InputDispatcher.TextAreaLastAutoScrollCaret)
                {
                    float viewportH = bounds.Height - paddingV * 2;
                    if (caretRelY < scrollY)
                    {
                        InputDispatcher.TextAreaScrollOffsetY = caretRelY;
                    }
                    else if (caretRelY + caretH > scrollY + viewportH)
                    {
                        InputDispatcher.TextAreaScrollOffsetY = caretRelY + caretH - viewportH;
                    }
                    InputDispatcher.TextAreaLastAutoScrollCaret = caretIdx;
                }
            }
        }

        // Scrollbar when content overflows
        if (InputDispatcher.TextAreaMaxScrollY > 0)
        {
            float maxScr = InputDispatcher.TextAreaMaxScrollY;
            const float scrollbarWidth = 5f;
            const float scrollbarMargin = 2f;
            float scrollbarPadV = t.Radius;
            float scrollbarX = bounds.X + bounds.Width - scrollbarWidth - scrollbarMargin;
            float trackHeight = bounds.Height - scrollbarPadV * 2;

            float contentHeight = bounds.Height + maxScr;
            float thumbRatio = bounds.Height / contentHeight;
            float thumbHeight = Math.Max(20f, trackHeight * thumbRatio);
            float trackRange = trackHeight - thumbHeight;
            float thumbY = bounds.Y + scrollbarPadV + (maxScr > 0 ? (scrollY / maxScr) * trackRange : 0f);

            // Track (subtle)
            var trackBounds = new Rect(scrollbarX, bounds.Y + scrollbarPadV, scrollbarWidth, trackHeight);
            ctx.DrawRect(trackBounds, theme.Colors.Border.Opacity(0.1f), radius: 2.5f);

            // Thumb
            var thumbBounds = new Rect(scrollbarX, thumbY, scrollbarWidth, thumbHeight);
            ctx.DrawRect(thumbBounds, theme.Colors.Text.Opacity(0.3f), radius: 2.5f);

            // Store scrollbar geometry for InputDispatcher drag support
            InputDispatcher.TextAreaScrollbarTrackBounds = new Rect(
                absoluteX + scrollbarX - scrollbarMargin,
                absoluteY + bounds.Y + scrollbarPadV,
                scrollbarWidth + scrollbarMargin * 2,
                trackHeight);
            InputDispatcher.TextAreaScrollbarThumbHeight = thumbHeight;
        }
        else
        {
            InputDispatcher.TextAreaScrollbarTrackBounds = default;
        }

        // Character count with color ramping (green→yellow→red as limit approaches)
        if (ta.CharacterCountStyle.HasValue)
        {
            int len = (ta.Value.Value ?? "").Length;
            int? maxLen = ta.MaxLengthValue;
            string countText = maxLen.HasValue ? $"{len}/{maxLen}" : $"{len}";
            float countFontSize = theme.Typography.Scale.Body.Size - 2f;

            ColorValue countColor;
            if (maxLen.HasValue && maxLen.Value > 0)
            {
                float ratio = (float)len / maxLen.Value;
                if (ratio > 1f)
                {
                    countColor = theme.Colors.Danger;
                }
                else if (ratio > 0.9f)
                {
                    // 90-100%: yellow→red
                    float t2 = (ratio - 0.9f) / 0.1f;
                    countColor = ColorValue.Lerp(theme.Colors.Warning, theme.Colors.Danger, t2);
                }
                else if (ratio > 0.75f)
                {
                    // 75-90%: green→yellow
                    float t2 = (ratio - 0.75f) / 0.15f;
                    countColor = ColorValue.Lerp(theme.Colors.Success, theme.Colors.Warning, t2);
                }
                else
                {
                    countColor = theme.Colors.TextMuted;
                }
            }
            else
            {
                countColor = theme.Colors.TextMuted;
            }

            var countSize = ctx.MeasureText(countText, countFontSize);
            var countBounds = new Rect(
                bounds.X + bounds.Width - paddingH - countSize.Width,
                bounds.Y + bounds.Height - paddingV - countSize.Height,
                countSize.Width, countSize.Height);
            PaintText(countText, countBounds, 0, countColor, fontSize: countFontSize);
        }
    }

    // ── Toast Overlay Painting ──────────────────────────────────────────

    private void PaintToasts()
    {
        // Remove expired toasts
        Toast.RemoveExpired();

        var toasts = Toast.ActiveToasts;
        if (toasts.Count == 0)
        {
            Toast.HitZones.Clear();
            return;
        }

        HasActiveToasts = true;
        Toast.HitZones.Clear();

        var tt = theme.Toast;
        float viewportW = ViewportLogicalWidth;
        float viewportH = ViewportLogicalHeight;
        float fontSize = tt.TextStyle.Size;
        float actionFontSize = tt.ActionTextStyle.Size;
        float dismissSize = 20f;
        float iconSize = 18f;
        float iconGap = 8f;

        // Compute toast layout for each entry (bottom-right, stacking upward)
        float cursorY = viewportH - tt.Margin;

        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var entry = toasts[i];
            var opts = entry.Options;

            // Measure message text height (may wrap)
            float contentLeftPad = tt.AccentBarWidth + tt.Padding.Left;
            if (opts.Type != ToastType.Default)
            {
                contentLeftPad += iconSize + iconGap;
            }
            float contentRightPad = tt.Padding.Right + dismissSize + 4f;
            float textAvailWidth = tt.Width - contentLeftPad - contentRightPad;

            float textHeight;
            string? resolvedFont = ctx.DefaultFontPath;
            if (!string.IsNullOrEmpty(resolvedFont))
            {
                var msgLayoutOptions = new TextLayoutOptions
                {
                    FontPath = resolvedFont,
                    FontSize = fontSize,
                    MaxWidth = textAvailWidth,
                    MaxLines = 3,
                    Overflow = TextOverflow.Ellipsis,
                };
                var msgLayoutResult = TextLayoutEngine.Layout(opts.Message, msgLayoutOptions);
                textHeight = Math.Max(msgLayoutResult.BoundingBox.Height, fontSize * 1.3f);
            }
            else
            {
                // Approximate fallback when no font is available
                float avgCharWidthRatio = 0.6f;
                float lineHeightRatio = 1.2f;
                float charsPerLine = textAvailWidth / (fontSize * avgCharWidthRatio);
                int lines = Math.Min(3, (int)Math.Ceiling(opts.Message.Length / Math.Max(charsPerLine, 1f)));
                textHeight = Math.Max(lines * fontSize * lineHeightRatio, fontSize * 1.3f);
            }

            // Action button height (if present)
            float actionHeight = 0f;
            float actionWidth = 0f;
            if (opts.Action != null)
            {
                actionWidth = MeasureTextWidth(opts.Action.Label, actionFontSize) + 16f;
                actionHeight = actionFontSize * 1.6f + 4f;
            }

            float toastHeight = tt.Padding.Top + textHeight + actionHeight + tt.Padding.Bottom;
            toastHeight = Math.Max(toastHeight, 44f);

            float toastX = viewportW - tt.Margin - tt.Width;
            float toastY = cursorY - toastHeight;
            var toastBounds = new Rect(toastX, toastY, tt.Width, toastHeight);

            // Don't paint toasts that overflow above the viewport
            if (toastY < 0)
            {
                break;
            }

            // Entrance animation: slide in from right with fade
            bool toastReducedMotion = ControlStateAnimator.ReducedMotion;
            float entranceT = 1f;
            if (!toastReducedMotion)
            {
                long ageMs = Environment.TickCount64 - entry.CreatedTick;
                entranceT = Math.Clamp(ageMs / 250f, 0f, 1f);
                // Ease-out cubic
                float t = 1f - entranceT;
                entranceT = 1f - (t * t * t);
            }

            ScopeGuard toastSlideScope = default;
            ScopeGuard toastFadeScope = default;
            if (entranceT < 0.999f)
            {
                float slideX = (1f - entranceT) * (tt.Width + tt.Margin);
                toastSlideScope = ctx.PushTranslate(slideX, 0);
                toastFadeScope = ctx.PushOpacity(entranceT);
                ControlStateAnimator.SignalActiveTransition();
            }

            // Shadow
            PaintShadow(tt.Shadow, toastBounds, tt.Radius);

            // Background
            ctx.DrawRect(toastBounds, tt.Background, radius: tt.Radius);

            // Accent bar on the left edge (for typed toasts)
            ColorValue? accentColor = opts.Type switch
            {
                ToastType.Info => tt.InfoAccent,
                ToastType.Success => tt.SuccessAccent,
                ToastType.Warning => tt.WarningAccent,
                ToastType.Error => tt.ErrorAccent,
                _ => null
            };

            if (accentColor != null)
            {
                // Full-height accent stripe: a square bar spanning the whole toast, clipped
                // to the toast's rounded rect so its top-left/bottom-left corners follow the
                // border curve while it still reaches the very top and bottom edges.
                using var stripeClip = ctx.PushRoundedClip(toastBounds, tt.Radius);
                var barBounds = new Rect(toastX, toastY, tt.AccentBarWidth, toastHeight);
                ctx.DrawRect(barBounds, accentColor.Value);
            }

            // Type icon
            float textX = toastX + tt.AccentBarWidth + tt.Padding.Left;
            float textY = toastY + tt.Padding.Top;
            if (opts.Type != ToastType.Default && accentColor != null)
            {
                string iconGlyph = opts.Type switch
                {
                    ToastType.Info => "i",
                    ToastType.Success => "\u2713",
                    ToastType.Warning => "!",
                    ToastType.Error => "\u00D7",
                    _ => ""
                };
                var iconBounds = new Rect(textX, textY, iconSize, textHeight);
                PaintText(iconGlyph, iconBounds, 0, accentColor.Value,
                    fontSize: iconSize, alignment: TextAlignment.Center);
                textX += iconSize + iconGap;
            }

            // Message text
            float msgAreaWidth = tt.Width - (textX - toastX) - contentRightPad;
            var msgBounds = new Rect(textX, textY, msgAreaWidth, textHeight);
            PaintText(opts.Message, msgBounds, 0, tt.TextColor,
                fontSize: fontSize, maxLines: 3,
                overflow: TextOverflow.Ellipsis);

            // Dismiss (×) button
            float dismissX = toastX + tt.Width - tt.Padding.Right - dismissSize;
            float dismissY = toastY + tt.Padding.Top;
            var dismissBounds = new Rect(dismissX, dismissY, dismissSize, dismissSize);
            PaintText("×", dismissBounds, 0, tt.DismissColor,
                fontSize: 14f, alignment: TextAlignment.Center);

            // Action button
            Rect actionBounds = default;
            if (opts.Action != null)
            {
                float actionX = textX;
                float actionY = textY + textHeight + 2f;
                actionBounds = new Rect(actionX, actionY, actionWidth, actionHeight - 4f);
                PaintText(opts.Action.Label, actionBounds, 4f, tt.ActionColor,
                    fontSize: actionFontSize,
                    fontWeight: FontWeight.SemiBold);
            }

            // Store hit zone for input dispatcher
            Toast.HitZones.Add(new ToastHitZone
            {
                Id = entry.Id,
                Bounds = toastBounds,
                ActionBounds = actionBounds,
                OnAction = opts.Action?.OnClick
            });

            toastFadeScope.Dispose();
            toastSlideScope.Dispose();

            cursorY = toastY - tt.Gap;
        }
    }

    private static readonly Dictionary<int, long> qrFirstPaintTick = new();

    private void PaintQrCode(QrCode qr, Rect bounds)
    {
        var fg = qr.Foreground ?? theme.Colors.Text;
        var bg = qr.Background ?? theme.Colors.Surface;

        ctx.DrawRect(bounds, bg);

        if (string.IsNullOrEmpty(qr.Content))
        {
            return;
        }

        bool[][]? matrix = qr.GetEncodedMatrix();
        if (matrix is null)
        {
            return;
        }

        int modules = matrix.Length;
        float moduleSize = bounds.Width / modules;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Entrance animation: random dissolve-in
        // Same visibility-gated pattern as ChartAnimationTracker: return 0 while
        // off-screen (clipped anyway), start animating on first visible frame.
        const double qrAnimMs = 800.0;
        float globalProgress = 1f;
        if (!reducedMotion)
        {
            int qrHash = qr.Content.GetHashCode(StringComparison.Ordinal);
            float centerY = absoluteY + bounds.Height * 0.5f;
            bool isVisible = centerY > 0 && centerY < ViewportLogicalHeight;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!qrFirstPaintTick.TryGetValue(qrHash, out long startTick))
            {
                globalProgress = 0f;
                if (isVisible)
                {
                    qrFirstPaintTick[qrHash] = now;
                }
            }
            else
            {
                double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTick).TotalMilliseconds;
                globalProgress = Math.Clamp((float)(elapsedMs / qrAnimMs), 0f, 1f);
            }
            if (globalProgress < 1f)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        for (int r = 0; r < modules; r++)
        {
            for (int c = 0; c < modules; c++)
            {
                if (matrix[r][c])
                {
                    float mx = bounds.X + c * moduleSize;
                    float my = bounds.Y + r * moduleSize;

                    if (globalProgress < 1f)
                    {
                        // Pseudo-random delay per module
                        uint mHash = (uint)((r * 31 + c * 97) ^ (r * c * 13));
                        float moduleDelay = (mHash % 100) / 100f * 0.7f;
                        float moduleT = Math.Clamp((globalProgress - moduleDelay) / 0.3f, 0f, 1f);
                        if (moduleT <= 0f)
                        {
                            continue;
                        }
                        ctx.DrawRect(new Rect(mx, my, moduleSize, moduleSize), fg.Opacity(moduleT));
                    }
                    else
                    {
                        ctx.DrawRect(new Rect(mx, my, moduleSize, moduleSize), fg);
                    }
                }
            }
        }
    }

    // ── HeatMapChart ────────────────────────────────────────────────────

    private void PaintHeatMap(HeatMapChart chart, Rect bounds)
    {
        if (chart.cellsList.Count == 0)
        {
            return;
        }

        // Animation setup
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        int animHash = 0;
        bool onScreen = false;

        if (animate)
        {
            animHash = ChartAnimationTracker.ComputeHeatMapChartHash(chart);
            onScreen = IsCurrentlyVisible(bounds);
        }

        // Reuse cached layout when cellsList identity is unchanged.
        // Reconciler transfers the cache across re-renders; this check
        // covers first-paint-after-ctor and in-place mutation callers
        // that forgot to call InvalidateLayoutCache().
        var layout = chart.layoutCache;
        if (layout == null || !ReferenceEquals(chart.layoutCacheKey, chart.cellsList))
        {
            layout = BuildHeatMapLayout(chart);
            chart.layoutCache = layout;
            chart.layoutCacheKey = chart.cellsList;
        }

        int rows = layout.Rows;
        int cols = layout.Cols;
        double minVal = layout.MinVal;
        double maxVal = layout.MaxVal;

        // Layout: row labels on left, column labels on top
        float leftPad = 50f;
        float topPad = 24f;
        float bottomPad = 4f;
        float rightPad = 4f;

        // Measure max row label width
        float maxRowLabelW = 0f;
        for (int i = 0; i < rows; i++)
        {
            var sz = ctx.MeasureText(layout.RowLabels[i], 10f);
            if (sz.Width > maxRowLabelW)
            {
                maxRowLabelW = sz.Width;
            }
        }
        leftPad = Math.Max(leftPad, maxRowLabelW + 8f);

        float plotX = bounds.X + leftPad;
        float plotY = bounds.Y + topPad;
        float plotW = bounds.Width - leftPad - rightPad;
        float plotH = bounds.Height - topPad - bottomPad;

        if (plotW <= 0 || plotH <= 0)
        {
            return;
        }

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.3f), 1f), radius: 6f);

        // Cell sizes
        float gap = chart.cellGap;
        float cellW = (plotW - gap * (cols - 1)) / cols;
        float cellH = (plotH - gap * (rows - 1)) / rows;

        // Default heat map colors
        var lowColor = chart.lowColor ?? new ColorValue("#1E3A5F");   // dark blue
        var highColor = chart.highColor ?? new ColorValue("#FF5722"); // red-orange
        var midColor = chart.midColor;
        var nullClr = chart.nullColor ?? theme.Colors.Border.Opacity(0.15f);

        int totalCells = rows * cols;

        // Lazily allocate value-label cache if needed.
        if (chart.showValueLabels && layout.ValueLabelText == null)
        {
            layout.ValueLabelText = new string?[totalCells];
        }

        // Draw column labels (stagger with entrance animation)
        for (int c = 0; c < cols; c++)
        {
            float colLabelOpacity = 1f;
            if (animate)
            {
                float colT = ChartAnimationTracker.GetCellProgress(
                    chart, animHash, chart.animateTrigger, c, totalCells, ChartAnimationTracker.HeatMapDuration, onScreen);
                colLabelOpacity = colT;
                if (colT < 1f && (colT > 0f || onScreen))
                {
                    HasActiveChartAnimations = true;
                }
            }
            if (colLabelOpacity > 0.01f)
            {
                float cx = plotX + c * (cellW + gap) + cellW / 2f;
                var labelSz = ctx.MeasureText(layout.ColLabels[c], 10f);
                ctx.DrawText(layout.ColLabels[c],
                    MathF.Round(cx - labelSz.Width / 2f),
                    MathF.Round(bounds.Y + 4f),
                    10f, theme.Colors.TextMuted.Opacity(colLabelOpacity));
            }
        }

        // Draw cells and row labels
        for (int r = 0; r < rows; r++)
        {
            float cy = plotY + r * (cellH + gap);

            // Row label (fades in with first cell in row)
            float rowLabelOpacity = 1f;
            if (animate)
            {
                int firstCellIdx = r * cols;
                float rowT = ChartAnimationTracker.GetCellProgress(
                    chart, animHash, chart.animateTrigger, firstCellIdx, totalCells, ChartAnimationTracker.HeatMapDuration, onScreen);
                rowLabelOpacity = rowT;
                if (rowT < 1f && (rowT > 0f || onScreen))
                {
                    HasActiveChartAnimations = true;
                }
            }
            if (rowLabelOpacity > 0.01f)
            {
                var rowLabelSz = ctx.MeasureText(layout.RowLabels[r], 10f);
                ctx.DrawText(layout.RowLabels[r],
                    MathF.Round(plotX - rowLabelSz.Width - 6f),
                    MathF.Round(cy + cellH / 2f - rowLabelSz.Height / 2f),
                    10f, theme.Colors.TextMuted.Opacity(rowLabelOpacity));
            }

            for (int c = 0; c < cols; c++)
            {
                int cellIdx = r * cols + c;
                float cx = plotX + c * (cellW + gap);
                var cellRect = new Rect(cx, cy, cellW, cellH);

                // Animation: staggered scale + opacity per cell
                float cellProgress = 1f;
                if (animate)
                {
                    cellProgress = ChartAnimationTracker.GetCellProgress(
                        chart, animHash, chart.animateTrigger, cellIdx, totalCells, ChartAnimationTracker.HeatMapDuration, onScreen);

                    if (cellProgress < 1f && (cellProgress > 0f || onScreen))
                    {
                        HasActiveChartAnimations = true;
                    }

                    if (cellProgress <= 0f)
                    {
                        continue;
                    }

                    // Scale from center
                    float scale = cellProgress;
                    float scaledW = cellW * scale;
                    float scaledH = cellH * scale;
                    cellRect = new Rect(
                        cx + (cellW - scaledW) / 2f,
                        cy + (cellH - scaledH) / 2f,
                        scaledW, scaledH);
                }

                bool hasVal = layout.HasValue[cellIdx];
                double cellVal = layout.Values[cellIdx];
                ColorValue cellColor;
                if (hasVal)
                {
                    float t = (float)((cellVal - minVal) / (maxVal - minVal));
                    t = Math.Clamp(t, 0f, 1f);

                    if (chart.customColorMapper != null)
                    {
                        cellColor = chart.customColorMapper(cellVal);
                    }
                    else if (chart.colorScale == HeatMapColorScale.Diverging && midColor.HasValue)
                    {
                        // Two-segment lerp: low→mid→high
                        cellColor = t < 0.5f
                            ? ColorValue.Lerp(lowColor, midColor.Value, t * 2f)
                            : ColorValue.Lerp(midColor.Value, highColor, (t - 0.5f) * 2f);
                    }
                    else
                    {
                        cellColor = ColorValue.Lerp(lowColor, highColor, t);
                    }
                }
                else
                {
                    cellColor = nullClr;
                }

                ctx.DrawRect(cellRect, cellColor, radius: chart.cellRadius);

                // Value label inside cell
                if (chart.showValueLabels && hasVal)
                {
                    // Cache formatted text per cell index. This eliminates
                    // ~totalCells string allocations per paint for the
                    // value-labels path.
                    string? valStr = layout.ValueLabelText![cellIdx];
                    if (valStr == null)
                    {
                        valStr = cellVal.ToString("F0");
                        layout.ValueLabelText[cellIdx] = valStr;
                    }

                    // Choose contrasting text color
                    float brightness = cellColor.R * 0.299f + cellColor.G * 0.587f + cellColor.B * 0.114f;
                    var textClr = brightness > 0.5f
                        ? new ColorValue("#000000").Opacity(0.8f)
                        : new ColorValue("#FFFFFF").Opacity(0.9f);

                    var valSz = ctx.MeasureText(valStr, 9f);
                    if (valSz.Width < cellW - 2f && valSz.Height < cellH - 2f)
                    {
                        ctx.DrawText(valStr,
                            MathF.Round(cx + cellW / 2f - valSz.Width / 2f),
                            MathF.Round(cy + cellH / 2f - valSz.Height / 2f),
                            9f, textClr);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds the row/column/grid layout for a HeatMapChart. Called once per
    /// unique cellsList identity; result is cached on the chart and reused
    /// across paints.
    /// </summary>
    private static HeatMapLayoutCache BuildHeatMapLayout(HeatMapChart chart)
    {
        var rowLabelsList = new List<string>();
        var colLabelsList = new List<string>();
        var rowSet = new Dictionary<string, int>();
        var colSet = new Dictionary<string, int>();

        // Pass 1: collect unique rows and columns in encounter order
        // while also indexing them for pass 2.
        foreach (var cell in chart.cellsList)
        {
            string rowStr = cell.Row?.ToString() ?? "";
            string colStr = cell.Column?.ToString() ?? "";
            if (!rowSet.ContainsKey(rowStr))
            {
                rowSet[rowStr] = rowLabelsList.Count;
                rowLabelsList.Add(rowStr);
            }
            if (!colSet.ContainsKey(colStr))
            {
                colSet[colStr] = colLabelsList.Count;
                colLabelsList.Add(colStr);
            }
        }

        int rows = rowLabelsList.Count;
        int cols = colLabelsList.Count;
        int total = rows * cols;

        var values = new double[total];
        var hasVal = new bool[total];
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;

        // Pass 2: fill the flat grid.
        foreach (var cell in chart.cellsList)
        {
            string rowStr = cell.Row?.ToString() ?? "";
            string colStr = cell.Column?.ToString() ?? "";
            int r = rowSet[rowStr];
            int c = colSet[colStr];
            int idx = r * cols + c;
            values[idx] = cell.Value;
            hasVal[idx] = true;
            if (cell.Value < minVal)
            {
                minVal = cell.Value;
            }
            if (cell.Value > maxVal)
            {
                maxVal = cell.Value;
            }
        }

        if (maxVal <= minVal)
        {
            maxVal = minVal + 1;
        }

        return new HeatMapLayoutCache
        {
            RowLabels = [.. rowLabelsList],
            ColLabels = [.. colLabelsList],
            Values = values,
            HasValue = hasVal,
            Rows = rows,
            Cols = cols,
            MinVal = minVal,
            MaxVal = maxVal
        };
    }

    // ── TreeMapChart ────────────────────────────────────────────────────

    private void PaintTreeMapChart(TreeMapChart chart, Rect bounds)
    {
        var chartTheme = ChartTheme.Default(theme);

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.15f), 1f), radius: 6f);

        if (chart.nodesList.Count == 0)
        {
            PaintText("No data", bounds, 0f, theme.Colors.Text.Opacity(0.4f),
                fontSize: 12f, alignment: TextAlignment.Center);
            return;
        }

        // Animation setup
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        int animHash = 0;
        bool onScreen = false;

        if (animate)
        {
            animHash = ChartAnimationTracker.ComputeTreeMapChartHash(chart);
            onScreen = IsCurrentlyVisible(bounds);
        }

        float gap = chart.cellGap;
        float radius = chart.cellRadius;

        // Flatten top-level nodes and compute total value
        var nodes = chart.nodesList;
        double totalValue = 0;
        foreach (var node in nodes)
        {
            totalValue += Math.Max(0, node.Value);
        }

        if (totalValue <= 0)
        {
            return;
        }

        // Squarified treemap layout using the Bruls et al. algorithm
        var rects = new List<(TreeMapNode Node, Rect Rect)>();
        LayoutTreeMapSquarified(nodes, new Rect(bounds.X + gap, bounds.Y + gap,
            bounds.Width - gap * 2, bounds.Height - gap * 2), totalValue, rects);

        // Paint each rectangle
        int totalRects = rects.Count;
        for (int i = 0; i < totalRects; i++)
        {
            var (node, rect) = rects[i];
            var inset = new Rect(rect.X + gap / 2f, rect.Y + gap / 2f,
                Math.Max(0, rect.Width - gap), Math.Max(0, rect.Height - gap));

            if (inset.Width < 1f || inset.Height < 1f)
            {
                continue;
            }

            // Animation: staggered scale from center
            float cellProgress = 1f;
            if (animate)
            {
                cellProgress = ChartAnimationTracker.GetCellProgress(
                    chart, animHash, chart.animateTrigger, i, totalRects, ChartAnimationTracker.TreeMapDuration, onScreen);

                if (cellProgress < 1f && (cellProgress > 0f || onScreen))
                {
                    HasActiveChartAnimations = true;
                }

                if (cellProgress <= 0f)
                {
                    continue;
                }

                // Scale from center of the inset rect
                float scale = cellProgress;
                float scaledW = inset.Width * scale;
                float scaledH = inset.Height * scale;
                inset = new Rect(
                    inset.X + (inset.Width - scaledW) / 2f,
                    inset.Y + (inset.Height - scaledH) / 2f,
                    scaledW, scaledH);
            }

            var color = node.ColorOverride ?? chartTheme.Palette.GetColor(i % chartTheme.Palette.Count);
            ctx.DrawRect(inset, color, radius: radius);

            // Label (smooth fade-in as cell animation completes)
            if (chart.showLabels && cellProgress > 0.5f)
            {
                float labelOpacity = Math.Clamp((cellProgress - 0.5f) / 0.5f, 0f, 1f);
                float areaFraction = (float)(node.Value / totalValue);
                if (areaFraction >= chart.labelMinArea && inset.Width > 20f && inset.Height > 16f)
                {
                    float fontSize = Math.Clamp(Math.Min(inset.Width, inset.Height) * 0.18f, 9f, 16f);
                    string label = node.Label;
                    var labelSz = ctx.MeasureText(label, fontSize);

                    // Truncate if too wide
                    if (labelSz.Width > inset.Width - 6f)
                    {
                        while (label.Length > 2 && labelSz.Width > inset.Width - 6f)
                        {
                            label = label[..^1];
                            labelSz = ctx.MeasureText(label + "…", fontSize);
                        }
                        label += "…";
                        labelSz = ctx.MeasureText(label, fontSize);
                    }

                    // Contrast-aware text color
                    float brightness = color.R * 0.299f + color.G * 0.587f + color.B * 0.114f;
                    var textClr = brightness > 0.5f
                        ? new ColorValue("#000000").Opacity(0.85f * labelOpacity)
                        : new ColorValue("#FFFFFF").Opacity(0.95f * labelOpacity);

                    // Center label in cell
                    float lx = inset.X + (inset.Width - labelSz.Width) / 2f;
                    float ly = inset.Y + (inset.Height - labelSz.Height) / 2f - fontSize * 0.15f;

                    ctx.DrawText(label, MathF.Round(lx), MathF.Round(ly), fontSize, textClr);

                    // Value below label if enough space
                    string valStr = node.Value.ToString("N0");
                    float valFontSize = fontSize * 0.75f;
                    var valSz = ctx.MeasureText(valStr, valFontSize);
                    float valY = ly + labelSz.Height + 2f;
                    if (valY + valSz.Height < inset.Y + inset.Height - 2f && valSz.Width < inset.Width - 6f)
                    {
                        ctx.DrawText(valStr,
                            MathF.Round(inset.X + (inset.Width - valSz.Width) / 2f),
                            MathF.Round(valY),
                            valFontSize, textClr.Opacity(0.7f));
                    }
                }
            }
        }
    }

    private static void LayoutTreeMapSquarified(IReadOnlyList<TreeMapNode> nodes, Rect bounds,
        double totalValue, List<(TreeMapNode Node, Rect Rect)> result)
    {
        if (nodes.Count == 0 || totalValue <= 0 || bounds.Width < 1f || bounds.Height < 1f)
        {
            return;
        }

        // Sort by value descending
        var sorted = new List<TreeMapNode>(nodes);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        SquarifyRecursive(sorted, 0, bounds, totalValue, result);
    }

    private static void SquarifyRecursive(List<TreeMapNode> nodes, int start, Rect bounds,
        double totalValue, List<(TreeMapNode Node, Rect Rect)> result)
    {
        if (start >= nodes.Count || bounds.Width < 1f || bounds.Height < 1f)
        {
            return;
        }

        if (start == nodes.Count - 1)
        {
            // Last node gets remaining space
            result.Add((nodes[start], bounds));
            return;
        }

        // Determine layout direction: short side
        bool vertical = bounds.Width >= bounds.Height;
        float shortSide = vertical ? bounds.Height : bounds.Width;

        // Greedily add nodes to current row while improving worst aspect ratio
        var row = new List<int>();
        double rowSum = 0;
        double bestWorst = double.MaxValue;

        for (int i = start; i < nodes.Count; i++)
        {
            double nodeVal = Math.Max(0, nodes[i].Value);
            double newSum = rowSum + nodeVal;
            row.Add(i);

            double rowWidth = (float)(newSum / totalValue) * (vertical ? bounds.Width : bounds.Height);
            if (rowWidth < 0.5f)
            {
                rowSum = newSum;
                continue;
            }

            double worst = WorstAspectRatio(row, nodes, newSum, shortSide, totalValue,
                vertical ? bounds.Width : bounds.Height);

            if (row.Count > 1 && worst > bestWorst)
            {
                // Adding this node made it worse — remove it and lay out current row
                row.RemoveAt(row.Count - 1);
                break;
            }

            bestWorst = worst;
            rowSum = newSum;
        }

        // Lay out the row
        float rowFraction = (float)(rowSum / totalValue);
        float rowExtent = rowFraction * (vertical ? bounds.Width : bounds.Height);

        float offset = 0f;
        foreach (int idx in row)
        {
            float nodeFraction = rowSum > 0 ? (float)(Math.Max(0, nodes[idx].Value) / rowSum) : 0f;
            float nodeExtent = nodeFraction * shortSide;

            Rect nodeRect;
            if (vertical)
            {
                nodeRect = new Rect(bounds.X, bounds.Y + offset, rowExtent, nodeExtent);
            }
            else
            {
                nodeRect = new Rect(bounds.X + offset, bounds.Y, nodeExtent, rowExtent);
            }

            result.Add((nodes[idx], nodeRect));
            offset += nodeExtent;
        }

        // Recurse for remaining nodes
        int nextStart = row[^1] + 1;
        if (nextStart < nodes.Count)
        {
            double remainingValue = totalValue - rowSum;
            Rect remainingBounds;
            if (vertical)
            {
                remainingBounds = new Rect(bounds.X + rowExtent, bounds.Y,
                    bounds.Width - rowExtent, bounds.Height);
            }
            else
            {
                remainingBounds = new Rect(bounds.X, bounds.Y + rowExtent,
                    bounds.Width, bounds.Height - rowExtent);
            }

            SquarifyRecursive(nodes, nextStart, remainingBounds, remainingValue, result);
        }
    }

    private static double WorstAspectRatio(List<int> row, List<TreeMapNode> nodes,
        double rowSum, float shortSide, double totalValue, float longSide)
    {
        float rowWidth = (float)(rowSum / totalValue) * longSide;
        if (rowWidth < 0.001f)
        {
            return double.MaxValue;
        }

        double worst = 0;
        foreach (int idx in row)
        {
            double nodeVal = Math.Max(0, nodes[idx].Value);
            float nodeHeight = (float)(nodeVal / rowSum) * shortSide;
            if (nodeHeight < 0.001f)
            {
                continue;
            }

            double ratio = Math.Max(rowWidth / nodeHeight, nodeHeight / rowWidth);
            worst = Math.Max(worst, ratio);
        }

        return worst;
    }

    // ── WaterfallChart ──────────────────────────────────────────────────

    private void PaintWaterfallChart(WaterfallChart chart, Rect bounds)
    {
        var chartTheme = ChartTheme.Default(theme);

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.15f), 1f), radius: 6f);

        var items = chart.itemsList;
        if (items.Count == 0)
        {
            PaintText("No data", bounds, 0f, theme.Colors.Text.Opacity(0.4f),
                fontSize: 12f, alignment: TextAlignment.Center);
            return;
        }

        // Animation setup
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        int animHash = 0;
        bool onScreen = false;

        if (animate)
        {
            animHash = ChartAnimationTracker.ComputeWaterfallChartHash(chart);
            onScreen = IsCurrentlyVisible(bounds);
        }

        // Colors
        var posColor = chart.positiveColor ?? new ColorValue("#4CAF50");
        var negColor = chart.negativeColor ?? new ColorValue("#F44336");
        var totColor = chart.totalColor ?? chartTheme.Palette.GetColor(0);
        var connColor = theme.Colors.Text.Opacity(0.25f);

        // Layout constants
        float padding = 12f;
        float labelAreaH = 40f; // space at bottom for labels
        float topPad = 10f;
        float chartX = bounds.X + padding;
        float chartW = bounds.Width - padding * 2f;
        float chartY = bounds.Y + topPad;
        float chartH = bounds.Height - topPad - labelAreaH;

        if (chartH < 20f || chartW < 20f)
        {
            return;
        }

        // Compute running totals and find min/max
        int count = items.Count;
        var starts = new float[count];
        var ends = new float[count];
        double running = 0;
        double minVal = 0;
        double maxVal = 0;

        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            if (item.Type == WaterfallItemType.Total || item.Type == WaterfallItemType.Subtotal)
            {
                starts[i] = 0f;
                ends[i] = (float)item.Value;
                running = item.Value;
            }
            else
            {
                starts[i] = (float)running;
                running += item.Value;
                ends[i] = (float)running;
            }

            minVal = Math.Min(minVal, Math.Min(starts[i], ends[i]));
            maxVal = Math.Max(maxVal, Math.Max(starts[i], ends[i]));
        }

        // Add padding to range
        double range = maxVal - minVal;
        if (range < 0.001)
        {
            range = 1;
        }

        minVal -= range * 0.05;
        maxVal += range * 0.05;
        range = maxVal - minVal;

        // Bar geometry
        float barGap = 4f;
        float totalGaps = barGap * (count - 1);
        float barW = Math.Max(8f, (chartW - totalGaps) / count);
        float radius = Math.Min(3f, barW / 4f);

        // Zero line Y
        float zeroY = chartY + chartH - (float)((0 - minVal) / range * chartH);

        // Draw zero line
        ctx.DrawLine(new Point(chartX, zeroY), new Point(chartX + chartW, zeroY),
            new Stroke(theme.Colors.Text.Opacity(0.15f), 1f));

        // Draw bars
        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            float barX = chartX + i * (barW + barGap);

            float startY = chartY + chartH - (float)((starts[i] - minVal) / range * chartH);
            float endY = chartY + chartH - (float)((ends[i] - minVal) / range * chartH);

            // Animation: staggered bar growth
            float barProgress = 1f;
            if (animate)
            {
                barProgress = ChartAnimationTracker.GetBarProgress(chart, animHash, chart.animateTrigger, i, count, onScreen);
                if (barProgress < 1f && (barProgress > 0f || onScreen))
                {
                    HasActiveChartAnimations = true;
                }
                if (barProgress <= 0f)
                {
                    continue;
                }
                // Interpolate endY toward startY based on progress
                endY = startY + (endY - startY) * barProgress;
            }

            float topY = Math.Min(startY, endY);
            float barH = Math.Max(1f, Math.Abs(endY - startY));

            var barColor = item.Type == WaterfallItemType.Total || item.Type == WaterfallItemType.Subtotal
                ? totColor
                : item.Value >= 0 ? posColor : negColor;

            var barRect = new Rect(barX, topY, barW, barH);
            ctx.DrawRect(barRect, barColor, radius: radius);

            // Connector line to next bar (appears after bar reaches position)
            if (chart.showConnectors && i < count - 1)
            {
                float connOpacity = barProgress < 0.8f ? 0f : (barProgress - 0.8f) / 0.2f;
                if (connOpacity > 0.01f)
                {
                    float connY = endY;
                    float nextBarX = chartX + (i + 1) * (barW + barGap);
                    ctx.DrawLine(new Point(barX + barW, connY), new Point(nextBarX, connY),
                        new Stroke(connColor.Opacity(connOpacity), 1f));
                }
            }

            // Value label (fades in with bar progress)
            if (chart.showValueLabels)
            {
                float valOpacity = 0.7f * barProgress;
                if (valOpacity > 0.01f)
                {
                    string valStr = item.Value >= 0
                        ? "+" + item.Value.ToString("N0")
                        : item.Value.ToString("N0");
                    if (item.Type != WaterfallItemType.Delta)
                    {
                        valStr = item.Value.ToString("N0");
                    }

                    float valFontSize = Math.Min(9f, barW * 0.35f);
                    if (valFontSize >= 6f)
                    {
                        var valSz = ctx.MeasureText(valStr, valFontSize);
                        float valX = barX + (barW - valSz.Width) / 2f;
                        float valY = item.Value >= 0 || item.Type != WaterfallItemType.Delta
                            ? topY - valSz.Height - 2f
                            : topY + barH + 2f;

                        ctx.DrawText(valStr, MathF.Round(valX), MathF.Round(valY),
                            valFontSize, theme.Colors.Text.Opacity(valOpacity));
                    }
                }
            }

            // X-axis label (fades in with bar progress)
            float labelFontSize = Math.Min(9f, barW * 0.35f);
            if (labelFontSize >= 6f && barProgress > 0.01f)
            {
                string label = item.Label;
                var labelSz = ctx.MeasureText(label, labelFontSize);

                // Truncate if too wide
                while (label.Length > 2 && labelSz.Width > barW + barGap - 2f)
                {
                    label = label[..^1];
                    labelSz = ctx.MeasureText(label + "…", labelFontSize);
                }

                if (label != item.Label)
                {
                    label += "…";
                    labelSz = ctx.MeasureText(label, labelFontSize);
                }

                float labelX = barX + (barW - labelSz.Width) / 2f;
                float labelY = chartY + chartH + 4f;
                ctx.DrawText(label, MathF.Round(labelX), MathF.Round(labelY),
                    labelFontSize, theme.Colors.Text.Opacity(0.6f * barProgress));
            }
        }
    }

    // ── ScatterPlot ─────────────────────────────────────────────────────

    private void PaintScatterPlot(ScatterPlot chart, Rect bounds)
    {
        var chartTheme = ChartTheme.Default(theme);

        // Background
        ctx.DrawRect(bounds, theme.Colors.SurfaceAlt, radius: 6f);
        ctx.DrawRect(bounds, stroke: new Stroke(theme.Colors.Border.Opacity(0.15f), 1f), radius: 6f);

        var series = chart.seriesList;
        if (series.Count == 0)
        {
            PaintText("No data", bounds, 0f, theme.Colors.Text.Opacity(0.4f),
                fontSize: 12f, alignment: TextAlignment.Center);
            return;
        }

        // Animation setup
        bool animate = chart.animateTrigger != AnimateTrigger.None && !skipAnimations;
        float animProgress = 1f;

        if (animate)
        {
            int animHash = ChartAnimationTracker.ComputeScatterPlotHash(chart);
            bool onScreen = IsCurrentlyVisible(bounds);

            animProgress = ChartAnimationTracker.GetProgress(
                chart, animHash, chart.animateTrigger, ChartAnimationTracker.ScatterDuration, onScreen);

            if (animProgress < 1f && (animProgress > 0f || onScreen))
            {
                HasActiveChartAnimations = true;
            }
        }

        // Layout
        float padding = 16f;
        float axisLabelW = 30f;
        float axisLabelH = 16f;
        float plotX = bounds.X + padding + axisLabelW;
        float plotY = bounds.Y + padding;
        float plotW = bounds.Width - padding * 2 - axisLabelW;
        float plotH = bounds.Height - padding * 2 - axisLabelH;

        if (plotW < 20f || plotH < 20f)
        {
            return;
        }

        // Find data range across all series
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var s in series)
        {
            foreach (var pt in s.dataPointsList)
            {
                minX = Math.Min(minX, pt.X);
                maxX = Math.Max(maxX, pt.X);
                minY = Math.Min(minY, pt.Y);
                maxY = Math.Max(maxY, pt.Y);
            }
        }

        // Add padding to range
        double rangeX = maxX - minX;
        double rangeY = maxY - minY;
        if (rangeX < 0.001)
        {
            rangeX = 1;
        }

        if (rangeY < 0.001)
        {
            rangeY = 1;
        }

        minX -= rangeX * 0.05;
        maxX += rangeX * 0.05;
        minY -= rangeY * 0.05;
        maxY += rangeY * 0.05;
        rangeX = maxX - minX;
        rangeY = maxY - minY;

        // Grid lines — staggered during entrance
        var gridColor = theme.Colors.Text.Opacity(0.08f);
        var axisTextColor = theme.Colors.Text.Opacity(0.5f);
        float gridFontSize = 8f;
        int gridLines = 5;

        for (int i = 0; i <= gridLines; i++)
        {
            float t = i / (float)gridLines;

            float gridOpacity = 1f;
            if (animate && animProgress < 1f)
            {
                float gridT = Math.Clamp(animProgress * 2.5f - i * 0.15f, 0f, 1f);
                gridOpacity = gridT;
                if (gridT < 0.01f)
                {
                    continue;
                }
            }

            // Horizontal grid line + Y label
            float gy = plotY + plotH - t * plotH;
            ctx.DrawLine(new Point(plotX, gy), new Point(plotX + plotW, gy),
                new Stroke(gridColor.Opacity(gridOpacity), 1f));
            double yVal = minY + t * rangeY;
            string yLabel = yVal.ToString("F0");
            var ySz = ctx.MeasureText(yLabel, gridFontSize);
            ctx.DrawText(yLabel, plotX - ySz.Width - 4f, gy - ySz.Height / 2f,
                gridFontSize, axisTextColor.Opacity(gridOpacity));

            // Vertical grid line + X label
            float gx = plotX + t * plotW;
            ctx.DrawLine(new Point(gx, plotY), new Point(gx, plotY + plotH),
                new Stroke(gridColor.Opacity(gridOpacity), 1f));
            double xVal = minX + t * rangeX;
            string xLabel = xVal.ToString("F0");
            var xSz = ctx.MeasureText(xLabel, gridFontSize);
            ctx.DrawText(xLabel, gx - xSz.Width / 2f, plotY + plotH + 3f,
                gridFontSize, axisTextColor.Opacity(gridOpacity));
        }

        // Plot border
        ctx.DrawRect(new Rect(plotX, plotY, plotW, plotH),
            stroke: new Stroke(theme.Colors.Border.Opacity(0.2f), 1f));

        // Draw points — per-point stagger for organic entrance
        float baseRadius = chart.pointRadiusValue;
        float opacity = chart.pointOpacityValue;

        for (int si = 0; si < series.Count; si++)
        {
            var s = series[si];
            if (s.hiddenState)
            {
                continue;
            }

            var color = (s.colorOverride ?? chartTheme.Palette.GetColor(si)).Opacity(opacity);

            for (int pi = 0; pi < s.dataPointsList.Count; pi++)
            {
                var pt = s.dataPointsList[pi];
                float px = plotX + (float)((pt.X - minX) / rangeX) * plotW;
                float py = plotY + plotH - (float)((pt.Y - minY) / rangeY) * plotH;

                float r = baseRadius;

                // Per-point stagger: pseudo-random delay based on point hash
                float pointScale = 1f;
                if (animate && animProgress < 1f)
                {
                    uint pointHash = (uint)(pt.X.GetHashCode() ^ (pt.Y.GetHashCode() * 397));
                    float pointDelay = (pointHash % 100) / 100f * 0.6f;
                    float pointT = Math.Clamp((animProgress - pointDelay) / 0.4f, 0f, 1f);
                    pointScale = pointT;
                    if (pointT <= 0f)
                    {
                        continue;
                    }
                }

                if (chart.bubbleEnabled && s.bubbleDataPointsList != null && pi < s.bubbleDataPointsList.Count)
                {
                    // Map bubble size
                    double sizeVal = s.bubbleDataPointsList[pi].Size;
                    r = (float)(chart.bubbleMinRadius +
                        (sizeVal / 100.0) * (chart.bubbleMaxRadius - chart.bubbleMinRadius));
                    r = Math.Clamp(r, chart.bubbleMinRadius, chart.bubbleMaxRadius);
                }

                r *= pointScale;

                // Clip to plot area
                if (px >= plotX - r && px <= plotX + plotW + r &&
                    py >= plotY - r && py <= plotY + plotH + r)
                {
                    ctx.DrawCircle(new Point(px, py), r, color.Opacity(pointScale));
                }
            }
        }

        // Legend (if multiple series)
        if (series.Count > 1)
        {
            float legendX = plotX + plotW - 10f;
            float legendY = plotY + 6f;
            float legendFontSize = 8f;

            for (int si = series.Count - 1; si >= 0; si--)
            {
                var s = series[si];
                var color = s.colorOverride ?? chartTheme.Palette.GetColor(si);
                string name = s.SeriesName;
                var nameSz = ctx.MeasureText(name, legendFontSize);

                float itemX = legendX - nameSz.Width - 14f;
                ctx.DrawCircle(new Point(itemX, legendY + nameSz.Height / 2f), 3f, color);
                ctx.DrawText(name, itemX + 8f, legendY, legendFontSize, theme.Colors.Text.Opacity(0.7f));
                legendY += nameSz.Height + 4f;
            }
        }
    }

    // ── Barcode ──────────────────────────────────────────────────────────

    private static readonly Dictionary<int, long> barcodeFirstPaintTick = new();

    private void PaintBarcode(Barcode barcode, Rect bounds)
    {
        var fg = barcode.ForegroundColorOverride ?? theme.Colors.Text;
        var bg = barcode.BackgroundColorOverride ?? theme.Colors.Surface;

        ctx.DrawRect(bounds, bg);

        if (string.IsNullOrEmpty(barcode.Content))
        {
            PaintText("No content", bounds, 0f, theme.Colors.Text.Opacity(0.4f),
                fontSize: 12f, alignment: TextAlignment.Center);
            return;
        }

        bool[]? modules = barcode.GetEncodedModules();
        if (modules is null)
        {
            PaintText("Encoding error", bounds, 0f, theme.Colors.Danger,
                fontSize: 12f, alignment: TextAlignment.Center);
            return;
        }

        if (modules.Length == 0)
        {
            return;
        }

        // Reserve space for text label below bars
        float textHeight = barcode.ShowText ? 16f : 0f;
        float barAreaHeight = bounds.Height - textHeight;
        if (barAreaHeight < 4f)
        {
            barAreaHeight = bounds.Height;
            textHeight = 0f;
        }

        // Compute bar width
        float quietZone = bounds.Width * 0.04f;
        float availableWidth = bounds.Width - quietZone * 2f;
        float moduleWidth = availableWidth / modules.Length;
        bool reducedMotion = ControlStateAnimator.ReducedMotion;

        // Entrance animation: left-to-right stagger
        // Same visibility-gated pattern as ChartAnimationTracker: return 0 while
        // off-screen (clipped anyway), start animating on first visible frame.
        const double bcAnimMs = 800.0;
        float globalProgress = 1f;
        if (!reducedMotion)
        {
            int bcHash = barcode.Content.GetHashCode(StringComparison.Ordinal) ^ barcode.BarcodeDisplayFormat.GetHashCode();
            float centerY = absoluteY + bounds.Height * 0.5f;
            bool isVisible = centerY > 0 && centerY < ViewportLogicalHeight;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!barcodeFirstPaintTick.TryGetValue(bcHash, out long startTick))
            {
                globalProgress = 0f;
                if (isVisible)
                {
                    barcodeFirstPaintTick[bcHash] = now;
                }
            }
            else
            {
                double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTick).TotalMilliseconds;
                globalProgress = Math.Clamp((float)(elapsedMs / bcAnimMs), 0f, 1f);
            }
            if (globalProgress < 1f)
            {
                ControlStateAnimator.SignalActiveTransition();
            }
        }

        // Draw bars
        float startX = bounds.X + quietZone;
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i])
            {
                float barX = startX + i * moduleWidth;

                if (globalProgress < 1f)
                {
                    float barFrac = (float)i / modules.Length;
                    float barT = Math.Clamp((globalProgress * 1.5f - barFrac * 0.5f), 0f, 1f);
                    if (barT <= 0f)
                    {
                        continue;
                    }
                    float barH = barAreaHeight * barT;
                    ctx.DrawRect(new Rect(barX, bounds.Y + barAreaHeight - barH, moduleWidth, barH),
                        fg.Opacity(barT));
                }
                else
                {
                    ctx.DrawRect(new Rect(barX, bounds.Y, moduleWidth, barAreaHeight), fg);
                }
            }
        }

        // Draw human-readable text below
        if (barcode.ShowText && textHeight > 0f)
        {
            float textOpacity = globalProgress;
            float fontSize = barcode.LabelTextStyle?.Size ?? 11f;
            var textRect = new Rect(bounds.X, bounds.Y + barAreaHeight + 1f,
                bounds.Width, textHeight);
            PaintText(barcode.Content, textRect, 0f, fg.Opacity(textOpacity),
                fontSize: fontSize, alignment: TextAlignment.Center);
        }
    }

    private void PaintDragDropOverlay()
    {
        if (!InputDispatcher.IsDragDropActive)
        {
            return;
        }

        var sourceNode = InputDispatcher.DragDropSourceNode;
        var targetNode = InputDispatcher.DragDropTargetNode;
        var mousePos = InputDispatcher.DragDropMousePosition;

        // Paint drop target feedback (highlight pulses when draggable hovers)
        if (targetNode != null)
        {
            var dragData = targetNode.LayoutData.DragData;
            var targetBounds = dragData?.AbsoluteBounds ?? default;
            var feedback = dragData?.Feedback ?? DragFeedbackKind.Highlight;

            // Breathing pulse on drop zone
            float pulseOpacity = 1f;
            if (!ControlStateAnimator.ReducedMotion)
            {
                double elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(0).TotalMilliseconds;
                pulseOpacity = 0.7f + 0.3f * MathF.Sin((float)(elapsedMs * 0.006));
                ControlStateAnimator.SignalActiveTransition();
            }

            switch (feedback)
            {
                case DragFeedbackKind.Highlight:
                    ctx.DrawRect(targetBounds,
                        theme.Colors.Primary.Opacity(0.15f * pulseOpacity),
                        radius: 6f);
                    ctx.DrawRect(targetBounds,
                        stroke: new Stroke(theme.Colors.Primary.Opacity(pulseOpacity), 2f),
                        radius: 6f);
                    break;

                case DragFeedbackKind.Border:
                    var borderColor = dragData?.FeedbackBorderColor ?? theme.Colors.Primary;
                    float borderWidth = dragData?.FeedbackBorderWidth > 0 ? dragData.FeedbackBorderWidth : 2f;
                    ctx.DrawRect(targetBounds,
                        stroke: new Stroke(borderColor.Opacity(pulseOpacity), borderWidth),
                        radius: 6f);
                    break;

                case DragFeedbackKind.None:
                    break;
            }
        }

        // Paint drag preview — lifted from surface with shadow and slight rotation
        if (sourceNode != null)
        {
            var sourceDragData = sourceNode.LayoutData.DragData;

            // Prefer the caller's custom preview node (set via .DragPreview(...)). It
            // is detached from the main tree, so lay it out here to size it. Falling
            // back to the source bounds — never to the payload's ToString(), which
            // dumped a raw "CardDragPayload { … }" string onto the screen.
            var previewNode = sourceDragData?.Preview;
            float previewW, previewH;
            if (previewNode != null)
            {
                dragPreviewLayout ??= new LayoutEngine();
                dragPreviewLayout.Layout(previewNode, LayoutConstraints.Loose(new Size(400f, 600f)));
                previewW = previewNode.LayoutData.Bounds.Width;
                previewH = previewNode.LayoutData.Bounds.Height;
            }
            else
            {
                var sourceBounds = sourceDragData?.AbsoluteBounds ?? default;
                previewW = sourceBounds.Width;
                previewH = sourceBounds.Height;
            }

            float previewX = mousePos.X - previewW / 2f;
            float previewY = mousePos.Y - previewH / 2f;
            var previewBounds = new Rect(previewX, previewY, previewW, previewH);
            var previewCenter = new Point(previewX + previewW / 2f, previewY + previewH / 2f);

            // Slight rotation for organic pickup feel
            ScopeGuard rotScope = default;
            if (!ControlStateAnimator.ReducedMotion)
            {
                rotScope = ctx.PushRotate(Angle.Degrees(2f), previewCenter);
            }

            // Drop shadow for lift effect
            var shadowBounds = new Rect(previewX + 2f, previewY + 4f, previewW, previewH);
            ctx.DrawRect(shadowBounds, new ColorValue("#000000").Opacity(0.25f), radius: 8f);

            // Scaled up slightly (1.05x lift)
            ScopeGuard scaleScope = default;
            if (!ControlStateAnimator.ReducedMotion)
            {
                scaleScope = ctx.PushScale(1.05f, 1.05f, previewCenter);
            }

            using var opScope = ctx.PushOpacity(0.9f);

            if (previewNode != null)
            {
                // Paint the caller's preview at the pointer. The node laid out at
                // origin (0,0,w,h); translate it into place. absoluteX/Y is only used
                // for drag-bounds tracking (irrelevant here) — save/restore anyway.
                float savedAx = absoluteX, savedAy = absoluteY;
                absoluteX = previewX;
                absoluteY = previewY;
                using (ctx.PushTranslate(previewX, previewY))
                {
                    PaintRecursive(previewNode);
                }

                absoluteX = savedAx;
                absoluteY = savedAy;
            }
            else
            {
                // No custom preview: a neutral lifted ghost of the dragged surface.
                ctx.DrawRect(previewBounds, theme.Colors.Surface, radius: 6f);
                ctx.DrawRect(previewBounds,
                    stroke: new Stroke(theme.Colors.Primary, 1.5f), radius: 6f);
            }

            scaleScope.Dispose();
            rotScope.Dispose();
        }
    }

    // ── Navigation Transition ─────────────────────────────────────────

    private void PaintNavigationTransition(NavigationTransitionHost nth, Rect bounds)
    {
        float progress = nth.Progress;
        var kind = nth.TransitionType.Kind;

        // Curtain composites a third layer above both pages and swaps the page
        // beneath at the mid-point — it has no page-pair hero overlay.
        if (kind == PageTransitionKind.Curtain)
        {
            PaintCurtainTransition(nth, bounds, progress);
            return;
        }

        switch (kind)
        {
            case PageTransitionKind.Custom:
                PaintDissolveScaleTransition(nth, bounds, progress);
                break;

            case PageTransitionKind.Fade:
                PaintCrossfadeTransition(nth, progress);
                break;

            case PageTransitionKind.Dissolve:
                PaintDissolveTransition(nth, progress);
                break;

            default:
                PaintDirectionalSlideTransition(nth, bounds, kind, progress);
                break;
        }

        // Hero overlay — interpolate hero positions
        if (nth.OutgoingHeroes.Count > 0 && nth.IncomingHeroes.Count > 0)
        {
            PaintHeroOverlay(nth, bounds, progress);
        }
    }

    // Paints the incoming page, preferring its already-rendered tree.
    private void PaintIncomingPage(NavigationTransitionHost nth)
    {
        if (nth.IncomingPage is null)
        {
            return;
        }

        if (nth.IncomingPage.RenderedTree is not null)
        {
            PaintRecursive(nth.IncomingPage.RenderedTree);
        }
        else
        {
            PaintRecursive(nth.IncomingPage);
        }
    }

    // Directional slides. `Slide` mirrors direction on pop; the fixed variants
    // (SlideLeft/Right/Up/Down) always move the same way regardless of push/pop.
    private void PaintDirectionalSlideTransition(
        NavigationTransitionHost nth, Rect bounds, PageTransitionKind kind, float progress)
    {
        bool vertical = kind is PageTransitionKind.SlideUp or PageTransitionKind.SlideDown;
        float direction = ResolveSlideDirection(kind, nth.IsPush);
        float extent = vertical ? bounds.Height : bounds.Width;

        // Outgoing exits against the direction; incoming enters from ahead of it.
        float outPrimary = -direction * progress * extent;
        float inPrimary = direction * (1f - progress) * extent;

        float outX = vertical ? 0f : outPrimary;
        float outY = vertical ? outPrimary : 0f;
        float inX = vertical ? 0f : inPrimary;
        float inY = vertical ? inPrimary : 0f;

        if (nth.OutgoingTree is not null)
        {
            using (ctx.PushTranslate(outX, outY))
            {
                PaintRecursive(nth.OutgoingTree);
            }
        }

        using (ctx.PushTranslate(inX, inY))
        {
            PaintIncomingPage(nth);
        }
    }

    // Dissolve: two sequential phases.
    //   Phase 1 (progress 0 → dissolveEnd): the outgoing page dissolves away pixel
    //     by pixel. The GPU discards each fragment whose screen-space hash noise is
    //     below a rising threshold, so random pixels scatter out; the threshold
    //     eases in (localT²) so it accelerates toward the end. Only the outgoing
    //     page is painted, so the whole frame's dissolve == the old page dissolving.
    //   Phase 2 (dissolveEnd → 1): old is fully gone; the incoming page fades in.
    // (True per-pixel dissolve — the grid version could only do coarse blocks; this
    // uses a real shader discard, so text dissolves per-pixel too.)
    private void PaintDissolveTransition(NavigationTransitionHost nth, float progress)
    {
        const float dissolveEnd = 0.7f;

        if (progress < dissolveEnd)
        {
            float localT = progress / dissolveEnd;
            ctx.SetFrameDissolve(localT * localT); // ease-in: accelerate toward the end
            if (nth.OutgoingTree is not null)
            {
                PaintRecursive(nth.OutgoingTree);
            }
        }
        else
        {
            ctx.SetFrameDissolve(0f);
            float localT = Math.Clamp((progress - dissolveEnd) / (1f - dissolveEnd), 0f, 1f);
            float inOpacity = MathF.Pow(localT, 2.2f); // gamma-even fade-in
            if (inOpacity > 0.004f)
            {
                using (ctx.PushOpacity(inOpacity))
                {
                    PaintIncomingPage(nth);
                }
            }
        }
    }

    private static float ResolveSlideDirection(PageTransitionKind kind, bool isPush)
    {
        return kind switch
        {
            PageTransitionKind.SlideLeft => 1f,
            PageTransitionKind.SlideRight => -1f,
            PageTransitionKind.SlideUp => 1f,
            PageTransitionKind.SlideDown => -1f,
            // Slide: intelligent directional — push slides left, pop slides right.
            _ => isPush ? 1f : -1f,
        };
    }

    // Fade: a sequential crossfade. The outgoing page fades out over the first
    // 55% of the transition; the incoming page fades in over the last 45% — old
    // leaves, THEN new arrives (a brief pass through the background between them),
    // no muddy 50/50 overlap.
    //
    // Opacity is gamma-shaped (^2.2), not linear. Perceived lightness ≈
    // opacity^(1/2.2), so a *linear* opacity ramp holds visibly bright for most of
    // its length then collapses at the end — it reads as "nothing happens… then
    // BANG the page is gone." Raising to ^2.2 drops alpha fast-early / slow-late so
    // the *perceived* brightness changes at a constant rate: an even, elegant fade.
    // (PushOpacity fades the whole subtree, text included — EtchBackend scales
    // emitted shape/glyph/image alpha by the layer opacity.)
    private void PaintCrossfadeTransition(NavigationTransitionHost nth, float progress)
    {
        const float gamma = 2.2f;

        float outLocal = Math.Clamp(progress / 0.55f, 0f, 1f);
        float inLocal = Math.Clamp((progress - 0.55f) / 0.45f, 0f, 1f);

        float outOpacity = MathF.Pow(1f - outLocal, gamma);
        float inOpacity = MathF.Pow(inLocal, gamma);

        if (nth.OutgoingTree is not null && outOpacity > 0.004f)
        {
            using (ctx.PushOpacity(outOpacity))
            {
                PaintRecursive(nth.OutgoingTree);
            }
        }

        if (inOpacity > 0.004f)
        {
            using (ctx.PushOpacity(inOpacity))
            {
                PaintIncomingPage(nth);
            }
        }
    }

    // Custom: dissolve + scale. The outgoing page shrinks and fades out; the
    // incoming page scales up from 90% and fades in, both around the centre. The
    // scale spans 0.90→1.0 (not the subtler 0.95) so the zoom is actually legible;
    // paired with an even, unhurried driver curve it reads as a real dissolve.
    private const float DissolveScaleFrom = 0.90f;

    private void PaintDissolveScaleTransition(NavigationTransitionHost nth, Rect bounds, float progress)
    {
        var center = new Point(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);

        float outOpacity = 1f - progress;
        float inOpacity = progress;
        float outScale = LerpF(1f, DissolveScaleFrom, progress);
        float inScale = LerpF(DissolveScaleFrom, 1f, progress);

        if (nth.OutgoingTree is not null && outOpacity > 0.01f)
        {
            using (ctx.PushScale(outScale, outScale, center))
            using (ctx.PushOpacity(outOpacity))
            {
                PaintRecursive(nth.OutgoingTree);
            }
        }

        if (inOpacity > 0.01f)
        {
            using (ctx.PushScale(inScale, inScale, center))
            using (ctx.PushOpacity(inOpacity))
            {
                PaintIncomingPage(nth);
            }
        }
    }

    // Curtain: the page beneath is the outgoing page while the curtain covers
    // the screen (progress < 0.5) and the incoming page once it is fully covered,
    // so the swap is never seen. The curtain factory node is composited on top.
    private void PaintCurtainTransition(NavigationTransitionHost nth, Rect bounds, float progress)
    {
        if (progress < 0.5f)
        {
            if (nth.OutgoingTree is not null)
            {
                PaintRecursive(nth.OutgoingTree);
            }
        }
        else
        {
            PaintIncomingPage(nth);
        }

        var factory = nth.TransitionType.CurtainFactory;
        if (factory is null)
        {
            return;
        }

        var size = new Size(bounds.Width, bounds.Height);
        var curtainNode = factory(progress, size);
        if (curtainNode is null || ReferenceEquals(curtainNode, Node.Empty))
        {
            return;
        }

        // The curtain node is detached from the main tree; lay it out on demand.
        curtainLayout ??= new LayoutEngine();
        curtainLayout.Layout(curtainNode, LayoutConstraints.Loose(size));

        // absoluteX/Y is only used for drag-bounds tracking here; save/restore it.
        float savedAx = absoluteX, savedAy = absoluteY;
        absoluteX = bounds.X;
        absoluteY = bounds.Y;
        using (ctx.PushTranslate(bounds.X, bounds.Y))
        {
            PaintRecursive(curtainNode);
        }

        absoluteX = savedAx;
        absoluteY = savedAy;
    }

    private void PaintHeroOverlay(NavigationTransitionHost nth, Rect bounds, float progress)
    {
        foreach (var outHero in nth.OutgoingHeroes)
        {
            // Find matching hero in incoming page
            HeroCapture? matchingIn = null;
            foreach (var inHero in nth.IncomingHeroes)
            {
                if (inHero.Key.Equals(outHero.Key))
                {
                    matchingIn = inHero;
                    break;
                }
            }

            if (matchingIn is null)
            {
                continue;
            }

            // Interpolate bounds
            var fromBounds = outHero.Bounds;
            var toBounds = matchingIn.Bounds;
            float x = fromBounds.X + (toBounds.X - fromBounds.X) * progress;
            float y = fromBounds.Y + (toBounds.Y - fromBounds.Y) * progress;
            float w = fromBounds.Width + (toBounds.Width - fromBounds.Width) * progress;
            float h = fromBounds.Height + (toBounds.Height - fromBounds.Height) * progress;

            // Interpolate corner radius
            float fromRadius = outHero.CornerRadius;
            float toRadius = matchingIn.CornerRadius;
            float radius = fromRadius + (toRadius - fromRadius) * progress;

            // For now, draw a clip region at interpolated position and paint the
            // source node (from the outgoing hero) scaled to fit
            var heroRect = new Rect(x, y, w, h);
            var clipScope = ctx.PushRoundedClip(heroRect, radius);

            // Draw the hero's source node translated to the interpolated position
            if (outHero.SourceNode is not null)
            {
                var sourceLayout = outHero.SourceNode.LayoutData;
                float translateX = x - outHero.Bounds.X;
                float translateY = y - outHero.Bounds.Y;
                float scaleX = w / Math.Max(1f, outHero.Bounds.Width);
                float scaleY = h / Math.Max(1f, outHero.Bounds.Height);

                var translateScope = ctx.PushTranslate(translateX, translateY);
                var scaleScope = ctx.PushScale(scaleX, scaleY);

                PaintRecursive(outHero.SourceNode);

                scaleScope.Dispose();
                translateScope.Dispose();
            }

            clipScope.Dispose();
        }
    }
}
