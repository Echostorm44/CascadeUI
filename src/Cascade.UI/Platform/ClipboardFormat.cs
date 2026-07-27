namespace Cascade.UI;

/// <summary>
/// Identifies a clipboard data format. Standard formats are predefined as
/// static members. Application-specific formats use <see cref="Custom"/>.
/// </summary>
public sealed class ClipboardFormat : IEquatable<ClipboardFormat>
{
    private readonly string name;

    private ClipboardFormat(string name)
    {
        this.name = name;
    }

    /// <summary>Plain Unicode text (CF_UNICODETEXT on Windows).</summary>
    public static ClipboardFormat Text { get; } = new("Text");

    /// <summary>HTML fragment (CF_HTML on Windows).</summary>
    public static ClipboardFormat Html { get; } = new("Html");

    /// <summary>Rich Text Format.</summary>
    public static ClipboardFormat Rtf { get; } = new("Rtf");

    /// <summary>Bitmap image data (CF_DIBV5 on Windows).</summary>
    public static ClipboardFormat Image { get; } = new("Image");

    /// <summary>File list (CF_HDROP on Windows).</summary>
    public static ClipboardFormat Files { get; } = new("Files");

    /// <summary>
    /// Creates a named custom clipboard format for application-specific data.
    /// </summary>
    /// <param name="formatName">The format name registered with the OS clipboard.</param>
    public static ClipboardFormat Custom(string formatName)
    {
        return new ClipboardFormat(formatName);
    }

    /// <summary>The format name.</summary>
    public string Name => name;

    /// <inheritdoc/>
    public bool Equals(ClipboardFormat? other)
    {
        return other is not null && name == other.name;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ClipboardFormat other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return name.GetHashCode(StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return name;
    }
}
