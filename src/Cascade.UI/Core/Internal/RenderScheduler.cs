namespace Cascade.UI;

/// <summary>
/// Batches reactive field changes within a single frame into one re-render
/// per affected component. Maintains a dirty set of components that need
/// re-rendering and processes them on the next frame tick in top-down order
/// (parent before child) to avoid redundant renders.
/// </summary>
internal sealed class RenderScheduler
{
    private readonly Lock dirtyLock = new();
    private readonly HashSet<ComponentHost> dirtySet = [];
    private readonly List<ComponentHost> pendingBatch = [];
    private bool isProcessing;

    /// <summary>
    /// Event raised when the scheduler has dirty components but needs
    /// a frame tick to process them. The platform layer hooks this to
    /// schedule a frame callback.
    /// </summary>
    internal event Action? FrameRequested;

    /// <summary>
    /// Marks a component as needing re-render. Multiple marks within the
    /// same frame are coalesced into a single render.
    /// </summary>
    internal void MarkDirty(ComponentHost host)
    {
        bool needsFrame;
        lock (dirtyLock)
        {
            needsFrame = dirtySet.Count == 0;
            dirtySet.Add(host);
        }

        if (needsFrame && !isProcessing)
        {
            FrameRequested?.Invoke();
        }
    }

    /// <summary>
    /// Processes all dirty components. Called once per frame.
    /// Components are sorted top-down (by tree depth) so that parent
    /// renders happen before child renders, avoiding redundant work.
    /// Loops until no more components are dirtied by re-renders.
    /// </summary>
    /// <summary>
    /// Processes queued dirty re-renders. Returns true if at least one host was
    /// actually re-rendered this frame (i.e. content was rebuilt), so callers can
    /// invalidate content-derived caches (e.g. ScrollView retained layers) that a
    /// pure scroll or animation frame must not disturb.
    /// </summary>
    internal bool ProcessFrame()
    {
        isProcessing = true;
        bool reRendered = false;
        try
        {
            const int maxIterations = 10;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                lock (dirtyLock)
                {
                    if (dirtySet.Count == 0)
                    {
                        return reRendered;
                    }

                    pendingBatch.AddRange(dirtySet);
                    dirtySet.Clear();
                }

                pendingBatch.Sort(static (a, b) => a.TreeDepth.CompareTo(b.TreeDepth));

                foreach (var host in pendingBatch)
                {
                    if (!host.IsMounted)
                    {
                        continue;
                    }

                    host.ReRender();
                    reRendered = true;
                }

                pendingBatch.Clear();
            }
        }
        finally
        {
            pendingBatch.Clear();
            isProcessing = false;
        }

        return reRendered;
    }

    /// <summary>
    /// Removes a component from the dirty set. Called when a component
    /// is unmounted to prevent rendering a disposed component.
    /// </summary>
    internal void RemoveDirty(ComponentHost host)
    {
        lock (dirtyLock)
        {
            dirtySet.Remove(host);
        }
    }

    /// <summary>
    /// Returns the number of components currently marked dirty.
    /// Used for testing and diagnostics.
    /// </summary>
    internal int DirtyCount
    {
        get
        {
            lock (dirtyLock)
            {
                return dirtySet.Count;
            }
        }
    }

    /// <summary>
    /// Returns true if the scheduler is currently processing a frame.
    /// </summary>
    internal bool IsProcessing => isProcessing;
}
