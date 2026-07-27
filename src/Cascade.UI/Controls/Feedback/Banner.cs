namespace Cascade.UI;

/// <summary>
/// A persistent notification banner displayed at the top or bottom of a
/// container. Used for system messages, warnings, and announcements that
/// should remain visible until dismissed.
/// </summary>
/// <remarks>
/// Banner extends Node directly (not Component) because it has no reactive
/// state. The layout solver and painter handle Banner via custom
/// MeasureBanner/PaintBanner methods for precise visual control over the
/// icon badge, message text, and dismiss affordance.
/// </remarks>
public class Banner : Node
{
    internal string Message { get; }
    internal BannerType Type { get; }
    internal Action? OnDismiss { get; }
    internal Node? CustomIcon { get; }
    internal Node? Action { get; }
    internal ColorValue AccentColor { get; }

    /// <summary>
    /// Creates a banner notification.
    /// </summary>
    /// <param name="message">The banner message text.</param>
    /// <param name="type">Visual type determining color and icon.</param>
    /// <param name="onDismiss">
    /// Optional dismiss callback. When provided, a dismiss button is shown.
    /// </param>
    /// <param name="icon">Optional custom icon. Uses type default when null.</param>
    /// <param name="action">Optional action node (e.g., a button).</param>
    public Banner(
        string message,
        BannerType type = BannerType.Info,
        Action? onDismiss = null,
        Node? icon = null,
        Node? action = null)
    {
        Message = message;
        Type = type;
        OnDismiss = onDismiss;
        CustomIcon = icon;
        Action = action;

        var (bg, accent) = GetBannerColors(type);
        AccentColor = accent;

        this.Background(bg);
        this.Border(accent, 1f);
        this.CornerRadius(8f);
        this.Padding(14f, 12f);
    }

    /// <summary>
    /// Absolute bounds set by the painter for hit testing the dismiss button.
    /// </summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Absolute bounds of the dismiss target for click handling.
    /// </summary>
    internal Rect DismissHitRect { get; set; }

    internal string GetIconText()
    {
        return Type switch
        {
            BannerType.Info    => "i",
            BannerType.Success => "✓",
            BannerType.Warning => "!",
            BannerType.Error   => "✕",
            _ => "i",
        };
    }

    private static (ColorValue bg, ColorValue accent) GetBannerColors(BannerType type)
    {
        bool dark = ThemeSwitcher.IsDarkMode;
        return type switch
        {
            BannerType.Info    => (dark ? new ColorValue("#1A3A5C") : new ColorValue("#E3F2FD"),
                                  new ColorValue("#4A9AF5")),
            BannerType.Success => (dark ? new ColorValue("#1C3B2A") : new ColorValue("#E8F5E9"),
                                  new ColorValue("#66BB6A")),
            BannerType.Warning => (dark ? new ColorValue("#3D2E0F") : new ColorValue("#FFF8E1"),
                                  new ColorValue("#FFA726")),
            BannerType.Error   => (dark ? new ColorValue("#3D1515") : new ColorValue("#FFEBEE"),
                                  new ColorValue("#EF5350")),
            _ => (dark ? new ColorValue("#1A3A5C") : new ColorValue("#E3F2FD"),
                  new ColorValue("#4A9AF5")),
        };
    }
}
/// <summary>
/// Visual type for a <see cref="Banner"/> notification.
/// </summary>
public enum BannerType
{
    /// <summary>Informational — primary color accent.</summary>
    Info,

    /// <summary>Success — success color accent.</summary>
    Success,

    /// <summary>Warning — warning color accent.</summary>
    Warning,

    /// <summary>Error — danger color accent.</summary>
    Error
}
