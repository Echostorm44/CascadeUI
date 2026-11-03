using System.Globalization;
using System.Text.RegularExpressions;

namespace Cascade.UI;

/// <summary>
/// Colour conversions the picker works in: HSB (its own model), sRGB channels, HSL and OKLCH, and
/// the text forms of each, read back leniently (with or without the function name, commas or
/// percent signs).
/// </summary>
internal static partial class ColorMath
{
    /// <summary>sRGB channels (0–1) and straight alpha.</summary>
    internal static (float R, float G, float B, float A) ToSrgb(ColorValue color)
    {
        if (color.A <= 0f)
        {
            return (0f, 0f, 0f, 0f);
        }
        float inv = 1f / color.A;
        return (LinearToSrgb(color.R * inv), LinearToSrgb(color.G * inv), LinearToSrgb(color.B * inv), color.A);
    }

    internal static float LinearToSrgb(float c)
    {
        c = Math.Clamp(c, 0f, 1f);
        return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
    }

    internal static float SrgbToLinear(float c)
    {
        c = Math.Clamp(c, 0f, 1f);
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    internal static (float H, float S, float B) RgbToHsb(float r, float g, float b)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
        float h = 0f;
        if (delta > 1e-5f)
        {
            if (max == r)
            {
                h = 60f * ((g - b) / delta % 6f);
            }
            else if (max == g)
            {
                h = 60f * ((b - r) / delta + 2f);
            }
            else
            {
                h = 60f * ((r - g) / delta + 4f);
            }
        }
        if (h < 0f)
        {
            h += 360f;
        }
        return (h, max > 0f ? delta / max : 0f, max);
    }

