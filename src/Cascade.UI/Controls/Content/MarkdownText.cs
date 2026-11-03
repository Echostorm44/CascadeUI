namespace Cascade.UI;

/// <summary>
/// Inline-flow text built by <see cref="Markdown"/>: styled spans (bold, italic, code, strike,
/// links, per-span colours) wrapped as one paragraph. Words never break between spans unless
/// there is whitespace, a word wider than the line breaks by character, and '\n' is a hard break.
/// Measured with the same shaping and fonts the painter draws with.
/// </summary>
internal sealed class MarkdownText : Node
{
    internal MarkdownText(IReadOnlyList<MarkdownSpan> spans, float fontSize, FontWeight weight = FontWeight.Regular,
        bool italic = false, ColorValue? color = null, bool wrap = true, bool monospace = false)
    {
        Spans = spans;
        FontSize = fontSize;
        Weight = weight;
        IsItalicBase = italic;
        BaseColor = color;
        Wraps = wrap;
        Monospace = monospace;
        HasLinks = spans.Any(s => s.Link is not null);
    }

    internal IReadOnlyList<MarkdownSpan> Spans { get; }
    internal float FontSize { get; }
    internal FontWeight Weight { get; }
    internal bool IsItalicBase { get; }

    /// <summary>The text colour; null is the theme's text colour (or the muted one with <see cref="Muted"/>).</summary>
    internal ColorValue? BaseColor { get; }
    internal bool Muted { get; init; }
    internal bool Wraps { get; }
    internal bool Monospace { get; }
    internal bool HasLinks { get; }

    /// <summary>The Markdown this belongs to, which handles its link clicks.</summary>
    internal Markdown? Owner { get; set; }

    /// <summary>The plain text (accessibility, copying).</summary>
    internal string PlainText => string.Concat(Spans.Select(s => s.Text));

    internal readonly record struct Fragment(int Span, string Text, float X, float Y, float Width, float Height, float FontSize, string? FontPath);

    internal List<Fragment> Fragments { get; } = [];

    private static readonly FontFamily MonospaceFamily = FontFamily.ForCategory(SystemFont.Monospace);

    internal float CodeFontSize => Monospace ? FontSize : FontSize * 0.9f;

    internal string? FontPathFor(MarkdownSpan span, string? defaultFontPath)
    {
        bool code = Monospace || span.Style.HasFlag(MarkdownSpanStyle.Code);
        var weight = span.Style.HasFlag(MarkdownSpanStyle.Bold) ? FontWeight.Bold : Weight;
        bool italic = IsItalicBase || span.Style.HasFlag(MarkdownSpanStyle.Italic);
        return FontFamilyResolver.Resolve(code ? MonospaceFamily : null, weight, italic, defaultFontPath);
    }

    private float SizeFor(MarkdownSpan span) =>
        !Monospace && span.Style.HasFlag(MarkdownSpanStyle.Code) ? CodeFontSize : FontSize;

    internal static float Advance(string text, string? fontPath, float fontSize)
    {
        if (text.Length == 0)
        {
            return 0f;
        }
        if (fontPath is null)
        {
            return text.Length * fontSize * 0.5f;
        }
        return TextLayoutEngine.Layout(text, new TextLayoutOptions { FontPath = fontPath, FontSize = fontSize, MaxWidth = float.PositiveInfinity, NoWrap = true })
            .AdvanceBox.Width;
    }

    internal static float LineHeightOf(string? fontPath, float fontSize)
    {
        if (fontPath is null)
        {
            return fontSize * 1.4f;
        }
        var layout = TextLayoutEngine.Layout("Ag", new TextLayoutOptions { FontPath = fontPath, FontSize = fontSize, MaxWidth = float.PositiveInfinity, NoWrap = true });
        return layout.Lines.Count > 0 ? layout.Lines[0].Height : fontSize * 1.4f;
    }

    // A word: the pieces (per span) up to and including its trailing whitespace.
    private readonly record struct Piece(int Span, string Text);

