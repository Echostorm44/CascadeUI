using Cascade.UI.Backend.Etch;
using System.Diagnostics;
using Cascade.UI.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// Central coordinator that connects the component lifecycle, render scheduling,
/// animation, and layout into a coherent frame loop. Created by App.Run and driven
/// by the platform message loop's frame timer.
/// </summary>
internal sealed class FrameOrchestrator : IDisposable
{
    private readonly RenderScheduler renderScheduler;
    private readonly AnimationScheduler animationScheduler;
    private readonly LayoutEngine layoutEngine;
    private readonly InputDispatcher inputDispatcher;

    private readonly Action requestFrame;
    private readonly Action cancelFrame;

    private ComponentHost? rootHost;
    private bool frameRequested;
    private long lastTickTimestamp;
    private float windowWidth;
    private float windowHeight;
    private bool disposed;

    // Cached across frames to avoid per-tick allocations. The DrawContext
    // and NodePainter both hold mutable per-frame state that is refreshed
    // via BeginFrame at the start of each paint pass.
    private DrawContext? cachedDrawContext;
    private NodePainter? cachedPainter;

    /// <summary>
    /// The active theme used for painting. Reads from ThemeSwitcher.Current at paint
    /// time so that runtime theme changes propagate immediately. The setter is
    /// intentionally a no-op — App.Run sets the theme via ThemeSwitcher.Apply().
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
    internal CascadeTheme? Theme
    {
        get => ThemeSwitcher.Current;
        set
        {
            // Bootstrap path: App.Run sets this before the first frame.
            // ThemeSwitcher.Apply is called separately by App.Run, so
            // we only need this for API compatibility.
        }
    }

    /// <summary>
    /// The render backend used to create DrawContexts. Set before MountRoot to enable GPU painting.
    /// When null, the paint pass is skipped.
    /// </summary>
    internal EtchBackend? RenderBackend { get; set; }

    /// <summary>
    /// The DPI scale factor used to convert logical layout coordinates to physical
    /// GPU pixels. Set to <c>window.DpiScale</c> during bootstrap. The Etch scene
    /// receives a root scale transform of <c>(PixelRatio, PixelRatio)</c> so that
    /// all layout coordinates are correctly scaled to the physical surface.
    /// Default is 1.0 (no scaling).
    /// </summary>
    internal float PixelRatio { get; set; } = 1f;

    /// <summary>
    /// Callback invoked each frame to acquire a frame handle from the GPU backend.
    /// Returns (frame handle, width, height). Set by the backend initializer.
    /// When null, the built-in paint pass is skipped and PaintCallback is used instead.
    /// </summary>
    internal Func<(ulong frame, uint width, uint height)>? BeginFrameCallback { get; set; }

    /// <summary>
    /// Callback invoked after painting to present the rendered frame to the screen.
    /// Receives the frame handle and the theme background color for the GPU clear color.
    /// </summary>
    internal Action<ulong, ColorValue>? PresentFrameCallback { get; set; }

    /// <summary>
    /// Callback invoked after painting to end a frame without presenting (cleanup only).
    /// Receives the frame handle from BeginFrameCallback.
    /// </summary>
    internal Action<ulong>? EndFrameCallback { get; set; }

    /// <summary>
    /// Legacy paint callback for custom paint logic (used when BeginFrameCallback is null).
    /// Invoked after layout is complete, passing the laid-out root node tree.
    /// </summary>
    internal Action<Node>? PaintCallback { get; set; }

    /// <summary>
    /// Legacy present callback for custom present logic (used when BeginFrameCallback is null).
    /// </summary>
    internal Action? PresentCallback { get; set; }

    internal RenderScheduler Scheduler => renderScheduler;
    internal AnimationScheduler Animations => animationScheduler;
    internal LayoutEngine LayoutEngine => layoutEngine;
    internal InputDispatcher Input => inputDispatcher;
    internal ComponentHost? RootHost => rootHost;
    internal bool IsFrameRequested => frameRequested;

