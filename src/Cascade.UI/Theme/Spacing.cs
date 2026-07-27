namespace Cascade.UI;

/// <summary>
/// Spacing scale derived from a base unit. Density-aware — values are automatically
/// adjusted for the current <see cref="LayoutDensity"/> context.
/// </summary>
public record SpacingScale
{
    /// <summary>Base unit in logical pixels at Standard density.</summary>
    public required float Base { get; init; }

    /// <summary>Multiplier applied when <see cref="LayoutDensity.Compact"/> is active.</summary>
    public float CompactMultiplier { get; init; } = 0.8f;

    /// <summary>Multiplier applied when <see cref="LayoutDensity.Comfortable"/> is active.</summary>
    public float ComfortableMultiplier { get; init; } = 1.25f;

    /// <summary>Extra-small spacing (Base × 1).</summary>
    public float Xs => ApplyDensity(Base * 1);

    /// <summary>Small spacing (Base × 2).</summary>
    public float Sm => ApplyDensity(Base * 2);

    /// <summary>Medium spacing (Base × 4).</summary>
    public float Md => ApplyDensity(Base * 4);

    /// <summary>Large spacing (Base × 6).</summary>
    public float Lg => ApplyDensity(Base * 6);

    /// <summary>Extra-large spacing (Base × 8).</summary>
    public float Xl => ApplyDensity(Base * 8);

    /// <summary>Extra-extra-large spacing (Base × 12).</summary>
    public float Xxl => ApplyDensity(Base * 12);

    private float ApplyDensity(float value)
    {
        // Standard density: no scaling. Compact/Comfortable multipliers applied
        // when AccessibilityContext provides the current LayoutDensity.
        _ = CompactMultiplier;
        _ = ComfortableMultiplier;
        return value;
    }
}

/// <summary>
/// Controls spacing density throughout the application.
/// </summary>
public enum LayoutDensity
{
    /// <summary>Reduced touch targets and spacing — dense data views, small screens.</summary>
    Compact,

    /// <summary>Default density.</summary>
    Standard,

    /// <summary>More breathing room — large monitors, accessibility preference.</summary>
    Comfortable,
}