    /// <summary>Lays the spans out in lines no wider than <paramref name="maxWidth"/>; returns the size.</summary>
    internal Size Layout(float maxWidth, string? defaultFontPath)
    {
        Fragments.Clear();
        float lineHeight = LineHeightOf(FontPathFor(new MarkdownSpan(""), defaultFontPath), FontSize);
        bool wrap = Wraps && !float.IsPositiveInfinity(maxWidth) && maxWidth > 0f;

        var paths = new string?[Spans.Count];
        for (int s = 0; s < Spans.Count; s++)
        {
            paths[s] = FontPathFor(Spans[s], defaultFontPath);
        }

        float x = 0f, y = 0f, widest = 0f;
        var line = new List<Fragment>();

        void EndLine()
        {
            // Smaller runs (code) sit centred in the line.
            for (int f = 0; f < line.Count; f++)
            {
                var frag = line[f];
                Fragments.Add(frag with { Y = y + (lineHeight - frag.Height) / 2f });
            }
            line.Clear();
            widest = Math.Max(widest, x);
            x = 0f;
            y += lineHeight;
        }

        void Place(Piece piece)
        {
            var span = Spans[piece.Span];
            float size = SizeFor(span);
            float width = Advance(piece.Text, paths[piece.Span], size);
            float height = size == FontSize ? lineHeight : LineHeightOf(paths[piece.Span], size);
            // Neighbouring pieces of one span on a line join up, so text draws in whole runs.
            if (line.Count > 0 && line[^1].Span == piece.Span && Math.Abs(line[^1].X + line[^1].Width - x) < 0.01f)
            {
                var last = line[^1];
                string joined = last.Text + piece.Text;
                line[^1] = last with { Text = joined, Width = Advance(joined, last.FontPath, last.FontSize) };
                x = last.X + line[^1].Width;
                return;
            }
            line.Add(new Fragment(piece.Span, piece.Text, x, 0f, width, height, size, paths[piece.Span]));
            x += width;
        }

        foreach (var word in Words())
        {
            if (word.Count == 1 && word[0].Text == "\n")
            {
                EndLine();
                continue;
            }

            float wordWidth = 0f, inkWidth = 0f;
            foreach (var piece in word)
            {
                var span = Spans[piece.Span];
                wordWidth += Advance(piece.Text, paths[piece.Span], SizeFor(span));
            }
            var lastPiece = word[^1];
            string trimmed = lastPiece.Text.TrimEnd();
            inkWidth = wordWidth - Advance(lastPiece.Text, paths[lastPiece.Span], SizeFor(Spans[lastPiece.Span]))
                + Advance(trimmed, paths[lastPiece.Span], SizeFor(Spans[lastPiece.Span]));

            if (wrap && x > 0f && x + inkWidth > maxWidth)
            {
                EndLine();
            }

            if (wrap && inkWidth > maxWidth)
            {
                // Too wide for any line: break it by character.
                foreach (var piece in word)
                {
                    var span = Spans[piece.Span];
                    float size = SizeFor(span);
                    int start = 0;
                    for (int c = 1; c <= piece.Text.Length; c++)
                    {
                        string chunk = piece.Text[start..c];
                        if (x + Advance(chunk, paths[piece.Span], size) > maxWidth && c - 1 > start)
                        {
                            Place(new Piece(piece.Span, piece.Text[start..(c - 1)]));
                            EndLine();
                            start = c - 1;
                        }
                    }
                    if (start < piece.Text.Length)
                    {
                        Place(new Piece(piece.Span, piece.Text[start..]));
                    }
                }
                continue;
            }

            if (x == 0f && line.Count == 0 && Fragments.Count > 0 && word[0].Text.Length > 0 && char.IsWhiteSpace(word[0].Text[0]) && wrap)
            {
                // A wrapped line does not start with the space that ended the last one.
                foreach (var piece in word)
                {
                    string text = piece.Text.TrimStart();
                    if (text.Length > 0)
                    {
                        Place(piece with { Text = text });
                    }
                }
                continue;
            }

            foreach (var piece in word)
            {
                Place(piece);
            }
        }

        if (line.Count > 0 || Fragments.Count == 0 || x > 0f)
        {
            EndLine();
        }

        // Trailing spaces do not widen a line.
        float measured = 0f;
        foreach (var frag in Fragments)
        {
            string ink = frag.Text.TrimEnd();
            float right = frag.X + (ink.Length == frag.Text.Length ? frag.Width : Advance(ink, frag.FontPath, frag.FontSize));
            measured = Math.Max(measured, right);
        }
        return new Size(MathF.Ceiling(measured) + 1f, y);
    }

    // Splits the spans into words: runs up to and including whitespace, which may cross spans.
    private List<List<Piece>> Words()
    {
        var words = new List<List<Piece>>();
        var current = new List<Piece>();
        for (int s = 0; s < Spans.Count; s++)
        {
            string text = Spans[s].Text;
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    if (i > start)
                    {
                        current.Add(new Piece(s, text[start..i]));
                    }
                    if (current.Count > 0)
                    {
                        words.Add(current);
                        current = [];
                    }
                    words.Add([new Piece(s, "\n")]);
                    start = i + 1;
                    continue;
                }
                bool endsWord = char.IsWhiteSpace(c) && (i + 1 >= text.Length || !char.IsWhiteSpace(text[i + 1]));
                if (endsWord && Wraps)
                {
                    current.Add(new Piece(s, text[start..(i + 1)]));
                    words.Add(current);
                    current = [];
                    start = i + 1;
                }
            }
            if (start < text.Length)
            {
                current.Add(new Piece(s, text[start..]));
            }
        }
        if (current.Count > 0)
        {
            words.Add(current);
        }
        return words;
    }

    /// <summary>The link under a point in the node's content area, if any.</summary>
    internal string? LinkAt(Point local)
    {
        foreach (var frag in Fragments)
        {
            if (Spans[frag.Span].Link is { } link
                && local.X >= frag.X && local.X < frag.X + frag.Width
                && local.Y >= frag.Y - 2f && local.Y < frag.Y + frag.Height + 2f)
            {
                return link;
            }
        }
        return null;
    }
}
