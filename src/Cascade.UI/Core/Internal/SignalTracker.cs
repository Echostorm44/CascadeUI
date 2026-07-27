namespace Cascade.UI;

/// <summary>
/// Tracks which reactive fields (signals) a component's <see cref="Component.Render"/>
/// method reads. After Render() completes, the tracker subscribes to those fields
/// so that future writes trigger a re-render of only the affected component.
/// </summary>
/// <remarks>
/// <para>
/// The source generator inserts calls to <see cref="RecordRead"/> in every reactive
/// field getter and calls to <see cref="NotifyWrite"/> in every reactive field setter.
/// During a Render() call, <see cref="BeginTracking"/> opens a tracking scope that
/// collects all reads. After Render(), <see cref="EndTracking"/> closes the scope
/// and returns the set of accessed signals so the caller can subscribe to changes.
/// </para>
/// <para>
/// Thread safety: tracking scopes are thread-static (one per thread). Signal
/// subscriptions use lock-free concurrent collections for multi-threaded writes.
/// </para>
/// </remarks>
internal static class SignalTracker
{
    [ThreadStatic]
    private static TrackingScope? activeScope;

    /// <summary>
    /// Begins a new tracking scope. All <see cref="RecordRead"/> calls on this
    /// thread will be collected until <see cref="EndTracking"/> is called.
    /// </summary>
    internal static TrackingScope BeginTracking()
    {
        var scope = new TrackingScope();
        activeScope = scope;
        return scope;
    }

    /// <summary>
    /// Ends the current tracking scope and returns it. The caller should use
    /// the scope's recorded signals to set up change subscriptions.
    /// </summary>
    internal static TrackingScope? EndTracking()
    {
        var scope = activeScope;
        activeScope = null;
        return scope;
    }

    /// <summary>
    /// Called by source-generated signal getters during Render() to record
    /// that the current component depends on this signal.
    /// </summary>
    internal static void RecordRead(SignalSource source)
    {
        activeScope?.RecordRead(source);
    }

    /// <summary>
    /// Called by source-generated signal setters when a reactive field value
    /// changes. Notifies all subscribers that depend on this signal.
    /// </summary>
    internal static void NotifyWrite(SignalSource source)
    {
        source.NotifySubscribers();
    }

    /// <summary>
    /// Returns true if a tracking scope is currently active on this thread.
    /// Used by diagnostics and testing.
    /// </summary>
    internal static bool IsTracking => activeScope is not null;
}

/// <summary>
/// Represents a single reactive field that can be read-tracked and subscribed to.
/// The source generator creates one <see cref="SignalSource"/> per reactive field.
/// </summary>
internal sealed class SignalSource
{
    private readonly Lock subscriberLock = new();
    private List<Action>? subscribers;

    /// <summary>
    /// A human-readable name for debugging (e.g. "MyComponent.count").
    /// </summary>
    internal string Name { get; }

    internal SignalSource(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds a subscriber that will be called when this signal's value changes.
    /// </summary>
    internal void Subscribe(Action callback)
    {
        lock (subscriberLock)
        {
            subscribers ??= [];
            subscribers.Add(callback);
        }
    }

    /// <summary>
    /// Removes a specific subscriber.
    /// </summary>
    internal void Unsubscribe(Action callback)
    {
        lock (subscriberLock)
        {
            subscribers?.Remove(callback);
        }
    }

    /// <summary>
    /// Removes all subscribers. Called when a component is unmounted.
    /// </summary>
    internal void ClearSubscribers()
    {
        lock (subscriberLock)
        {
            subscribers?.Clear();
        }
    }

    /// <summary>
    /// Notifies all current subscribers that this signal changed.
    /// Takes a snapshot of subscribers under lock, then invokes outside the lock.
    /// </summary>
    internal void NotifySubscribers()
    {
        Action[]? snapshot;
        lock (subscriberLock)
        {
            if (subscribers is null || subscribers.Count == 0)
            {
                return;
            }

            snapshot = [.. subscribers];
        }

        foreach (var callback in snapshot)
        {
            callback();
        }
    }
}

/// <summary>
/// Collects signal reads during a single Render() call. After the render
/// completes, the collected signals are used to set up subscriptions.
/// </summary>
internal sealed class TrackingScope
{
    private readonly HashSet<SignalSource> readSignals = [];

    /// <summary>
    /// Records that the given signal was read during this scope.
    /// </summary>
    internal void RecordRead(SignalSource source)
    {
        readSignals.Add(source);
    }

    /// <summary>
    /// The set of signals that were read during this tracking scope.
    /// </summary>
    internal IReadOnlySet<SignalSource> ReadSignals => readSignals;

    /// <summary>
    /// Returns the names of all tracked fields for DevTools display.
    /// </summary>
    internal IReadOnlyList<string> TrackedFieldNames
    {
        get
        {
            var names = new List<string>(readSignals.Count);
            foreach (var signal in readSignals)
            {
                names.Add(signal.Name);
            }
            return names;
        }
    }

    /// <summary>
    /// Subscribes the given callback to all signals recorded in this scope,
    /// and unsubscribes from any signals in <paramref name="previousScope"/>
    /// that are no longer read.
    /// </summary>
    internal void ApplySubscriptions(Action onChange, TrackingScope? previousScope)
    {
        if (previousScope is not null)
        {
            foreach (var old in previousScope.readSignals)
            {
                if (!readSignals.Contains(old))
                {
                    old.Unsubscribe(onChange);
                }
            }
        }

        foreach (var signal in readSignals)
        {
            if (previousScope is null || !previousScope.readSignals.Contains(signal))
            {
                signal.Subscribe(onChange);
            }
        }
    }

    /// <summary>
    /// Unsubscribes the given callback from all tracked signals.
    /// Called during unmount to clean up.
    /// </summary>
    internal void RemoveAllSubscriptions(Action onChange)
    {
        foreach (var signal in readSignals)
        {
            signal.Unsubscribe(onChange);
        }
    }
}
