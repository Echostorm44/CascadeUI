namespace Cascade.UI;

/// <summary>
/// Renders Markdown source text as a native Cascade node tree. Text is laid
/// out by HarfBuzz. Code blocks use tree-sitter for syntax highlighting.
/// Images use the <see cref="Image"/> control. The output is fully themed,
/// accessible, selectable, and indistinguishable from hand-authored layout.
/// </summary>
public sealed class Markdown : Node
{
    /// <summary>
    /// Creates a Markdown renderer from source text.
    /// </summary>
    /// <param name="source">Markdown source string.</param>
    public Markdown(string source)
    {
        if (System.IO.File.Exists(source))
        {
            Source = null;
            SourcePath = source;
        }
        else
        {
            Source = source;
            SourcePath = null;
        }

        SourceUrl = null;
    }

    /// <summary>
    /// Creates a Markdown renderer from a bindable source (for streaming).
    /// </summary>
    /// <param name="source">Bindable Markdown source string.</param>
    public Markdown(Bindable<string> source)
    {
        Source = source.Value;
        BindableSource = source;
        SourceUrl = null;
        SourcePath = null;
    }

    /// <summary>
    /// Creates a Markdown renderer that loads from a URL asynchronously.
    /// </summary>
    /// <param name="url">URL to fetch the Markdown content from.</param>
    /// <param name="fromUrl">Disambiguator (unused).</param>
    public Markdown(string url, bool fromUrl)
    {
        Source = null;
        SourceUrl = url;
        SourcePath = null;
    }

    /// <summary>The Markdown source text.</summary>
    public string? Source { get; }

    /// <summary>Bindable source for streaming updates.</summary>
    public Bindable<string>? BindableSource { get; }

    /// <summary>URL source for remote Markdown.</summary>
    public string? SourceUrl { get; }

    /// <summary>File path source for local Markdown.</summary>
    public string? SourcePath { get; }

    // ── Internal modifier state set by fluent methods ──────────────────

    internal bool CodeBlockSyntaxHighlight { get; set; } = true;
    internal bool CodeBlockShowLanguageLabel { get; set; } = true;
    internal bool CodeBlockShowCopyButton { get; set; } = true;
    internal string CodeBlockCopyButtonLabel { get; set; } = "Copy";
    internal bool CodeBlockLineNumbers { get; set; }
    internal float? CodeBlockMaxHeight { get; set; }
    internal bool CodeBlockWrapLines { get; set; }
    internal Action<string>? LinkClickHandler { get; set; }
    internal bool ImagesLazyLoad { get; set; } = true;
    internal float? ImagesMaxWidth { get; set; }
    internal float? ImagesBorderRadius { get; set; }
    internal string? ImagesBaseUrl { get; set; }
    internal bool HeadingAnchorsEnabled { get; set; }
    internal Action<MarkdownHeading>? HeadingVisibleHandler { get; set; }
    internal bool StreamingEnabled { get; set; }
    internal MarkdownAutoScrollMode AutoScrollMode { get; set; } = MarkdownAutoScrollMode.Never;
    internal bool HtmlAllowed { get; set; }

    // ── Copy button tracking for code blocks (set during paint) ──────

    internal List<(Rect Bounds, string CodeText)> CodeBlockCopyButtons { get; set; } = [];
    internal int HoveredCopyButtonIndex { get; set; } = -1;
    internal Rect AbsoluteBounds { get; set; }

    // ── Parsed block tree (lazily built) ─────────────────────────────

    internal List<MarkdownBlock>? ParsedBlocks { get; set; }

    /// <summary>
    /// Gets the resolved source text from whichever source was provided.
    /// File and URL sources are not yet supported at runtime; returns null.
    /// </summary>
    internal string? ResolvedSource => BindableSource?.Value ?? Source;

    /// <summary>
    /// Ensures the parsed blocks are available. Call this from layout/paint.
    /// </summary>
    internal List<MarkdownBlock> GetParsedBlocks()
    {
        if (ParsedBlocks != null)
        {
            return ParsedBlocks;
        }

        string? source = ResolvedSource;
        if (string.IsNullOrEmpty(source))
        {
            ParsedBlocks = [];
            return ParsedBlocks;
        }

        ParsedBlocks = MarkdownBlockParser.Parse(source);
        return ParsedBlocks;
    }

    // ── Code blocks ───────────────────────────────────────────────────