    /// <summary>
    /// Snapshot of every flag the frame loop consults to decide whether to
    /// continue ticking. Used by <c>cascade_diagnostics</c> so an agent can
    /// identify which subsystem is preventing the frame loop from idling.
    /// Cheap to read; no allocation.
    /// </summary>
    internal FrameLoopSentinels Sentinels => new(
        framesInFlight:            frameRequested,
        renderDirtyCount:          renderScheduler.DirtyCount,
        animationsActive:          animationScheduler.HasActiveAnimations,
        animationsActiveCount:     animationScheduler.ActiveCount,
        sharedAnimationsActive:    SharedScheduler.Instance.HasActiveAnimations,
        sharedAnimationsCount:     SharedScheduler.Instance.ActiveCount,
        caretActive:               InputDispatcher.IsCaretActive,
        spinnersActive:            NodePainter.HasActiveSpinners,
        chartAnimationsActive:     NodePainter.HasActiveChartAnimations,
        toastsActive:              NodePainter.HasActiveToasts,
        continuousCanvasesActive:  NodePainter.HasActiveContinuousCanvases,
        stateTransitionsActive:    ControlStateAnimator.HasActiveTransitions);

    /// <param name="requestFrame">
    /// Called when the orchestrator needs the platform to start delivering frame ticks.
    /// On Windows this calls Win32MessageLoop.StartFrameTimer(16).
    /// </param>
    /// <param name="cancelFrame">
    /// Called when no more frames are needed (nothing dirty, no animations).
    /// On Windows this calls Win32MessageLoop.StopFrameTimer().
    /// </param>
    internal FrameOrchestrator(Action requestFrame, Action cancelFrame)
    {
        this.requestFrame = requestFrame;
        this.cancelFrame = cancelFrame;

        renderScheduler = new RenderScheduler();
        animationScheduler = new AnimationScheduler();
        layoutEngine = new LayoutEngine();
        inputDispatcher = new InputDispatcher();

        renderScheduler.FrameRequested += OnFrameRequested;
        animationScheduler.FrameRequested += OnFrameRequested;
        SharedScheduler.Instance.FrameRequested += OnFrameRequested;
        inputDispatcher.RequestRepaint = OnFrameRequested;
    }

    /// <summary>
    /// Instantiates the root component, mounts it, and triggers the first render.
    /// </summary>
    internal void MountRoot<TRoot>(float width, float height) where TRoot : Component, new()
    {
        windowWidth = width;
        windowHeight = height;

        var root = new TRoot();
        rootHost = new ComponentHost(root, renderScheduler, treeDepth: 0);
        rootHost.Mount();
        rootHost.CompleteMountAsync();

#if CASCADE_DEVTOOLS
        // Wire DevTools NodeTreeWalker to the live component tree
        DevTools.NodeTreeWalker.SetRoot(rootHost);
        DevTools.NodeTreeWalker.SetInputDispatcher(inputDispatcher);
#endif

        // Initial layout after first render
        PerformLayout();

        // Request a frame so the first paint happens
        OnFrameRequested();
    }

