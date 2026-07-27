namespace Cascade.UI;

/// <summary>
/// Renders a QR code as pure vector paths for any string content.
/// Sharp at any size without anti-aliasing artifacts.
/// </summary>
public sealed class QrCode : Node
{
    /// <summary>
    /// Creates a QR code.
    /// </summary>
    /// <param name="content">The string content to encode.</param>
    /// <param name="size">Size in logical pixels.</param>
    /// <param name="errorCorrection">Error correction level.</param>
    /// <param name="foreground">Foreground (module) color. Null uses theme text color.</param>
    /// <param name="background">Background color. Null uses theme surface color.</param>
    public QrCode(
        string content,
        float size = 200,
        QrErrorCorrection errorCorrection = QrErrorCorrection.Medium,
        ColorValue? foreground = null,
        ColorValue? background = null)
    {
        Content = content;
        QrSize = size;
        ErrorCorrection = errorCorrection;
        Foreground = foreground;
        Background = background;
    }

    /// <summary>The encoded string content.</summary>
    public string Content { get; }

    /// <summary>Size in logical pixels.</summary>
    public float QrSize { get; }

    /// <summary>Error correction level.</summary>
    public QrErrorCorrection ErrorCorrection { get; }

    /// <summary>Foreground (module) color.</summary>
    public ColorValue? Foreground { get; }

    /// <summary>Background color.</summary>
    public ColorValue? Background { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal Node LogoNode { get; private set; } = Node.Empty;
    internal float LogoSizeFraction { get; private set; } = 0.25f;
    internal bool LogoBackgroundEnabled { get; private set; } = true;
    internal bool HasLogo { get; private set; }
    internal QrErrorCorrection EffectiveErrorCorrection =>
        HasLogo ? QrErrorCorrection.High : ErrorCorrection;

    // Cached encoded matrix — Content and EffectiveErrorCorrection are
    // immutable after construction (HasLogo is set via Logo() before first
    // paint), so the encoded module grid never changes. Computed lazily on
    // first paint to avoid encoding work for off-screen QR codes.
    private bool[][]? cachedMatrix;
    private bool encodingFailed;

    /// <summary>
    /// Returns the cached encoded module matrix, encoding on first call.
    /// Returns null if encoding failed (caller should render nothing).
    /// </summary>
    internal bool[][]? GetEncodedMatrix()
    {
        if (cachedMatrix is not null)
        {
            return cachedMatrix;
        }
        if (encodingFailed)
        {
            return null;
        }
        try
        {
            cachedMatrix = QrEncoder.Encode(Content, EffectiveErrorCorrection);
            return cachedMatrix;
        }
        catch
        {
            encodingFailed = true;
            return null;
        }
    }

    /// <summary>
    /// Adds a logo overlay in the center. Automatically uses High error
    /// correction when a logo is present.
    /// </summary>
    /// <param name="logo">The logo image node.</param>
    /// <param name="size">Logo size as a fraction of the QR code size.</param>
    /// <param name="background">Whether to show a white background behind the logo.</param>
    public QrCode Logo(Node logo, float size = 0.25f, bool background = true)
    {
        LogoNode = logo;
        LogoSizeFraction = size;
        LogoBackgroundEnabled = background;
        HasLogo = true;
        return this;
    }
}

/// <summary>
/// Error correction level for <see cref="QrCode"/>. Higher levels allow
/// the code to remain scannable when partially obscured, at the cost of
/// increased density.
/// </summary>
public enum QrErrorCorrection
{
    /// <summary>~7% recovery capacity.</summary>
    Low,

    /// <summary>~15% recovery capacity (recommended default).</summary>
    Medium,

    /// <summary>~25% recovery capacity.</summary>
    Quartile,

    /// <summary>~30% recovery capacity. Use when a logo overlays the center.</summary>
    High
}
