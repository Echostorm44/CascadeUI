namespace Cascade.UI;

/// <summary>
/// Predefined dialog sizes. Controls the width of the dialog container.
/// Height is determined by content unless explicitly specified.
/// </summary>
public class DialogSize
{
    private DialogSize()
    {
    }

    internal float? Width { get; private init; }

    internal float? Height { get; private init; }

    /// <summary>Wraps content — default for small dialogs. Same as <see cref="FitContent"/>.</summary>
    public static DialogSize Auto { get; } = new();

    /// <summary>~400px wide — confirmations, alerts, prompts.</summary>
    public static DialogSize Small { get; } = new();

    /// <summary>~560px wide — forms, settings panels.</summary>
    public static DialogSize Medium { get; } = new();

    /// <summary>~720px wide — complex content.</summary>
    public static DialogSize Large { get; } = new();

    /// <summary>Fills the window — used for mobile-style flow screens.</summary>
    public static DialogSize FullScreen { get; } = new();

    /// <summary>Same as <see cref="Auto"/> — wraps content.</summary>
    public static DialogSize FitContent { get; } = new();

    /// <summary>
    /// Custom size with explicit dimensions. Height is optional — when null,
    /// height is determined by content.
    /// </summary>
    /// <param name="width">Width in logical pixels.</param>
    /// <param name="height">Optional height in logical pixels.</param>
    public static DialogSize Custom(float width, float? height = null)
    {
        return new DialogSize
        {
            Width = width,
            Height = height
        };
    }
}
