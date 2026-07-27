namespace Cascade.UI;

/// <summary>
/// A position in a text document, expressed as a character offset with
/// bidirectional affinity.
/// </summary>
public readonly record struct TextPosition(
    int Offset,
    TextAffinity Affinity = TextAffinity.Downstream
)
{
    /// <summary>Position at the start of the document with downstream affinity.</summary>
    public static readonly TextPosition Zero = new(0);
}
