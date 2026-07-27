namespace Cascade.UI;

/// <summary>
/// Corner radius scale for controls and containers.
/// </summary>
public record RadiusScale
{
    /// <summary>No rounding (0).</summary>
    public required float None { get; init; }

    /// <summary>Small radius — badges, chips.</summary>
    public required float Sm { get; init; }

    /// <summary>Base radius — inputs, most controls.</summary>
    public required float Base { get; init; }

    /// <summary>Medium radius — cards, panels.</summary>
    public required float Md { get; init; }

    /// <summary>Large radius — large cards, sheets.</summary>
    public required float Lg { get; init; }

    /// <summary>Extra-large radius — prominent cards, dialogs.</summary>
    public required float Xl { get; init; }

    /// <summary>Full rounding (9999) — pills, fully rounded elements.</summary>
    public required float Full { get; init; }
}
