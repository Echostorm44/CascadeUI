namespace Cascade.UI;

/// <summary>
/// Frame-level animation tick coordinator. Maintains the set of active
/// animations, advances them each frame, and removes completed ones.
/// Animation of visual-only properties does not trigger layout re-computation.
/// </summary>
internal sealed class AnimationScheduler
{
    private readonly List<AnimationEntry> entries = [];
    private readonly List<AnimationEntry> pendingAdd = [];
    private readonly List<int> pendingRemove = [];
    private bool isTicking;
    private int nextId;

    /// <summary>
    /// Raised when the scheduler transitions from zero to one or more
    /// active animations. The platform layer hooks this to begin frame callbacks.
    /// </summary>
    internal event Action? FrameRequested;

    /// <summary>The number of currently active (non-paused, non-complete) animations.</summary>
    internal int ActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsComplete && !entries[i].IsPaused)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>True if there are any active animations.</summary>
    internal bool HasActiveAnimations => ActiveCount > 0;

    /// <summary>
    /// Registers an animation tick callback and returns a handle for control.
    /// </summary>
    /// <param name="tick">Called each frame with delta time in seconds.</param>
    /// <param name="isComplete">Checked after each tick; returns true when animation is done.</param>
    internal AnimationHandle Register(Action<float> tick, Func<bool> isComplete)
    {
        int id = nextId++;
        var entry = new AnimationEntry(id, tick, isComplete);

        if (isTicking)
        {
            pendingAdd.Add(entry);
        }
        else
        {
            bool wasEmpty = entries.Count == 0;
            entries.Add(entry);
            if (wasEmpty)
            {
                FrameRequested?.Invoke();
            }
        }

        return new AnimationHandle(this, id);
    }

    /// <summary>
    /// Removes an animation by its handle ID.
    /// </summary>
    internal void Unregister(int handleId)
    {
        if (isTicking)
        {
            pendingRemove.Add(handleId);
            return;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Id == handleId)
            {
                entries.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Removes every registered animation immediately. Intended for test
    /// isolation: production code lets animations complete and self-remove, so
    /// the shared scheduler is never force-cleared at runtime. Not valid to call
    /// while a tick is in progress.
    /// </summary>
    internal void Clear()
    {
        entries.Clear();
        pendingAdd.Clear();
        pendingRemove.Clear();
    }

    /// <summary>
    /// Pauses the animation with the given handle.
    /// </summary>
    internal void Pause(int handleId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == handleId)
            {
                entries[i].IsPaused = true;
                return;
            }
        }
    }

    /// <summary>
    /// Resumes the animation with the given handle.
    /// </summary>
    internal void Resume(int handleId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Id == handleId)
            {
                entries[i].IsPaused = false;
                return;
            }
        }

        if (HasActiveAnimations)
        {
            FrameRequested?.Invoke();
        }
    }

    /// <summary>
    /// Advances all active animations by the given time delta. Removes
    /// completed animations after the tick pass.
    /// </summary>
    internal void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || entries.Count == 0)
        {
            return;
        }

        isTicking = true;
        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.IsComplete || entry.IsPaused)
                {
                    continue;
                }

                entry.Tick(deltaTime);

                if (entry.CheckComplete())
                {
                    entry.IsComplete = true;
                }
            }
        }
        finally
        {
            isTicking = false;
        }

        // Apply deferred mutations
        foreach (var id in pendingRemove)
        {
            Unregister(id);
        }
        pendingRemove.Clear();

        foreach (var entry in pendingAdd)
        {
            entries.Add(entry);
        }
        pendingAdd.Clear();

        // Sweep completed entries
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].IsComplete)
            {
                entries.RemoveAt(i);
            }
        }

        if (HasActiveAnimations)
        {
            FrameRequested?.Invoke();
        }
    }

    /// <summary>
    /// Returns a snapshot of all currently registered animations and their state.
    /// Used by DevTools to report animation status.
    /// </summary>
    internal IReadOnlyList<AnimationSnapshot> GetSnapshot()
    {
        var result = new List<AnimationSnapshot>(entries.Count + pendingAdd.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            result.Add(new AnimationSnapshot(e.Id, e.IsPaused, e.IsComplete));
        }
        for (int i = 0; i < pendingAdd.Count; i++)
        {
            var e = pendingAdd[i];
            result.Add(new AnimationSnapshot(e.Id, e.IsPaused, e.IsComplete));
        }
        return result;
    }

    private sealed class AnimationEntry(int id, Action<float> tick, Func<bool> isComplete)
    {
        internal int Id { get; } = id;
        internal bool IsPaused { get; set; }
        internal bool IsComplete { get; set; }

        internal void Tick(float dt)
        {
            tick(dt);
        }

        internal bool CheckComplete()
        {
            return isComplete();
        }
    }
}

/// <summary>
/// Handle returned by <see cref="AnimationScheduler.Register"/> for
/// controlling a registered animation (pause, resume, unregister).
/// </summary>
internal readonly struct AnimationHandle
{
    private readonly AnimationScheduler scheduler;

    internal AnimationHandle(AnimationScheduler scheduler, int id)
    {
        this.scheduler = scheduler;
        Id = id;
    }

    /// <summary>The unique ID for this animation registration.</summary>
    internal int Id { get; }

    /// <summary>Pauses this animation.</summary>
    internal void Pause()
    {
        scheduler.Pause(Id);
    }

    /// <summary>Resumes this animation.</summary>
    internal void Resume()
    {
        scheduler.Resume(Id);
    }

    /// <summary>Removes this animation from the scheduler.</summary>
    internal void Unregister()
    {
        scheduler.Unregister(Id);
    }
}

/// <summary>
/// Point-in-time snapshot of an animation's state for DevTools reporting.
/// </summary>
internal readonly record struct AnimationSnapshot(int Id, bool IsPaused, bool IsComplete);
