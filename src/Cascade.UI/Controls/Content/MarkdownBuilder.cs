namespace Cascade.UI;

/// <summary>
/// Turns parsed Markdown blocks into ordinary Cascade nodes: <see cref="MarkdownText"/> for
/// styled text, <see cref="Image"/>, <see cref="ScrollView"/> for long or wide code,
/// <see cref="Button"/> to copy code, <see cref="Checkbox"/> for task items, rows and columns for
/// lists, quotes and tables. Colours come from the theme the document is built under.
/// </summary>
internal sealed class MarkdownBuilder
{
    private readonly Markdown md;
    private readonly ColorSet colors;
    private readonly MarkdownHighlighter.Palette code;
    private readonly List<(MarkdownHeading Heading, Node Node)> headings;
    private readonly Dictionary<string, int> anchors = new(StringComparer.Ordinal);
    private int listDepth;

    internal MarkdownBuilder(Markdown md, CascadeTheme theme, List<(MarkdownHeading Heading, Node Node)> headings)
    {
        this.md = md;
        colors = theme.Colors;
        code = MarkdownHighlighter.For(theme);
        this.headings = headings;

        // Headings already built (a streaming rebuild of the tail) keep their numbering.
        foreach (var (heading, _) in headings)
        {
            MarkdownParser.UniqueAnchor(heading.Title, anchors);
        }
    }

    private static float Body => LayoutSolver.BodyFontSize;

    internal Node Block(MarkdownBlock block, bool muted = false) => block.Type switch
    {
        MarkdownBlockType.Heading => Heading(block),
        MarkdownBlockType.Paragraph => Inline(block.Text, Body, FontWeight.Regular, muted),
        MarkdownBlockType.CodeBlock => CodeBlock(block),
        MarkdownBlockType.BulletList or MarkdownBlockType.OrderedList => List(block, muted),
        MarkdownBlockType.BlockQuote => Quote(block),
        MarkdownBlockType.HorizontalRule => new Separator(color: colors.Border).Padding(0f, 4f),
        MarkdownBlockType.Table => Table(block, muted),
        _ => Node.Empty,
    };

    private MarkdownText Text(IReadOnlyList<MarkdownSpan> spans, float size, FontWeight weight = FontWeight.Regular, bool muted = false)
    {
        var text = new MarkdownText(spans, size, weight) { Muted = muted, Owner = md };
        return text;
    }

    private Node Heading(MarkdownBlock block)
    {
        var (size, weight) = block.HeadingLevel switch
        {
            1 => (LayoutSolver.H1FontSize, FontWeight.Bold),
            2 => (LayoutSolver.H2FontSize, FontWeight.Bold),
            3 => (LayoutSolver.H3FontSize, FontWeight.Bold),
            4 => (Body + 1f, FontWeight.SemiBold),
            5 => (Body, FontWeight.SemiBold),
            _ => (Body - 1f, FontWeight.SemiBold),
        };
        var spans = MarkdownInlineParser.Parse(block.Text, md.HtmlAllowed);
        var node = Text(spans, size, weight, muted: block.HeadingLevel == 6);
        string title = string.Concat(spans.Where(s => !s.IsImage).Select(s => s.Text)).Trim();
        headings.Add((new MarkdownHeading(title, block.HeadingLevel, MarkdownParser.UniqueAnchor(title, anchors)), node));
        return node.AccessibleRole(AccessibleRole.Heading).AccessibleLabel(title);
    }

    // A paragraph: its text, with images (block-level, or several side by side) where they fall.
    private Node Inline(string markdown, float size, FontWeight weight, bool muted)
    {
        var spans = MarkdownInlineParser.Parse(markdown, md.HtmlAllowed);
        if (!spans.Any(s => s.IsImage))
        {
            var text = Text(spans, size, weight, muted);
            return text.AccessibleRole(AccessibleRole.Text).AccessibleLabel(text.PlainText);
        }

        var parts = new List<Node>();
        var run = new List<MarkdownSpan>();
        var pictures = new List<Node>();
        void FlushText()
        {
            if (run.Any(s => !string.IsNullOrWhiteSpace(s.Text)))
            {
                var text = Text([.. run], size, weight, muted);
                parts.Add(text.AccessibleRole(AccessibleRole.Text).AccessibleLabel(text.PlainText));
            }
            run.Clear();
        }
        void FlushPictures()
        {
            if (pictures.Count > 0)
            {
                parts.Add(new Row(spacing: 6f, children: [.. pictures]));
                pictures.Clear();
            }
        }
        foreach (var span in spans)
        {
            if (span.IsImage)
            {
                FlushText();
                pictures.Add(Picture(span));
            }
            else if (pictures.Count > 0 && string.IsNullOrWhiteSpace(span.Text))
            {
                // Whitespace between images keeps them on one row.
            }
            else
            {
                FlushPictures();
                run.Add(span);
            }
        }
        FlushText();
        FlushPictures();
        return parts.Count == 1 ? parts[0] : new Column(spacing: 6f, children: [.. parts]);
    }

    private Node Picture(MarkdownSpan span)
    {
        string src = md.ResolveImageSource(span.ImageSource!);
        bool web = src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
#pragma warning disable CASCADEA11Y002 // the alt text is applied below
        Image image = web ? new Image(src, placeholder: true) : new Image(src);
#pragma warning restore CASCADEA11Y002
        image = image.LazyLoad(md.ImagesLazyLoad).Fit(ImageFit.Contain)
            .OnError(Text([new MarkdownSpan(span.Text.Length > 0 ? span.Text : src)], Body, muted: true));
        Node node = image.AccessibleLabel(span.Text);
        if (md.ImagesMaxWidth is { } maxWidth)
        {
            node = node.MaxWidth(maxWidth);
        }
        if (md.ImagesBorderRadius is { } radius)
        {
            node = node.CornerRadius(radius);
        }
        if (span.Link is { } link)
        {
            node = node.OnTap(() => md.ActivateLink(link));
        }
        return node;
    }

