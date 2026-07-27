namespace Cascade.UI;

/// <summary>
/// An alignment within a container, expressed as a normalized (X, Y) pair.
/// X ranges from -1 (left) to 1 (right). Y ranges from -1 (top) to 1 (bottom).
/// </summary>
public readonly record struct Alignment(float X, float Y)
{
    // ── Cardinal alignments ───────────────────────────────────────────

    /// <summary>Top-left corner (-1, -1).</summary>
    public static readonly Alignment TopLeft = new(-1, -1);

    /// <summary>Top center (0, -1).</summary>
    public static readonly Alignment TopCenter = new(0, -1);

    /// <summary>Top-right corner (1, -1).</summary>
    public static readonly Alignment TopRight = new(1, -1);

    /// <summary>Center-left (-1, 0).</summary>
    public static readonly Alignment CenterLeft = new(-1, 0);

    /// <summary>Dead center (0, 0).</summary>
    public static readonly Alignment Center = new(0, 0);

    /// <summary>Center-right (1, 0).</summary>
    public static readonly Alignment CenterRight = new(1, 0);

    /// <summary>Bottom-left corner (-1, 1).</summary>
    public static readonly Alignment BottomLeft = new(-1, 1);

    /// <summary>Bottom center (0, 1).</summary>
    public static readonly Alignment BottomCenter = new(0, 1);

    /// <summary>Bottom-right corner (1, 1).</summary>
    public static readonly Alignment BottomRight = new(1, 1);

    // ── Leading/trailing variants (RTL-aware) ─────────────────────────

    /// <summary>Top-leading: top-left in LTR, top-right in RTL.</summary>
    public static readonly Alignment TopLeading = new(-1, -1);

    /// <summary>Top-trailing: top-right in LTR, top-left in RTL.</summary>
    public static readonly Alignment TopTrailing = new(1, -1);

    /// <summary>Center-leading: center-left in LTR, center-right in RTL.</summary>
    public static readonly Alignment CenterLeading = new(-1, 0);

    /// <summary>Center-trailing: center-right in LTR, center-left in RTL.</summary>
    public static readonly Alignment CenterTrailing = new(1, 0);

    /// <summary>Bottom-leading: bottom-left in LTR, bottom-right in RTL.</summary>
    public static readonly Alignment BottomLeading = new(-1, 1);

    /// <summary>Bottom-trailing: bottom-right in LTR, bottom-left in RTL.</summary>
    public static readonly Alignment BottomTrailing = new(1, 1);
}
