namespace Cascade.UI;

/// <summary>
/// Hit testing for laid-out text. Converts point coordinates to document offsets.
/// </summary>
public sealed class TextHitTest
{
    /// <summary>
    /// Returns the document offset and edge information for the character
    /// at the given point in the layout.
    /// </summary>
    public static TextHitResult HitTest(TextLayoutResult layout, float x, float y)
    {
        return layout.HitTest(x, y);
    }
}
