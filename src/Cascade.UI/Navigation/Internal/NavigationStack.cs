namespace Cascade.UI;

/// <summary>
/// Core navigation history management. Maintains an ordered stack of
/// <see cref="NavigationEntry"/> records representing pages in a navigator.
/// Supports push, pop, replace, popTo, and reset operations, with optional
/// result delivery via <see cref="TaskCompletionSource{TResult}"/>.
/// </summary>
internal sealed class NavigationStack
{
    private readonly List<NavigationEntry> entries = [];

    /// <summary>The number of entries currently on the stack.</summary>
    internal int Depth => entries.Count;

    /// <summary>True when there are at least two entries and a pop is meaningful.</summary>
    internal bool CanGoBack => entries.Count > 1;

    /// <summary>
    /// The current top-of-stack entry, or null if the stack is empty.
    /// </summary>
    internal NavigationEntry? Current => entries.Count > 0 ? entries[^1] : null;

    /// <summary>
    /// Returns an immutable snapshot of all entries for enumeration.
    /// </summary>
    internal IReadOnlyList<NavigationEntry> Entries => entries;

    /// <summary>
    /// Returns true if any entry on the stack has the given component type.
    /// </summary>
    internal bool Contains(Type componentType)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ComponentType == componentType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pushes a new entry onto the stack. The previous top entry's component
    /// is preserved (suspended, not destroyed).
    /// </summary>
    internal void Push(NavigationEntry entry)
    {
        entries.Add(entry);
    }

    /// <summary>
    /// Removes and returns the top entry. Returns null if the stack has only
    /// one entry (the root is never popped).
    /// </summary>
    internal NavigationEntry? Pop()
    {
        if (entries.Count <= 1)
        {
            return null;
        }

        var top = entries[^1];
        entries.RemoveAt(entries.Count - 1);
        return top;
    }

    /// <summary>
    /// Replaces the top entry with a new one. The old entry is removed and returned.
    /// Returns null if the stack is empty.
    /// </summary>
    internal NavigationEntry? Replace(NavigationEntry newEntry)
    {
        if (entries.Count == 0)
        {
            entries.Add(newEntry);
            return null;
        }

        var old = entries[^1];
        entries[^1] = newEntry;
        return old;
    }

    /// <summary>
    /// Pops entries until a page of the given type is at the top.
    /// Returns the list of removed entries. Returns an empty list if
    /// the type is not found in the stack.
    /// </summary>
    internal List<NavigationEntry> PopTo(Type componentType)
    {
        var removed = new List<NavigationEntry>();

        int targetIndex = -1;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].ComponentType == componentType)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            return removed;
        }

        while (entries.Count > targetIndex + 1)
        {
            removed.Add(entries[^1]);
            entries.RemoveAt(entries.Count - 1);
        }

        return removed;
    }

    /// <summary>
    /// Clears the entire stack and replaces it with a single new root entry.
    /// Returns all removed entries.
    /// </summary>
    internal List<NavigationEntry> Reset(NavigationEntry newRoot)
    {
        var removed = new List<NavigationEntry>(entries);
        entries.Clear();
        entries.Add(newRoot);
        return removed;
    }
}

/// <summary>
/// Represents a single page on the navigation stack with its component type,
/// instance, constructor arguments, and optional result source.
/// </summary>
internal sealed class NavigationEntry
{
    internal NavigationEntry(Type componentType, Component instance, object[]? args = null, string? key = null)
    {
        ComponentType = componentType;
        Instance = instance;
        Args = args ?? [];
        Key = key;
    }

    /// <summary>The component type for this page.</summary>
    internal Type ComponentType { get; }

    /// <summary>The live component instance for this page.</summary>
    internal Component Instance { get; }

    /// <summary>Constructor arguments used to create this page.</summary>
    internal object[] Args { get; }

    /// <summary>Optional reconciliation key for this entry.</summary>
    internal string? Key { get; }

    /// <summary>
    /// When non-null, this entry was pushed via PushForResultAsync and the
    /// caller is awaiting a result. Set the result to complete the task.
    /// </summary>
    internal object? ResultSource { get; set; }
}
