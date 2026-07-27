namespace Cascade.UI;

/// <summary>
/// Framework-provided host for a native view. Passed to the adapter during
/// creation. Exposes platform window handles, current bounds, display scale,
/// focus integration, and the active compositing mode.
/// </summary>
public sealed class NativeViewHost
{
    // Internal backing fields set by the framework during layout
    internal nint parentHandle;
    internal PlatformKind currentPlatform;
    internal Rect boundsInPixels;
    internal float scale = 1.0f;
    internal NativeCompositingMode compositingMode;
    internal Action<FocusDirection>? exitFocusCallback;
    internal Action? focusEnteredCallback;

    // ── Platform Handles ─────────────────────────────────────────────

    /// <summary>
    /// The parent window handle. The native view should be created as
    /// a child of this handle.
    /// HWND on Windows, NSView* on macOS, Window on X11, wl_surface* on Wayland.
    /// </summary>
    public nint ParentHandle => parentHandle;

    /// <summary>
    /// The platform identifier for branching in cross-platform adapters.
    /// </summary>
    public PlatformKind CurrentPlatform => currentPlatform;

    // ── Bounds ───────────────────────────────────────────────────────

    /// <summary>
    /// Current position relative to the parent window's client area,
    /// in physical pixels.
    /// </summary>
    public Rect BoundsInPixels => boundsInPixels;

    /// <summary>
    /// Display scale factor (e.g. 2.0 for Retina).
    /// </summary>
    public float Scale => scale;

    // ── Focus Integration ────────────────────────────────────────────

    /// <summary>
    /// Called by the adapter to return focus to Cascade's focus system.
    /// </summary>
    /// <param name="direction">Whether focus should move forward or backward in the tab order.</param>
    public void ExitFocus(FocusDirection direction)
    {
        exitFocusCallback?.Invoke(direction);
    }

    /// <summary>
    /// Called by the adapter to notify Cascade that the native view
    /// has taken focus internally (e.g. user clicked inside it).
    /// </summary>
    public void NotifyFocusEntered()
    {
        focusEnteredCallback?.Invoke();
    }

    // ── Compositing Mode ─────────────────────────────────────────────

    /// <summary>
    /// The active compositing mode for this native view instance.
    /// </summary>
    public NativeCompositingMode CompositingMode => compositingMode;
}