    /// <summary>
    /// Called by the platform frame timer (e.g. WM_TIMER on Windows, NSTimer on macOS,
    /// epoll timeout on Linux). Runs one complete frame: animate → re-render → layout → paint → present.
    /// </summary>
    internal void Tick()
    {
        if (disposed || rootHost == null)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        float deltaTime = lastTickTimestamp == 0
            ? 0.016f
            : (float)Stopwatch.GetElapsedTime(lastTickTimestamp, now).TotalSeconds;
        lastTickTimestamp = now;

        // Clamp to prevent huge delta spikes after debugger pauses etc.
        if (deltaTime > 0.25f)
        {
            deltaTime = 0.016f;
        }

#if DEBUG
        long frameStart = Stopwatch.GetTimestamp();
#endif

        DiagnosticsHub.BeginFrame();

        // 1. Advance animations
        animationScheduler.Tick(deltaTime);
        SharedScheduler.Instance.Tick(deltaTime);

        // 2. Process dirty re-renders (depth-sorted)
#if DEBUG
        long renderStart = Stopwatch.GetTimestamp();
#endif
        bool reRendered = renderScheduler.ProcessFrame();
#if DEBUG
        float renderTimeMs = (float)Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
#endif

        // A re-render rebuilds content that a ScrollView may be compositing from a
        // cached retained layer. Content changes that leave the layer's size, theme,
        // focus and animation state unchanged — e.g. a card's selection border, a
        // toggled row style — otherwise never trip the layer's recapture check, so
        // the ScrollView keeps showing the stale bitmap (a 2nd selected card never
        // highlights). Forcing a recapture here re-runs the layer content; the
        // presenter then rebuilds that layer's scene (a recapture bypasses the
        // main-stream scene cache — see EtchBackendProvider). Scrolling and
        // animation frames do NOT re-render, so the cache stays intact where it
        // matters.
        if (reRendered && rootHost.RenderedTree != null)
        {
            MarkAllScrollLayersDirty(rootHost.RenderedTree);
        }

#if CASCADE_DEVTOOLS
        // 2.5 Rebuild DevTools node index after render commits
        DevTools.NodeTreeWalker.RebuildIndex();
#endif

        // 3. Layout the updated tree
#if DEBUG
        long layoutStart = Stopwatch.GetTimestamp();
#endif
        DiagnosticsHub.BeginLayout();
        PerformLayout();
        DiagnosticsHub.EndLayout();
#if DEBUG
        float layoutTimeMs = (float)Stopwatch.GetElapsedTime(layoutStart).TotalMilliseconds;
#endif

        // 4. Update input dispatcher's root for hit testing
        inputDispatcher.SetRoot(rootHost.RenderedTree);

        // 5. Paint the laid-out tree
#if DEBUG
        long gpuStart = Stopwatch.GetTimestamp();
#endif
        DiagnosticsHub.BeginPaint();
        if (rootHost.RenderedTree != null)
        {
            if (BeginFrameCallback is not null && RenderBackend is not null && Theme is not null)
            {
                // Full GPU paint pipeline: begin frame → paint tree → present
                DiagnosticsHub.MarkPhase("paint.begin_frame_cb");
                var (frameHandle, frameWidth, frameHeight) = BeginFrameCallback();
                try
                {
                    DiagnosticsHub.MarkPhase("paint.ctx_begin");
                    var ctx = cachedDrawContext ??= new DrawContext(RenderBackend, frameHandle);
                    ctx.BeginFrame(
                        RenderBackend,
                        frameHandle,
                        new Size(frameWidth, frameHeight),
                        PixelRatio,
                        ResolveDefaultFontPath());

                    // Apply DPI scale transform at root so that logical layout
                    // coordinates map to physical GPU pixels.
                    if (PixelRatio != 1f)
                    {
                        RenderBackend.PushTransform(frameHandle,
                            System.Numerics.Matrix3x2.CreateScale(PixelRatio, PixelRatio));
                    }

                    DiagnosticsHub.MarkPhase("paint.painter_begin");
                    var painter = cachedPainter ??= new NodePainter(ctx, Theme, deltaTime);
                    painter.BeginFrame(Theme, deltaTime);
                    DiagnosticsHub.MarkPhase("paint.painter_paint");
                    painter.Paint(rootHost.RenderedTree);

                    if (PixelRatio != 1f)
                    {
                        RenderBackend.PopTransform(frameHandle);
                    }

                    DiagnosticsHub.MarkPhase("paint.present");
                    PresentFrameCallback?.Invoke(frameHandle, Theme.Colors.Background);
                    DiagnosticsHub.EndPhase();
                }
                catch
                {
                    // If painting fails, end the frame without presenting
                    EndFrameCallback?.Invoke(frameHandle);
                    throw;
                }
            }
            else
            {
                // Legacy callback path (for testing or custom rendering)
                PaintCallback?.Invoke(rootHost.RenderedTree);
                PresentCallback?.Invoke();
            }
        }
#if DEBUG
        float gpuTimeMs = (float)Stopwatch.GetElapsedTime(gpuStart).TotalMilliseconds;
        float frameTimeMs = (float)Stopwatch.GetElapsedTime(frameStart).TotalMilliseconds;
        DevTools.PerformancePanel.RecordFrame(frameTimeMs, layoutTimeMs, renderTimeMs, gpuTimeMs, 16.67f);
#endif
        DiagnosticsHub.EndPaint();
        DiagnosticsHub.EndFrame();

        // 6. If nothing else needs a frame, stop the timer to save CPU/battery.
        // Keep ticking while a TextInput caret is blinking or a Spinner is
        // visible so their animations stay smooth.
        if (!animationScheduler.HasActiveAnimations
            && !SharedScheduler.Instance.HasActiveAnimations
            && renderScheduler.DirtyCount == 0
            && !InputDispatcher.IsCaretActive
            && !NodePainter.HasActiveSpinners
            && !NodePainter.HasActiveChartAnimations
            && !NodePainter.HasActiveToasts
            && !NodePainter.HasActiveContinuousCanvases
            && !ControlStateAnimator.HasActiveTransitions)
        {
            frameRequested = false;
            cancelFrame();
        }
    }

