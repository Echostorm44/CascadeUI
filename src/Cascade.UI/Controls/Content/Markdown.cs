namespace Cascade.UI;

/// <summary>
/// Renders Markdown as a native Cascade node tree: headings, paragraphs with bold, italic,
/// strikethrough, code and links, images (the <see cref="Image"/> control), fenced and indented
/// code with syntax colouring, nested and task lists, block quotes, tables and rules. Inline HTML
/// is shown as typed unless <see cref="AllowHtml"/> enables a safe subset.
/// </summary>
public sealed class Markdown : Node, IPumpedNode
{
    /// <summary>
    /// Creates a Markdown renderer from source text, or from a file when
    /// <paramref name="source"/> is the path of one that exists.
    /// </summary>
    /// <param name="source">Markdown source string, or a file path.</param>
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
    /// Creates a Markdown renderer that loads from a URL in the background.
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

    // ── Built state ───────────────────────────────────────────────────

    private string? builtSource;
    private CascadeTheme? builtTheme;
    private Node? tree;
    private List<MarkdownBlock> blocks = [];
    private List<Node> blockNodes = [];
    private readonly List<(MarkdownHeading Heading, Node Node)> headings = [];
    private readonly HashSet<string> visibleHeadings = new(StringComparer.Ordinal);
    private Task<string>? loading;
    private string? loaded;

    /// <summary>The ScrollView this was last laid out in (set by layout), for anchors, auto-scroll and heading visibility.</summary>
    internal ScrollView? EnclosingScrollView { get; set; }

    /// <summary>How many times the tree was built from scratch (streaming appends rebuild only the tail).</summary>
    internal int FullBuilds { get; private set; }

    /// <summary>The source text in use: the bindable's, the given text, or a loaded file or URL.</summary>
    internal string? ResolvedSource => BindableSource?.Value ?? Source ?? loaded;

    /// <summary>The headings of the built document, in order, with their anchor ids.</summary>
    internal IReadOnlyList<(MarkdownHeading Heading, Node Node)> Headings
    {
        get
        {
            _ = Tree;
            return headings;
        }
    }

    /// <summary>The node tree the document is drawn as (built on first use, rebuilt when the source changes).</summary>
    internal Node Tree
    {
        get
        {
            string source = ResolvedSource ?? string.Empty;
            if (tree is null || !string.Equals(source, builtSource, StringComparison.Ordinal) || !ReferenceEquals(builtTheme, ThemeSwitcher.Current))
            {
                Build(source);
            }
            return tree!;
        }
    }

    /// <summary>The parsed blocks (built with the tree).</summary>
    internal List<MarkdownBlock> GetParsedBlocks()
    {
        _ = Tree;
        return blocks;
    }

    private void Build(string source)
    {
        var theme = ThemeSwitcher.Current;

        // Streaming: when the text only grew, the blocks before the last stay as built and only
        // the tail is parsed again (the last block may still be growing).
        if (StreamingEnabled && tree is not null && builtSource is { Length: > 0 } previous
            && ReferenceEquals(builtTheme, theme) && blocks.Count > 0
            && source.StartsWith(previous, StringComparison.Ordinal))
        {
            int keep = blocks.Count - 1;
            int tailStart = blocks[keep].Start;
            var tail = MarkdownBlockParser.Parse(source[tailStart..], tailStart);
            headings.RemoveAll(h => !blockNodes.Take(keep).Any(n => ContainsNode(n, h.Node)));
            var builder = new MarkdownBuilder(this, theme, headings);
            blocks = [.. blocks.Take(keep), .. tail];
            blockNodes = [.. blockNodes.Take(keep), .. tail.Select(b => builder.Block(b))];
        }
        else
        {
            FullBuilds++;
            headings.Clear();
            blocks = MarkdownBlockParser.Parse(source, 0);
            var builder = new MarkdownBuilder(this, theme, headings);
            blockNodes = [.. blocks.Select(b => builder.Block(b))];
        }

        tree = new Column(spacing: 12f, children: [.. blockNodes]);
        builtSource = source;
        builtTheme = theme;
    }

