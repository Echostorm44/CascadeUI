using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Cascade.UI;

/// <summary>
/// Represents a parsed SVG document. Instances are cached by content hash
/// so that the same SVG used in multiple <see cref="Image"/> controls shares
/// one parsed tree. The <see cref="ApplyColor"/> and <see cref="ApplyColorMap"/>
/// methods support the color override features exposed by <see cref="Image.SvgColor"/>
/// and <see cref="Image.SvgColorMap"/>.
/// </summary>
public sealed class SvgDocument : IDisposable
{
    private static readonly ConcurrentDictionary<string, WeakReference<SvgDocument>> cache = new();

    private static readonly Regex viewBoxRegex = new(
        @"viewBox\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled);

    private static readonly Regex widthRegex = new(
        @"\bwidth\s*=\s*[""'](\d+(?:\.\d+)?)[""']",
        RegexOptions.Compiled);

    private static readonly Regex heightRegex = new(
        @"\bheight\s*=\s*[""'](\d+(?:\.\d+)?)[""']",
        RegexOptions.Compiled);

    private readonly string contentHash;
    private readonly string svgContent;
    private bool disposed;

    private SvgDocument(string svg, string hash)
    {
        svgContent = svg;
        contentHash = hash;
    }

    /// <summary>The original SVG content string.</summary>
    public string Content => svgContent;

    /// <summary>The SHA-256 content hash used for cache deduplication.</summary>
    public string ContentHash => contentHash;

    /// <summary>
    /// The intrinsic width from the SVG viewBox or width attribute.
    /// Defaults to 300 when neither is specified (per SVG spec).
    /// </summary>
    public float IntrinsicWidth { get; internal set; } = 300f;

    /// <summary>
    /// The intrinsic height from the SVG viewBox or height attribute.
    /// Defaults to 150 when neither is specified (per SVG spec).
    /// </summary>
    public float IntrinsicHeight { get; internal set; } = 150f;

    /// <summary>
    /// Parses an SVG string and returns a cached document.
    /// Repeated calls with identical content return the same instance
    /// (as long as a strong reference is held elsewhere).
    /// </summary>
    public static SvgDocument Parse(string svg)
    {
        ArgumentNullException.ThrowIfNull(svg);

        string hash = ComputeHash(svg);

        if (cache.TryGetValue(hash, out var weakRef) && weakRef.TryGetTarget(out var existing))
        {
            return existing;
        }

        var doc = new SvgDocument(svg, hash);
        ParseDimensions(svg, doc);
        cache[hash] = new WeakReference<SvgDocument>(doc);
        return doc;
    }

    /// <summary>
    /// Returns a new SVG string with <c>currentColor</c> references replaced
    /// by the specified color. Used by <see cref="Image.SvgColor"/> to tint
    /// monochrome icon SVGs.
    /// </summary>
    public string ApplyColor(ColorValue color)
    {
        string hex = ColorToSvgHex(color);
        return svgContent.Replace("currentColor", hex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a new SVG string with hex color values replaced according to the
    /// color map. Performs case-insensitive matching on hex strings (#RRGGBB format).
    /// Used by <see cref="Image.SvgColorMap"/> for multi-color theme adaptation.
    /// </summary>
    public string ApplyColorMap(IReadOnlyDictionary<string, ColorValue> colorMap)
    {
        ArgumentNullException.ThrowIfNull(colorMap);

        if (colorMap.Count == 0)
        {
            return svgContent;
        }

        string result = svgContent;
        foreach (var (hexColor, replacement) in colorMap)
        {
            string replacementHex = ColorToSvgHex(replacement);
            result = result.Replace(hexColor, replacementHex, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    /// <summary>Clears the document cache. Intended for test use.</summary>
    internal static void ClearCache()
    {
        cache.Clear();
    }

    /// <summary>Returns the number of documents currently in the cache.</summary>
    internal static int CacheCount => cache.Count;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            // Native resvg handle will be freed here once the Rust library is integrated.
            // For now, only managed state is held.
        }
    }

    /// <summary>
    /// Converts a <see cref="ColorValue"/> (premultiplied linear sRGB) to an
    /// SVG-compatible hex string in #RRGGBB format.
    /// </summary>
    private static string ColorToSvgHex(ColorValue color)
    {
        // ToHex() converts from premultiplied linear sRGB back to sRGB gamma
        // and returns #RRGGBBAA — we trim the alpha for SVG hex colors.
        string fullHex = color.ToHex();
        return fullHex[..7];
    }

    private static string ComputeHash(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static void ParseDimensions(string svg, SvgDocument doc)
    {
        // Lightweight dimension extraction from SVG markup. The full parse
        // (path geometry, styles, etc.) is deferred to the resvg native library.
        var viewBoxMatch = viewBoxRegex.Match(svg);

        if (viewBoxMatch.Success)
        {
            string[] parts = viewBoxMatch.Groups[1].Value.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 4 &&
                float.TryParse(parts[2], CultureInfo.InvariantCulture, out float w) &&
                float.TryParse(parts[3], CultureInfo.InvariantCulture, out float h))
            {
                doc.IntrinsicWidth = w;
                doc.IntrinsicHeight = h;
                return;
            }
        }

        var wMatch = widthRegex.Match(svg);
        var hMatch = heightRegex.Match(svg);

        if (wMatch.Success && float.TryParse(
            wMatch.Groups[1].Value, CultureInfo.InvariantCulture, out float width))
        {
            doc.IntrinsicWidth = width;
        }

        if (hMatch.Success && float.TryParse(
            hMatch.Groups[1].Value, CultureInfo.InvariantCulture, out float height))
        {
            doc.IntrinsicHeight = height;
        }
    }
}
