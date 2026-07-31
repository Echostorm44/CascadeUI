using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Cascade.UI.AI;

namespace Cascade.UI.DevTools;

/// <summary>
/// TCP-based MCP host that replaces the named-pipe DevToolsHost.
/// Listens on <c>127.0.0.1:0</c> (OS-assigned port) and registers the
/// instance in <see cref="SharedInstanceRegistry"/> so <c>--mcp</c> proxy
/// mode can discover and connect to it.
///
/// Accepts one client at a time. When a client disconnects, the host waits
/// for the next connection.
/// </summary>
internal sealed class McpHost : IDisposable
{
    /// <summary>
    /// Global registry name shared by ALL Cascade apps. The MCP proxy reads
    /// this single registry to discover any running Cascade GUI instance,
    /// regardless of the app's own appId.
    /// </summary>
    internal const string GlobalRegistryId = "CascadeApps";

    private readonly McpServer server;
    private readonly SharedInstanceRegistry registry;
    private readonly SharedInstanceRegistry globalRegistry;
    private readonly string appId;
    private readonly string windowId;
    private TcpListener? listener;
    private Thread? listenThread;
    private volatile bool disposed;

    /// <summary>The TCP port the host is listening on (after <see cref="Start"/>).</summary>
    public int Port { get; private set; }

    /// <summary>Whether the MCP server is currently running.</summary>
    public bool IsRunning => server.IsRunning;

    /// <summary>
    /// Creates the MCP host. Call <see cref="Start"/> to begin listening.
    /// </summary>
    /// <param name="windowTitle">Window title for the instance registry entry.</param>
    public McpHost(string? windowTitle = null)
    {
        // CASCADE_APP_ID lets a process register under an explicit instance id
        // instead of the entry-assembly name. This disambiguates MCP targeting
        // when several processes of the same app run at once (otherwise they all
        // share one id and `--app` resolves to "most recently activated", which
        // is non-deterministic — see SharedInstanceRegistry.FindTarget).
        appId = Environment.GetEnvironmentVariable("CASCADE_APP_ID")
            ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name
            ?? "CascadeApp";
        string normalizedId = appId.Replace(' ', '-');
        windowId = $"cascade-{normalizedId}-{Environment.ProcessId}";

        server = new McpServer(new McpServerConfig
        {
            AppName = appId,
            AppId = normalizedId,
            AppVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0",
            CascadeVersion = typeof(McpHost).Assembly.GetName().Version?.ToString() ?? "0.0.0",
#if DEBUG
            IsDebugBuild = true,
#else
            IsDebugBuild = false,
#endif
#if CASCADE_DEVTOOLS
            HasDevTools = true,
#else
            HasDevTools = false,
#endif
            HasLiveInstance = true,
            HasAppSurface = true,
        });

        McpTools.RegisterAll(server);
        McpResources.RegisterAll(server);
        McpPrompts.RegisterAll(server);

        registry = new SharedInstanceRegistry(appId);
        globalRegistry = new SharedInstanceRegistry(GlobalRegistryId);

        windowTitle ??= appId;
    }

    /// <summary>
    /// Starts the TCP listener on loopback and registers the instance.
    /// </summary>
    /// <remarks>
    /// In DEBUG builds the listener starts unconditionally so developers get
    /// MCP access simply by pressing F5.
    ///
    /// In non-DEBUG builds the listener is <b>gated by the <c>CASCADE_MCP=1</c>
    /// environment variable</b>. End-user AOT apps therefore pay zero MCP
    /// runtime cost unless the user (or agent runtime) explicitly opts in.
    /// Without the env var, <see cref="Start"/> is a no-op — no listener,
    /// no registry write, no background thread.
    /// </remarks>
    public void Start()
    {
        if (disposed)
        {
            return;
        }

#if !DEBUG
        // Release builds: require explicit opt-in to avoid any cost for
        // end-user AOT apps. Agent launches set CASCADE_MCP=1 explicitly.
        if (!string.Equals(Environment.GetEnvironmentVariable("CASCADE_MCP"), "1", StringComparison.Ordinal))
        {
            return;
        }
#endif

        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;

        registry.Register(new InstanceEntry
        {
            WindowId = windowId,
            Port = Port,
            Title = appId,
            Pid = Environment.ProcessId,
            Focused = true,
            ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        // Also register in the global registry so any MCP proxy can find us
        try
        {
            globalRegistry.Register(new InstanceEntry
            {
                WindowId = windowId,
                Port = Port,
                Title = appId,
                Pid = Environment.ProcessId,
                Focused = true,
                ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            Debug.WriteLine($"[MCP] Registered in global registry (CascadeApps)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MCP] FAILED to register in global registry: {ex.GetType().Name}: {ex.Message}");
        }

        listenThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "McpHost",
        };
        listenThread.Start();

        Debug.WriteLine($"[MCP] Server listening on 127.0.0.1:{Port} (registry: {appId})");
    }

    /// <summary>
    /// Stops the TCP listener and unregisters the instance.
    /// </summary>
    public void Stop()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        server.Stop();

        try
        {
            listener?.Stop();
            listener?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already stopped
        }

        try
        {
            registry.Unregister(windowId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MCP] Failed to unregister: {ex.Message}");
        }

        try
        {
            globalRegistry.Unregister(windowId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MCP] Failed to unregister from global: {ex.Message}");
        }

        registry.Dispose();
        globalRegistry.Dispose();
        Debug.WriteLine("[MCP] Server stopped.");
    }

    public void Dispose()
    {
        Stop();
    }

    private void ListenLoop()
    {
        while (!disposed)
        {
            TcpClient? client = null;
            try
            {
                Debug.WriteLine("[MCP] Waiting for client connection...");
                client = listener!.AcceptTcpClient();

                if (disposed)
                {
                    client.Dispose();
                    break;
                }

                Debug.WriteLine($"[MCP] Client connected from {client.Client.RemoteEndPoint}");

                var stream = client.GetStream();
                server.Start(stream, stream);

                // Wait until the server finishes (client disconnected)
                while (server.IsRunning && !disposed)
                {
                    Thread.Sleep(100);
                }

                server.Stop();
                client.Dispose();
            }
            catch (SocketException) when (disposed)
            {
                // Expected during shutdown — listener was stopped
                break;
            }
            catch (ObjectDisposedException) when (disposed)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MCP] Connection error: {ex.Message}");
                client?.Dispose();

                if (!disposed)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