    private static bool ContainsNode(Node root, Node target)
    {
        if (ReferenceEquals(root, target))
        {
            return true;
        }
        var children = NodeDiffer.GetChildren(root);
        for (int i = 0; i < children.Count; i++)
        {
            if (ContainsNode(children[i], target))
            {
                return true;
            }
        }
        return false;
    }

    void IPumpedNode.Pump()
    {
        // A file or URL source loads in the background; the text is taken when it arrives.
        if (loaded is null && loading is null && (SourcePath is not null || SourceUrl is not null))
        {
            var ui = SynchronizationContext.Current;
            loading = SourcePath is { } path ? LoadFileAsync(path, ui) : LoadUrlAsync(SourceUrl!, ui);
        }
        if (loading is { IsCompleted: true } done)
        {
            loaded = done.IsCompletedSuccessfully ? done.Result : $"*Could not load {SourcePath ?? SourceUrl}.*";
            loading = null;
        }

        NoteVisibleHeadings();
    }

    void IPumpedNode.AdoptFrom(Node previous)
    {
        if (previous is not Markdown old || ReferenceEquals(old, this))
        {
            return;
        }

        // The same document re-rendered keeps its tree (and the state of the controls in it).
        if (old.SourcePath == SourcePath && old.SourceUrl == SourceUrl)
        {
            loading = old.loading;
            loaded = old.loaded;
        }
        if (old.tree is not null && SameOptions(old))
        {
            tree = old.tree;
            builtSource = old.builtSource;
            builtTheme = old.builtTheme;
            blocks = old.blocks;
            blockNodes = old.blockNodes;
            headings.AddRange(old.headings);
            FullBuilds = old.FullBuilds;
            foreach (var (_, node) in headings)
            {
                RetargetOwner(node);
            }
            RetargetOwner(tree);
        }
        visibleHeadings.UnionWith(old.visibleHeadings);
    }

