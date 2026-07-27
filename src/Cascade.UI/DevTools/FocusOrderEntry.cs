namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Focus order entry showing the tab navigation sequence.
/// </summary>
/// <remarks>
/// Extracted from <see cref="AccessibilityPanel"/> so it remains visible to
/// <see cref="NodeTreeWalker"/> and the MCP tool surface in Release builds
/// compiled with <c>CASCADE_DEVTOOLS</c>.
/// </remarks>
public sealed class FocusOrderEntry
{
    /// <summary>Position in the focus order (1-based).</summary>
    public int Order { get; init; }

    /// <summary>Node ID.</summary>
    public required string NodeId { get; init; }

    /// <summary>Node type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Node label or text content.</summary>
    public string? Label { get; init; }

    /// <summary>Bounds for drawing focus order indicators.</summary>
    public Rect Bounds { get; init; }
}

#endif
