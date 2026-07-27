namespace Cascade.UI;

/// <summary>
/// Set of shadow presets at different elevation levels.
/// </summary>
public record ShadowSet
{
    /// <summary>Small shadow — subtle lift.</summary>
    public required ShadowSpec Sm { get; init; }

    /// <summary>Medium shadow — standard elevation.</summary>
    public required ShadowSpec Md { get; init; }

    /// <summary>Large shadow — prominent elevation.</summary>
    public required ShadowSpec Lg { get; init; }

    /// <summary>Extra-large shadow — highest elevation (dialogs, popovers).</summary>
    public required ShadowSpec Xl { get; init; }

    /// <summary>No shadow.</summary>
    public static ShadowSpec None => ShadowSpec.None;
}
