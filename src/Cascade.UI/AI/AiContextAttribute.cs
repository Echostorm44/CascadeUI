namespace Cascade.UI;

/// <summary>
/// Marks a method as the AI context provider for an <see cref="AiSurfaceAttribute"/>
/// class. The method is called on the UI thread when a <c>get_context</c> MCP request
/// arrives. Return a plain C# record that will be serialized to JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AiContextAttribute : Attribute
{
    /// <summary>
    /// When true, the framework re-calls the context method after every render and
    /// emits a resource update if the value changed (deep equality comparison).
    /// </summary>
    public bool Reactive { get; set; }
}
