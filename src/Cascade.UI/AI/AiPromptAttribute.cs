namespace Cascade.UI;

/// <summary>
/// Marks a method as an AI prompt template. Prompt methods are called on the UI
/// thread when a <c>prompts/get</c> MCP request arrives, and return a resolved
/// prompt with app state woven in.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AiPromptAttribute : Attribute
{
    /// <summary>
    /// The prompt name exposed to MCP clients. Prefixed with <c>{appid}-</c> automatically.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable description of what this prompt helps with.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Creates an <see cref="AiPromptAttribute"/> with a name and description.
    /// </summary>
    public AiPromptAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
