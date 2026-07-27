using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Reactive state inspection panel. Displays all signals, computed properties,
/// async data states, and local storage contents, with live editing support.
/// </summary>
internal static class StatePanel
{
    // SignalEntry, ComputedEntry, AsyncDataEntry, LocalStorageEntry, and
    // UndoEntry are declared in standalone files under #if CASCADE_DEVTOOLS
    // so agents inspecting a Release + CascadeDevTools build can consume them
    // via NodeTreeWalker / MCP tools.
    // See: SignalEntry.cs, StateDtos.cs

    /// <summary>Returns all reactive signals across all mounted components.</summary>
    public static IReadOnlyList<SignalEntry> GetSignals()
    {
        return NodeTreeWalker.GetAllSignals();
    }

    /// <summary>Returns all computed properties across all mounted components.</summary>
    public static IReadOnlyList<ComputedEntry> GetComputed()
    {
        return NodeTreeWalker.GetAllComputed();
    }

    /// <summary>Returns all async data fields across all mounted components.</summary>
    public static IReadOnlyList<AsyncDataEntry> GetAsyncData()
    {
        return NodeTreeWalker.GetAllAsyncData();
    }

    /// <summary>Returns all local storage entries.</summary>
    public static IReadOnlyList<LocalStorageEntry> GetLocalStorage()
    {
        return NodeTreeWalker.GetAllLocalStorage();
    }

    /// <summary>Returns the undo stack (most recent first).</summary>
    public static IReadOnlyList<UndoEntry> GetUndoStack()
    {
        return NodeTreeWalker.GetUndoStack();
    }

    /// <summary>
    /// Attempts to set a signal value from the DevTools panel.
    /// The value string is parsed and converted to the signal's type.
    /// Returns true if the value was set successfully.
    /// </summary>
    public static bool TrySetSignalValue(string componentName, string fieldName, string newValue)
    {
        return NodeTreeWalker.TrySetSignal(componentName, fieldName, newValue);
    }

    /// <summary>
    /// Deletes a local storage entry.
    /// </summary>
    public static bool DeleteLocalStorageEntry(string key)
    {
        return NodeTreeWalker.DeleteLocalStorage(key);
    }

    /// <summary>
    /// Clears all local storage.
    /// </summary>
    public static void ClearLocalStorage()
    {
        NodeTreeWalker.ClearLocalStorage();
    }
}

#endif
