using System.Collections.Concurrent;
using System.Globalization;
using Etch.Text.Shape;
using HB = Etch.Text.HarfBuzz;

namespace Cascade.UI;

/// <summary>
/// A font locator is the string Cascade's text pipeline keys fonts by: a font file path, optionally
/// followed by a weight for a variable font (<c>C:\fonts\InterVariable.ttf|wght=300</c>). Every
/// cache and layout option keeps treating it as an opaque string; only the places that open the
/// file (the shaper, the render backend) split it into the path and the
/// <see cref="FontVariation"/>s to apply. <c>|</c> cannot appear in a Windows path and does not
/// in practice elsewhere.
/// </summary>
internal static class FontLocator
{
    private const string WeightMarker = "|wght=";

    // Per font file: its weight axis's default value, or NaN for a font without one.
    private static readonly ConcurrentDictionary<string, float> weightAxisDefaults = new(StringComparer.Ordinal);

    /// <summary>
    /// The locator of <paramref name="fontPath"/> at <paramref name="weight"/>: the plain path for a
    /// static font or the variable font's default weight, else the path with the weight attached.
    /// <see cref="FontWeight.None"/> means regular.
    /// </summary>
    public static string WithWeight(string fontPath, FontWeight weight)
    {
        string path = FilePath(fontPath);
        float axisDefault = WeightAxisDefault(path);
        int requested = weight == FontWeight.None ? (int)FontWeight.Regular : (int)weight;
        if (float.IsNaN(axisDefault) || requested == (int)MathF.Round(axisDefault))
        {
            return path;
        }
        return path + WeightMarker + requested.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The font file a locator names.</summary>
    public static string FilePath(string locator)
    {
        int marker = locator.IndexOf(WeightMarker, StringComparison.Ordinal);
        return marker < 0 ? locator : locator[..marker];
    }

    /// <summary>The weight a locator asks for, or <see cref="FontWeight.None"/> for the font as it is.</summary>
    public static FontWeight Weight(string locator)
    {
        int marker = locator.IndexOf(WeightMarker, StringComparison.Ordinal);
        if (marker < 0
            || !int.TryParse(locator.AsSpan(marker + WeightMarker.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight))
        {
            return FontWeight.None;
        }
        return (FontWeight)weight;
    }

    /// <summary>The design-axis settings to load a locator's font with (empty for the font as it is).</summary>
    public static FontVariation[] Variations(string locator)
    {
        var weight = Weight(locator);
        return weight == FontWeight.None ? [] : [FontVariation.Weight((float)weight)];
    }

    /// <summary>Whether the font file is a variable font with a weight axis. Cached per file.</summary>
    public static bool HasWeightAxis(string fontPath) => !float.IsNaN(WeightAxisDefault(FilePath(fontPath)));

    private static float WeightAxisDefault(string fontPath)
    {
        return weightAxisDefaults.GetOrAdd(fontPath, static path =>
        {
            if (!File.Exists(path))
            {
                return float.NaN;
            }
            try
            {
                using var blob = HB.Blob.FromFile(path);
                using var face = new HB.Face(blob, 0);
                foreach (var axis in face.GetVariationAxes())
                {
                    if (axis.Tag == FontVariation.WeightAxis)
                    {
                        return axis.DefaultValue;
                    }
                }
                return float.NaN;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                return float.NaN;
            }
        });
    }
}

/// <summary>
/// Font file contents, read once per file and shared by everything that loads the font (the
/// shaper's ink faces, the render backend's faces at every size and weight). A variable font is
/// most of a megabyte; reading it per weight or per consumer multiplied that.
/// </summary>
internal static class FontFiles
{
    private static readonly ConcurrentDictionary<string, byte[]> contents = new(StringComparer.Ordinal);

    /// <summary>The bytes of <paramref name="fontPath"/>. Callers must not modify them.</summary>
    public static byte[] Bytes(string fontPath) => contents.GetOrAdd(fontPath, static path => File.ReadAllBytes(path));
}