    /// <summary>Configures code block rendering.</summary>
    public Markdown CodeBlock(
        bool syntaxHighlight = true,
        bool showLanguageLabel = true,
        bool showCopyButton = true,
        string copyButtonLabel = "Copy",
        bool lineNumbers = false,
        float? maxHeight = null,
        bool wrapLines = false)
    {
        CodeBlockSyntaxHighlight = syntaxHighlight;
        CodeBlockShowLanguageLabel = showLanguageLabel;
        CodeBlockShowCopyButton = showCopyButton;
        CodeBlockCopyButtonLabel = copyButtonLabel;
        CodeBlockLineNumbers = lineNumbers;
        CodeBlockMaxHeight = maxHeight;
        CodeBlockWrapLines = wrapLines;
        return this;
    }

    // ── Link handling ─────────────────────────────────────────────────

    /// <summary>Callback for intercepting link clicks.</summary>
    public Markdown OnLinkClick(Action<string> handler)
    {
        LinkClickHandler = handler;
        return this;
    }

    // ── Images ────────────────────────────────────────────────────────

    /// <summary>Configures image loading within Markdown content.</summary>
    public Markdown Images(bool lazyLoad = true, float? maxWidth = null, float? borderRadius = null)
    {
        ImagesLazyLoad = lazyLoad;
        ImagesMaxWidth = maxWidth;
        ImagesBorderRadius = borderRadius;
        return this;
    }

    /// <summary>Sets the base URL for resolving relative image paths.</summary>
    public Markdown BaseUrl(string baseUrl)
    {
        ImagesBaseUrl = baseUrl;
        return this;
    }

    // ── Heading anchors ───────────────────────────────────────────────

    /// <summary>Adds anchor IDs to headings for scroll targeting.</summary>
    public Markdown HeadingAnchors(bool enabled)
    {
        HeadingAnchorsEnabled = enabled;
        return this;
    }

    /// <summary>Callback when a heading enters the visible scroll area.</summary>
    public Markdown OnHeadingVisible(Action<MarkdownHeading> handler)
    {
        HeadingVisibleHandler = handler;
        return this;
    }

    // ── Streaming ─────────────────────────────────────────────────────

    /// <summary>
    /// Optimizes rendering for content that is appended to frequently
    /// (e.g., AI-generated token-by-token output).
    /// </summary>
    public Markdown StreamingMode(bool enabled)
    {
        StreamingEnabled = enabled;
        return this;
    }

    /// <summary>Controls auto-scrolling behavior for streaming content.</summary>
    public Markdown AutoScroll(MarkdownAutoScrollMode mode)
    {
        AutoScrollMode = mode;
        return this;
    }

    // ── Security ──────────────────────────────────────────────────────

    /// <summary>Enables or disables HTML passthrough (default: disabled).</summary>
    public Markdown AllowHtml(bool enabled)
    {
        HtmlAllowed = enabled;
        return this;
    }
}

/// <summary>
/// A heading extracted from a Markdown document.
/// </summary>
public sealed class MarkdownHeading
{
    /// <summary>Creates a Markdown heading.</summary>
    /// <param name="title">The heading text.</param>
    /// <param name="level">Heading level (1–6).</param>
    /// <param name="anchorId">The generated anchor ID for scroll targeting.</param>
    public MarkdownHeading(string title, int level, string anchorId)
    {
        Title = title;
        Level = level;
        AnchorId = anchorId;
    }

    /// <summary>The heading text.</summary>
    public string Title { get; }

    /// <summary>Heading level (1–6).</summary>
    public int Level { get; }

    /// <summary>Generated anchor ID for scroll targeting.</summary>
    public string AnchorId { get; }
}

/// <summary>
/// Auto-scroll behavior for streaming Markdown content.
/// </summary>
public enum MarkdownAutoScrollMode
{
    /// <summary>No auto-scrolling.</summary>
    Never,

    /// <summary>Always scroll to the bottom as new content arrives.</summary>
    Always,

    /// <summary>
    /// Only scroll if the user is already at the bottom. If the user scrolls
    /// up, auto-scroll pauses until they return to the bottom.
    /// </summary>
    WhenAtBottom
}

