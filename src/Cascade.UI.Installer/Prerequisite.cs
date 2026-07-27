using System.IO;
using IOPath = System.IO.Path;

namespace Cascade.UI.Installer;

/// <summary>
/// A condition checked before any install work begins. If a required prerequisite fails, the install
/// aborts cleanly (nothing is written) with a <see cref="PrerequisiteException"/>.
/// </summary>
public sealed record Prerequisite
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Func<Task<bool>> Check { get; init; }
    public string? DownloadUrl { get; init; }
    public bool Required { get; init; } = true;

    /// <summary>A custom prerequisite backed by an async predicate.</summary>
    public static Prerequisite Custom(string name, Func<Task<bool>> check, string? helpUrl = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(check);
        return new Prerequisite { Name = name, Description = name, Check = check, DownloadUrl = helpUrl };
    }

    /// <summary>Requires the OS version to be at least <paramref name="minimum"/>.</summary>
    public static Prerequisite OsVersion(Version minimum, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(minimum);
        return new Prerequisite
        {
            Name = $"OS {minimum} or later",
            Description = description ?? $"This application requires operating system version {minimum} or later.",
            Check = () => Task.FromResult(Environment.OSVersion.Version >= minimum),
        };
    }

    /// <summary>Requires at least <paramref name="minimumMb"/> MB free on the volume holding <paramref name="directory"/>.</summary>
    public static Prerequisite DiskSpace(string directory, long minimumMb)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return new Prerequisite
        {
            Name = "Disk space",
            Description = $"At least {minimumMb} MB of free disk space is required.",
            Check = () => Task.FromResult(HasFreeSpace(directory, minimumMb)),
        };
    }

    private static bool HasFreeSpace(string directory, long minimumMb)
    {
        try
        {
            string? root = IOPath.GetPathRoot(IOPath.GetFullPath(directory));
            if (string.IsNullOrEmpty(root))
            {
                return true;
            }
            return new DriveInfo(root).AvailableFreeSpace >= minimumMb * 1024L * 1024L;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return true; // can't determine — don't block the install on a probe failure
        }
    }
}

/// <summary>Thrown when a required <see cref="Prerequisite"/> is not satisfied.</summary>
public sealed class PrerequisiteException : Exception
{
    public PrerequisiteException(string message, string? helpUrl = null) : base(message)
    {
        HelpUrl = helpUrl;
    }

    public PrerequisiteException()
    {
    }

    public PrerequisiteException(string message) : base(message)
    {
    }

    public PrerequisiteException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>An optional URL with information on satisfying the prerequisite.</summary>
    public string? HelpUrl { get; }
}
