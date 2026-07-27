using System;
using System.Collections.Generic;

namespace Cascade.IDE.Shared;

public sealed class PreviewProcessManager : IDisposable
{
    private readonly List<PreviewProcess> activeProcesses = [];
    private readonly object syncLock = new();
    private bool disposed;

    public IReadOnlyList<PreviewProcess> ActiveProcesses
    {
        get
        {
            lock (syncLock)
            {
                return activeProcesses.ToArray();
            }
        }
    }

    public PreviewProcess CreatePreview(PreviewTarget target, PreviewOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            options ??= new PreviewOptions();
            var process = new PreviewProcess
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Target = target,
                Options = options,
                Status = PreviewStatus.Ready,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            activeProcesses.Add(process);
            return process;
        }
    }

    public void UpdateTarget(PreviewProcess process, PreviewTarget newTarget)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(newTarget);

        process.Target = newTarget;
        process.Status = PreviewStatus.Ready;
    }

    public void Start(PreviewProcess process)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        process.Status = PreviewStatus.Running;
        process.StartedAt = DateTimeOffset.UtcNow;
    }

    public void Stop(PreviewProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            process.Status = PreviewStatus.Stopped;
            activeProcesses.Remove(process);
        }
    }

    public void StopAll()
    {
        lock (syncLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            foreach (var process in activeProcesses)
            {
                process.Status = PreviewStatus.Stopped;
            }

            activeProcesses.Clear();
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (!disposed)
            {
                foreach (var process in activeProcesses)
                {
                    process.Status = PreviewStatus.Stopped;
                }

                activeProcesses.Clear();
                disposed = true;
            }
        }
    }
}

public sealed class PreviewProcess
{
    public required string Id { get; init; }
    public PreviewTarget Target { get; internal set; } = null!;
    public PreviewOptions Options { get; internal set; } = new();
    public PreviewStatus Status { get; internal set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; internal set; }
    public int McpPort { get; set; }
}

public sealed record PreviewTarget
{
    public required string ComponentTypeName { get; init; }
    public required string ProjectPath { get; init; }
    public string? Route { get; init; }
    public int WindowWidth { get; init; } = 1280;
    public int WindowHeight { get; init; } = 800;
    public string Theme { get; init; } = "AppleTheme.Light";
}

public sealed record PreviewOptions
{
    public bool ShowGrid { get; init; }
    public bool ShowLayoutBounds { get; init; }
    public bool ShowSpacing { get; init; }
    public bool ReducedMotion { get; init; }
    public bool DarkMode { get; init; }
}

public enum PreviewStatus
{
    Ready,
    Running,
    Stopped,
    Error,
}
