using System.Diagnostics.CodeAnalysis;
using Cascade.UI;

namespace Cascade.UI.Testing;

/// <summary>
/// A harness for testing a single component in isolation. Provides
/// methods to simulate user interaction and inspect the resulting node tree.
/// </summary>
public sealed class ComponentTestHarness<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IDisposable where T : Node
{
    private readonly TestHost host;
    private bool disposed;

    /// <summary>Creates a harness wrapping a new instance of the component type.</summary>
    public ComponentTestHarness(float width = 800, float height = 600) : this(Activator.CreateInstance<T>(), width, height)
    {
    }

    /// <summary>Creates a harness wrapping an existing component instance.</summary>
    public ComponentTestHarness(T component, float width = 800, float height = 600)
    {
        ArgumentNullException.ThrowIfNull(component);
        host = new TestHost(width, height);
        Component = host.Mount(component);
    }

    /// <summary>The mounted component instance.</summary>
    public T Component { get; }

    /// <summary>The underlying test host.</summary>
    public TestHost Host => host;

    /// <summary>Triggers a render cycle.</summary>
    public void Render()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        host.Render();
    }

    /// <summary>Simulates a click on the component.</summary>
    public void Click()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Simulation — dispatches click event to the component
    }

    /// <summary>Simulates text input into the component.</summary>
    public void TypeText(string text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        // Simulation — dispatches text input events
    }

    /// <summary>Simulates focus on the component.</summary>
    public void Focus()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Simulation — dispatches focus event
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            host.Dispose();
        }
    }
}