    /// <summary>
    /// Called when the window is resized. Triggers a re-layout and repaint.
    /// </summary>
    internal void HandleResize(float width, float height)
    {
        windowWidth = width;
        windowHeight = height;

        // A window resize recreates the GPU surface/swapchain, which invalidates the
        // cached retained-layer textures ScrollViews composite. Their size-based
        // recapture check does not fire when the viewport grows but the content
        // height is unchanged (a taller viewport is offset by less max-scroll, so
        // contentHeight — and thus LayerHeight — stays the same), so the ScrollView
        // would composite a now-blank texture. Mark every ScrollView layer dirty so
        // each recaptures against the new surface.
        if (rootHost?.RenderedTree != null)
        {
            MarkAllScrollLayersDirty(rootHost.RenderedTree);
        }

        OnFrameRequested();
    }

    /// <summary>
    /// Recursively marks every <see cref="ScrollView"/> in the tree as needing a
    /// layer recapture. Called on resize because the GPU surface recreation can
    /// invalidate cached layer textures without changing a ScrollView's content
    /// dimensions (see <see cref="HandleResize"/>).
    /// </summary>
    internal static void MarkAllScrollLayersDirty(Node node)
    {
        if (node is ScrollView sv)
        {
            sv.IsLayerDirty = true;
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            MarkAllScrollLayersDirty(child);
        }
    }

    private string? cachedDefaultFontPath;
    private string? cachedSemiBoldFontPath;
    private bool semiBoldResolved;

    private void PerformLayout()
    {
        if (rootHost?.RenderedTree != null)
        {
            DiagnosticsHub.MarkPhase("layout.font_resolve");
            // Make the font path available for text measurement during layout
            LayoutSolver.DefaultFontPath = ResolveDefaultFontPath();
            LayoutSolver.SemiBoldFontPath = ResolveSemiBoldFontPath();

            DiagnosticsHub.MarkPhase("layout.theme_metrics");
            // Pass theme button metrics for accurate measurement
            if (Theme != null)
            {
                var bt = Theme.Button;
                LayoutSolver.ButtonPaddingH = bt.PaddingH;
                LayoutSolver.ButtonMinHeight = bt.Height;
                bool isSemiBold = bt.TextStyle.Weight is FontWeight.SemiBold or FontWeight.Bold
                    or FontWeight.ExtraBold or FontWeight.Black or FontWeight.Medium;
                LayoutSolver.ButtonUseSemiBold = isSemiBold;
                LayoutSolver.ButtonFontSize = bt.TextStyle.Size;
                LayoutSolver.BodyFontSize = Theme.Typography.Scale.Body.Size;
                LayoutSolver.H1FontSize = Theme.Typography.Scale.H1.Size;
                LayoutSolver.H2FontSize = Theme.Typography.Scale.H2.Size;
                LayoutSolver.H3FontSize = Theme.Typography.Scale.H3.Size;
                LayoutSolver.CardPadding = Theme.Card.Padding.Left;
            }

            DiagnosticsHub.MarkPhase("layout.engine");
            var constraints = LayoutConstraints.Tight(
                new Size(windowWidth, windowHeight));
            layoutEngine.Layout(rootHost.RenderedTree, constraints);
            DiagnosticsHub.EndPhase();
        }
    }

