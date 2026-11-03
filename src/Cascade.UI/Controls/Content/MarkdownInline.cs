using System.Text;

namespace Cascade.UI;

/// <summary>Inline styles a Markdown span can carry (several at once).</summary>
[Flags]
internal enum MarkdownSpanStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Code = 4,
    Strike = 8,
    Underline = 16,
}

/// <summary>
/// One run of inline Markdown: text in one style, optionally a link, or an image (with
/// <see cref="ImageSource"/> set and <see cref="Text"/> its alt text). A span may carry its own
/// colour (code highlighting).
/// </summary>
internal sealed record MarkdownSpan(
    string Text,
    MarkdownSpanStyle Style = MarkdownSpanStyle.None,
    string? Link = null,
    string? ImageSource = null,
    ColorValue? Color = null)
{
    public bool IsImage => ImageSource is not null;
}

/// <summary>
/// Parses inline Markdown (emphasis, strong, strikethrough, code, links, autolinks, images,
/// escapes, hard breaks and, when allowed, a safe subset of inline HTML) into styled spans.
/// </summary>
internal static class MarkdownInlineParser
{
    private const string Escapable = "\\`*_{}[]()#+-.!|~<>\"'";

    internal static List<MarkdownSpan> Parse(string text, bool allowHtml)
    {
        var spans = new List<MarkdownSpan>();
        ParseInto(spans, text, MarkdownSpanStyle.None, null, allowHtml);
        return Merge(spans);
    }

    private static void ParseInto(List<MarkdownSpan> spans, string text, MarkdownSpanStyle style, string? link, bool allowHtml)
    {
        var plain = new StringBuilder();
        void Flush()
        {
            if (plain.Length > 0)
            {
                spans.Add(new MarkdownSpan(plain.ToString(), style, link));
                plain.Clear();
            }
        }

        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            // Backslash escapes, and a backslash at the end of a line is a hard break.
            if (c == '\\' && i + 1 < text.Length)
            {
                if (text[i + 1] == '\n')
                {
                    plain.Append('\n');
                    i += 2;
                    continue;
                }
                if (Escapable.Contains(text[i + 1], StringComparison.Ordinal))
                {
                    plain.Append(text[i + 1]);
                    i += 2;
                    continue;
                }
            }

            // Two or more spaces before a newline: a hard break. A lone newline is a space.
            if (c == '\n')
            {
                bool hard = plain.Length >= 2 && plain[^1] == ' ' && plain[^2] == ' ';
                while (plain.Length > 0 && plain[^1] == ' ')
                {
                    plain.Length--;
                }
                plain.Append(hard ? '\n' : ' ');
                i++;
                continue;
            }

            // Code span: a run of backticks closed by the same run; content is literal.
            if (c == '`')
            {
                int run = CountRun(text, i, '`');
                int close = FindRun(text, i + run, '`', run);
                if (close >= 0)
                {
                    Flush();
                    string code = text[(i + run)..close].Replace('\n', ' ');
                    if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    {
                        code = code[1..^1];
                    }
                    spans.Add(new MarkdownSpan(code, style | MarkdownSpanStyle.Code, link));
                    i = close + run;
                    continue;
                }
                plain.Append(text, i, run);
                i += run;
                continue;
            }

            // Image: ![alt](src "title")
            if (c == '!' && i + 1 < text.Length && text[i + 1] == '[' && TryLink(text, i + 1, out string alt, out string src, out int imageEnd))
            {
                Flush();
                spans.Add(new MarkdownSpan(StripMarkers(alt), style, link, ImageSource: src));
                i = imageEnd;
                continue;
            }

            // Link: [text](url "title")
            if (c == '[' && link is null && TryLink(text, i, out string label, out string url, out int linkEnd))
            {
                Flush();
                ParseInto(spans, label, style, url, allowHtml);
                i = linkEnd;
                continue;
            }

            // Autolink: <https://...> or <mailto:...>
            if (c == '<' && link is null && TryAutolink(text, i, out string auto, out int autoEnd))
            {
                Flush();
                spans.Add(new MarkdownSpan(auto.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? auto[7..] : auto, style, auto));
                i = autoEnd;
                continue;
            }

            // Inline HTML (a safe subset, only when allowed; otherwise shown as typed).
            if (c == '<' && allowHtml && TryHtml(spans, text, i, style, link, out int htmlEnd, Flush))
            {
                i = htmlEnd;
                continue;
            }

            // Emphasis: ** or __ (strong), * or _ (emphasis), ~~ (strikethrough).
            if (c is '*' or '_' or '~')
            {
                int run = CountRun(text, i, c);
                if (c == '~' && run < 2)
                {
                    plain.Append(c);
                    i++;
                    continue;
                }

                // Intraword underscores (snake_case) are not emphasis.
                bool leftFlanking = i + run < text.Length && !char.IsWhiteSpace(text[i + run]);
                bool intraword = c == '_' && i > 0 && char.IsLetterOrDigit(text[i - 1]);
                if (leftFlanking && !intraword)
                {
                    int use = c == '~' ? 2 : Math.Min(run, 3);
                    int close = FindClosingEmphasis(text, i + use, c, use);
                    if (close >= 0)
                    {
                        Flush();
                        var add = c == '~' ? MarkdownSpanStyle.Strike
                            : use == 3 ? MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic
                            : use == 2 ? MarkdownSpanStyle.Bold
                            : MarkdownSpanStyle.Italic;
                        ParseInto(spans, text[(i + use)..close], style | add, link, allowHtml);
                        i = close + use;
                        continue;
                    }
                }
                plain.Append(text, i, run);
                i += run;
                continue;
            }

            plain.Append(c);
            i++;
        }

