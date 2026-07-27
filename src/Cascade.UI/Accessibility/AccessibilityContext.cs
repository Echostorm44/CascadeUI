namespace Cascade.UI;

/// <summary>
/// A reactive record reflecting the current accessibility preferences from
/// the operating system. All properties are reactive — reading them in
/// <c>Render()</c> causes the component to re-render when they change.
/// </summary>
public record AccessibilityContext
{
    /// <summary>
    /// The current accessibility context. Resolves to the context of the
    /// window that owns the current component. Static reactive access point —
    /// reading any property in <c>Render()</c> subscribes to changes.
    /// </summary>
    public static AccessibilityContext Current
    {
        get => AccessibilityTreeBuilder.GetCurrentContext();
    }

    /// <summary>
    /// Display scale factor from the OS (1.0 = 100%, 1.5 = 150%, 2.0 = 200%).
    /// </summary>
    public float DisplayScale { get; init; }

    /// <summary>
    /// Text scale factor from the OS accessibility settings.
    /// </summary>
    public float TextScale { get; init; }

    /// <summary>
    /// True when the OS reports a "reduce motion" preference. When true, all
    /// animations are suppressed — transitions complete instantly, springs jump
    /// to target, page transitions are instant cuts.
    /// </summary>
    public bool ReducedMotion { get; init; }

    /// <summary>
    /// True when the OS reports high contrast mode. The theme automatically
    /// switches to the high-contrast variant.
    /// </summary>
    public bool HighContrast { get; init; }

    /// <summary>
    /// True when the OS reports a "reduce transparency" preference. Backdrop
    /// effects (blur, acrylic, mica) are disabled and replaced with solid surfaces.
    /// </summary>
    public bool ReducedTransparency { get; init; }

    /// <summary>
    /// True when a cursor (mouse/trackpad) is available.
    /// </summary>
    public bool HasCursor { get; init; }

    /// <summary>
    /// The user's preferred layout density, affecting spacing multipliers
    /// throughout the theme system.
    /// </summary>
    public LayoutDensity LayoutDensity { get; init; }

    /// <summary>
    /// True when a screen reader is active (detected via platform API:
    /// UIA on Windows, VoiceOver on macOS, AT-SPI on Linux).
    /// Use sparingly — prefer making the app accessible without conditional logic.
    /// </summary>
    public bool ScreenReaderActive { get; init; }
}