    private void OnFrameRequested()
    {
        if (!frameRequested && !disposed)
        {
            frameRequested = true;
            requestFrame();
        }
    }

    /// <summary>
    /// Resolves the default font path from the active theme's font family.
    /// Caches the result since font resolution involves filesystem probing.
    /// </summary>
    private string? ResolveDefaultFontPath()
    {
        if (cachedDefaultFontPath != null)
        {
            return cachedDefaultFontPath;
        }

        if (Theme?.Typography.FontFamily == null)
        {
            return null;
        }

        var family = Theme.Typography.FontFamily;
        string? fontPath = null;

        if (family.Kind == FontFamilyKind.SystemDefault || family.Kind == FontFamilyKind.SystemCategory)
        {
            // Use FontFallback to find a system font that covers basic Latin
            fontPath = FontFallback.FindFallbackFont('A');
        }
        else if (family.Kind == FontFamilyKind.Bundled && family.BundledName != null)
        {
            // Bundled fonts are resolved from the application's fonts directory.
            // Try exact name, then common extensions, then name-Regular pattern.
            string appDir = AppContext.BaseDirectory;
            string fontsDir = System.IO.Path.Combine(appDir, "fonts");
            fontPath = ResolveBundledFontFile(fontsDir, family.BundledName);

            // Fall back to system font if bundled font is not found
            if (fontPath == null)
            {
                fontPath = FontFallback.FindFallbackFont('A');
            }
        }

        cachedDefaultFontPath = fontPath;
        return fontPath;
    }

    /// <summary>
    /// Resolves a semibold font variant from the default font path.
    /// Used by LayoutSolver for measuring button labels and other bold text.
    /// </summary>
    private string? ResolveSemiBoldFontPath()
    {
        if (semiBoldResolved)
        {
            return cachedSemiBoldFontPath;
        }

        semiBoldResolved = true;
        string? basePath = ResolveDefaultFontPath();
        if (basePath == null)
        {
            return null;
        }

        string resolved = FontFallback.ResolveFontForWeight(basePath, FontWeight.SemiBold);
        // Only cache if we found a different (weight-specific) font
        cachedSemiBoldFontPath = resolved != basePath ? resolved : null;
        return cachedSemiBoldFontPath;
    }

    /// <summary>
    /// Resolves a bundled font file from the fonts directory by family name.
    /// Tries exact name, common extensions (.ttf, .otf, .ttc), and -Regular pattern.
    /// </summary>
    private static string? ResolveBundledFontFile(string fontsDir, string familyName)
    {
        if (!Directory.Exists(fontsDir))
        {
            return null;
        }

        // Exact name (e.g. "fonts/Inter" — unlikely but supported)
        string exact = System.IO.Path.Combine(fontsDir, familyName);
        if (File.Exists(exact))
        {
            return exact;
        }

        // Common font file extensions
        string[] extensions = [".ttf", ".otf", ".ttc"];
        foreach (string ext in extensions)
        {
            // Try "fonts/Inter.ttf"
            string withExt = System.IO.Path.Combine(fontsDir, familyName + ext);
            if (File.Exists(withExt))
            {
                return withExt;
            }

            // Try "fonts/Inter-Regular.ttf"
            string withRegular = System.IO.Path.Combine(fontsDir, familyName + "-Regular" + ext);
            if (File.Exists(withRegular))
            {
                return withRegular;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        renderScheduler.FrameRequested -= OnFrameRequested;
        animationScheduler.FrameRequested -= OnFrameRequested;
        SharedScheduler.Instance.FrameRequested -= OnFrameRequested;

        if (rootHost != null)
        {
            rootHost.Unmount();
            rootHost = null;
        }

        cancelFrame();
    }
}
