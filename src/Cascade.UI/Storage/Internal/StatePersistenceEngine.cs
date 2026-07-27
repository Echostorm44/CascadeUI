using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Cascade.UI;

/// <summary>
/// Engine for persisting and restoring application state across sessions.
/// Handles window state (position, size, maximized), scroll positions,
/// and layout state (split view proportions, data grid column widths).
/// </summary>
internal sealed class StatePersistenceEngine
{
    private const string WindowStateKey = "__cascade_window_state";
    private const string ScrollStatePrefix = "__cascade_scroll_";
    private const string LayoutStatePrefix = "__cascade_layout_";
    private readonly IStorageEngine storageEngine;

    public StatePersistenceEngine(IStorageEngine storageEngine)
    {
        this.storageEngine = storageEngine;
    }

    // ── Window State ──────────────────────────────────────────────

    /// <summary>Persists the window position, size, and maximized state.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Serializes simple DTO (PersistedWindowState) with primitive properties.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Serializes simple DTO (PersistedWindowState) with primitive properties.")]
    public void SaveWindowState(PersistedWindowState state)
    {
        var json = JsonSerializer.Serialize(state);
        storageEngine.Write(WindowStateKey, json);
    }

    /// <summary>
    /// Restores the window state from storage. Returns null if no state
    /// is saved or deserialization fails.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Deserializes simple DTO (PersistedWindowState) with primitive properties.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Deserializes simple DTO (PersistedWindowState) with primitive properties.")]
    public PersistedWindowState? RestoreWindowState()
    {
        string? json = storageEngine.Read(WindowStateKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersistedWindowState>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Scroll State ──────────────────────────────────────────────

    /// <summary>Persists a scroll position by key.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Serializes simple DTO (ScrollState) with primitive properties.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Serializes simple DTO (ScrollState) with primitive properties.")]
    public void SaveScrollPosition(string key, float x, float y)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string json = JsonSerializer.Serialize(new ScrollState { X = x, Y = y });
        storageEngine.Write(ScrollStatePrefix + key, json);
    }

    /// <summary>
    /// Restores a scroll position by key. Returns null if no state is
    /// saved or deserialization fails.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Deserializes simple DTO (ScrollState) with primitive properties.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Deserializes simple DTO (ScrollState) with primitive properties.")]
    public (float x, float y)? RestoreScrollPosition(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string? json = storageEngine.Read(ScrollStatePrefix + key);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<ScrollState>(json);
            if (state != null)
            {
                return (state.X, state.Y);
            }
        }
        catch (JsonException)
        {
            // Fall through to return null
        }

        return null;
    }

    // ── Layout State ──────────────────────────────────────────────

    /// <summary>
    /// Persists arbitrary layout state as a JSON string (e.g., split
    /// view proportions, data grid column widths).
    /// </summary>
    public void SaveLayoutState(string key, string jsonState)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        storageEngine.Write(LayoutStatePrefix + key, jsonState);
    }

    /// <summary>
    /// Restores layout state by key. Returns null if no state is saved.
    /// </summary>
    public string? RestoreLayoutState(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return storageEngine.Read(LayoutStatePrefix + key);
    }

    // ── Cleanup ───────────────────────────────────────────────────

    /// <summary>
    /// Removes all persisted state (window, scroll, and layout).
    /// </summary>
    public void ClearAll()
    {
        storageEngine.Remove(WindowStateKey);
        storageEngine.ClearPrefix(ScrollStatePrefix);
        storageEngine.ClearPrefix(LayoutStatePrefix);
    }
}

/// <summary>Persisted window position, size, and maximized state.</summary>
internal sealed class PersistedWindowState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public bool IsMaximized { get; set; }
}

/// <summary>Persisted scroll position for a single scroll view.</summary>
internal sealed class ScrollState
{
    public float X { get; set; }
    public float Y { get; set; }
}
