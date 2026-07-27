using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// A node in the accessibility tree. Maps to the semantic structure
/// that screen readers and assistive technologies consume. Extracted from
/// <c>AccessibilityPanel</c> so the DTO is available in Release agent builds.
/// </summary>
public sealed class AccessibleNode
{
    /// <summary>Node identifier.</summary>
    public required string NodeId { get; init; }

    /// <summary>Semantic role (button, heading, text input, etc.).</summary>
    public AccessibleRole Role { get; init; }

    /// <summary>Accessible label (primary text for assistive technology).</summary>
    public string? Label { get; init; }

    /// <summary>Accessible description (secondary text).</summary>
    public string? Description { get; init; }

    /// <summary>Whether this node is focusable.</summary>
    public bool Focusable { get; init; }

    /// <summary>Whether this node is currently focused.</summary>
    public bool Focused { get; init; }

    /// <summary>Whether this node is disabled.</summary>
    public bool Disabled { get; init; }

    /// <summary>Tab index for focus ordering.</summary>
    public int TabIndex { get; init; }

    /// <summary>Live region politeness level.</summary>
    public LiveRegion LiveRegion { get; init; }

    /// <summary>ARIA-equivalent state properties.</summary>
    public IReadOnlyDictionary<string, string> StateProperties { get; init; } = new Dictionary<string, string>();

    /// <summary>Child nodes in the accessibility tree.</summary>
    public IReadOnlyList<AccessibleNode> Children { get; init; } = [];
}

/// <summary>Live region politeness level.</summary>
public enum LiveRegion
{
    Off,
    Polite,
    Assertive,
}

#endif
