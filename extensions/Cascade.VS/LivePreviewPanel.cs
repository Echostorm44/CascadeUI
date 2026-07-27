using System;
using System.Collections.Generic;
using Cascade.IDE.Shared;

namespace Cascade.VS;

/// <summary>
/// The live preview tool window for Cascade UI.
/// Embeds a running Cascade preview process and provides
/// theme selection, size presets, and overlay controls.
/// </summary>
public sealed class LivePreviewPanel
{
    private readonly PreviewProcessManager processManager;
    private readonly HotReloadClient hotReloadClient;
    private PreviewProcess? currentProcess;
    private string selectedTheme = "AppleTheme.Light";
    private PreviewSize selectedSize = PreviewSize.Desktop;
    private bool overlaysEnabled;
    private bool inspectMode;

    public LivePreviewPanel(PreviewProcessManager processManager, HotReloadClient hotReloadClient)
    {
        ArgumentNullException.ThrowIfNull(processManager);
        ArgumentNullException.ThrowIfNull(hotReloadClient);
        this.processManager = processManager;
        this.hotReloadClient = hotReloadClient;
    }

    /// <summary>The currently running preview process.</summary>
    public PreviewProcess? CurrentProcess => currentProcess;

    /// <summary>Whether a preview is currently running.</summary>
    public bool IsPreviewActive => currentProcess is not null && currentProcess.Status == PreviewStatus.Running;

    /// <summary>The selected theme.</summary>
    public string SelectedTheme => selectedTheme;

    /// <summary>The selected preview size.</summary>
    public PreviewSize SelectedSize => selectedSize;

    /// <summary>Whether overlays are enabled.</summary>
    public bool OverlaysEnabled => overlaysEnabled;

    /// <summary>Whether inspect mode is active.</summary>
    public bool InspectMode => inspectMode;

    public event Action<PreviewProcess>? OnPreviewStarted;
    public event Action? OnPreviewStopped;

    /// <summary>Starts a preview for the given component.</summary>
    public PreviewProcess StartPreview(string componentTypeName, string projectPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(componentTypeName);
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        if (currentProcess is not null)
        {
            StopPreview();
        }

        var (width, height) = GetSizeDimensions(selectedSize);
        var target = new PreviewTarget
        {
            ComponentTypeName = componentTypeName,
            ProjectPath = projectPath,
            Theme = selectedTheme,
            WindowWidth = width,
            WindowHeight = height,
        };

        var options = new PreviewOptions
        {
            ShowGrid = overlaysEnabled,
            ShowLayoutBounds = overlaysEnabled,
        };

        currentProcess = processManager.CreatePreview(target, options);
        processManager.Start(currentProcess);
        hotReloadClient.Connect(currentProcess);

        OnPreviewStarted?.Invoke(currentProcess);
        return currentProcess;
    }

    /// <summary>Stops the current preview.</summary>
    public void StopPreview()
    {
        if (currentProcess is not null)
        {
            hotReloadClient.Disconnect();
            processManager.Stop(currentProcess);
            currentProcess = null;
            OnPreviewStopped?.Invoke();
        }
    }

    /// <summary>Changes the preview theme.</summary>
    public void SetTheme(string theme)
    {
        ArgumentException.ThrowIfNullOrEmpty(theme);
        selectedTheme = theme;

        if (currentProcess is not null)
        {
            var target = currentProcess.Target with { Theme = theme };
            processManager.UpdateTarget(currentProcess, target);
        }
    }

    /// <summary>Changes the preview size.</summary>
    public void SetSize(PreviewSize size)
    {
        selectedSize = size;

        if (currentProcess is not null)
        {
            var (width, height) = GetSizeDimensions(size);
            var target = currentProcess.Target with { WindowWidth = width, WindowHeight = height };
            processManager.UpdateTarget(currentProcess, target);
        }
    }

    /// <summary>Toggles overlay display.</summary>
    public void ToggleOverlays()
    {
        overlaysEnabled = !overlaysEnabled;
    }

    /// <summary>Toggles inspect mode.</summary>
    public void ToggleInspectMode()
    {
        inspectMode = !inspectMode;
    }

    /// <summary>Gets the available size presets.</summary>
    public static IReadOnlyList<PreviewSize> GetSizePresets()
    {
        return [PreviewSize.Phone, PreviewSize.Tablet, PreviewSize.Desktop, PreviewSize.Wide];
    }

    /// <summary>Gets the available themes for the dropdown.</summary>
    public static IReadOnlyList<string> GetThemeOptions()
    {
        return
        [
            "AppleTheme.Light", "AppleTheme.Dark",
            "FluentTheme.Light", "FluentTheme.Dark",
            "Material3Theme.Light", "Material3Theme.Dark",
        ];
    }

    private static (int Width, int Height) GetSizeDimensions(PreviewSize size)
    {
        return size switch
        {
            PreviewSize.Phone => (390, 844),
            PreviewSize.Tablet => (1024, 768),
            PreviewSize.Desktop => (1280, 800),
            PreviewSize.Wide => (1920, 1080),
            _ => (1280, 800),
        };
    }
}

public enum PreviewSize { Phone, Tablet, Desktop, Wide, Custom }
