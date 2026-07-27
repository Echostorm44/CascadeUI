namespace Cascade.UI;

/// <summary>
/// Semantic color tokens used throughout a theme. Covers primary, surface,
/// text, border, and semantic (danger/success/warning) colors.
/// </summary>
public record ColorSet
{
    /// <summary>Primary interactive color — buttons, links, focus rings.</summary>
    public required ColorValue Primary { get; init; }

    /// <summary>Text rendered in the primary color.</summary>
    public required ColorValue PrimaryText { get; init; }

    /// <summary>Main surface color — cards, list rows, input fields.</summary>
    public required ColorValue Surface { get; init; }

    /// <summary>Alternate surface — sidebars, toolbars, secondary panels.</summary>
    public required ColorValue SurfaceAlt { get; init; }

    /// <summary>Outermost window/page background.</summary>
    public required ColorValue Background { get; init; }

    /// <summary>Border and separator color.</summary>
    public required ColorValue Border { get; init; }

    /// <summary>Primary text color.</summary>
    public required ColorValue Text { get; init; }

    /// <summary>Secondary/muted text — captions, hints, placeholders.</summary>
    public required ColorValue TextMuted { get; init; }

    /// <summary>Text rendered on primary-colored backgrounds.</summary>
    public required ColorValue TextOnPrimary { get; init; }

    /// <summary>Danger/error color.</summary>
    public required ColorValue Danger { get; init; }

    /// <summary>Subtle danger color — error background tints.</summary>
    public required ColorValue DangerSubtle { get; init; }

    /// <summary>Success color.</summary>
    public required ColorValue Success { get; init; }

    /// <summary>Subtle success color — success background tints.</summary>
    public required ColorValue SuccessSubtle { get; init; }

    /// <summary>Warning color.</summary>
    public required ColorValue Warning { get; init; }

    /// <summary>Subtle warning color — warning background tints.</summary>
    public required ColorValue WarningSubtle { get; init; }

    /// <summary>Focus ring color.</summary>
    public required ColorValue Focus { get; init; }

    /// <summary>Semantic alias for <see cref="SurfaceAlt"/>.</summary>
    public ColorValue Muted => SurfaceAlt;
}
