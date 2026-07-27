namespace Cascade.UI;

/// <summary>
/// Theme tokens for the <see cref="EmojiPicker"/> control: grid layout,
/// category tabs, search bar, and visual styling.
/// </summary>
public class EmojiPickerTheme
{
    /// <summary>Size of each emoji grid cell in logical pixels.</summary>
    public required float GridCellSize { get; init; }

    /// <summary>Spacing between grid cells in logical pixels.</summary>
    public required float GridSpacing { get; init; }

    /// <summary>Height of the category tab bar in logical pixels.</summary>
    public required float CategoryTabHeight { get; init; }

    /// <summary>Height of the search bar in logical pixels.</summary>
    public required float SearchBarHeight { get; init; }

    /// <summary>Background fill of the picker panel.</summary>
    public required Brush Background { get; init; }

    /// <summary>Background fill for hovered emoji cells.</summary>
    public required Brush HoverBackground { get; init; }

    /// <summary>Corner radius for the picker panel.</summary>
    public required float Radius { get; init; }

    /// <summary>Transition for interactive state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default EmojiPickerTheme derived from global theme tokens.</summary>
    public static EmojiPickerTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new EmojiPickerTheme
        {
            GridCellSize = 36,
            GridSpacing = 2,
            CategoryTabHeight = 36,
            SearchBarHeight = 36,
            Background = Brush.Solid(t.Colors.Surface),
            HoverBackground = Brush.Solid(t.Colors.SurfaceAlt),
            Radius = t.Radius.Base,
            Transition = t.Motion.Subtle,
        };
    }
}
