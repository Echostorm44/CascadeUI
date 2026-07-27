using Cascade.UI;

namespace ThemeGallery;

/// <summary>
/// Shared helpers for theme-aware colors used across all gallery pages.
/// </summary>
internal static class ThemeHelper
{
    /// <summary>Muted text color that adapts to light/dark mode.</summary>
    internal static ColorValue MutedText =>
        ThemeSwitcher.IsDarkMode ? new ColorValue("#999999") : new ColorValue("#666666");

    /// <summary>Very muted text for labels, captions.</summary>
    internal static ColorValue SubtleText =>
        ThemeSwitcher.IsDarkMode ? new ColorValue("#777777") : new ColorValue("#888888");

    /// <summary>Content background that adapts to light/dark mode.</summary>
    internal static ColorValue ContentBackground =>
        ThemeSwitcher.IsDarkMode ? new ColorValue("#121212") : new ColorValue("#FFFFFF");

    /// <summary>Sidebar background that adapts to light/dark mode.</summary>
    internal static ColorValue SidebarBackground =>
        ThemeSwitcher.IsDarkMode ? new ColorValue("#1A1A1A") : new ColorValue("#F5F5F5");

    /// <summary>Border color that adapts to light/dark mode.</summary>
    internal static ColorValue BorderColor =>
        ThemeSwitcher.IsDarkMode ? new ColorValue("#333333") : new ColorValue("#E0E0E0");

    /// <summary>Standard section layout used by all gallery pages.</summary>
    internal static Node Section(string title, string description, Node content) =>
        new Column(spacing: 12, children:
        [
            new Label(title).Bold().FontSize(18),
            new Label(description).FontSize(13).Color(MutedText),
            content.Padding(EdgeInsets.Only(top: 8))
        ]);
}
