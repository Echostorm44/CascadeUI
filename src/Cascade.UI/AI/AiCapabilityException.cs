namespace Cascade.UI;

/// <summary>
/// Exception thrown by AI capability methods to communicate actionable error
/// messages to the AI model. The message should be written for the AI to read,
/// reason about, and either retry with corrected parameters or explain to the user.
/// </summary>
public sealed class AiCapabilityException : Exception
{
    /// <summary>
    /// Whether the AI can retry the operation with corrected parameters.
    /// Default is true.
    /// </summary>
    public bool Recoverable { get; }

    /// <summary>
    /// The capability name that produced this error, if known.
    /// </summary>
    public string? CapabilityName { get; init; }

    /// <summary>
    /// The parameter that caused the error, if applicable.
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>
    /// Creates an <see cref="AiCapabilityException"/> with an AI-directed message.
    /// </summary>
    public AiCapabilityException(string message, bool recoverable = true)
        : base(message)
    {
        Recoverable = recoverable;
    }
}
