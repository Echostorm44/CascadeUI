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
        get => current.Value ?? defaultContext;
    }

    /// <summary>
    /// Sets the current window context for the active async flow.
    /// </summary>
    internal static void SetCurrent(WindowContext context)
    {
        current.Value = context;
    }

    private static readonly AsyncLocal<WindowContext> current = new();
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
