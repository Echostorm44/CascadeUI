using Cascade.UI;

namespace CascadeControls;

/// <summary>
/// Theme tokens for <see cref="SampleControl"/>.
/// Override these in your CascadeTheme subclass to customize appearance.
/// </summary>
public sealed record SampleControlTheme
{
    public required Color Background { get; init; }
    public required Color Foreground { get; init; }
    public required float CornerRadius { get; init; }
    public required float Padding { get; init; }

    public static SampleControlTheme Default(CascadeTheme theme) => new()
    {
        Background   = theme.Surface,
        Foreground   = theme.OnSurface,
        CornerRadius = theme.CornerRadiusMedium,
        Padding      = 12,
    };
}
