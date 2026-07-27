using System;
using System.Collections.Generic;
using System.IO;

namespace Cascade.IDE.Shared;

public sealed class HotReloadClient
{
    private readonly object syncLock = new();
    private string? connectedEndpoint;

    public string? ConnectedEndpoint
    {
        get
        {
            lock (syncLock)
            {
                return connectedEndpoint;
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            lock (syncLock)
            {
                return connectedEndpoint is not null;
            }
        }
    }

    public event Action<HotReloadResult>? OnReloadComplete;
    public event Action<Exception>? OnError;

    public void Connect(PreviewProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (syncLock)
        {
            connectedEndpoint = $"localhost:{process.McpPort}";
        }
    }

    public void Connect(string endpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);

        lock (syncLock)
        {
            connectedEndpoint = endpoint;
        }
    }

    public void Disconnect()
    {
        lock (syncLock)
        {
            connectedEndpoint = null;
        }
    }

    public static HotReloadScope ClassifyChange(string filePath, string oldSource, string newSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(oldSource);
        ArgumentNullException.ThrowIfNull(newSource);

        if (string.Equals(oldSource, newSource, StringComparison.Ordinal))
        {
            return HotReloadScope.RenderOnly;
        }

        if (ContainsTypeHierarchyChange(oldSource, newSource))
        {
            return HotReloadScope.TypeHierarchy;
        }

        if (ContainsNewFieldDeclaration(oldSource, newSource))
        {
            return HotReloadScope.NewFields;
        }

        if (filePath.Contains("Theme", StringComparison.OrdinalIgnoreCase))
        {
            return HotReloadScope.ThemeChange;
        }

        return HotReloadScope.RenderOnly;
    }

    public HotReloadResult ApplyChange(string filePath, string newSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(newSource);

        if (!IsConnected)
        {
            var failureMessage = "Not connected to a preview process";
            var errorResult = new HotReloadResult
            {
                Succeeded = false,
                FailureReason = failureMessage,
                Scope = HotReloadScope.Unknown,
                AffectedComponents = [],
            };
            OnError?.Invoke(new InvalidOperationException(failureMessage));
            return errorResult;
        }

        var componentName = Path.GetFileNameWithoutExtension(filePath);
        var result = new HotReloadResult
        {
            Succeeded = true,
            Scope = HotReloadScope.RenderOnly,
            AffectedComponents = [componentName],
        };
        OnReloadComplete?.Invoke(result);
        return result;
    }

    private static bool ContainsTypeHierarchyChange(string oldSource, string newSource)
    {
        var oldBase = ExtractBaseClass(oldSource);
        var newBase = ExtractBaseClass(newSource);
        return !string.Equals(oldBase, newBase, StringComparison.Ordinal);
    }

    private static bool ContainsNewFieldDeclaration(string oldSource, string newSource)
    {
        var oldFieldCount = CountFieldDeclarations(oldSource);
        var newFieldCount = CountFieldDeclarations(newSource);
        return newFieldCount != oldFieldCount;
    }

    private static string? ExtractBaseClass(string source)
    {
        var classIdx = source.IndexOf("class ", StringComparison.Ordinal);
        if (classIdx < 0)
        {
            return null;
        }

        var colonIdx = source.IndexOf(':', classIdx);
        if (colonIdx < 0)
        {
            return null;
        }

        var endIdx = source.IndexOfAny(['{', '\n', '\r'], colonIdx);
        if (endIdx < 0)
        {
            endIdx = source.Length;
        }

        return source[(colonIdx + 1)..endIdx].Trim();
    }

    private static int CountFieldDeclarations(string source)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf("private ", idx, StringComparison.Ordinal)) >= 0)
        {
            var semicolonIdx = source.IndexOf(';', idx);
            var parenIdx = source.IndexOf('(', idx);
            if (semicolonIdx >= 0 && (parenIdx < 0 || semicolonIdx < parenIdx))
            {
                count++;
            }

            idx += 8;
        }

        return count;
    }
}

public sealed class HotReloadResult
{
    public bool Succeeded { get; init; }
    public string? FailureReason { get; init; }
    public HotReloadScope Scope { get; init; }
    public IReadOnlyList<string> AffectedComponents { get; init; } = [];
}

public enum HotReloadScope
{
    RenderOnly,
    ThemeChange,
    NewFields,
    TypeHierarchy,
    Unknown,
}
