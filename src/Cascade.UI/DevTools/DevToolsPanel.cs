using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI.DevTools;

/// <summary>
/// Configuration for the DevTools panel.
/// Passed via <c>App.Run&lt;TRoot&gt;(config => config.DevTools = new DevToolsConfig { ... })</c>.
/// </summary>
public sealed class DevToolsConfig
{
    /// <summary>Default dock position when DevTools opens.</summary>
    public DockPosition DockPosition { get; init; } = DockPosition.Right;

    /// <summary>Default width (docked right/left) or height (docked bottom) in logical pixels.</summary>
    public float DefaultSize { get; init; } = 400;

    /// <summary>Keyboard shortcut to toggle the panel. Default: F12.</summary>
    public Key ToggleKey { get; init; } = Key.F12;

    /// <summary>When true, DevTools opens automatically on app launch.</summary>
    public bool OpenOnLaunch { get; init; }

    /// <summary>Target frame time in milliseconds for the performance panel budget line. Default: 16.6ms (60fps).</summary>
    public float TargetFrameTime { get; init; } = 16.6f;
}

/// <summary>
/// Edge of the window where the DevTools panel is docked.
/// </summary>
public enum DockPosition
{
    /// <summary>Docked to the right edge of the window.</summary>
    Right,

    /// <summary>Docked to the left edge of the window.</summary>
    Left,

    /// <summary>Docked to the bottom edge of the window.</summary>
    Bottom,
}

/// <summary>
/// The DevTools panel tabs.
/// </summary>
public enum DevToolsTab
{
    Inspector,
    Layout,
    Performance,
    Accessibility,
    State,
    Network,
}

/// <summary>
/// The main DevTools panel. Provides in-app debugging and inspection tools
/// for Cascade UI applications. Available only in debug builds — completely
/// stripped by the NativeAOT linker in release mode.
/// </summary>
/// <remarks>
/// Toggle with F12 or Ctrl+Shift+I. Use <see cref="CascadeDevTools.Show"/>
/// to open programmatically. The panel contains six sub-panels:
/// Inspector, Layout, Performance, Accessibility, State, and Network.
/// </remarks>
[Conditional("DEBUG")]
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class DevToolsEnabledAttribute : Attribute { }

#if DEBUG

/// <summary>
/// Static API for controlling DevTools from application code.
/// All methods are no-ops in release builds.
/// </summary>
public static class CascadeDevTools
{
    private static DevToolsConfig config = new();
    private static bool isVisible;
    private static DevToolsTab activeTab = DevToolsTab.Inspector;
    private static bool pickModeActive;
    private static string? selectedNodeId;
    private static readonly List<DevToolsOverlay> activeOverlays = [];
    private static Action? onStateChanged;

    /// <summary>Whether the DevTools panel is currently visible.</summary>
    public static bool IsVisible => isVisible;

    /// <summary>The currently active panel tab.</summary>
    public static DevToolsTab ActiveTab
    {
        get => activeTab;
        set
        {
            activeTab = value;
            onStateChanged?.Invoke();
        }
    }

    /// <summary>Whether pick mode (Ctrl+Shift+C) is active.</summary>
    public static bool IsPickModeActive => pickModeActive;

    /// <summary>The ID of the currently selected/inspected node, if any.</summary>
    public static string? SelectedNodeId => selectedNodeId;

    /// <summary>The current DevTools configuration.</summary>
    public static DevToolsConfig Config => config;

    /// <summary>Shows the DevTools panel.</summary>
    public static void Show()
    {
        isVisible = true;
        onStateChanged?.Invoke();
    }

    /// <summary>Hides the DevTools panel.</summary>
    public static void Hide()
    {
        isVisible = false;
        pickModeActive = false;
        onStateChanged?.Invoke();
    }

