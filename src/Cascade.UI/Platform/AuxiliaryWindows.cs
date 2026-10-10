namespace Cascade.UI;

/// <summary>
/// A short-lived top-level window the app shows besides its main window — today the tray menu's
/// popup windows. DevTools (the MCP host and the <c>cascade mcp</c> CLI) list them, capture them
/// and send them input by id, so an agent can see and drive what the user sees.
/// </summary>
internal interface IAuxiliaryWindow
{
    /// <summary>Stable id for the CLI's <c>--window</c> (e.g. <c>tray-menu</c>, <c>tray-menu/1</c>).</summary>
    string Id { get; }

    /// <summary>What the window is (e.g. <c>tray_menu</c>).</summary>
    string Kind { get; }

    /// <summary>The native handle (HWND on Windows).</summary>
    nint Handle { get; }

    /// <summary>The window's screen rectangle in physical pixels.</summary>
    Rect ScreenBounds { get; }

    /// <summary>Device pixels per logical pixel.</summary>
    float PixelRatio { get; }

    /// <summary>The last presented frame (RGBA, device pixels). UI thread.</summary>
    ImageData? CaptureFrame();

    /// <summary>A pointer event at window-logical (<paramref name="x"/>, <paramref name="y"/>). UI thread.</summary>
    void SimulateMouse(NativeMouseEventType type, NativeMouseButton button, float x, float y);

    /// <summary>A key press (down, character, up). Returns false when the window no longer takes input. UI thread.</summary>
    bool SimulateKeyPress(Key key, ModifierKeys modifiers, char? character);
}

/// <summary>The open auxiliary windows, in the order they opened. Thread-safe.</summary>
internal static class AuxiliaryWindows
{
    private static readonly object gate = new();
    private static readonly List<IAuxiliaryWindow> windows = [];

    internal static void Register(IAuxiliaryWindow window)
    {
        lock (gate)
        {
            if (!windows.Contains(window))
            {
                windows.Add(window);
            }
        }
    }

    internal static void Unregister(IAuxiliaryWindow window)
    {
        lock (gate)
        {
            windows.Remove(window);
        }
    }

    /// <summary>A copy of the open windows.</summary>
    internal static IAuxiliaryWindow[] Snapshot()
    {
        lock (gate)
        {
            return [.. windows];
        }
    }

    /// <summary>The open window with <paramref name="id"/> (ordinal, case-insensitive), or null.</summary>
    internal static IAuxiliaryWindow? Find(string id)
    {
        lock (gate)
        {
            foreach (var window in windows)
            {
                if (string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return window;
                }
            }

            return null;
        }
    }
}
