namespace Cascade.UI;

/// <summary>
/// Curated named colors for decorative and data-driven use cases — avatars,
/// chart series, category indicators, file-type badges, and similar scenarios
/// where semantic tokens (Danger, Success, Warning) don't apply.
/// <para>
/// Each theme provides its own values tuned for that theme's aesthetic.
/// Apple's palette matches iOS system colors; Fluent uses Windows 11 communication
/// colors; Material3 uses M3 tonal variants. Light and dark modes differ so
/// every color looks gorgeous in context.
/// </para>
/// <para>
/// Note: <see cref="Red"/> is not the same as <see cref="ColorSet.Danger"/>.
/// Danger is semantic ("something went wrong"); Red is decorative ("this avatar
/// is red"). They may resolve to the same hex value in some themes, but they
/// are conceptually distinct and may diverge.
/// </para>
/// </summary>
public record NamedColorPalette
{
    /// <summary>A vibrant red tuned for the current theme and mode.</summary>
    public required ColorValue Red { get; init; }

    /// <summary>A warm orange tuned for the current theme and mode.</summary>
    public required ColorValue Orange { get; init; }

    /// <summary>A clear yellow tuned for the current theme and mode.</summary>
    public required ColorValue Yellow { get; init; }

    /// <summary>A natural green tuned for the current theme and mode.</summary>
    public required ColorValue Green { get; init; }

    /// <summary>A fresh mint/seafoam tuned for the current theme and mode.</summary>
    public required ColorValue Mint { get; init; }

    /// <summary>A cool teal tuned for the current theme and mode.</summary>
    public required ColorValue Teal { get; init; }

    /// <summary>A bright cyan tuned for the current theme and mode.</summary>
    public required ColorValue Cyan { get; init; }

    /// <summary>A clear blue tuned for the current theme and mode.</summary>
    public required ColorValue Blue { get; init; }

    /// <summary>A deep indigo tuned for the current theme and mode.</summary>
    public required ColorValue Indigo { get; init; }

    /// <summary>A rich purple tuned for the current theme and mode.</summary>
    public required ColorValue Purple { get; init; }

    /// <summary>A vivid pink tuned for the current theme and mode.</summary>
    public required ColorValue Pink { get; init; }

    /// <summary>A warm brown tuned for the current theme and mode.</summary>
    public required ColorValue Brown { get; init; }

    /// <summary>
    /// Returns the color at the given index, cycling through all 12 named colors.
    /// Useful for assigning colors to data series, avatars, or categories by index.
    /// </summary>
    public ColorValue this[int index]
    {
        get
        {
            var i = ((index % 12) + 12) % 12;
            return i switch
            {
                0  => Blue,
                1  => Green,
                2  => Orange,
                3  => Red,
                4  => Purple,
                5  => Teal,
                6  => Pink,
                7  => Yellow,
                8  => Indigo,
                9  => Cyan,
                10 => Mint,
                11 => Brown,
                _  => Blue,
            };
        }
    }

    /// <summary>Number of named colors in the palette.</summary>
    public static int Count => 12;
}
