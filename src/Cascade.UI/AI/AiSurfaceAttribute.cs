namespace Cascade.UI;

/// <summary>
/// Marks a class as an AI surface — a container of methods that can be
/// exposed as AI-callable tools via the generated MCP tool registry.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AiSurfaceAttribute : Attribute
{
    /// <summary>
    /// Human-readable name of this AI surface shown to AI clients.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Human-readable description of this AI surface.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When true, only capabilities marked <c>readOnly: true</c> are included
    /// in the tool list. All mutating capabilities are stripped.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Controls how the MCP proxy routes requests when multiple instances are running.
    /// Default is <see cref="AiRouting.Foreground"/>.
    /// </summary>
    public AiRouting Routing { get; set; } = AiRouting.Foreground;
}

/// <summary>
/// Controls how the MCP proxy routes requests when multiple app instances are running.
/// </summary>
public enum AiRouting
{
    /// <summary>Route to the currently focused window. Default behavior.</summary>
    Foreground,

    /// <summary>Connect to whichever instance, no preference.</summary>
    Any,

    /// <summary>Always route to the first/main instance (single-document apps).</summary>
    Primary,
}
