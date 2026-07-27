using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Cascade.IDE.Shared;

namespace Cascade.VS;

/// <summary>
/// The main Visual Studio package for Cascade UI.
/// Registers tool windows, commands, and extension services.
/// </summary>
[Guid(PackageGuidString)]
public sealed class CascadeVsPackage : IDisposable
{
    public const string PackageGuidString = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";

    private PreviewProcessManager? previewManager;
    private HotReloadClient? hotReloadClient;
    private ComponentAnalyzer? componentAnalyzer;
    private SignalAnnotator? signalAnnotator;
    private ThemeTokenResolver? themeTokenResolver;
    private bool initialized;

    /// <summary>Whether the package has been fully initialized.</summary>
    public bool IsInitialized => initialized;

    /// <summary>The preview process manager for this session.</summary>
    public PreviewProcessManager PreviewManager => previewManager
        ?? throw new InvalidOperationException("Package not initialized");

    /// <summary>The hot reload client for this session.</summary>
    public HotReloadClient HotReloadClient => hotReloadClient
        ?? throw new InvalidOperationException("Package not initialized");

    /// <summary>The component analyzer for this session.</summary>
    public ComponentAnalyzer ComponentAnalyzer => componentAnalyzer
        ?? throw new InvalidOperationException("Package not initialized");

    /// <summary>The signal annotator for this session.</summary>
    public SignalAnnotator SignalAnnotator => signalAnnotator
        ?? throw new InvalidOperationException("Package not initialized");

    /// <summary>The theme token resolver for this session.</summary>
    public ThemeTokenResolver ThemeTokenResolver => themeTokenResolver
        ?? throw new InvalidOperationException("Package not initialized");

    /// <summary>
    /// Initializes the Cascade VS package and all shared services.
    /// In a real VS extension, this would be called by the async package loader.
    /// </summary>
    public void Initialize(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        previewManager = new PreviewProcessManager();
        hotReloadClient = new HotReloadClient();
        componentAnalyzer = new ComponentAnalyzer();
        signalAnnotator = new SignalAnnotator();
        themeTokenResolver = new ThemeTokenResolver();

        initialized = true;
    }

    /// <summary>
    /// Shuts down the package, stopping all preview processes.
    /// </summary>
    public void Shutdown()
    {
        previewManager?.Dispose();
        hotReloadClient?.Disconnect();
        initialized = false;
    }

    /// <summary>Gets the list of registered tool window types.</summary>
    public static IReadOnlyList<string> GetToolWindowTypes()
    {
        return ["LivePreviewPanel", "InspectorPanel", "ThemeEditorPanel", "AccessibilityPanel"];
    }

    /// <summary>Gets the list of registered command IDs.</summary>
    public static IReadOnlyList<string> GetCommandIds()
    {
        return
        [
            "Cascade.TogglePreview",
            "Cascade.ToggleInspector",
            "Cascade.RestartPreview",
            "Cascade.ToggleOverlays",
            "Cascade.CompareThemes",
        ];
    }

    public void Dispose()
    {
        Shutdown();
        GC.SuppressFinalize(this);
    }
}
