using Cascade.UI;

namespace Cascade.UI.Testing;

/// <summary>
/// Fluent assertion helpers for inspecting Cascade UI node trees in tests.
/// Provides methods to query nodes by type, verify properties, and check structure.
/// </summary>
public sealed class NodeAssertions
{
    private readonly Node node;

    /// <summary>Creates assertions for the specified node.</summary>
    public NodeAssertions(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        this.node = node;
    }

    /// <summary>The node being asserted on.</summary>
    public Node Node => node;

    /// <summary>Asserts that the node is of the expected type.</summary>
    public NodeAssertions IsType<TNode>() where TNode : Node
    {
        if (node is not TNode)
        {
            throw new AssertionException($"Expected node of type {typeof(TNode).Name} but got {node.GetType().Name}.");
        }
        return this;
    }

    /// <summary>Asserts that the node is not Node.Empty.</summary>
    public NodeAssertions IsNotEmpty()
    {
        if (ReferenceEquals(node, Node.Empty))
        {
            throw new AssertionException("Expected a non-empty node but got Node.Empty.");
        }
        return this;
    }

    /// <summary>Asserts that the node is Node.Empty.</summary>
    public NodeAssertions IsEmpty()
    {
        if (!ReferenceEquals(node, Node.Empty))
        {
            throw new AssertionException($"Expected Node.Empty but got {node.GetType().Name}.");
        }
        return this;
    }

    /// <summary>Casts the node to the specified type for further inspection.</summary>
    public TNode As<TNode>() where TNode : Node
    {
        if (node is TNode typed)
        {
            return typed;
        }
        throw new AssertionException($"Cannot cast {node.GetType().Name} to {typeof(TNode).Name}.");
    }
}

/// <summary>
/// Exception thrown when a node assertion fails.
/// </summary>
public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
    public AssertionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Extension methods for creating NodeAssertions from nodes.
/// </summary>
public static class NodeAssertionExtensions
{
    /// <summary>Begins a fluent assertion chain on this node.</summary>
    public static NodeAssertions Should(this Node node)
    {
        return new NodeAssertions(node);
    }
}
