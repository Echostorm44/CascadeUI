using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Detail information for a selected node. Extracted from
/// <c>InspectorPanel</c> so the DTO is available in Release agent builds.
/// </summary>
public sealed class NodeDetail
{
    /// <summary>Node identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Full CLR type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Source location (file:line).</summary>
    public string? SourceLocation { get; init; }

    /// <summary>Computed layout bounds.</summary>
    public Rect Bounds { get; init; }

    /// <summary>Applied padding.</summary>
    public EdgeInsets Padding { get; init; }

    /// <summary>Applied margin.</summary>
    public EdgeInsets Margin { get; init; }

    /// <summary>Accessibility properties.</summary>
    public AccessibilityInfo? Accessibility { get; init; }

    /// <summary>Reactive state fields with current values.</summary>
    public IReadOnlyList<SignalInfo> Signals { get; init; } = [];

    /// <summary>Computed properties with current values.</summary>
    public IReadOnlyList<ComputedInfo> Computed { get; init; } = [];

    /// <summary>Total render count since mount.</summary>
    public int RenderCount { get; init; }

    /// <summary>Signal that triggered the most recent re-render.</summary>
    public string? LastRenderTrigger { get; init; }
}

/// <summary>Accessibility info for an inspected node.</summary>
public sealed class AccessibilityInfo
{
    /// <summary>Semantic role.</summary>
    public AccessibleRole Role { get; init; }

    /// <summary>Accessible label text.</summary>
    public string? Label { get; init; }

    /// <summary>Accessible description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the node is focusable.</summary>
    public bool Focusable { get; init; }

    /// <summary>Current focus state.</summary>
    public bool Focused { get; init; }

    /// <summary>Whether the node is disabled.</summary>
    public bool Disabled { get; init; }
}

/// <summary>A reactive signal field snapshot.</summary>
public sealed class SignalInfo
{
    /// <summary>Field name.</summary>
    public required string Name { get; init; }

    /// <summary>CLR type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Current value serialized to string.</summary>
    public required string Value { get; init; }

    /// <summary>Whether this field is readonly (opted out of reactivity).</summary>
    public bool IsReadonly { get; init; }

    /// <summary>Number of subscriber components.</summary>
    public int DependentCount { get; init; }
}

/// <summary>A computed property snapshot.</summary>
public sealed class ComputedInfo
{
    /// <summary>Property name.</summary>
    public required string Name { get; init; }

    /// <summary>CLR type name.</summary>
    public required string TypeName { get; init; }

    /// <summary>Current value serialized to string.</summary>
    public required string Value { get; init; }

    /// <summary>Signal fields this computed reads from.</summary>
    public IReadOnlyList<string> Reads { get; init; } = [];

    /// <summary>Whether the cached value is stale.</summary>
    public bool IsStale { get; init; }
}

#endif
