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

    /// <summary>The leading icon's size (0 without one): a little under the text's em size.</summary>
    public static float IconSize(TextInput input, CascadeTheme theme)
    {
        return input.Icon.Paths.Length == 0 ? 0f : MathF.Round(FontSize(theme) * 0.95f);
    }

    /// <summary>Distance from the input's left edge to where its text starts.</summary>
    public static float ContentLeft(TextInput input, CascadeTheme theme)
    {
        float icon = IconSize(input, theme);
        return theme.TextInput.PaddingH + (icon > 0f ? icon + IconGap : 0f);
    }
}