    // The built text nodes hand their link clicks to whichever Markdown now owns them.
    private void RetargetOwner(Node node)
    {
        if (node is MarkdownText text)
        {
            text.Owner = this;
        }
        var children = NodeDiffer.GetChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            RetargetOwner(children[i]);
        }
    }

    private bool SameOptions(Markdown other) =>
        other.CodeBlockSyntaxHighlight == CodeBlockSyntaxHighlight && other.CodeBlockShowLanguageLabel == CodeBlockShowLanguageLabel
        && other.CodeBlockShowCopyButton == CodeBlockShowCopyButton && other.CodeBlockCopyButtonLabel == CodeBlockCopyButtonLabel
        && other.CodeBlockLineNumbers == CodeBlockLineNumbers && other.CodeBlockMaxHeight == CodeBlockMaxHeight
        && other.CodeBlockWrapLines == CodeBlockWrapLines && other.ImagesLazyLoad == ImagesLazyLoad
        && other.ImagesMaxWidth == ImagesMaxWidth && other.ImagesBorderRadius == ImagesBorderRadius
        && other.ImagesBaseUrl == ImagesBaseUrl && other.HtmlAllowed == HtmlAllowed && other.StreamingEnabled == StreamingEnabled;

    private static async Task<string> LoadFileAsync(string path, SynchronizationContext? ui)
    {
        try
        {
            return await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        finally
        {
            Wake(ui);
        }
    }

    private static async Task<string> LoadUrlAsync(string url, SynchronizationContext? ui)
    {
        try
        {
            return await Imaging.ImageLoader.SharedHttp.GetStringAsync(new Uri(url)).ConfigureAwait(false);
        }
        finally
        {
            Wake(ui);
        }
    }

    // Frame scheduling is UI-thread affine: a load finishing elsewhere asks for the frame from there.
    private static void Wake(SynchronizationContext? ui)
    {
        if (ui is not null)
        {
            ui.Post(static _ => SharedScheduler.Instance.RequestFrame(), null);
        }
        else
        {
            SharedScheduler.Instance.RequestFrame();
        }
    }

    // ── Headings, anchors and links ─────────────────────────────────

    private void NoteVisibleHeadings()
    {
        if (HeadingVisibleHandler is null || tree is null || headings.Count == 0)
        {
            return;
        }

        float top, bottom;
        if (EnclosingScrollView is { } sv)
        {
            if (!HitTester.TryGetAbsoluteBounds(sv.Content, this, out var self))
            {
                return;
            }
            top = sv.OffsetY - self.Y;
            bottom = top + sv.Control.ViewportSize.Height;
        }
        else
        {
            if (LayoutData.Bounds.Height <= 0f)
            {
                return;
            }
            top = 0f;
            bottom = float.PositiveInfinity;
        }

        foreach (var (heading, node) in headings)
        {
            if (!HitTester.TryGetAbsoluteBounds(tree, node, out var b) || b.Height <= 0f)
            {
                continue;
            }
            bool visible = b.Bottom > top && b.Y < bottom;
            if (visible && visibleHeadings.Add(heading.AnchorId))
            {
                HeadingVisibleHandler(heading);
            }
            else if (!visible)
            {
                visibleHeadings.Remove(heading.AnchorId);
            }
        }
    }

    /// <summary>
    /// Scrolls the enclosing <see cref="ScrollView"/> so the heading with <paramref name="anchorId"/>
    /// is at the top. Returns false when there is no such heading or no ScrollView around this.
    /// </summary>
    public bool ScrollToHeading(string anchorId)
    {
        ArgumentNullException.ThrowIfNull(anchorId);
        string id = anchorId.TrimStart('#');
        if (EnclosingScrollView is not { } sv || tree is null)
        {
            return false;
        }
        foreach (var (heading, node) in headings)
        {
            if (string.Equals(heading.AnchorId, id, StringComparison.OrdinalIgnoreCase)
                && HitTester.TryGetAbsoluteBounds(sv.Content, node, out var b))
            {
                var content = sv.Content.LayoutData;
                _ = sv.Control.ScrollToAsync(sv.OffsetX, b.Y - content.Bounds.Y - content.Padding.Top);
                return true;
            }
        }
        return false;
    }

    /// <summary>A link in the document was clicked.</summary>
    internal void ActivateLink(string link)
    {
        if (link.StartsWith('#') && HeadingAnchorsEnabled && ScrollToHeading(link))
        {
            return;
        }
        if (LinkClickHandler is { } handler)
        {
            handler(link);
            return;
        }
        if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            App.OpenUrl(link);
        }
    }

    /// <summary>Where an image source points: absolute, against the base URL, or beside a file source.</summary>
    internal string ResolveImageSource(string src)
    {
        if (Uri.TryCreate(src, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https" or "file")
        {
            return absolute.IsFile ? absolute.LocalPath : src;
        }
        if (ImagesBaseUrl is { } baseUrl)
        {
            if (Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute, out var root)
                && Uri.TryCreate(root, src, out var combined))
            {
                return combined.IsFile ? combined.LocalPath : combined.ToString();
            }
            return System.IO.Path.Combine(baseUrl, src);
        }
        if (SourcePath is { } path && System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) is { } dir)
        {
            return System.IO.Path.Combine(dir, src);
        }
        if (SourceUrl is { } url && Uri.TryCreate(new Uri(url), src, out var beside))
        {
            return beside.ToString();
        }
        return src;
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

    /// <summary>
    /// Callback for link clicks. Without one, web and mail links open in the system browser or
    /// mail app; with <see cref="HeadingAnchors"/>, links to <c>#heading</c> scroll to it first.
    /// </summary>
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

    /// <summary>Sets the base URL (or folder) for resolving relative image paths.</summary>
    public Markdown BaseUrl(string baseUrl)
    {
        ImagesBaseUrl = baseUrl;
        return this;
    }

    // ── Heading anchors ───────────────────────────────────────────────

    /// <summary>
    /// Gives headings anchor ids (GitHub style: lower case, dashes, numbered when repeated), so
    /// links to <c>#anchor</c> scroll to them and <see cref="ScrollToHeading"/> can find them.
    /// </summary>
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
    /// (e.g., AI-generated token-by-token output): only the last block is parsed and built again.
    /// </summary>
    public Markdown StreamingMode(bool enabled)
    {
        StreamingEnabled = enabled;
        return this;
    }

    /// <summary>Keeps the enclosing ScrollView at the bottom as the content grows.</summary>
    public Markdown AutoScroll(MarkdownAutoScrollMode mode)
    {
        AutoScrollMode = mode;
        return this;
    }

    // ── Security ──────────────────────────────────────────────────────

    /// <summary>
    /// Enables a safe subset of inline HTML (default: disabled, tags show as typed): line breaks,
    /// bold, italic, underline, strikethrough, code, kbd, links and images. Scripts, styles and
    /// other tags are never run or applied.
    /// </summary>
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
    /// Extracts headings from a Markdown document for table-of-contents generation (not lines
    /// inside code blocks). Anchor ids repeat GitHub's: a second "Intro" is "intro-1".
    /// </summary>
    /// <param name="source">Markdown source text.</param>
    /// <returns>Ordered list of headings discovered in the document.</returns>
    public static IReadOnlyList<MarkdownHeading> ExtractHeadings(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var headings = new List<MarkdownHeading>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        void Collect(IEnumerable<MarkdownBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block.Type == MarkdownBlockType.Heading)
                {
                    string title = MarkdownInlineParser.StripMarkers(block.Text).Trim();
                    if (title.Length > 0)
                    {
                        headings.Add(new MarkdownHeading(title, block.HeadingLevel, UniqueAnchor(title, used)));
                    }
                }
                if (block.Children is { } children)
                {
                    Collect(children);
                }
            }
        }
        Collect(MarkdownBlockParser.Parse(source, 0));
        return headings;
    }

    internal static string UniqueAnchor(string title, Dictionary<string, int> used)
    {
        string anchor = GenerateAnchorId(title);
        if (used.TryGetValue(anchor, out int count))
        {
            used[anchor] = count + 1;
            return $"{anchor}-{count}";
        }
        used[anchor] = 1;
        return anchor;
    }

    internal static string GenerateAnchorId(string title)
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
    Table,
}