    internal static (float R, float G, float B) HsbToRgb(float hue, float saturation, float brightness)
    {
        float h = (hue % 360f + 360f) % 360f;
        float c = brightness * saturation;
        float hp = h / 60f;
        float x = c * (1f - MathF.Abs(hp % 2f - 1f));
        float m = brightness - c;
        var (r, g, b) = hp switch
        {
            < 1f => (c, x, 0f),
            < 2f => (x, c, 0f),
            < 3f => (0f, c, x),
            < 4f => (0f, x, c),
            < 5f => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return (r + m, g + m, b + m);
    }

    internal static ColorValue FromHsb(float hue, float saturation, float brightness, float alpha)
    {
        var (r, g, b) = HsbToRgb(hue, saturation, brightness);
        return ColorValue.FromRgba(r, g, b, alpha);
    }

    internal static (float H, float S, float L) HsbToHsl(float h, float s, float b)
    {
        float l = b * (1f - s / 2f);
        float sl = l is <= 0f or >= 1f ? 0f : (b - l) / MathF.Min(l, 1f - l);
        return (h, sl, l);
    }

    internal static (float H, float S, float B) HslToHsb(float h, float s, float l)
    {
        float b = l + s * MathF.Min(l, 1f - l);
        return (h, b <= 0f ? 0f : 2f * (1f - l / b), b);
    }

    // OKLab (Björn Ottosson), from and to linear sRGB.
    internal static (float L, float C, float H) ToOklch(ColorValue color)
    {
        var (r, g, b, _) = ToSrgb(color);
        float lr = SrgbToLinear(r), lg = SrgbToLinear(g), lb = SrgbToLinear(b);
        float l = MathF.Cbrt(0.4122214708f * lr + 0.5363325363f * lg + 0.0514459929f * lb);
        float m = MathF.Cbrt(0.2119034982f * lr + 0.6806995451f * lg + 0.1073969566f * lb);
        float s = MathF.Cbrt(0.0883024619f * lr + 0.2817188376f * lg + 0.6299787005f * lb);
        float labL = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
        float labA = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
        float labB = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
        float c = MathF.Sqrt(labA * labA + labB * labB);
        float h = MathF.Atan2(labB, labA) * 180f / MathF.PI;
        return (labL, c, h < 0f ? h + 360f : h);
    }

    internal static (float R, float G, float B) FromOklch(float lightness, float chroma, float hue)
    {
        float rad = hue * MathF.PI / 180f;
        float a = chroma * MathF.Cos(rad), b = chroma * MathF.Sin(rad);
        float l = lightness + 0.3963377774f * a + 0.2158037573f * b;
        float m = lightness - 0.1055613458f * a - 0.0638541728f * b;
        float s = lightness - 0.0894841775f * a - 1.2914855480f * b;
        l *= l * l;
        m *= m * m;
        s *= s * s;
        float lr = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        float lg = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float lb = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        return (LinearToSrgb(lr), LinearToSrgb(lg), LinearToSrgb(lb));
    }

    private static string N(float value, string format = "0") => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The colour as text in a format (alpha included when it is not opaque).</summary>
    internal static string Format(ColorFormat format, float hue, float saturation, float brightness, float alpha)
    {
        var (r, g, b) = HsbToRgb(hue, saturation, brightness);
        bool translucent = alpha < 0.999f;
        string a = N(alpha, "0.##");
        switch (format)
        {
            case ColorFormat.RGB:
                return translucent
                    ? $"rgba({N(r * 255f)}, {N(g * 255f)}, {N(b * 255f)}, {a})"
                    : $"rgb({N(r * 255f)}, {N(g * 255f)}, {N(b * 255f)})";
            case ColorFormat.HSL:
            {
                var (h, s, l) = HsbToHsl(hue, saturation, brightness);
                string body = $"{N(h)}, {N(s * 100f)}%, {N(l * 100f)}%";
                return translucent ? $"hsla({body}, {a})" : $"hsl({body})";
            }
            case ColorFormat.HSB:
            {
                string body = $"{N(hue)}, {N(saturation * 100f)}%, {N(brightness * 100f)}%";
                return translucent ? $"hsba({body}, {a})" : $"hsb({body})";
            }
            case ColorFormat.OKLCH:
            {
                var (l, c, h) = ToOklch(ColorValue.FromRgba(r, g, b, 1f));
                string body = $"{N(l * 100f, "0.#")}% {N(c, "0.###")} {N(h, "0.#")}";
                return translucent ? $"oklch({body} / {a})" : $"oklch({body})";
            }
            default:
            {
                static string Byte(float v) => ((int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f)).ToString("X2", CultureInfo.InvariantCulture);
                return "#" + Byte(r) + Byte(g) + Byte(b) + (translucent ? Byte(alpha) : string.Empty);
            }
        }
    }

    [GeneratedRegex(@"-?\d+(\.\d+)?|-?\.\d+")]
    private static partial Regex Numbers();

    /// <summary>
    /// Reads colour text in a format: hex (#RGB, #RRGGBB, #RRGGBBAA, with or without #), or the
    /// numbers of RGB, HSL, HSB or OKLCH in order, an optional alpha last. Null when it is not one.
    /// </summary>
    internal static (float H, float S, float B, float A)? Parse(string text, ColorFormat format)
    {
        string t = text.Trim();
        if (t.Length == 0)
        {
            return null;
        }
        if (t.StartsWith('#') || format == ColorFormat.Hex)
        {
            string hex = t.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            }
            if (hex.Length is not (6 or 8) || !hex.All(Uri.IsHexDigit))
            {
                return null;
            }
            float Channel(int at) => int.Parse(hex.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
            var (h, s, b) = RgbToHsb(Channel(0), Channel(2), Channel(4));
            return (h, s, b, hex.Length == 8 ? Channel(6) : 1f);
        }

        var values = Numbers().Matches(t).Select(m => float.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (values.Count < 3)
        {
            return null;
        }
        float alpha = values.Count >= 4 ? Math.Clamp(values[3] > 1f ? values[3] / 100f : values[3], 0f, 1f) : 1f;
        float Percent(float v) => Math.Clamp(v > 1f || t.Contains('%', StringComparison.Ordinal) ? v / 100f : v, 0f, 1f);
        switch (format)
        {
            case ColorFormat.RGB:
            {
                var (h, s, b) = RgbToHsb(Math.Clamp(values[0] / 255f, 0f, 1f), Math.Clamp(values[1] / 255f, 0f, 1f), Math.Clamp(values[2] / 255f, 0f, 1f));
                return (h, s, b, alpha);
            }
            case ColorFormat.HSL:
            {
                var (h, s, b) = HslToHsb(values[0], Percent(values[1]), Percent(values[2]));
                return (h % 360f, s, b, alpha);
            }
            case ColorFormat.HSB:
                return (values[0] % 360f, Percent(values[1]), Percent(values[2]), alpha);
            case ColorFormat.OKLCH:
            {
                float l = values[0] > 1f ? values[0] / 100f : values[0];
                var (r, g, b) = FromOklch(l, values[1], values[2]);
                var (h, s, bb) = RgbToHsb(r, g, b);
                return (h, s, bb, alpha);
            }
            default:
                return null;
        }
    }
}

/// <summary>A part of the picker a press can land on.</summary>
internal enum ColorPickerPart
{
    None,
    ModeTab,
    Canvas,
    HueBar,
    Ring,
    Triangle,
    Channel,
    Opacity,
    Format,
    Value,
    EyeDropper,
    Swatch,
    AddSwatch,
    Recent,
}

/// <summary>
/// Where each part of a <see cref="ColorPicker"/> is, in its own coordinates: one arrangement
/// shared by layout (the height), painting and input, so a press always lands where it was drawn.
/// </summary>
internal sealed class ColorPickerGeometry
{
    internal const float Pad = 10f;
    internal const float Gap = 10f;
    internal const float BarHeight = 14f;
    internal const float TabHeight = 26f;
    internal const float CanvasHeight = 150f;
    internal const float RowHeight = 28f;
    internal const float SwatchSize = 20f;
    internal const float SwatchGap = 6f;
    internal const float ChannelLabelWidth = 16f;
    internal const float ChannelValueWidth = 34f;
    internal const float RingThickness = 16f;

    internal List<Rect> Tabs { get; } = [];
    internal Rect Canvas { get; private set; }
    internal Rect HueBar { get; private set; }
    internal Point WheelCenter { get; private set; }
    internal float WheelOuter { get; private set; }
    internal float WheelInner => WheelOuter - RingThickness;
    internal (Point Hue, Point White, Point Black) Triangle { get; private set; }
    internal Rect WheelBounds { get; private set; }
    internal List<Rect> Channels { get; } = [];
    internal Rect Opacity { get; private set; }
    internal Rect Preview { get; private set; }
    internal Rect Format { get; private set; }
    internal Rect Value { get; private set; }
    internal Rect EyeDropper { get; private set; }
    internal List<Rect> Saved { get; } = [];
    internal Rect AddSwatch { get; private set; }
    internal List<Rect> Recent { get; } = [];
    internal float Height { get; private set; }

    internal static ColorPickerGeometry Arrange(ColorPicker cp, float width)
    {
        var g = new ColorPickerGeometry();
        float x = Pad, w = Math.Max(60f, width - Pad * 2f), y = Pad;

        var modes = cp.Modes;
        if (modes.Count > 1)
        {
            float tab = w / modes.Count;
            for (int i = 0; i < modes.Count; i++)
            {
                g.Tabs.Add(new Rect(x + i * tab, y, tab, TabHeight));
            }
            y += TabHeight + Gap;
        }

        switch (cp.Mode)
        {
            case ColorPickerMode.Wheel:
            {
                float size = Math.Min(w, 200f);
                g.WheelBounds = new Rect(x + (w - size) / 2f, y, size, size);
                g.WheelCenter = new Point(g.WheelBounds.X + size / 2f, y + size / 2f);
                g.WheelOuter = size / 2f;
                float r = g.WheelInner - 6f;
                // Hue at the top, white bottom left, black bottom right.
                g.Triangle = (
                    new Point(g.WheelCenter.X, g.WheelCenter.Y - r),
                    new Point(g.WheelCenter.X - r * 0.8660254f, g.WheelCenter.Y + r * 0.5f),
                    new Point(g.WheelCenter.X + r * 0.8660254f, g.WheelCenter.Y + r * 0.5f));
                y += size;
                break;
            }
            case ColorPickerMode.Sliders:
            {
                for (int i = 0; i < 3; i++)
                {
                    g.Channels.Add(new Rect(x + ChannelLabelWidth + 6f, y + (22f - BarHeight) / 2f, w - ChannelLabelWidth - ChannelValueWidth - 12f, BarHeight));
                    y += 22f + (i < 2 ? 6f : 0f);
                }
                break;
            }
            default:
                g.Canvas = new Rect(x, y, w, CanvasHeight);
                y += CanvasHeight + Gap;
                g.HueBar = new Rect(x, y, w, BarHeight);
                y += BarHeight;
                break;
        }

        if (cp.ShowOpacitySlider)
        {
            y += Gap;
            g.Opacity = new Rect(x, y, w, BarHeight);
            y += BarHeight;
        }

        // The value row: a preview, the format, the value text and the eye dropper.
        y += Gap;
        g.Preview = new Rect(x, y, RowHeight, RowHeight);
        float right = x + w;
        if (cp.EyeDropperAvailable)
        {
            g.EyeDropper = new Rect(right - RowHeight, y, RowHeight, RowHeight);
            right -= RowHeight + 6f;
        }
        float formatWidth = cp.Formats.Count > 1 ? 52f : 0f;
        float valueX = x + RowHeight + 6f;
        if (formatWidth > 0f)
        {
            g.Format = new Rect(valueX, y, formatWidth, RowHeight);
            valueX += formatWidth + 4f;
        }
        g.Value = new Rect(valueX, y, Math.Max(20f, right - valueX), RowHeight);
        y += RowHeight;

        // Swatch rows, wrapping: the saved ones and a "+" to save the current colour, then the recent ones.
        void Swatches(List<Rect> into, int count, bool withAdd)
        {
            int total = count + (withAdd ? 1 : 0);
            if (total == 0)
            {
                return;
            }
            y += Gap;
            int perRow = Math.Max(1, (int)((w + SwatchGap) / (SwatchSize + SwatchGap)));
            for (int i = 0; i < total; i++)
            {
                var rect = new Rect(x + i % perRow * (SwatchSize + SwatchGap), y + i / perRow * (SwatchSize + SwatchGap), SwatchSize, SwatchSize);
                if (withAdd && i == count)
                {
                    g.AddSwatch = rect;
                }
                else
                {
                    into.Add(rect);
                }
            }
            y += ((total - 1) / perRow + 1) * (SwatchSize + SwatchGap) - SwatchGap;
        }
        if (cp.SavedSwatches is { } saved)
        {
            Swatches(g.Saved, saved.Value?.Count ?? 0, withAdd: true);
        }
        Swatches(g.Recent, cp.RecentShown.Count, withAdd: false);

        g.Height = y + Pad;
        return g;
    }

    /// <summary>What a point (in the picker's coordinates) is over, and which one of several.</summary>
    internal (ColorPickerPart Part, int Index) HitTest(Point p)
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].Contains(p))
            {
                return (ColorPickerPart.ModeTab, i);
            }
        }
        const float slop = 6f;
        static bool Near(Rect r, Point p) => r.Width > 0f && p.X >= r.X - 2f && p.X <= r.Right + 2f && p.Y >= r.Y - slop && p.Y <= r.Bottom + slop;
        if (Canvas.Width > 0f && Canvas.Contains(p))
        {
            return (ColorPickerPart.Canvas, 0);
        }
        if (Near(HueBar, p))
        {
            return (ColorPickerPart.HueBar, 0);
        }
        if (WheelOuter > 0f)
        {
            float d = MathF.Sqrt((p.X - WheelCenter.X) * (p.X - WheelCenter.X) + (p.Y - WheelCenter.Y) * (p.Y - WheelCenter.Y));
            if (d <= WheelOuter && d >= WheelInner)
            {
                return (ColorPickerPart.Ring, 0);
            }
            if (d < WheelInner)
            {
                return (ColorPickerPart.Triangle, 0);
            }
        }
        for (int i = 0; i < Channels.Count; i++)
        {
            if (Near(Channels[i], p))
            {
                return (ColorPickerPart.Channel, i);
            }
        }
        if (Near(Opacity, p))
        {
            return (ColorPickerPart.Opacity, 0);
        }
        if (Format.Width > 0f && Format.Contains(p))
        {
            return (ColorPickerPart.Format, 0);
        }
        if (Value.Contains(p) || Preview.Contains(p))
        {
            return (ColorPickerPart.Value, 0);
        }
        if (EyeDropper.Width > 0f && EyeDropper.Contains(p))
        {
            return (ColorPickerPart.EyeDropper, 0);
        }
        for (int i = 0; i < Saved.Count; i++)
        {
            if (Saved[i].Contains(p))
            {
                return (ColorPickerPart.Swatch, i);
            }
        }
        if (AddSwatch.Width > 0f && AddSwatch.Contains(p))
        {
            return (ColorPickerPart.AddSwatch, 0);
        }
        for (int i = 0; i < Recent.Count; i++)
        {
            if (Recent[i].Contains(p))
            {
                return (ColorPickerPart.Recent, i);
            }
        }
        return (ColorPickerPart.None, 0);
    }

    /// <summary>Saturation and brightness at a point of the triangle (clamped into it).</summary>
    internal (float S, float B) TriangleAt(Point p)
    {
        var (hue, white, black) = Triangle;
        // Barycentric weights of the hue, white and black corners.
        float det = (white.Y - black.Y) * (hue.X - black.X) + (black.X - white.X) * (hue.Y - black.Y);
        float wh = ((white.Y - black.Y) * (p.X - black.X) + (black.X - white.X) * (p.Y - black.Y)) / det;
        float ww = ((black.Y - hue.Y) * (p.X - black.X) + (hue.X - black.X) * (p.Y - black.Y)) / det;
        wh = Math.Clamp(wh, 0f, 1f);
        ww = Math.Clamp(ww, 0f, 1f - wh);
        float brightness = wh + ww;
        return (brightness <= 0f ? 0f : wh / brightness, brightness);
    }

    /// <summary>Where a saturation and brightness sit in the triangle.</summary>
    internal Point TrianglePoint(float saturation, float brightness)
    {
        var (hue, white, black) = Triangle;
        float wh = saturation * brightness, ww = brightness - wh, wb = 1f - brightness;
        return new Point(hue.X * wh + white.X * ww + black.X * wb, hue.Y * wh + white.Y * ww + black.Y * wb);
    }
}