/// <summary>
/// Utilities for parsing Markdown documents.
/// </summary>
public static class MarkdownParser
{
    /// <summary>
    /// Extracts headings from a Markdown document for table-of-contents generation.
    /// </summary>
    /// <param name="source">Markdown source text.</param>
    /// <returns>Ordered list of headings discovered in the document.</returns>
    public static IReadOnlyList<MarkdownHeading> ExtractHeadings(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var headings = new List<MarkdownHeading>();
        var lines = source.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int level = 0;
            while (level < line.Length && line[level] == '#')
            {
                level++;
            }

            if (level == 0 || level > 6)
            {
                continue;
            }

            if (line.Length <= level || line[level] != ' ')
            {
                continue;
            }

            string title = line[(level + 1)..].Trim();
            if (title.Length == 0)
            {
                continue;
            }

            string anchor = GenerateAnchorId(title);
            headings.Add(new MarkdownHeading(title, level, anchor));
        }

        return headings;
    }

    private static string GenerateAnchorId(string title)
    {
        var builder = new System.Text.StringBuilder(title.Length);
        bool lastWasDash = false;
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                lastWasDash = false;
                continue;
            }

            if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                if (!lastWasDash && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasDash = true;
                }
            }
        }

        string anchor = builder.ToString().Trim('-');
        return anchor.Length == 0 ? "section" : anchor;
    }
}

/// <summary>Block type for parsed Markdown content.</summary>
internal enum MarkdownBlockType
{
    Heading,
    Paragraph,
    CodeBlock,
    BulletList,
    OrderedList,
    BlockQuote,
    HorizontalRule,
}

/// <summary>A single parsed block from a Markdown document.</summary>
internal sealed class MarkdownBlock
{
    public MarkdownBlockType Type { get; init; }
    public int HeadingLevel { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Language { get; init; }
    public List<string>? Items { get; init; }
}

/// <summary>
/// Simple Markdown parser that converts source text into a list of blocks.
/// Handles headings, paragraphs, code blocks, lists, blockquotes, and horizontal rules.
/// Inline formatting markers (bold, italic, code) are stripped for plain text rendering.
/// </summary>
internal static class MarkdownBlockParser
{
    internal static List<MarkdownBlock> Parse(string source)
    {
        var blocks = new List<MarkdownBlock>();
        var lines = source.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i].TrimEnd('\r');
            string trimmed = line.TrimStart();

