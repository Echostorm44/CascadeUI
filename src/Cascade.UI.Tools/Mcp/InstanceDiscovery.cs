using Cascade.UI;

namespace Cascade.UI.Tools.Mcp;

/// <summary>
/// Discovers running Cascade app instances via the shared memory registries.
///
/// With an explicit app ID (<c>--app</c>), reads that app's own registry.
/// Without one, reads the global registry — every Cascade app registers
/// there on startup — and applies the auto-detect rule: exactly one live
/// instance → use it; zero or multiple → a structured result the CLI
/// turns into an actionable error. Both registries validate PID liveness
/// and purge stale rows on every read (see <see cref="SharedInstanceRegistry.FindAll"/>).
/// </summary>
internal static class InstanceDiscovery
{
    /// <summary>Outcome of an instance discovery attempt.</summary>
    internal enum DiscoveryStatus
    {
        /// <summary>Exactly one target instance was resolved.</summary>
        Found,

        /// <summary>No live instance is registered.</summary>
        NoneRunning,

        /// <summary>
        /// Auto-detect found more than one live instance — the caller must
        /// disambiguate with <c>--app</c>.
        /// </summary>
        MultipleRunning,
    }

    /// <summary>Result of <see cref="Discover"/>: status plus the resolved or candidate instances.</summary>
    internal sealed class DiscoveryResult
    {
        /// <summary>Discovery outcome.</summary>
        public required DiscoveryStatus Status { get; init; }

        /// <summary>The resolved instance when <see cref="Status"/> is <see cref="DiscoveryStatus.Found"/>.</summary>
        public InstanceEntry? Instance { get; init; }

        /// <summary>All live instances seen during discovery (for error listings).</summary>
        public IReadOnlyList<InstanceEntry> AllInstances { get; init; } = [];
    }

    /// <summary>
    /// Resolves the target instance for a CLI command.
    /// </summary>
    /// <param name="appId">
    /// Explicit app ID from <c>--app</c>, or null to auto-detect via the
    /// global registry.
    /// </param>
    public static DiscoveryResult Discover(string? appId)
    {
        if (appId is not null)
        {
            InstanceEntry? target = FindTargetForApp(appId);
            if (target is null)
            {
                return new DiscoveryResult { Status = DiscoveryStatus.NoneRunning };
            }

            return new DiscoveryResult
            {
                Status = DiscoveryStatus.Found,
                Instance = target,
                AllInstances = [target],
            };
        }

        IReadOnlyList<InstanceEntry> all = FindAllInstances(null);
        return all.Count switch
        {
            0 => new DiscoveryResult { Status = DiscoveryStatus.NoneRunning },
            1 => new DiscoveryResult
            {
                Status = DiscoveryStatus.Found,
                Instance = all[0],
                AllInstances = all,
            },
            _ => new DiscoveryResult
            {
                Status = DiscoveryStatus.MultipleRunning,
                AllInstances = all,
            },
        };
    }

    /// <summary>
    /// Lists all live instances. With an app ID, reads that app's registry;
    /// without one, reads the global registry (all Cascade apps).
    /// Returns an empty list if none are found or the registry is unreadable.
    /// </summary>
    public static IReadOnlyList<InstanceEntry> FindAllInstances(string? appId)
    {
        var selector = InstanceSelector.Parse(appId);
        string registryId = selector.AppId ?? UI.DevTools.McpHost.GlobalRegistryId;
        try
        {
            using var registry = new SharedInstanceRegistry(registryId);
            var all = registry.FindAll();
            return selector.Pid is int pid ? all.FindAll(e => e.Pid == pid) : all;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>
    /// Finds the instance <c>--app</c> names: <c>Name</c> is that app's focused (else most
    /// recently activated) window — see <see cref="SharedInstanceRegistry.FindTarget"/>;
    /// <c>Name#pid</c> and <c>#pid</c> pick one process (see <see cref="InstanceSelector"/>).
    /// </summary>
    private static InstanceEntry? FindTargetForApp(string appId)
    {
        try
        {
            return InstanceSelector.Parse(appId).Resolve(UI.DevTools.McpHost.GlobalRegistryId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }
}