/// <summary>Column alignment in a Markdown table.</summary>
internal enum MarkdownTableAlign
{
    Default,
    Left,
    Center,
    Right,
}

/// <summary>A list item: its blocks, and its check state when it is a task (- [ ] / - [x]).</summary>
internal sealed class MarkdownListItem
{
    public List<MarkdownBlock> Blocks { get; init; } = [];
    public bool? Task { get; init; }
}

/// <summary>A single parsed block from a Markdown document. <see cref="Text"/> keeps its inline Markdown.</summary>
internal sealed class MarkdownBlock
{
    public MarkdownBlockType Type { get; init; }
    public int HeadingLevel { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Language { get; init; }

    /// <summary>The offset of the block's first line in the whole source.</summary>
    public int Start { get; init; }

    /// <summary>List items (lists).</summary>
    public List<MarkdownListItem>? ListItems { get; init; }

    /// <summary>The number the first item of an ordered list shows.</summary>
    public int StartNumber { get; init; } = 1;

    /// <summary>Nested blocks (block quotes).</summary>
    public List<MarkdownBlock>? Children { get; init; }

    /// <summary>Table rows (the first is the header) and column alignments.</summary>
    public List<string[]>? Rows { get; init; }
    public MarkdownTableAlign[]? Alignments { get; init; }

    /// <summary>The list item texts (the first paragraph of each), for simple inspection.</summary>
    public List<string>? Items => ListItems?.Select(i => i.Blocks.FirstOrDefault()?.Text ?? string.Empty).ToList();
}

/// <summary>
/// Splits Markdown source into blocks (CommonMark's block structure, plus GitHub tables, task
/// lists and strikethrough): ATX and setext headings, fenced (``` and ~~~) and indented code,
/// block quotes holding any blocks, bullet and ordered lists nesting by indentation, rules,
/// tables and paragraphs. An unclosed fence runs to the end (text still streaming in).
/// </summary>
internal static class MarkdownBlockParser
{
    internal static List<MarkdownBlock> Parse(string source, int baseOffset)
    {
        var lines = new List<string>();
        var starts = new List<int>();
        int pos = 0;
        while (pos <= source.Length)
        {
            int nl = source.IndexOf('\n', pos);
            int end = nl < 0 ? source.Length : nl;
            lines.Add(source[pos..end].TrimEnd('\r').Replace("\t", "    ", StringComparison.Ordinal));
            starts.Add(baseOffset + pos);
            if (nl < 0)
            {
                break;
            }
            pos = nl + 1;
        }
        return ParseLines(lines, starts);
    }

