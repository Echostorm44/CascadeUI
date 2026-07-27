namespace Cascade.UI;

/// <summary>
/// Renders 1D barcodes as pure vector paths. Supports Code128, Code39,
/// EAN-13, UPC-A, ITF, and other standard barcode formats.
/// </summary>
public sealed class Barcode : Node
{
    /// <summary>
    /// Creates a barcode.
    /// </summary>
    /// <param name="content">The string content to encode.</param>
    /// <param name="format">
    /// Barcode format. When null, the format is auto-detected from content.
    /// </param>
    /// <param name="width">Width in logical pixels.</param>
    /// <param name="height">Height in logical pixels.</param>
    /// <param name="showText">Show human-readable text below the bars.</param>
    /// <param name="textStyle">Text style for the human-readable label.</param>
    public Barcode(
        string content,
        BarcodeFormat? format = null,
        float width = 300,
        float height = 80,
        bool showText = true,
        TextStyle? textStyle = null)
    {
        Content = content;
        BarcodeDisplayFormat = format;
        BarcodeWidth = width;
        BarcodeHeight = height;
        ShowText = showText;
        LabelTextStyle = textStyle;
    }

    /// <summary>The encoded string content.</summary>
    public string Content { get; }

    /// <summary>Barcode format, or null for auto-detection.</summary>
    public BarcodeFormat? BarcodeDisplayFormat { get; }

    /// <summary>Width in logical pixels.</summary>
    public float BarcodeWidth { get; }

    /// <summary>Height in logical pixels.</summary>
    public float BarcodeHeight { get; }

    /// <summary>Whether to show human-readable text below the bars.</summary>
    public bool ShowText { get; }

    /// <summary>Text style for the human-readable label.</summary>
    public TextStyle? LabelTextStyle { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal ColorValue? ForegroundColorOverride { get; set; }
    internal ColorValue? BackgroundColorOverride { get; set; }

    // Cached encoded modules — Content and Format are immutable after
    // construction so the encoded bar pattern never changes. Computed
    // lazily on first paint to avoid encoding work for off-screen barcodes.
    private bool[]? cachedModules;
    private bool encodingFailed;

    /// <summary>
    /// Returns the cached encoded module pattern, encoding on first call.
    /// Returns null if encoding failed (caller should render error state).
    /// </summary>
    internal bool[]? GetEncodedModules()
    {
        if (cachedModules is not null)
        {
            return cachedModules;
        }
        if (encodingFailed)
        {
            return null;
        }
        try
        {
            cachedModules = BarcodeEncoder.Encode(Content, BarcodeDisplayFormat);
            return cachedModules;
        }
        catch
        {
            encodingFailed = true;
            return null;
        }
    }

    /// <summary>Sets the foreground (bar) color.</summary>
    public Barcode ForegroundColor(ColorValue color)
    {
        ForegroundColorOverride = color;
        return this;
    }

    /// <summary>Sets the background color.</summary>
    public Barcode BackgroundColor(ColorValue color)
    {
        BackgroundColorOverride = color;
        return this;
    }
}

/// <summary>
/// Barcode symbology format.
/// </summary>
public enum BarcodeFormat
{
    /// <summary>Variable length, full ASCII — most versatile, recommended default.</summary>
    Code128,

    /// <summary>Variable length, alphanumeric — older industrial use.</summary>
    Code39,

    /// <summary>More compact Code39 variant.</summary>
    Code93,

    /// <summary>13-digit product code (ISBN uses this).</summary>
    EAN13,

    /// <summary>8-digit short product code.</summary>
    EAN8,

    /// <summary>12-digit US product code.</summary>
    UPCA,

    /// <summary>Compressed UPC for small packages.</summary>
    UPCE,

    /// <summary>Interleaved 2-of-5, pairs of digits, logistics.</summary>
    ITF,

    /// <summary>14-digit logistics/shipping.</summary>
    ITF14,

    /// <summary>Variable length, numeric + symbols, blood bank/library.</summary>
    Codabar,

    /// <summary>MSI Plessey, inventory.</summary>
    MSI
}
