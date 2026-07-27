using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if CASCADE_DEVTOOLS

/// <summary>
/// Signal dependency graph for a component, showing which signals it reads
/// and which signal triggered the last re-render. Extracted from
/// <c>PerformancePanel</c> so the DTO is available in Release agent builds.
/// </summary>
public sealed class SignalDependencyGraph
{
    /// <summary>Component name.</summary>
    public required string ComponentName { get; init; }

    /// <summary>Signals this component depends on.</summary>
    public IReadOnlyList<SignalDependency> Dependencies { get; init; } = [];
}

/// <summary>A single dependency in the signal graph.</summary>
public sealed class SignalDependency
{
    /// <summary>Signal field name.</summary>
    public required string SignalName { get; init; }

    /// <summary>Owning component or store type name.</summary>
    public required string Owner { get; init; }

    /// <summary>Whether this signal triggered the most recent re-render.</summary>
    public bool WasLastTrigger { get; init; }

    /// <summary>Number of times this signal has changed since mount.</summary>
    public int ChangeCount { get; init; }
}

#endif
