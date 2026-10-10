namespace Cascade.UI;

/// <summary>
/// The colours a menu panel is painted with. In-window menus take them from the theme's dropdown
/// tokens; the tray menu takes them from its own (light or dark) theme, or from the system colours
/// when Windows high contrast is on, so the menu follows the user's contrast theme exactly as a
/// native menu would.
/// </summary>
internal readonly record struct MenuPalette(
    ColorValue Background,
    ColorValue Border,
    float BorderWidth,
    ColorValue Separator,
    ColorValue Text,
    ColorValue HighlightBackground,
    ColorValue HighlightText,
    ColorValue DisabledText,
    ColorValue MutedText,
    ColorValue Accent,
    ColorValue Danger,
    bool IsSystemColors = false)
{
    /// <summary>Text opacity of a disabled item (theme palettes).</summary>
    internal const float DisabledAlpha = 0.4f;

    /// <summary>The theme's dropdown colours (what every in-window menu uses).</summary>
    internal static MenuPalette FromTheme(CascadeTheme theme)
    {
        var st = theme.Select;
        return new MenuPalette(
            Background:          st.DropdownBackground,
            Border:              st.BorderColor,
            BorderWidth:         st.BorderWidth,
            Separator:           st.BorderColor,
            Text:                st.TextColor,
            HighlightBackground: st.ItemHoverBackground,
            HighlightText:       st.TextColor,
            DisabledText:        st.TextColor.ScaleAlpha(DisabledAlpha),
            MutedText:           st.TextColor.ScaleAlpha(0.55f),
            Accent:              theme.Colors.Primary,
            Danger:              theme.Colors.Danger);
    }

    /// <summary>
    /// The Windows system colours a native menu uses in a high-contrast theme: menu background
    /// and text, the highlight pair for the hot item, grey text for disabled items, and a
    /// window-frame border.
    /// </summary>
    internal static MenuPalette FromSystemColors(Func<int, ColorValue> sysColor)
    {
        ArgumentNullException.ThrowIfNull(sysColor);
        var text = sysColor(SystemColorIndex.MenuText);
        var highlight = sysColor(SystemColorIndex.Highlight);
        return new MenuPalette(
            Background:          sysColor(SystemColorIndex.Menu),
            Border:              sysColor(SystemColorIndex.WindowFrame),
            BorderWidth:         1f,
            Separator:           text,
            Text:                text,
            HighlightBackground: highlight,
            HighlightText:       sysColor(SystemColorIndex.HighlightText),
            DisabledText:        sysColor(SystemColorIndex.GrayText),
            MutedText:           text,
            Accent:              text,
            Danger:              text,
            IsSystemColors:      true);
    }
}

/// <summary>The <c>GetSysColor</c> indexes a high-contrast menu is drawn with.</summary>
internal static class SystemColorIndex
{
    internal const int Menu = 4;
    internal const int WindowFrame = 6;
    internal const int MenuText = 7;
    internal const int Highlight = 13;
    internal const int HighlightText = 14;
    internal const int GrayText = 17;
}

/// <summary>How a menu panel is framed.</summary>
internal enum MenuPanelChrome
{
    /// <summary>Drawn inside an app window: rounded, bordered and shadowed by the painter.</summary>
    InWindow,

    /// <summary>The whole of its own popup window, which the window manager rounds, borders and shadows.</summary>
    SystemFramed,

    /// <summary>The whole of its own square popup window: the painter draws a 1 px border.</summary>
    Framed,
}
