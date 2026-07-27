namespace Cascade.UI;

/// <summary>
/// When a position sits at a line break or bidirectional boundary,
/// affinity determines which visual side the caret appears on.
/// </summary>
public enum TextAffinity
{
    /// <summary>Caret belongs to the preceding run.</summary>
    Upstream,

    /// <summary>Caret belongs to the following run (default).</summary>
    Downstream
}
