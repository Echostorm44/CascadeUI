using System.Reflection.Metadata;

namespace Cascade.UI.Core.Internal;

/// <summary>
/// Opens the app's hot-reload pipe so <c>cascade run --watch</c> can push Roslyn deltas into the
/// running process instead of killing and relaunching it on every edit.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HotReloadEngine"/> and <see cref="HotReloadPipeServer"/> were both complete and
/// unit-tested, but nothing outside the test project ever constructed them — so no app ever
/// opened <c>cascade-hotreload-{pid}</c>, the CLI logged
/// <c>⚠ Hot reload pipe not available, using full rebuild</c> on every run, and the incremental
/// path documented in AGENTS.md was unreachable in practice. This is the missing wiring
/// (TOOLING-003).
/// </para>
/// <para>
/// Two gates, so an end-user app pays nothing. <c>CASCADE_HOT_RELOAD</c> is set by the watch
/// command and by nothing else, and <see cref="MetadataUpdater.IsSupported"/> is false for a
/// NativeAOT or otherwise non-updatable runtime — which is exactly where applying a delta could
/// not work anyway.
/// </para>
/// </remarks>
internal static class HotReloadHost
{
    // Rooted for the life of the process: the listener runs on a background task and must not be
    // collected while the window is up.
    private static HotReloadEngine? engine;
    private static HotReloadPipeServer? server;

    /// <summary>
    /// Starts the pipe server when the process was launched by the watcher and the runtime can
    /// actually apply metadata deltas. Safe to call more than once; never throws.
    /// </summary>
    internal static void StartIfRequested()
    {
        if (server is not null)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("CASCADE_HOT_RELOAD") != "1")
        {
            return;
        }

        if (!MetadataUpdater.IsSupported)
        {
            return;
        }

        try
        {
            engine = new HotReloadEngine();
            server = new HotReloadPipeServer(engine);
            server.Start();
        }
        catch (Exception)
        {
            // A dev-loop convenience must never stop the app from starting. The CLI falls back to
            // a full rebuild on its own when the pipe does not answer.
            server = null;
            engine?.Dispose();
            engine = null;
        }
    }
}
