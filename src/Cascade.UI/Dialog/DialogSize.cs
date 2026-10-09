namespace Cascade.UI;

/// <summary>
/// Predefined dialog sizes. Controls the width of the dialog container.
/// Height is determined by content unless explicitly specified. Every size is kept inside the
/// window: a width larger than the window (less a small margin) shrinks to fit.
/// </summary>
public class DialogSize
{
    private DialogSize(DialogSizeKind kind, float? width = null, float? height = null)
    {
        Kind = kind;
        Width = width;
        Height = height;
    }

    internal DialogSizeKind Kind { get; }

    internal float? Width { get; }

    internal float? Height { get; }

    /// <summary>Wraps content — default for small dialogs. Same as <see cref="FitContent"/>.</summary>
    public static DialogSize Auto { get; } = new(DialogSizeKind.Auto);

    /// <summary>400px wide — confirmations, alerts, prompts.</summary>
    public static DialogSize Small { get; } = new(DialogSizeKind.Fixed, 400f);

    /// <summary>560px wide — forms, settings panels.</summary>
    public static DialogSize Medium { get; } = new(DialogSizeKind.Fixed, 560f);

    /// <summary>720px wide — complex content.</summary>
    public static DialogSize Large { get; } = new(DialogSizeKind.Fixed, 720f);

    /// <summary>Fills the window — used for mobile-style flow screens.</summary>
    public static DialogSize FullScreen { get; } = new(DialogSizeKind.FullScreen);

    /// <summary>Same as <see cref="Auto"/> — wraps content.</summary>
    public static DialogSize FitContent { get; } = new(DialogSizeKind.Auto);

    /// <summary>
    /// Custom size with explicit dimensions. Height is optional — when null,
    /// height is determined by content.
    /// </summary>
    /// <param name="width">Width in logical pixels.</param>
    /// <param name="height">Optional height in logical pixels.</param>
    public static DialogSize Custom(float width, float? height = null)
    {
        if (!(width > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A dialog width must be positive.");
        }

        if (height is { } h && !(h > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A dialog height must be positive.");
        }

        return new DialogSize(DialogSizeKind.Fixed, width, height);
    }
}

/// <summary>How a <see cref="DialogSize"/> constrains the panel.</summary>
internal enum DialogSizeKind
{
    /// <summary>Content-sized, up to the window.</summary>
    Auto,

    /// <summary>A fixed width (and optionally height), shrunk to fit the window.</summary>
    Fixed,

    /// <summary>Exactly the window.</summary>
    FullScreen,
}
