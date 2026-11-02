using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cascade.Generators;

/// <summary>
/// Reads an SVG icon file into the path data an <c>Icon</c> carries. Every drawn shape becomes
/// one path string, in document order: <c>path</c> as written, and <c>circle</c>,
/// <c>ellipse</c>, <c>rect</c>, <c>line</c>, <c>polyline</c> and <c>polygon</c> converted to the
/// equivalent path.
/// </summary>
/// <remarks>
/// Shared by <see cref="IconPackGenerator"/> and <c>tools/LucideImport</c> (which links this file)
/// so the built-in pack and an app's own packs are read the same way. No XML parser, because
/// generators target netstandard2.0 and icon files are small and regular. What it cannot
/// represent (transforms, <c>use</c>, percentage lengths) is listed in
/// <see cref="Result.Unsupported"/> rather than skipped silently.
/// </remarks>
internal static class SvgIconParser
{
    /// <summary>The paths, view box, title and anything that could not be represented.</summary>
    internal sealed class Result
    {
        public List<string> Paths { get; } = new List<string>();

        public float ViewBoxWidth { get; set; } = 24f;

        public float ViewBoxHeight { get; set; } = 24f;

        /// <summary>The first <c>title</c> element's text, or null.</summary>
        public string? Title { get; set; }

        /// <summary>One entry per feature the icon uses that the path data cannot carry.</summary>
        public List<string> Unsupported { get; } = new List<string>();
    }

    // Containers whose children define things rather than draw them.
    private static readonly HashSet<string> NonRenderingContainers = new HashSet<string>(StringComparer.Ordinal)
    {
        "defs", "clipPath", "mask", "symbol", "pattern", "marker",
    };

    /// <summary>Parses <paramref name="svg"/>.</summary>
    public static Result Parse(string svg)
    {
        var result = new Result();
        bool viewBoxSeen = false;
        int hiddenDepth = 0;
        int i = 0;

        while (i < svg.Length)
        {
            int lt = svg.IndexOf('<', i);
            if (lt < 0)
            {
                break;
            }

            if (string.CompareOrdinal(svg, lt, "<!--", 0, 4) == 0)
            {
                int commentEnd = svg.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                i = commentEnd < 0 ? svg.Length : commentEnd + 3;
                continue;
            }

            if (lt + 1 < svg.Length && (svg[lt + 1] == '?' || svg[lt + 1] == '!'))
            {
                int declEnd = svg.IndexOf('>', lt + 1);
                i = declEnd < 0 ? svg.Length : declEnd + 1;
                continue;
            }

            bool closing = lt + 1 < svg.Length && svg[lt + 1] == '/';
            int nameStart = closing ? lt + 2 : lt + 1;
            int nameEnd = nameStart;
            while (nameEnd < svg.Length && IsNameChar(svg[nameEnd]))
            {
                nameEnd++;
            }

            string name = svg.Substring(nameStart, nameEnd - nameStart);
            int tagEnd = FindTagEnd(svg, nameEnd);
            if (tagEnd < 0)
            {
                break;
            }

            i = tagEnd + 1;
            bool selfClosing = svg[tagEnd - 1] == '/';

            if (closing)
            {
                if (hiddenDepth > 0 && NonRenderingContainers.Contains(name))
                {
                    hiddenDepth--;
                }
                continue;
            }

            if (NonRenderingContainers.Contains(name))
            {
                if (!selfClosing)
                {
                    hiddenDepth++;
                }
                continue;
            }

            if (hiddenDepth > 0)
            {
                continue;
            }

            var attributes = ParseAttributes(svg, nameEnd, selfClosing ? tagEnd - 1 : tagEnd);

            if (name == "svg" && !viewBoxSeen)
            {
                viewBoxSeen = true;
                ReadViewBox(attributes, result);
                continue;
            }

            if (name == "title")
            {
                if (result.Title is null && !selfClosing)
                {
                    int textEnd = svg.IndexOf("</title", i, StringComparison.Ordinal);
                    if (textEnd > i)
                    {
                        string title = DecodeEntities(svg.Substring(i, textEnd - i)).Trim();
                        result.Title = title.Length > 0 ? title : null;
                    }
                }
                continue;
            }

            if (attributes.TryGetValue("display", out var display) && display.Trim() == "none")
            {
                continue;
            }

            if (attributes.ContainsKey("transform"))
            {
                result.Unsupported.Add("transform on <" + name + ">");
            }

            if (name == "use")
            {
                result.Unsupported.Add("<use>");
                continue;
            }

            string? path = ShapeToPath(name, attributes, result);
            if (path is not null)
            {
                result.Paths.Add(path);
            }
        }

        return result;
    }

    // ── Shapes ────────────────────────────────────────────────────────

