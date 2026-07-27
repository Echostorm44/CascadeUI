#pragma warning disable CA1822 // JsonHelper is an instance API (ctx.Json.*) by design.
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer;

/// <summary>
/// JSON config helpers for <c>OnInstallAsync</c> (exposed as <see cref="InstallContext.Json"/>).
/// Built on <see cref="JsonNode"/> so it is reflection-free and AOT-safe — installers compose config
/// with <see cref="JsonObject"/> rather than anonymous types.
/// </summary>
public sealed class JsonHelper
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/> (creating the directory).</summary>
    public async Task WriteAsync(string path, JsonNode content)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);
        EnsureDirectory(path);
        await File.WriteAllTextAsync(path, content.ToJsonString(Indented)).ConfigureAwait(false);
    }

    /// <summary>Deep-merges <paramref name="patch"/> into the existing JSON at <paramref name="path"/> (preserving other keys).</summary>
    public async Task MergeAsync(string path, JsonObject patch)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(patch);

        JsonObject root = new();
        if (File.Exists(path))
        {
            string text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            try
            {
                root = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }

        Merge(root, patch);
        EnsureDirectory(path);
        await File.WriteAllTextAsync(path, root.ToJsonString(Indented)).ConfigureAwait(false);
    }

    /// <summary>Reads a value at a dotted path (e.g. <c>ConnectionStrings.Default</c>), or default if absent.</summary>
    public async Task<T?> ReadValueAsync<T>(string path, string dotPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(dotPath);
        if (!File.Exists(path))
        {
            return default;
        }

        string text = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return default;
        }

        foreach (string segment in dotPath.Split('.'))
        {
            node = (node as JsonObject)?[segment];
            if (node is null)
            {
                return default;
            }
        }
        return node!.GetValue<T>();
    }

    private static void Merge(JsonObject target, JsonObject patch)
    {
        foreach (KeyValuePair<string, JsonNode?> entry in patch)
        {
            if (entry.Value is JsonObject patchChild && target[entry.Key] is JsonObject targetChild)
            {
                Merge(targetChild, patchChild);
            }
            else
            {
                target[entry.Key] = entry.Value?.DeepClone();
            }
        }
    }

    private static void EnsureDirectory(string path)
    {
        string? dir = IOPath.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
