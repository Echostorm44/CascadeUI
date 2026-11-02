namespace Cascade.UI;

/// <summary>
/// Window state (normal, maximized, minimized, full-screen).
/// </summary>
public enum WindowState
{
    /// <summary>Normal windowed state.</summary>
    Normal,

    /// <summary>Maximized to fill the screen.</summary>
    Maximized,

    /// <summary>Minimized to the taskbar/dock.</summary>
    Minimized,

    /// <summary>Full-screen (no window chrome).</summary>
    FullScreen
}

/// <summary>
/// A reactive record reflecting the current window's dimensions and state.
/// All properties are reactive — reading them in <c>Render()</c> causes the
/// component to re-render when the window changes.
/// </summary>
public record WindowContext
{
    /// <summary>
    /// The current window context. Resolves to the context of the window that
    /// owns the current component. Static reactive access point — reading any
    /// property in <c>Render()</c> subscribes to changes.
    /// </summary>
    public static WindowContext Current
    {
        get
        {
            if (WindowContextSource.Active is { } source)
            {
                SignalTracker.RecordRead(source.Signal);
                return source.Value;
            }
            return defaultContext;
        }
    }

    /// <summary>The size class of a window <paramref name="width"/> logical pixels wide.</summary>
    internal static WindowSizeClass SizeClassFor(float width, Breakpoints breakpoints)
    {
        return width >= breakpoints.Large ? WindowSizeClass.Large
            : width >= breakpoints.Expanded ? WindowSizeClass.Expanded
            : width >= breakpoints.Medium ? WindowSizeClass.Medium
            : WindowSizeClass.Compact;
    }

    // What Current returns with no window (tests, tools).
    private static readonly WindowContext defaultContext = new()
    {
        Size = new Size(1280, 720),
        SizeClass = WindowSizeClass.Expanded,
        State = WindowState.Normal,
        DisplayScale = 1.0f,
        DisplaySize = new Size(1920, 1080)
    };

    /// <summary>
    /// Current window content area size in logical pixels (excludes title bar).
    /// </summary>
    public Size Size { get; init; }

    /// <summary>
    /// Window content width in logical pixels.
    /// </summary>
    public float Width => Size.Width;

    /// <summary>
    /// Window content height in logical pixels.
    /// </summary>
    public float Height => Size.Height;

    /// <summary>
    /// Semantic size class derived from <see cref="Width"/> using the framework
    /// breakpoint thresholds.
    /// </summary>
    public WindowSizeClass SizeClass { get; init; }

    /// <summary>
    /// Current window state (Normal, Maximized, Minimized, FullScreen).
    /// </summary>
    public WindowState State { get; init; }

    /// <summary>
    /// Display scale factor (1.0, 1.5, 2.0, etc.).
    /// </summary>
    public float DisplayScale { get; init; }

    /// <summary>
    /// Full display resolution in logical pixels.
    /// </summary>
    public Size DisplaySize { get; init; }
}

/// <summary>
/// One window's <see cref="WindowContext"/>: the value its frame loop publishes and the signal a
/// component's Render() subscribes to by reading <see cref="WindowContext.Current"/>. Publishing
/// a different value re-renders those components.
/// </summary>
internal sealed class WindowContextSource
{
    /// <summary>The window whose frame is being produced on this thread.</summary>
    [ThreadStatic]
    internal static WindowContextSource? Active;

    internal SignalSource Signal { get; } = new("WindowContext.Current");

    internal WindowContext Value { get; private set; } = new();

    /// <summary>Makes <paramref name="value"/> current, notifying readers when it differs.</summary>
    internal void Publish(WindowContext value)
    {
        if (Value == value)
        {
            return;
        }
        Value = value;
        Signal.NotifySubscribers();
    }
}