    /// <summary>Toggles the DevTools panel visibility.</summary>
    public static void Toggle()
    {
        if (isVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>
    /// Initializes DevTools with the given configuration. Called by the framework
    /// during app startup when a <see cref="DevToolsConfig"/> is provided.
    /// </summary>
    internal static void Initialize(DevToolsConfig devToolsConfig)
    {
        config = devToolsConfig ?? new DevToolsConfig();

        if (config.OpenOnLaunch)
        {
            Show();
        }
    }

    /// <summary>
    /// Registers a callback invoked whenever DevTools state changes (visibility, tab, selection).
    /// Used by the framework's overlay renderer.
    /// </summary>
    internal static void OnStateChanged(Action callback)
    {
        onStateChanged += callback;
    }

    /// <summary>Enters pick mode — hover to highlight, click to select a node.</summary>
    public static void EnterPickMode()
    {
        pickModeActive = true;
        onStateChanged?.Invoke();
    }

    /// <summary>Exits pick mode.</summary>
    public static void ExitPickMode()
    {
        pickModeActive = false;
        onStateChanged?.Invoke();
    }

    /// <summary>
    /// Selects a node by ID for inspection. Used by pick mode and the Inspector tree.
    /// </summary>
    public static void SelectNode(string? nodeId)
    {
        selectedNodeId = nodeId;
        if (nodeId is not null && !isVisible)
        {
            Show();
        }
        onStateChanged?.Invoke();
    }

    /// <summary>
    /// Enables network request logging for the given HttpClient.
    /// Requests made through this client will appear in the Network panel.
    /// </summary>
    public static void EnableNetworkLogging(object httpClient)
    {
        NetworkPanel.RegisterClient(httpClient.GetType().Name);
    }

    /// <summary>
    /// Returns the list of currently active visual overlays.
    /// </summary>
    internal static IReadOnlyList<DevToolsOverlay> ActiveOverlays => activeOverlays;

    /// <summary>
    /// Toggles a visual overlay on/off.
    /// </summary>
    internal static void ToggleOverlay(DevToolsOverlay overlay)
    {
        if (!activeOverlays.Remove(overlay))
        {
            activeOverlays.Add(overlay);
        }
        onStateChanged?.Invoke();
    }

    /// <summary>
    /// Handles keyboard shortcuts for DevTools.
    /// Returns true if the key was consumed by DevTools.
    /// </summary>
    internal static bool HandleKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == config.ToggleKey && modifiers == ModifierKeys.None)
        {
            Toggle();
            return true;
        }

        if (key == Key.I && modifiers == (ModifierKeys.Ctrl | ModifierKeys.Shift))
        {
            Toggle();
            return true;
        }

        if (key == Key.C && modifiers == (ModifierKeys.Ctrl | ModifierKeys.Shift))
        {
            if (pickModeActive)
            {
                ExitPickMode();
            }
            else
            {
                EnterPickMode();
            }
            return true;
        }

        if (modifiers == (ModifierKeys.Ctrl | ModifierKeys.Shift))
        {
            return key switch
            {
                Key.L => ToggleOverlayAndReturn(DevToolsOverlay.LayoutBounds),
                Key.M => ToggleOverlayAndReturn(DevToolsOverlay.PaddingMargin),
                Key.A => ToggleOverlayAndReturn(DevToolsOverlay.AccessibilityLabels),
                Key.R => ToggleOverlayAndReturn(DevToolsOverlay.RepaintRegions),
                Key.F => ToggleOverlayAndReturn(DevToolsOverlay.FocusOrder),
                _ => false,
            };
        }

        return false;
    }

    private static bool ToggleOverlayAndReturn(DevToolsOverlay overlay)
    {
        ToggleOverlay(overlay);
        return true;
    }
}

#else

/// <summary>
/// Static API for controlling DevTools. All methods are no-ops in release builds.
/// </summary>
public static class CascadeDevTools
{
    /// <summary>Whether the DevTools panel is currently visible. Always false in release.</summary>
    public static bool IsVisible => false;

    /// <summary>Shows the DevTools panel. No-op in release builds.</summary>
    [Conditional("DEBUG")]
    public static void Show() { }

    /// <summary>Hides the DevTools panel. No-op in release builds.</summary>
    [Conditional("DEBUG")]
    public static void Hide() { }

    /// <summary>Toggles the DevTools panel. No-op in release builds.</summary>
    [Conditional("DEBUG")]
    public static void Toggle() { }

    /// <summary>Enables network logging. No-op in release builds.</summary>
    [Conditional("DEBUG")]
    public static void EnableNetworkLogging(object httpClient) { }
}

#endif