        Flush();
    }

    private static int CountRun(string text, int start, char c)
    {
        int n = 0;
        while (start + n < text.Length && text[start + n] == c)
        {
            n++;
        }
        return n;
    }

    // The next run of exactly `length` copies of c at or after `from`.
    private static int FindRun(string text, int from, char c, int length)
    {
        int i = from;
        while (i < text.Length)
        {
            if (text[i] == c)
            {
                int run = CountRun(text, i, c);
                if (run == length)
                {
                    return i;
                }
                i += run;
                continue;
            }
            i++;
        }
        return -1;
    }

    // A closing delimiter: `length` copies of c, not preceded by whitespace, skipping code spans.
    private static int FindClosingEmphasis(string text, int from, char c, int length)
    {
        int i = from;
        while (i < text.Length)
        {
            char ch = text[i];
            if (ch == '\\')
            {
                i += 2;
                continue;
            }
            if (ch == '`')
            {
                int run = CountRun(text, i, '`');
                int close = FindRun(text, i + run, '`', run);
                i = close >= 0 ? close + run : i + run;
                continue;
            }
            if (ch == c)
            {
                int run = CountRun(text, i, c);
                bool rightFlanking = i > from && !char.IsWhiteSpace(text[i - 1]);
                bool intraword = c == '_' && i + run < text.Length && char.IsLetterOrDigit(text[i + run]);
                if (rightFlanking && !intraword && run >= length)
                {
                    // Of a longer run, the closing delimiter is its last `length` characters
                    // (so ***a*** closes bold and italic together).
                    return i + run - length;
                }
                i += run;
                continue;
            }
            i++;
        }
        return -1;
    }

    // [label](destination "optional title"), with nested brackets in the label.
    private static bool TryLink(string text, int open, out string label, out string destination, out int end)
    {
        label = destination = string.Empty;
        end = open;
        int depth = 0;
        int close = -1;
        for (int i = open; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\\')
            {
                i++;
                continue;
            }
            if (ch == '`')
            {
                int run = CountRun(text, i, '`');
                int codeClose = FindRun(text, i + run, '`', run);
                if (codeClose >= 0)
                {
                    i = codeClose + run - 1;
                    continue;
                }
            }
            if (ch == '[')
            {
                depth++;
            }
            else if (ch == ']' && --depth == 0)
            {
                close = i;
                break;
            }
        }
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return false;
        }

        int parenDepth = 0;
        int destEnd = -1;
        for (int i = close + 2; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\\')
            {
                i++;
                continue;
            }
            if (ch == '(')
            {
                parenDepth++;
            }
            else if (ch == ')')
            {
                if (parenDepth == 0)
                {
                    destEnd = i;
                    break;
                }
                parenDepth--;
            }
        }
        if (destEnd < 0)
        {
            return false;
        }

        string inside = text[(close + 2)..destEnd].Trim();
        if (inside.StartsWith('<') && inside.IndexOf('>', StringComparison.Ordinal) is int angle and > 0)
        {
            destination = inside[1..angle];
        }
        else
        {
            int space = inside.IndexOfAny([' ', '\t', '\n']);
            destination = space >= 0 ? inside[..space] : inside;
        }
        label = text[(open + 1)..close];
        end = destEnd + 1;
        return true;
    }

    private static bool TryAutolink(string text, int open, out string url, out int end)
    {
        url = string.Empty;
        end = open;
        int close = text.IndexOf('>', open + 1);
        if (close < 0)
        {
            return false;
        }
        string inside = text[(open + 1)..close];
        if (inside.Length == 0 || inside.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }
        if (inside.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || inside.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || inside.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            url = inside;
        }
        else if (inside.Contains('@', StringComparison.Ordinal) && inside.Contains('.', StringComparison.Ordinal))
        {
            url = "mailto:" + inside;
        }
        else
        {
            return false;
        }
        end = close + 1;
        return true;
    }

    // The inline HTML Markdown renderers commonly allow: <br>, <b>/<strong>, <i>/<em>, <u>, <s>/<del>,
    // <code>, <kbd>, <a href> and <img src alt>. Anything else is shown as typed.
    private static bool TryHtml(List<MarkdownSpan> spans, string text, int open, MarkdownSpanStyle style, string? link, out int end, Action flush)
    {
        end = open;
        int close = text.IndexOf('>', open + 1);
        if (close < 0)
        {
            return false;
        }
        string tag = text[(open + 1)..close].Trim();
        if (tag.EndsWith('/'))
        {
            tag = tag[..^1].TrimEnd();
        }
        string name = tag.Split(' ', 2)[0].ToUpperInvariant();

        if (name is "BR")
        {
            flush();
            spans.Add(new MarkdownSpan("\n", style, link));
            end = close + 1;
            return true;
        }
        if (name is "IMG")
        {
            string? src = Attribute(tag, "src");
            if (src is null)
            {
                return false;
            }
            flush();
            spans.Add(new MarkdownSpan(Attribute(tag, "alt") ?? string.Empty, style, link, ImageSource: src));
            end = close + 1;
            return true;
        }

        MarkdownSpanStyle? add = name switch
        {
            "B" or "STRONG" => MarkdownSpanStyle.Bold,
            "I" or "EM" => MarkdownSpanStyle.Italic,
            "U" or "INS" => MarkdownSpanStyle.Underline,
            "S" or "DEL" or "STRIKE" => MarkdownSpanStyle.Strike,
            "CODE" or "KBD" or "SAMP" => MarkdownSpanStyle.Code,
            "A" => MarkdownSpanStyle.None,
            _ => null,
        };
        if (add is null)
        {
            return false;
        }
        int endTag = text.IndexOf("</" + name, close + 1, StringComparison.OrdinalIgnoreCase);
        if (endTag < 0)
        {
            return false;
        }
        int endTagClose = text.IndexOf('>', endTag);
        if (endTagClose < 0)
        {
            return false;
        }
        flush();
        string inner = text[(close + 1)..endTag];
        string? href = name == "A" ? Attribute(tag, "href") : null;
        if (add.Value == MarkdownSpanStyle.Code)
        {
            spans.Add(new MarkdownSpan(System.Net.WebUtility.HtmlDecode(inner), style | MarkdownSpanStyle.Code, link));
        }
        else
        {
            ParseInto(spans, inner, style | add.Value, href ?? link, allowHtml: true);
        }
        end = endTagClose + 1;
        return true;
    }

    private static string? Attribute(string tag, string name)
    {
        int at = tag.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }
        int start = at + name.Length + 1;
        if (start >= tag.Length)
        {
            return null;
        }
        char quote = tag[start];
        if (quote is '"' or '\'')
        {
            int stop = tag.IndexOf(quote, start + 1);
            return stop < 0 ? null : System.Net.WebUtility.HtmlDecode(tag[(start + 1)..stop]);
        }
        int space = tag.IndexOf(' ', start);
        return tag[start..(space < 0 ? tag.Length : space)];
    }

    /// <summary>The text of inline Markdown with its markers removed (alt text, heading titles).</summary>
    internal static string StripMarkers(string text)
    {
        var sb = new StringBuilder();
        foreach (var span in Parse(text, allowHtml: false))
        {
            if (!span.IsImage)
            {
                sb.Append(span.Text);
            }
        }
        return sb.ToString();
    }

    // Neighbouring spans in the same style and link become one.
    private static List<MarkdownSpan> Merge(List<MarkdownSpan> spans)
    {
        var merged = new List<MarkdownSpan>(spans.Count);
        foreach (var span in spans)
        {
            if (merged.Count > 0 && merged[^1] is { IsImage: false } last && !span.IsImage
                && last.Style == span.Style && last.Link == span.Link && last.Color == span.Color)
            {
                merged[^1] = last with { Text = last.Text + span.Text };
            }
            else if (span.Text.Length > 0 || span.IsImage)
            {
                merged.Add(span);
            }
        }
        return merged;
    }
}