    private Node CodeBlock(MarkdownBlock block)
    {
        float size = Body - 1f;
        bool wrap = md.CodeBlockWrapLines;
        var lines = MarkdownHighlighter.Highlight(block.Text, block.Language, md.CodeBlockSyntaxHighlight, code);
        float gutter = lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Length * size * 0.62f + 14f;

        var rows = new Node[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            var text = new MarkdownText(lines[i], size, wrap: wrap, monospace: true, color: code.Plain) { Owner = md };
            Node line = wrap ? text.Expand() : text;
            if (md.CodeBlockLineNumbers)
            {
                var number = new MarkdownText([new MarkdownSpan((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Color: code.Comment)],
                    size, wrap: false, monospace: true).Width(gutter);
                line = new Row(children: [number, line]);
            }
            rows[i] = line;
        }
        Node body = new Column(children: rows);

        // Lines that do not wrap scroll across; a maximum height scrolls down.
        float? maxHeight = md.CodeBlockMaxHeight;
        if (!wrap || maxHeight is not null)
        {
            var direction = !wrap && maxHeight is not null ? ScrollDirection.Both : !wrap ? ScrollDirection.Horizontal : ScrollDirection.Vertical;
            var scroll = new ScrollView(body).Direction(direction);
            scroll.FitContentHeight = true;
            body = maxHeight is { } max ? scroll.MaxHeight(max) : scroll;
        }

        var header = new List<Node>();
        if (md.CodeBlockShowLanguageLabel && block.Language is { } language)
        {
            header.Add(new MarkdownText([new MarkdownSpan(language, Color: code.Comment)], LayoutSolver.BodyFontSize - 2f, wrap: false));
        }
        if (md.CodeBlockShowCopyButton)
        {
            string text = block.Text;
            header.Add(new Spacer());
            header.Add(new Button(md.CodeBlockCopyButtonLabel, () => _ = Clipboard.WriteTextAsync(text))
                .Variant("ghost").Style(new TextStyle(LayoutSolver.BodyFontSize - 2f, FontWeight.Medium, 0f)));
        }

        var parts = header.Count > 0
            ? new Node[] { new Row(spacing: 8f, crossAxisAlignment: CrossAxisAlignment.Center, children: [.. header]), body }
            : [body];
        return new Column(spacing: 6f, children: parts)
            .Padding(12f)
            .Background(code.Background)
            .CornerRadius(6f)
            .AccessibleRole(AccessibleRole.Text)
            .AccessibleLabel(block.Language is { } l ? $"{l} code: {block.Text}" : block.Text);
    }

    private Node List(MarkdownBlock block, bool muted)
    {
        bool ordered = block.Type == MarkdownBlockType.OrderedList;
        var items = block.ListItems ?? [];
        string bullet = (listDepth % 3) switch { 0 => "•", 1 => "◦", _ => "▪" };
        listDepth++;
        var rows = new Node[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            Node marker = item.Task is { } done
                ? new Checkbox(done ? CheckboxValue.Checked : CheckboxValue.Unchecked, static _ => { }).AccessibleLabel(done ? "Done" : "Not done")
                : Text([new MarkdownSpan(ordered ? $"{block.StartNumber + i}." : bullet)], Body, muted: true).Width(ordered ? 26f : 16f);
            var content = new Column(spacing: 6f, children: [.. item.Blocks.Select(b => Block(b, muted))]).Expand();
            rows[i] = new Row(spacing: 6f, children: [marker, content]).AccessibleRole(AccessibleRole.ListItem);
        }
        listDepth--;
        return new Column(spacing: 4f, children: rows).AccessibleRole(AccessibleRole.List);
    }

    private Node Quote(MarkdownBlock block)
    {
        var bar = new Column().Width(3f).Background(colors.Primary.Opacity(0.5f)).CornerRadius(1.5f);
        var content = new Column(spacing: 8f, children: [.. (block.Children ?? []).Select(b => Block(b, muted: true))]).Expand();
        return new Row(spacing: 12f, crossAxisAlignment: CrossAxisAlignment.Stretch, children: [bar, content]);
    }

    private Node Table(MarkdownBlock block, bool muted)
    {
        var rows = block.Rows ?? [];
        var children = new List<Node>();
        for (int r = 0; r < rows.Count; r++)
        {
            bool header = r == 0;
            var cells = rows[r].Select(cell =>
                (Node)Text(MarkdownInlineParser.Parse(cell, md.HtmlAllowed), Body, header ? FontWeight.SemiBold : FontWeight.Regular, muted)
                    .Padding(10f, 6f).Expand()).ToArray();
            Node row = new Row(children: cells).AccessibleRole(AccessibleRole.Row);
            if (header)
            {
                row = row.Background(colors.SurfaceAlt);
            }
            else if (r % 2 == 0)
            {
                row = row.Background(colors.SurfaceAlt.Opacity(0.5f));
            }
            if (r > 0)
            {
                children.Add(new Separator(color: colors.Border));
            }
            children.Add(row);
        }
        return new Column(children: [.. children])
            .Padding(1f) // rows inside the border, not over it
            .Border(colors.Border, 1f, 6f)
            .AccessibleRole(AccessibleRole.Table);
    }
}
