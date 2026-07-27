using System.Net.Http;

using Cascade.UI.Updater.Core;

namespace Cascade.UI.Installer.Update;

/// <summary>
/// Decides whether an update is available: fetches the <see cref="ReleaseManifest"/> over HTTPS for
/// the configured channel and compares its version to the running one. The pure
/// <see cref="Evaluate"/> step is separated from I/O so it is trivially testable.
/// </summary>
public sealed class UpdateChecker
{
    private readonly UpdateConfig config;
    private readonly string currentVersion;
    private readonly HttpMessageHandler? handler;
    private DateTimeOffset lastCheckTime = DateTimeOffset.MinValue;

    public UpdateChecker(UpdateConfig config, string currentVersion, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrEmpty(currentVersion);
        this.config = config;
        this.currentVersion = currentVersion;
        this.handler = handler;
    }

    public UpdateConfig Config => config;
    public string CurrentVersion => currentVersion;
    public DateTimeOffset LastCheckTime => lastCheckTime;

    /// <summary>Whether enough time has elapsed (or it is the first startup check) to check again.</summary>
    public bool ShouldCheck()
    {
        if (lastCheckTime == DateTimeOffset.MinValue && config.CheckOnStartup)
        {
            return true;
        }
        return DateTimeOffset.UtcNow - lastCheckTime >= config.CheckInterval;
    }

    /// <summary>Fetches the manifest over HTTPS and evaluates the configured channel for the given RID.</summary>
    public async Task<UpdateCheckResult> CheckAsync(string rid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(rid);
        lastCheckTime = DateTimeOffset.UtcNow;

        using var httpClient = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        string json = await httpClient.GetStringAsync(new Uri(config.ManifestUrl), cancellationToken).ConfigureAwait(false);

        ReleaseManifest? manifest = ReleaseManifest.FromJson(json);
        if (manifest is null)
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = "Manifest could not be parsed" };
        }

        ReleaseChannel? channel = manifest.Channel(config.Channel);
        if (channel is null)
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = $"Channel '{config.Channel}' not in manifest" };
        }

        return Evaluate(channel, rid);
    }

    /// <summary>Pure decision: is <paramref name="channel"/> newer than the running version, and usable for <paramref name="rid"/>?</summary>
    public UpdateCheckResult Evaluate(ReleaseChannel channel, string rid)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrEmpty(rid);

        if (!TryParseVersion(channel.Version, out Version? remote) ||
            !TryParseVersion(currentVersion, out Version? local))
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = "Invalid version format" };
        }

        if (remote <= local)
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = "Already up to date" };
        }

        if (!config.AllowPrerelease && channel.Version.Contains('-', StringComparison.Ordinal))
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = "Prerelease not allowed" };
        }

        ReleaseArtifacts? artifacts = channel.ArtifactsFor(rid);
        if (artifacts is null)
        {
            return new UpdateCheckResult { IsAvailable = false, Reason = $"No artifact for '{rid}'" };
        }

        return new UpdateCheckResult
        {
            IsAvailable = true,
            Version = channel.Version,
            ReleaseNotes = channel.ReleaseNotes,
            Full = artifacts.Full,
            Delta = artifacts.DeltaFrom(currentVersion),
        };
    }

    private static bool TryParseVersion(string? versionText, out Version? version)
    {
        version = null;
        if (string.IsNullOrEmpty(versionText))
        {
            return false;
        }

        string sanitized = versionText;
        int separator = versionText.IndexOfAny(['-', '+']);
        if (separator > 0)
        {
            sanitized = versionText[..separator];
        }

        return !string.IsNullOrWhiteSpace(sanitized) && Version.TryParse(sanitized, out version);
    }
}

public sealed class UpdateCheckResult
{
    public bool IsAvailable { get; init; }
    public string? Version { get; init; }
    public string ReleaseNotes { get; init; } = "";

    /// <summary>The full package artifact (always present when <see cref="IsAvailable"/>).</summary>
    public ArtifactRef? Full { get; init; }

    /// <summary>A delta patch from the running version, if the manifest offers one.</summary>
    public DeltaRef? Delta { get; init; }

    public string? Reason { get; init; }

    /// <summary>The estimated download size — the delta's when one is available, else the full package's.</summary>
    public long DownloadSizeBytes => Delta?.Size ?? Full?.Size ?? 0;

    public bool IsDelta => Delta is not null;
}
