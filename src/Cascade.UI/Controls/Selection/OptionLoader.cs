using System.Diagnostics;

namespace Cascade.UI;

/// <summary>A control that loads something in the background, pumped once a frame by layout.</summary>
internal interface IPumpedNode
{
    /// <summary>Starts work that is due and adopts work that has finished. Runs on the UI thread.</summary>
    void Pump();

    /// <summary>Takes over a replaced node's background work, so a re-render does not restart it.</summary>
    void AdoptFrom(Node previous);
}

/// <summary>
/// Loads results for a query from an async source, for a control whose list comes from a search
/// (an async combobox, an autocomplete): waits for the query to settle (debounce), runs one request at
/// a time, and adopts the reply on the UI thread at the next frame, so the source may complete on any
/// thread. A reply for an older query is still shown until the newer one arrives.
/// </summary>
internal sealed class OptionLoader<TResult>
{
    private Task<IEnumerable<TResult>>? running;
    private string? runningQuery;
    private string? wantedQuery;
    private long due;

    /// <summary>The latest results, for <see cref="ResultsQuery"/>.</summary>
    internal IReadOnlyList<TResult> Results { get; private set; } = [];

    /// <summary>The query <see cref="Results"/> answer, or null before the first reply.</summary>
    internal string? ResultsQuery { get; private set; }

    /// <summary>Whether a request is waiting to start or running.</summary>
    internal bool IsLoading => wantedQuery is not null || running is not null;

    /// <summary>The last request's failure message, or null.</summary>
    internal string? Error { get; private set; }

    /// <summary>Asks for results for <paramref name="query"/>, once it has been unchanged for <paramref name="delay"/>.</summary>
    internal void Want(string query, TimeSpan delay)
    {
        if (query == wantedQuery)
        {
            return;
        }
        if (wantedQuery is null && (running is not null ? query == runningQuery : query == ResultsQuery))
        {
            return; // already asked or answered
        }
        wantedQuery = query;
        due = Stopwatch.GetTimestamp() + (long)(Math.Max(0, delay.TotalSeconds) * Stopwatch.Frequency);
    }

    /// <summary>Starts a due request and adopts a finished one. Call once a frame on the UI thread.</summary>
    internal void Pump(Func<string, Task<IEnumerable<TResult>>> source)
    {
        if (running is { IsCompleted: true } done)
        {
            if (done.IsCompletedSuccessfully)
            {
                Results = [.. done.Result ?? []];
                ResultsQuery = runningQuery;
                Error = null;
            }
            else
            {
                Error = done.Exception?.GetBaseException().Message ?? "Couldn't load";
            }
            running = null;
            SharedScheduler.Instance.RequestFrame();
        }

        if (wantedQuery is not null && running is null && Stopwatch.GetTimestamp() >= due)
        {
            runningQuery = wantedQuery;
            wantedQuery = null;
            try
            {
                running = source(runningQuery) ?? Task.FromResult<IEnumerable<TResult>>([]);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                running = Task.FromException<IEnumerable<TResult>>(ex);
            }
        }

        if (IsLoading)
        {
            // Keep frames coming until the reply is in.
            ControlStateAnimator.SignalActiveTransition();
        }
    }

    /// <summary>Forgets the results (the control closed).</summary>
    internal void Clear()
    {
        wantedQuery = null;
        Results = [];
        ResultsQuery = null;
        Error = null;
    }
}
