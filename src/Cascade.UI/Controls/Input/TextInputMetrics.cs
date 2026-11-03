namespace Cascade.UI;

/// <summary>
/// Where a <see cref="TextInput"/>'s text starts, shared by the painter (text, placeholder, caret,
/// selection) and the input dispatcher (click-to-caret) so the two never disagree.
/// </summary>
internal static class TextInputMetrics
{
    /// <summary>Gap between the leading icon and the text.</summary>
    public const float IconGap = 8f;

    public static float FontSize(CascadeTheme theme) => theme.Typography.Scale.Body.Size;

    /// <summary>
    /// The leading icon's size (0 without one): the size the icon declares, else a little under the
    /// text's em size.
    /// </summary>
    public static float IconSize(TextInput input, CascadeTheme theme)
    {
        if (input.Icon.Paths.Length == 0)
        {
            return 0f;
        }
        return input.Icon.DefaultSize > 0f ? input.Icon.DefaultSize : MathF.Round(FontSize(theme) * 0.95f);
    }

    /// <summary>Gap between a prefix or suffix and the text.</summary>
    public const float AdornmentGap = 6f;

    /// <summary>A prefix's or suffix's laid-out width (0 when there is none).</summary>
    public static float AdornmentWidth(Node adornment) => adornment.IsLayoutEmpty ? 0f : adornment.LayoutData.Bounds.Width;

    /// <summary>Where the prefix (<c>.Prefix</c>) starts: after the padding and the leading icon.</summary>
    public static float PrefixLeft(TextInput input, CascadeTheme theme)
    {
        float icon = IconSize(input, theme);
        return theme.TextInput.PaddingH + (icon > 0f ? icon + IconGap : 0f);
    }

    /// <summary>Distance from the input's left edge to where its text starts.</summary>
    public static float ContentLeft(TextInput input, CascadeTheme theme)
    {
        float prefix = AdornmentWidth(input.PrefixNode);
        return PrefixLeft(input, theme) + (prefix > 0f ? prefix + AdornmentGap : 0f);
    }

    /// <summary>Distance from the input's right edge to where its text must stop (the padding and any suffix).</summary>
    public static float ContentRight(TextInput input, CascadeTheme theme)
    {
        float suffix = AdornmentWidth(input.SuffixNode);
        return theme.TextInput.PaddingH + (suffix > 0f ? suffix + AdornmentGap : 0f);
    }
}