    private static string? ShapeToPath(string name, Dictionary<string, string> a, Result result)
    {
        switch (name)
        {
            case "path":
            {
                if (!a.TryGetValue("d", out var d))
                {
                    return null;
                }
                d = d.Trim();
                return d.Length > 0 ? d : null;
            }
            case "circle":
            {
                double cx = Length(a, "cx", result);
                double cy = Length(a, "cy", result);
                double r = Length(a, "r", result);
                return r > 0 ? EllipsePath(cx, cy, r, r) : null;
            }
            case "ellipse":
            {
                double cx = Length(a, "cx", result);
                double cy = Length(a, "cy", result);
                double? rx = OptionalLength(a, "rx", result);
                double? ry = OptionalLength(a, "ry", result);
                // SVG 2: an absent radius takes the other one ("auto").
                double rxv = rx ?? ry ?? 0;
                double ryv = ry ?? rx ?? 0;
                return rxv > 0 && ryv > 0 ? EllipsePath(cx, cy, rxv, ryv) : null;
            }
            case "rect":
                return RectPath(a, result);
            case "line":
            {
                double x1 = Length(a, "x1", result);
                double y1 = Length(a, "y1", result);
                double x2 = Length(a, "x2", result);
                double y2 = Length(a, "y2", result);
                return "M" + N(x1) + " " + N(y1) + "L" + N(x2) + " " + N(y2);
            }
            case "polyline":
            case "polygon":
            {
                if (!a.TryGetValue("points", out var points))
                {
                    return null;
                }
                var numbers = ParseNumbers(points);
                // An odd trailing coordinate is an error in SVG; the points before it still draw.
                int pairs = numbers.Count / 2;
                if (pairs < 2)
                {
                    return null;
                }
                var sb = new StringBuilder();
                sb.Append('M').Append(N(numbers[0])).Append(' ').Append(N(numbers[1]));
                for (int p = 1; p < pairs; p++)
                {
                    sb.Append('L').Append(N(numbers[2 * p])).Append(' ').Append(N(numbers[(2 * p) + 1]));
                }
                if (name == "polygon")
                {
                    sb.Append('Z');
                }
                return sb.ToString();
            }
            default:
                return null;
        }
    }

    private static string EllipsePath(double cx, double cy, double rx, double ry)
    {
        // Two half-ellipse arcs: a single arc whose end equals its start draws nothing.
        string radii = N(rx) + " " + N(ry);
        return "M" + N(cx - rx) + " " + N(cy)
            + "A" + radii + " 0 1 0 " + N(cx + rx) + " " + N(cy)
            + "A" + radii + " 0 1 0 " + N(cx - rx) + " " + N(cy)
            + "Z";
    }

    private static string? RectPath(Dictionary<string, string> a, Result result)
    {
        double x = Length(a, "x", result);
        double y = Length(a, "y", result);
        double w = Length(a, "width", result);
        double h = Length(a, "height", result);
        if (w <= 0 || h <= 0)
        {
            return null;
        }

        double? rxIn = OptionalLength(a, "rx", result);
        double? ryIn = OptionalLength(a, "ry", result);
        double rx = rxIn ?? ryIn ?? 0;
        double ry = ryIn ?? rxIn ?? 0;
        rx = Math.Min(Math.Max(rx, 0), w / 2);
        ry = Math.Min(Math.Max(ry, 0), h / 2);

        if (rx <= 0 || ry <= 0)
        {
            return "M" + N(x) + " " + N(y) + "H" + N(x + w) + "V" + N(y + h) + "H" + N(x) + "Z";
        }

        string corner = "A" + N(rx) + " " + N(ry) + " 0 0 1 ";
        return "M" + N(x + rx) + " " + N(y)
            + "H" + N(x + w - rx)
            + corner + N(x + w) + " " + N(y + ry)
            + "V" + N(y + h - ry)
            + corner + N(x + w - rx) + " " + N(y + h)
            + "H" + N(x + rx)
            + corner + N(x) + " " + N(y + h - ry)
            + "V" + N(y + ry)
            + corner + N(x + rx) + " " + N(y)
            + "Z";
    }

    // ── View box ──────────────────────────────────────────────────────

    private static void ReadViewBox(Dictionary<string, string> a, Result result)
    {
        if (a.TryGetValue("viewBox", out var viewBox))
        {
            var parts = ParseNumbers(viewBox);
            if (parts.Count >= 4 && parts[2] > 0 && parts[3] > 0)
            {
                result.ViewBoxWidth = (float)parts[2];
                result.ViewBoxHeight = (float)parts[3];
                return;
            }
        }

        // No usable viewBox: user units are the width and height, when they are plain numbers.
        double? width = OptionalLength(a, "width", null);
        double? height = OptionalLength(a, "height", null);
        if (width > 0 && height > 0)
        {
            result.ViewBoxWidth = (float)width!.Value;
            result.ViewBoxHeight = (float)height!.Value;
        }
    }

    // ── Attributes ────────────────────────────────────────────────────

    private static double Length(Dictionary<string, string> a, string name, Result result)
    {
        return OptionalLength(a, name, result) ?? 0;
    }