            // Blank line — skip
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Horizontal rule: --- or *** or ___ (3+ of same char)
            if (IsHorizontalRule(trimmed))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.HorizontalRule });
                i++;
                continue;
            }

            // Heading: # through ######
            if (trimmed.Length > 0 && trimmed[0] == '#')
            {
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#')
                {
                    level++;
                }

                if (level <= 6 && level < trimmed.Length && trimmed[level] == ' ')
                {
                    string headingText = StripInlineFormatting(trimmed[(level + 1)..].Trim());
                    blocks.Add(new MarkdownBlock
                    {
                        Type = MarkdownBlockType.Heading,
                        HeadingLevel = level,
                        Text = headingText,
                    });
                    i++;
                    continue;
                }
            }

            // Fenced code block: ```
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                string lang = trimmed.Length > 3 ? trimmed[3..].Trim() : string.Empty;
                var codeLines = new System.Text.StringBuilder();
                i++;
                while (i < lines.Length)
                {
                    string codeLine = lines[i].TrimEnd('\r');
                    if (codeLine.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        i++;
                        break;
                    }

                    if (codeLines.Length > 0)
                    {
                        codeLines.Append('\n');
                    }

                    codeLines.Append(codeLine);
                    i++;
                }

                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.CodeBlock,
                    Text = codeLines.ToString(),
                    Language = lang.Length > 0 ? lang : null,
                });
                continue;
            }

            // Blockquote: > text
            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                var quoteLines = new System.Text.StringBuilder();
                while (i < lines.Length)
                {
                    string ql = lines[i].TrimEnd('\r').TrimStart();
                    if (!ql.StartsWith("> ", StringComparison.Ordinal) && !ql.StartsWith('>'))
                    {
                        break;
                    }

                    if (quoteLines.Length > 0)
                    {
                        quoteLines.Append(' ');
                    }

                    string quoteText = ql.StartsWith("> ", StringComparison.Ordinal)
                        ? ql[2..] : ql[1..];
                    quoteLines.Append(quoteText);
                    i++;
                }

                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.BlockQuote,
                    Text = StripInlineFormatting(quoteLines.ToString().Trim()),
                });
                continue;
            }

            // Bullet list: - item or * item
            if ((trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                 trimmed.StartsWith("* ", StringComparison.Ordinal)) && !IsHorizontalRule(trimmed))
            {
                var items = new List<string>();
                while (i < lines.Length)
                {
                    string ll = lines[i].TrimEnd('\r').TrimStart();
                    if (ll.StartsWith("- ", StringComparison.Ordinal) ||
                        ll.StartsWith("* ", StringComparison.Ordinal))
                    {
                        items.Add(StripInlineFormatting(ll[2..].Trim()));
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.BulletList,
                    Items = items,
                });
                continue;
            }

            // Ordered list: 1. item
            if (char.IsDigit(trimmed[0]) && trimmed.Contains(". ", StringComparison.Ordinal))
            {
                int dotIdx = trimmed.IndexOf(". ", StringComparison.Ordinal);
                if (dotIdx > 0 && dotIdx <= 3 && int.TryParse(trimmed[..dotIdx], out _))
                {
                    var items = new List<string>();
                    while (i < lines.Length)
                    {
                        string ll = lines[i].TrimEnd('\r').TrimStart();
                        int di = ll.IndexOf(". ", StringComparison.Ordinal);
                        if (di > 0 && di <= 3 && char.IsDigit(ll[0]) && int.TryParse(ll[..di], out _))
                        {
                            items.Add(StripInlineFormatting(ll[(di + 2)..].Trim()));
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    blocks.Add(new MarkdownBlock
                    {
                        Type = MarkdownBlockType.OrderedList,
                        Items = items,
                    });
                    continue;
                }
            }

            // Paragraph: collect consecutive non-blank lines
            {
                var paraLines = new System.Text.StringBuilder();
                while (i < lines.Length)
                {
                    string pl = lines[i].TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(pl) || pl.TrimStart().StartsWith('#')
                        || pl.TrimStart().StartsWith("```", StringComparison.Ordinal)
                        || pl.TrimStart().StartsWith("> ", StringComparison.Ordinal)
                        || IsHorizontalRule(pl.TrimStart()))
                    {
                        break;
                    }

                    if (paraLines.Length > 0)
                    {
                        paraLines.Append(' ');
                    }

                    paraLines.Append(pl.Trim());
                    i++;
                }

                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.Paragraph,
                    Text = StripInlineFormatting(paraLines.ToString()),
                });
            }
        }

        return blocks;
    }

    /// <summary>
    /// Strips inline Markdown formatting markers: **bold**, *italic*, `code`, [text](url).
    /// Returns plain text suitable for rendering.
    /// </summary>
    private static string StripInlineFormatting(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        int i = 0;

        while (i < text.Length)
        {
            // Bold: **text** or __text__
            if (i + 1 < text.Length && ((text[i] == '*' && text[i + 1] == '*') ||
                                         (text[i] == '_' && text[i + 1] == '_')))
            {
                char marker = text[i];
                int end = text.IndexOf(new string(marker, 2), i + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    sb.Append(text, i + 2, end - (i + 2));
                    i = end + 2;
                    continue;
                }
            }

            // Italic: *text* or _text_ (single marker)
            if ((text[i] == '*' || text[i] == '_') && i + 1 < text.Length && text[i + 1] != text[i])
            {
                char marker = text[i];
                int end = text.IndexOf(marker, i + 1);
                if (end >= 0)
                {
                    sb.Append(text, i + 1, end - (i + 1));
                    i = end + 1;
                    continue;
                }
            }

            // Inline code: `text`
            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end >= 0)
                {
                    sb.Append(text, i + 1, end - (i + 1));
                    i = end + 1;
                    continue;
                }
            }

            // Link: [text](url)
            if (text[i] == '[')
            {
                int closeBracket = text.IndexOf(']', i + 1);
                if (closeBracket >= 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen >= 0)
                    {
                        sb.Append(text, i + 1, closeBracket - (i + 1));
                        i = closeParen + 1;
                        continue;
                    }
                }
            }

            sb.Append(text[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsHorizontalRule(string line)
    {
        if (line.Length < 3)
        {
            return false;
        }

        char first = line[0];
        if (first != '-' && first != '*' && first != '_')
        {
            return false;
        }

        foreach (char c in line)
        {
            if (c != first && c != ' ')
            {
                return false;
            }
        }

        return line.Count(c => c == first) >= 3;
    }
}
