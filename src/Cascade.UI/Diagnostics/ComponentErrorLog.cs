namespace Cascade.UI.Diagnostics;

/// <summary>
/// Exceptions thrown by components (Render, OnMounted) that the framework caught. A component
/// that throws keeps showing its last good tree, so without this record the failure is invisible:
/// the window just stops updating. The most recent errors are kept for <c>cascade mcp diagnostics</c>
/// and written to stderr and the debug output as they happen.
/// </summary>
internal static class ComponentErrorLog
{
    public const int Capacity = 16;

    internal readonly record struct Entry(DateTime At, string Component, bool HandledByBoundary, Exception Error);

    private static readonly Queue<Entry> recent = new(Capacity);
    private static readonly Lock gate = new();
    private static int total;

    public static int TotalCount
    {
        get
        {
            lock (gate)
            {
                return total;
            }
        }
    }

    public static void Report(string component, Exception error, bool handledByBoundary)
    {
        lock (gate)
        {
            if (recent.Count == Capacity)
            {
                recent.Dequeue();
            }
            recent.Enqueue(new Entry(DateTime.Now, component, handledByBoundary, error));
            total++;
        }

        string line = $"[Cascade] {(handledByBoundary ? "Component error (sent to ErrorBoundary)" : "Unhandled component error (no ErrorBoundary)")} in {component}: {error}";
        System.Diagnostics.Debug.WriteLine(line);
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (IOException)
        {
            // No usable stderr (e.g. a GUI app without a console): the record above still has it.
        }
    }

    public static Entry[] Snapshot()
    {
        lock (gate)
        {
            return recent.ToArray();
        }
    }

    internal static void ResetForTests()
    {
        lock (gate)
        {
            recent.Clear();
            total = 0;
        }
    }
}
