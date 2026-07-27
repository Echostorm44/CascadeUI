namespace Cascade.UI;

/// <summary>
/// Marks a method as an AI-callable capability. The source generator registers
/// the method in the static <c>AiToolRegistry</c> with name, description, and
/// parameter schema derived from the method signature.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AiCapabilityAttribute : Attribute
{
    /// <summary>
    /// Creates an <see cref="AiCapabilityAttribute"/> with a description.
    /// </summary>
    public AiCapabilityAttribute(string description)
    {
        Description = description;
    }

    /// <summary>
    /// Creates an <see cref="AiCapabilityAttribute"/> without a description.
    /// </summary>
    public AiCapabilityAttribute()
    {
    }

    /// <summary>
    /// The tool name exposed to AI agents. Defaults to the method name if not specified.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// A human-readable description of what this capability does.
    /// Must be at least 20 words for adequate AI comprehension.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// When true, AI knows calling this capability has no side effects.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// When true, capability streams progress updates via <see cref="IProgress{T}"/>.
    /// </summary>
    public bool Streaming { get; set; }

    /// <summary>
    /// When true, generates a preview tool that executes, captures state delta,
    /// then restores original state. No external side effects allowed.
    /// </summary>
    public bool Previewable { get; set; }

    /// <summary>
    /// When true, a native confirmation dialog is shown before execution.
    /// User must confirm before the capability proceeds.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Message shown in the confirmation dialog when <see cref="RequiresConfirmation"/> is true.
    /// </summary>
    public string? ConfirmationMessage { get; set; }
}
