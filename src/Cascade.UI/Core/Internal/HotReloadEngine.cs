namespace Cascade.UI.Core.Internal;

/// <summary>
/// Orchestrates the hot reload pipeline: receives file change notifications,
/// coordinates metadata delta application, and triggers component re-render
/// while preserving state.
/// </summary>
/// <remarks>
/// Target latency: sub-700ms from file save to visual update.
/// - File detection: &lt; 50ms
/// - Incremental compilation: &lt; 500ms
/// - Delta application: &lt; 100ms
/// - Re-render: &lt; 16ms
/// </remarks>
internal sealed class HotReloadEngine : IDisposable
{
    private readonly StatePreserver statePreserver;
    private readonly List<Action<HotReloadResult>> listeners = [];
    private bool disposed;
    private int reloadCount;

    public HotReloadEngine()
    {
        statePreserver = new StatePreserver();
    }

    /// <summary>Number of successful hot reloads performed.</summary>
    public int ReloadCount => reloadCount;

    /// <summary>Whether the engine is active and accepting updates.</summary>
    public bool IsActive => !disposed;

    /// <summary>The state preserver used by this engine.</summary>
    internal StatePreserver StatePreserver => statePreserver;

    /// <summary>Registers a listener for hot reload results.</summary>
    public void OnReload(Action<HotReloadResult> listener)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(listener);
        listeners.Add(listener);
    }

    /// <summary>
    /// Applies a metadata delta to the running application.
    /// Returns a result indicating success, failure, or restart requirement.
    /// </summary>
    public HotReloadResult ApplyDelta(MetadataDelta delta)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.RequiresRestart)
        {
            var restartResult = new HotReloadResult
            {
                Status = HotReloadStatus.RestartRequired,
                ChangedFile = delta.ChangedFile,
                Reason = delta.RestartReason ?? "Structural change requires restart",
                ElapsedMs = 0,
            };
            NotifyListeners(restartResult);
            return restartResult;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Preserve state before applying delta
        var snapshot = statePreserver.CaptureSnapshot();

        // Apply the metadata update
        bool applied = MetadataUpdateReceiver.ApplyUpdate(delta);

        if (!applied)
        {
            var failResult = new HotReloadResult
            {
                Status = HotReloadStatus.Failed,
                ChangedFile = delta.ChangedFile,
                Reason = "Failed to apply metadata delta",
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
            NotifyListeners(failResult);
            return failResult;
        }

        // Restore state after delta applied
        statePreserver.RestoreSnapshot(snapshot);

        sw.Stop();
        Interlocked.Increment(ref reloadCount);

        var successResult = new HotReloadResult
        {
            Status = HotReloadStatus.Success,
            ChangedFile = delta.ChangedFile,
            StatePreserved = true,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
        };
        NotifyListeners(successResult);
        return successResult;
    }

    /// <summary>
    /// Handles a compile error by notifying listeners without applying any delta.
    /// </summary>
    public HotReloadResult ReportCompileError(string file, string error, int line, int column)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var result = new HotReloadResult
        {
            Status = HotReloadStatus.CompileError,
            ChangedFile = file,
            Reason = error,
            ErrorLine = line,
            ErrorColumn = column,
            ElapsedMs = 0,
        };
        NotifyListeners(result);
        return result;
    }

    private void NotifyListeners(HotReloadResult result)
    {
        foreach (var listener in listeners)
        {
            listener(result);
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            listeners.Clear();
        }
    }
}

/// <summary>Result of a hot reload attempt.</summary>
internal sealed class HotReloadResult
{
    public required HotReloadStatus Status { get; init; }
    public required string ChangedFile { get; init; }
    public string? Reason { get; init; }
    public bool StatePreserved { get; init; }
    public double ElapsedMs { get; init; }
    public int ErrorLine { get; init; }
    public int ErrorColumn { get; init; }

    /// <summary>Formats the result as a terminal status line.</summary>
    public string FormatStatusLine()
    {
        return Status switch
        {
            HotReloadStatus.Success =>
                $"[cascade] \u2713 Hot reload: {System.IO.Path.GetFileName(ChangedFile)} ({ElapsedMs:F0}ms, state preserved)",
            HotReloadStatus.RestartRequired =>
                $"[cascade] \u26a0 Hot reload: {System.IO.Path.GetFileName(ChangedFile)} (full restart required \u2014 {Reason})",
            HotReloadStatus.CompileError =>
                $"[cascade] \u2717 Compile error: {ChangedFile}:{ErrorLine} \u2014 {Reason}",
            HotReloadStatus.Failed =>
                $"[cascade] \u2717 Hot reload failed: {System.IO.Path.GetFileName(ChangedFile)} \u2014 {Reason}",
            _ => $"[cascade] ? Unknown status for {ChangedFile}",
        };
    }
}

/// <summary>Status of a hot reload attempt.</summary>
internal enum HotReloadStatus
{
    Success,
    RestartRequired,
    CompileError,
    Failed,
}

/// <summary>Represents a metadata delta from incremental compilation.</summary>
internal sealed class MetadataDelta
{
    /// <summary>The file that changed.</summary>
    public required string ChangedFile { get; init; }

    /// <summary>The raw IL delta bytes (from Roslyn EnC).</summary>
    public byte[] IlDelta { get; init; } = [];

    /// <summary>The raw metadata delta bytes.</summary>
    public byte[] MetadataBytes { get; init; } = [];

    /// <summary>The raw PDB delta bytes.</summary>
    public byte[] PdbDelta { get; init; } = [];

    /// <summary>Whether this change requires a full restart.</summary>
    public bool RequiresRestart { get; init; }

    /// <summary>Reason why restart is required (if applicable).</summary>
    public string? RestartReason { get; init; }

    /// <summary>Types that were updated in this delta.</summary>
    public IReadOnlyList<string> UpdatedTypes { get; init; } = [];
}
