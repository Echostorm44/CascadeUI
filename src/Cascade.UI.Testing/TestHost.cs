using Cascade.UI;

namespace Cascade.UI.Testing;

/// <summary>
/// Boots a headless component tree for testing. No window is created
/// and no GPU rendering occurs. Components are mounted, rendered, and
/// their node trees are available for assertion.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly List<Node> mountedNodes = [];
    private readonly List<ComponentHost> componentHosts = [];
    private readonly RenderScheduler scheduler = new();
    private bool disposed;

    /// <summary>Creates a test host with default viewport dimensions.</summary>
    public TestHost(float viewportWidth = 1920, float viewportHeight = 1080)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    /// <summary>The viewport width in logical pixels.</summary>
    public float ViewportWidth { get; }

    /// <summary>The viewport height in logical pixels.</summary>
    public float ViewportHeight { get; }

    /// <summary>All mounted root nodes.</summary>
    public IReadOnlyList<Node> MountedNodes => mountedNodes;

    /// <summary>Mounts a component and triggers its first render cycle.</summary>
    public T Mount<T>() where T : Node, new()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var node = new T();
        MountNode(node);
        return node;
    }

    /// <summary>Mounts a pre-constructed node.</summary>
    public T Mount<T>(T node) where T : Node
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(node);
        MountNode(node);
        return node;
    }

    /// <summary>Triggers a render cycle for all mounted components.</summary>
    public void Render()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        // Process any pending reactive updates (re-renders from signal changes)
        scheduler.ProcessFrame();

        // Run layout pass over all mounted node trees
        var rootConstraints = LayoutConstraints.Loose(new Size(ViewportWidth, ViewportHeight));

        foreach (var host in componentHosts)
        {
            if (host.IsMounted && host.RenderedTree is not null)
            {
                LayoutSolver.Measure(host.RenderedTree, rootConstraints);
            }
        }

        foreach (var node in mountedNodes)
        {
            if (node is not Component)
            {
                LayoutSolver.Measure(node, rootConstraints);
            }
        }
    }

    /// <summary>Unmounts all nodes and cleans up.</summary>
    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            foreach (var host in componentHosts)
            {
                if (host.IsMounted)
                {
                    host.Unmount();
                }
            }
            componentHosts.Clear();
            mountedNodes.Clear();
        }
    }

    private void MountNode(Node node)
    {
        mountedNodes.Add(node);
        if (node is Component component)
        {
            var host = new ComponentHost(component, scheduler, treeDepth: 0);
            componentHosts.Add(host);
            host.Mount();
            host.CompleteMountAsync();
        }
    }
}
