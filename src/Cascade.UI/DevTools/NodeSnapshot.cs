using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Snapshot of a single node in the component tree. Extracted from
/// <c>InspectorPanel</c> so the DTO is available in Release agent builds
/// (<c>-p:CascadeDevTools=true</c>) while the panel's on-screen rendering
/// remains Debug-only.
/// </summary>
public sealed class NodeSnapshot
{
    /// <summary>Unique ID for this node within the current tree.</summary>
    public required string Id { get; init; }

    /// <summary>The CLR type name of the node or component.</summary>
    public required string TypeName { get; init; }

    /// <summary>Source file path where this node was created.</summary>
    public string? SourceFile { get; init; }

    /// <summary>Source line number.</summary>
    public int? SourceLine { get; init; }

    /// <summary>Computed layout bounds.</summary>
    public Rect Bounds { get; init; }

    /// <summary>Accessibility role assigned to this node.</summary>
    public AccessibleRole? Role { get; init; }

    /// <summary>Accessibility label.</summary>
    public string? AccessibleLabel { get; init; }

    /// <summary>Number of times this component has re-rendered since mount.</summary>
    public int RenderCount { get; init; }

    /// <summary>Reactive field names this component depends on.</summary>
    public IReadOnlyList<string> ReactiveDependencies { get; init; } = [];

    /// <summary>Child nodes.</summary>
    public IReadOnlyList<NodeSnapshot> Children { get; init; } = [];
}

#endif
