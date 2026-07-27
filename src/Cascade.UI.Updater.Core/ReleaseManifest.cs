using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade.UI.Updater.Core;

/// <summary>
/// The published update manifest (the doc's channel/artifacts format). One manifest per app,
/// hosted at <see cref="UpdateConfig.ManifestUrl"/>, listing every release channel and, for each,
/// the per-RID full artifact and any delta patches from prior versions.
/// </summary>
public sealed class ReleaseManifest
{
    public required string AppId { get; init; }

    /// <summary>PGP public key for verifying channel signatures (also embedded in the app for rotation).</summary>
    public string? PublicKey { get; init; }

    /// <summary>Channel name (e.g. <c>stable</c>, <c>beta</c>) → its current release.</summary>
    public Dictionary<string, ReleaseChannel> Channels { get; init; } = new(StringComparer.Ordinal);

    public string ToJson() => JsonSerializer.Serialize(this, ReleaseJsonContext.Default.ReleaseManifest);

    public static ReleaseManifest? FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);
        return JsonSerializer.Deserialize(json, ReleaseJsonContext.Default.ReleaseManifest);
    }

    /// <summary>Returns the named channel, or null if absent.</summary>
    public ReleaseChannel? Channel(string channel)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        return Channels.TryGetValue(channel, out ReleaseChannel? c) ? c : null;
    }
}

public sealed class ReleaseChannel
{
    public required string Version { get; init; }
    public string? ReleaseDate { get; init; }
    public string ReleaseNotes { get; init; } = "";
    public string? ReleaseNotesUrl { get; init; }

    /// <summary>Runtime identifier (e.g. <c>win-x64</c>) → its artifacts.</summary>
    public Dictionary<string, ReleaseArtifacts> Artifacts { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Base64 GPG signature over this channel's content (verified before acceptance).</summary>
    public string? Signature { get; init; }

    public ReleaseArtifacts? ArtifactsFor(string rid)
    {
        ArgumentException.ThrowIfNullOrEmpty(rid);
        return Artifacts.TryGetValue(rid, out ReleaseArtifacts? a) ? a : null;
    }
}

public sealed class ReleaseArtifacts
{
    /// <summary>The full self-contained package for this RID.</summary>
    public required ArtifactRef Full { get; init; }

    /// <summary>Delta patches keyed by the version they apply against.</summary>
    public Collection<DeltaRef> Delta { get; init; } = [];

    /// <summary>The smallest delta that patches <paramref name="fromVersion"/> to this release, if any.</summary>
    public DeltaRef? DeltaFrom(string fromVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        DeltaRef? best = null;
        foreach (DeltaRef d in Delta)
        {
            if (string.Equals(d.FromVersion, fromVersion, StringComparison.Ordinal) &&
                (best is null || d.Size < best.Size))
            {
                best = d;
            }
        }
        return best;
    }
}

public class ArtifactRef
{
    public required string Url { get; init; }
    public required string Sha256 { get; init; }
    public long Size { get; init; }
}

public sealed class DeltaRef : ArtifactRef
{
    public required string FromVersion { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ReleaseManifest))]
[JsonSerializable(typeof(ReleaseChannel))]
[JsonSerializable(typeof(ReleaseArtifacts))]
[JsonSerializable(typeof(ArtifactRef))]
[JsonSerializable(typeof(DeltaRef))]
internal sealed partial class ReleaseJsonContext : JsonSerializerContext;