    private static double? OptionalLength(Dictionary<string, string> a, string name, Result? result)
    {
        if (!a.TryGetValue(name, out var raw))
        {
            return null;
        }

        string value = raw.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal))
        {
            result?.Unsupported.Add("percentage " + name + "=\"" + value + "\"");
            return null;
        }

        if (value.EndsWith("px", StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - 2);
        }

        if (value == "auto")
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        result?.Unsupported.Add("length " + name + "=\"" + value + "\"");
        return null;
    }

    private static Dictionary<string, string> ParseAttributes(string svg, int start, int end)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        int i = start;
        while (i < end)
        {
            while (i < end && char.IsWhiteSpace(svg[i]))
            {
                i++;
            }

            int nameStart = i;
            while (i < end && IsNameChar(svg[i]))
            {
                i++;
            }

            if (i == nameStart)
            {
                i++;
                continue;
            }

            string name = svg.Substring(nameStart, i - nameStart);
            while (i < end && char.IsWhiteSpace(svg[i]))
            {
                i++;
            }

            if (i >= end || svg[i] != '=')
            {
                continue; // a bare attribute name carries no value
            }

            i++;
            while (i < end && char.IsWhiteSpace(svg[i]))
            {
                i++;
            }

            if (i >= end || (svg[i] != '"' && svg[i] != '\''))
            {
                continue;
            }

            char quote = svg[i];
            int valueStart = i + 1;
            int valueEnd = svg.IndexOf(quote, valueStart);
            if (valueEnd < 0 || valueEnd > end)
            {
                break;
            }

            attributes[name] = DecodeEntities(svg.Substring(valueStart, valueEnd - valueStart));
            i = valueEnd + 1;
        }
        return attributes;
    }

    // The '>' that ends the tag starting before 'from', skipping any inside quoted values.
    private static int FindTagEnd(string svg, int from)
    {
        char quote = '\0';
        for (int i = from; i < svg.Length; i++)
        {
            char c = svg[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsNameChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == ':' || c == '-' || c == '_' || c == '.';
    }

    private static string DecodeEntities(string value)
    {
        if (value.IndexOf('&') < 0)
        {
            return value;
        }

        var sb = new StringBuilder(value.Length);
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            int semi = c == '&' ? value.IndexOf(';', i + 1) : -1;
            if (semi < 0)
            {
                sb.Append(c);
                i++;
                continue;
            }

            string entity = value.Substring(i + 1, semi - i - 1);
            string? decoded = DecodeEntity(entity);
            if (decoded is null)
            {
                sb.Append(c);
                i++;
                continue;
            }

            sb.Append(decoded);
            i = semi + 1;
        }
        return sb.ToString();
    }

    private static string? DecodeEntity(string entity)
    {
        switch (entity)
        {
            case "amp": return "&";
            case "lt": return "<";
            case "gt": return ">";
            case "quot": return "\"";
            case "apos": return "'";
        }

        if (entity.Length > 1 && entity[0] == '#')
        {
            bool hex = entity[1] == 'x' || entity[1] == 'X';
            string digits = entity.Substring(hex ? 2 : 1);
            var style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
            if (int.TryParse(digits, style, CultureInfo.InvariantCulture, out int code)
                && code >= 0 && code <= 0x10FFFF)
            {
                return char.ConvertFromUtf32(code);
            }
        }
        return null;
    }

    // ── Numbers ───────────────────────────────────────────────────────

    // SVG number lists: separated by whitespace and/or commas, or by nothing before a sign
    // ("1-2") or a second decimal point ("0.5.5" is 0.5 and .5).
    internal static List<double> ParseNumbers(string text)
    {
        var numbers = new List<double>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            int start = i;
            if (c == '+' || c == '-')
            {
                i++;
            }

            bool sawDot = false;
            bool sawDigit = false;
            while (i < text.Length)
            {
                char d = text[i];
                if (d >= '0' && d <= '9')
                {
                    sawDigit = true;
                    i++;
                }
                else if (d == '.' && !sawDot)
                {
                    sawDot = true;
                    i++;
                }
                else
                {
                    break;
                }
            }

            if (sawDigit && i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                int expStart = i;
                i++;
                if (i < text.Length && (text[i] == '+' || text[i] == '-'))
                {
                    i++;
                }

                int expDigits = i;
                while (i < text.Length && text[i] >= '0' && text[i] <= '9')
                {
                    i++;
                }

                if (i == expDigits)
                {
                    i = expStart; // 'e' with no exponent digits is not part of the number
                }
            }

            if (!sawDigit)
            {
                i = start + 1; // not a number: skip the character
                continue;
            }

            numbers.Add(double.Parse(text.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture));
        }
        return numbers;
    }

    // Shortest invariant form; rounds away float noise from arithmetic on the attribute values.
    private static string N(double value)
    {
        double rounded = Math.Round(value, 4);
        if (rounded == 0)
        {
            return "0";
        }
        return rounded.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
