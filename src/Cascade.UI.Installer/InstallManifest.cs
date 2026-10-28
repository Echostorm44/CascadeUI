using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UI.Installer;

public sealed class InstallManifest
{
    public string AppId { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public string InstallDir { get; set; } = "";
    public Collection<string> InstalledFiles { get; init; } = [];
    public Collection<string> CreatedShortcuts { get; init; } = [];
    public Collection<string> RegisteredServices { get; init; } = [];
    public Collection<string> RegistryKeys { get; init; } = [];

    /// <summary>
    /// Single registry values written into keys the app does not own (another app's file type, say), so
    /// uninstall removes the value and leaves the key: <c>HKCU\path|name</c>, or <c>HKCU\path|name|expected</c>
    /// to remove it only while it still holds <c>expected</c>. An empty name is the key's default value.
    /// </summary>
    public Collection<string> RegistryValues { get; init; } = [];

    public void AddFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        InstalledFiles.Add(path);
    }

    public void AddShortcut(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        CreatedShortcuts.Add(path);
    }

    public void AddService(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        RegisteredServices.Add(name);
    }

    public void AddRegistryKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        RegistryKeys.Add(key);
    }

    public void AddRegistryValue(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        RegistryValues.Add(value);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, ManifestJsonContext.Default.InstallManifest);
    }

    public static InstallManifest? FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, ManifestJsonContext.Default.InstallManifest);
    }
}

[JsonSerializable(typeof(InstallManifest))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;