    private static List<MarkdownBlock> ParseLines(List<string> lines, List<int> starts)
    {
        var blocks = new List<MarkdownBlock>();
        int i = 0;
        while (i < lines.Count)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            int indent = Indent(line);
            string trimmed = line.TrimStart();
            int start = starts[i];

            // Indented code: four spaces (not continuing a paragraph, which is handled there).
            if (indent >= 4)
            {
                var code = new List<string>();
                while (i < lines.Count && (Indent(lines[i]) >= 4 || string.IsNullOrWhiteSpace(lines[i])))
                {
                    code.Add(lines[i].Length >= 4 ? lines[i][4..] : string.Empty);
                    i++;
                }
                while (code.Count > 0 && string.IsNullOrWhiteSpace(code[^1]))
                {
                    code.RemoveAt(code.Count - 1);
                }
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.CodeBlock, Text = string.Join('\n', code), Start = start });
                continue;
            }

            if (IsFence(trimmed, out char fenceChar, out int fenceLength))
            {
                string info = trimmed[fenceLength..].Trim();
                var code = new List<string>();
                i++;
                while (i < lines.Count)
                {
                    string t = lines[i].TrimStart();
                    if (IsFence(t, out char closeChar, out int closeLength) && closeChar == fenceChar && closeLength >= fenceLength && t[closeLength..].Trim().Length == 0)
                    {
                        i++;
                        break;
                    }
                    code.Add(lines[i].Length >= indent && Indent(lines[i]) >= indent ? lines[i][indent..] : lines[i].TrimStart());
                    i++;
                }
                string language = info.Split(' ', 2)[0];
                blocks.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.CodeBlock,
                    Text = string.Join('\n', code),
                    Language = language.Length > 0 ? language : null,
                    Start = start,
                });
                continue;
            }

            if (IsRule(trimmed))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.HorizontalRule, Start = start });
                i++;
                continue;
            }

            if (AtxHeading(trimmed, out int level, out string headingText))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading, HeadingLevel = level, Text = headingText, Start = start });
                i++;
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                var inner = new List<string>();
                var innerStarts = new List<int>();
                while (i < lines.Count && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    string t = lines[i].TrimStart();
                    if (t.StartsWith('>'))
                    {
                        inner.Add(t.Length > 1 && t[1] == ' ' ? t[2..] : t[1..]);
                    }
                    else if (inner.Count > 0 && !StartsBlock(t))
                    {
                        inner.Add(t); // lazy continuation of the quoted paragraph
                    }
                    else
                    {
                        break;
                    }
                    innerStarts.Add(starts[i]);
                    i++;
                }
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.BlockQuote, Children = ParseLines(inner, innerStarts), Start = start });
                continue;
            }

            if (ListMarker(line, out bool ordered, out int number, out int contentColumn, out char delimiter))
            {
                blocks.Add(ParseList(lines, starts, ref i, ordered, number, delimiter, indent));
                continue;
            }

            if (i + 1 < lines.Count && trimmed.Contains('|', StringComparison.Ordinal) && TableSeparator(lines[i + 1], out var alignments))
            {
                var rows = new List<string[]> { Cells(trimmed, alignments.Length) };
                i += 2;
                while (i < lines.Count && !string.IsNullOrWhiteSpace(lines[i]) && lines[i].Contains('|', StringComparison.Ordinal))
                {
                    rows.Add(Cells(lines[i].Trim(), alignments.Length));
                    i++;
                }
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Table, Rows = rows, Alignments = alignments, Start = start });
                continue;
            }

            // Paragraph, which a setext underline (=== or ---) turns into a heading.
            var para = new List<string>();
            int setext = 0;
            while (i < lines.Count && !string.IsNullOrWhiteSpace(lines[i]))
            {
                string t = lines[i].TrimStart();
                if (para.Count > 0)
                {
                    if (t.Length > 0 && t.All(c => c == '=') )
                    {
                        setext = 1;
                        i++;
                        break;
                    }
                    if (t.Length > 0 && t.All(c => c == '-'))
                    {
                        setext = 2;
                        i++;
                        break;
                    }
                    if (StartsBlock(t) || (ListMarker(lines[i], out bool o, out int n, out _, out _) && (!o || n == 1)))
                    {
                        break;
                    }
                }
                para.Add(lines[i].Trim() + (lines[i].EndsWith("  ", StringComparison.Ordinal) ? "  " : string.Empty));
                i++;
            }
            string text = string.Join('\n', para).TrimEnd();
            blocks.Add(setext > 0
                ? new MarkdownBlock { Type = MarkdownBlockType.Heading, HeadingLevel = setext, Text = text, Start = start }
                : new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Text = text, Start = start });
        }
        return blocks;
    }

    private static MarkdownBlock ParseList(List<string> lines, List<int> starts, ref int i, bool ordered, int firstNumber, char delimiter, int listIndent)
    {
        var items = new List<MarkdownListItem>();
        int start = starts[i];
        while (i < lines.Count)
        {
            if (!ListMarker(lines[i], out bool o, out _, out int contentColumn, out char d) || o != ordered || d != delimiter
                || Indent(lines[i]) > listIndent + 3 || Indent(lines[i]) < listIndent)
            {
                break;
            }

            // The item's lines: its first (after the marker), then those indented to its content
            // (or blank, or a lazy paragraph continuation), with that indentation removed.
            var inner = new List<string> { lines[i].Length > contentColumn ? lines[i][contentColumn..] : string.Empty };
            var innerStarts = new List<int> { starts[i] };
            i++;
            bool lastBlank = false;
            while (i < lines.Count)
            {
                string l = lines[i];
                if (string.IsNullOrWhiteSpace(l))
                {
                    inner.Add(string.Empty);
                    innerStarts.Add(starts[i]);
                    lastBlank = true;
                    i++;
                    continue;
                }
                if (Indent(l) >= contentColumn)
                {
                    inner.Add(l[contentColumn..]);
                }
                else if (!lastBlank && !StartsBlock(l.TrimStart()) && !ListMarker(l, out _, out _, out _, out _))
                {
                    inner.Add(l.TrimStart());
                }
                else
                {
                    break;
                }
                innerStarts.Add(starts[i]);
                lastBlank = false;
                i++;
            }

            bool? task = null;
            string first = inner[0];
            if (first.Length >= 3 && first[0] == '[' && first[2] == ']' && first[1] is ' ' or 'x' or 'X' && (first.Length == 3 || first[3] == ' '))
            {
                task = first[1] != ' ';
                inner[0] = first.Length > 4 ? first[4..] : string.Empty;
            }
            items.Add(new MarkdownListItem { Blocks = ParseLines(inner, innerStarts), Task = task });

            // A blank line followed by something that is not another item ends the list.
            if (i < lines.Count && lastBlank && !ListMarker(lines[i], out _, out _, out _, out _))
            {
                break;
            }
        }
        return new MarkdownBlock
        {
            Type = ordered ? MarkdownBlockType.OrderedList : MarkdownBlockType.BulletList,
            ListItems = items,
            StartNumber = firstNumber,
            Start = start,
        };
    }

    private static int Indent(string line)
    {
        int n = 0;
        while (n < line.Length && line[n] == ' ')
        {
            n++;
        }
        return n;
    }

    // Starts a block that interrupts a paragraph.
    private static bool StartsBlock(string trimmed) =>
        trimmed.StartsWith('>') || IsFence(trimmed, out _, out _) || IsRule(trimmed) || AtxHeading(trimmed, out _, out _);

    private static bool IsFence(string trimmed, out char fence, out int length)
    {
        fence = trimmed.Length > 0 ? trimmed[0] : '\0';
        length = 0;
        if (fence is not ('`' or '~'))
        {
            return false;
        }
        while (length < trimmed.Length && trimmed[length] == fence)
        {
            length++;
        }
        return length >= 3 && (fence == '~' || !trimmed[length..].Contains('`', StringComparison.Ordinal));
    }

    private static bool IsRule(string trimmed)
    {
        if (trimmed.Length < 3 || trimmed[0] is not ('-' or '*' or '_'))
        {
            return false;
        }
        char first = trimmed[0];
        int count = 0;
        foreach (char c in trimmed)
        {
            if (c == first)
            {
                count++;
            }
            else if (c != ' ')
            {
                return false;
            }
        }
        return count >= 3;
    }

    private static bool AtxHeading(string trimmed, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }
        if (level is 0 or > 6 || (level < trimmed.Length && trimmed[level] != ' '))
        {
            return false;
        }
        text = trimmed[level..].Trim();
        // A closing run of #s is not part of the text.
        int close = text.Length;
        while (close > 0 && text[close - 1] == '#')
        {
            close--;
        }
        if (close < text.Length && (close == 0 || text[close - 1] == ' '))
        {
            text = text[..close].TrimEnd();
        }
        return true;
    }

    private static bool ListMarker(string line, out bool ordered, out int number, out int contentColumn, out char delimiter)
    {
        ordered = false;
        number = 1;
        contentColumn = 0;
        delimiter = '\0';
        int indent = Indent(line);
        if (indent >= line.Length)
        {
            return false;
        }
        int p = indent;
        char c = line[p];
        if (c is '-' or '*' or '+')
        {
            if (IsRule(line.TrimStart()))
            {
                return false;
            }
            delimiter = c;
            p++;
        }
        else if (char.IsDigit(c))
        {
            int digits = 0;
            while (p < line.Length && char.IsDigit(line[p]) && digits < 9)
            {
                p++;
                digits++;
            }
            if (p >= line.Length || line[p] is not ('.' or ')'))
            {
                return false;
            }
            number = int.Parse(line.AsSpan(indent, digits), System.Globalization.CultureInfo.InvariantCulture);
            delimiter = line[p];
            ordered = true;
            p++;
        }
        else
        {
            return false;
        }
        if (p < line.Length && line[p] != ' ')
        {
            return false;
        }
        int spaces = 0;
        while (p + spaces < line.Length && line[p + spaces] == ' ' && spaces < 4)
        {
            spaces++;
        }
        contentColumn = p + Math.Max(1, spaces);
        return true;
    }

    private static bool TableSeparator(string line, out MarkdownTableAlign[] alignments)
    {
        alignments = [];
        string t = line.Trim();
        if (!t.Contains('-', StringComparison.Ordinal) || t.Any(c => c is not ('|' or '-' or ':' or ' ')))
        {
            return false;
        }
        var cells = Split(t);
        if (cells.Count == 0 || cells.Any(c => c.Trim().Trim(':').Length == 0 || c.Trim().Trim(':').Any(ch => ch != '-')))
        {
            return false;
        }
        alignments = [.. cells.Select(c =>
        {
            string s = c.Trim();
            bool left = s.StartsWith(':'), right = s.EndsWith(':');
            return left && right ? MarkdownTableAlign.Center : right ? MarkdownTableAlign.Right : left ? MarkdownTableAlign.Left : MarkdownTableAlign.Default;
        })];
        return true;
    }

    private static string[] Cells(string line, int count)
    {
        var cells = Split(line).Select(c => c.Trim().Replace("\\|", "|", StringComparison.Ordinal)).ToList();
        while (cells.Count < count)
        {
            cells.Add(string.Empty);
        }
        return [.. cells.Take(count)];
    }

    // Splits a table row on unescaped pipes outside code spans, without the outer pipes.
    private static List<string> Split(string row)
    {
        string t = row.Trim();
        if (t.StartsWith('|'))
        {
            t = t[1..];
        }
        if (t.EndsWith('|') && !t.EndsWith("\\|", StringComparison.Ordinal))
        {
            t = t[..^1];
        }
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inCode = false;
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (c == '\\' && i + 1 < t.Length && t[i + 1] == '|')
            {
                current.Append("\\|");
                i++;
                continue;
            }
            if (c == '`')
            {
                inCode = !inCode;
            }
            if (c == '|' && !inCode)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        cells.Add(current.ToString());
        return cells;
    }
}
