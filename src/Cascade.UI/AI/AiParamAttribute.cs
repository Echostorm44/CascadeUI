namespace Cascade.UI;

/// <summary>
/// Provides a description for an AI capability parameter. The description is
/// included in the generated JSON schema for the MCP tool definition.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class AiParamAttribute : Attribute
{
    /// <summary>
    /// Human-readable description of the parameter for AI comprehension.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates an <see cref="AiParamAttribute"/> with a description.
    /// </summary>
    public AiParamAttribute(string description)
    {
        Description = description;
    }
}
