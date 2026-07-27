namespace Cascade.UI;

/// <summary>
/// Configuration node for the window title bar area. Cascade owns the entire
/// window rectangle — this node controls the title bar's content, height,
/// and appearance. Place at the top of the app shell component tree.
/// </summary>
public class WindowChrome : Node
{
    /// <summary>
    /// A sentinel value representing no window chrome (frameless window).
    /// Setting <c>App.Window.Chrome = WindowChrome.None</c> removes all
    /// title bar decoration — the entire window surface is application content.
    /// </summary>
    public static WindowChrome None { get; } = new();

    /// <summary>
    /// The window title displayed in the title bar.
    /// Can be a plain string or a fully custom title node.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Custom title node, used when a plain string is insufficient.
    /// When set, <see cref="Title"/> is ignored for display purposes.
    /// </summary>
    public Node? TitleNode { get; init; }

    /// <summary>
    /// Content placed on the leading side of the title bar.
    /// On macOS this appears after the traffic light buttons; on Windows it
    /// appears at the left edge.
    /// </summary>
    public Node? Leading { get; init; }

    /// <summary>
    /// Content placed on the trailing side of the title bar.
    /// Inserted between the last app control and the system buttons on Windows.
    /// The framework clips trailing content so it never overlaps system buttons.
    /// </summary>
    public Node? Trailing { get; init; }

    /// <summary>
    /// Custom system button definitions. When null, the active theme provides
    /// default system button rendering. Button position always follows OS convention.
    /// </summary>
    public SystemButtons? SystemButtons { get; init; }

    /// <summary>
    /// Height of the title bar in logical pixels.
    /// Default: platform standard (32px Windows 11, 28px macOS).
    /// </summary>
    public float? Height { get; init; }

    /// <summary>
    /// Background color of the title bar.
    /// Default: the current theme's surface color.
    /// </summary>
    public ColorValue? Background { get; init; }

    /// <summary>
    /// Whether to display the title text. Default: true.
    /// </summary>
    public bool ShowTitle { get; init; } = true;

    /// <summary>
    /// Alignment of the title text within the title bar.
    /// Default: platform standard (Left on Windows, Center on macOS).
    /// </summary>
    public Alignment TitleAlignment { get; init; }
}

/// <summary>
/// Custom system button definitions for a <see cref="WindowChrome"/>.
/// Buttons are positioned on the OS-correct side regardless of content.
/// </summary>
public sealed class SystemButtons
{
    /// <summary>The minimize button node.</summary>
    public Node Minimize { get; init; } = Node.Empty;

    /// <summary>The maximize/restore button node.</summary>
    public Node Maximize { get; init; } = Node.Empty;

    /// <summary>The close button node.</summary>
    public Node Close { get; init; } = Node.Empty;

    /// <summary>
    /// Creates a custom system button set.
    /// </summary>
    public static SystemButtons Custom(Node minimize, Node maximize, Node close)
    {
        return new SystemButtons
        {
            Minimize = minimize,
            Maximize = maximize,
            Close = close
        };
    }
}
