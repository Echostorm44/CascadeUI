#pragma warning disable CA2000 // harnesses are disposed by using
#pragma warning disable CA1861 // expected values read best inline

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// Markdown renders as a real node tree: inline formatting and links survive (they were stripped
/// to plain text), images, tables, nested and task lists, code blocks with line numbers, wrapping,
/// a height cap and a copy button, safe HTML, streaming, auto-scroll and heading anchors.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class MarkdownRenderingTests
{
    private static List<MarkdownSpan> Inline(string text, bool html = false) => MarkdownInlineParser.Parse(text, html);

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in NodeDiffer.GetChildren(node))
        {
            foreach (var n in Walk(child))
            {
                yield return n;
            }
        }
    }

    private static T First<T>(Markdown md) where T : Node => Walk(md.Tree).OfType<T>().First();

    // ── Inline ───────────────────────────────────────────────────────

    [Test]
    public async Task Inline_KeepsEmphasisCodeLinksAndStrikethrough()
    {
        var spans = Inline("a **bold** and *it* `x * y` [site](https://e.com \"t\") ~~old~~ snake_case_name \\*lit\\*");
        await Assert.That(spans.Single(s => s.Text == "bold").Style).IsEqualTo(MarkdownSpanStyle.Bold);
        await Assert.That(spans.Single(s => s.Text == "it").Style).IsEqualTo(MarkdownSpanStyle.Italic);
        await Assert.That(spans.Single(s => s.Text == "x * y").Style).IsEqualTo(MarkdownSpanStyle.Code);
        await Assert.That(spans.Single(s => s.Text == "site").Link).IsEqualTo("https://e.com");
        await Assert.That(spans.Single(s => s.Text == "old").Style).IsEqualTo(MarkdownSpanStyle.Strike);
        string plain = string.Concat(spans.Select(s => s.Text));
        await Assert.That(plain).Contains("snake_case_name");
        await Assert.That(plain).Contains("*lit*");

        var nested = Inline("***both*** and **bold with *it* inside**");
        await Assert.That(nested.Single(s => s.Text == "both").Style).IsEqualTo(MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic);
        await Assert.That(nested.Single(s => s.Text == "it").Style).IsEqualTo(MarkdownSpanStyle.Bold | MarkdownSpanStyle.Italic);
    }

    [Test]
    public async Task Inline_ImagesAutolinksAndHardBreaks()
    {
        var spans = Inline("![a cat](cat.png) <https://x.org> line one  \nline two");
        await Assert.That(spans[0].ImageSource).IsEqualTo("cat.png");
        await Assert.That(spans[0].Text).IsEqualTo("a cat");
        await Assert.That(spans.Single(s => s.Link == "https://x.org").Text).IsEqualTo("https://x.org");
        await Assert.That(string.Concat(spans.Select(s => s.Text))).Contains("one\nline two");
    }

    [Test]
    public async Task Html_IsShownAsTyped_UnlessAllowed_ThenOnlyTheSafeSubset()
    {
        var off = Inline("<b>hi</b><script>x</script>");
        await Assert.That(string.Concat(off.Select(s => s.Text))).IsEqualTo("<b>hi</b><script>x</script>");

        var on = Inline("<b>hi</b> <a href=\"https://e.com\">go</a><br><script>x</script>", html: true);
        await Assert.That(on.Single(s => s.Text == "hi").Style).IsEqualTo(MarkdownSpanStyle.Bold);
        await Assert.That(on.Single(s => s.Text == "go").Link).IsEqualTo("https://e.com");
        await Assert.That(string.Concat(on.Select(s => s.Text))).Contains("\n<script>x</script>");
    }

    // ── Blocks ───────────────────────────────────────────────────────

    [Test]
    public async Task Blocks_NestedAndTaskLists_Tables_Setext_Fences_Quotes()
    {
        const string source = """
            Title
            =====

            - one
              - nested
            - [x] done
            - [ ] todo

            3. third
            4. fourth

            | Name | Qty |
            |:-----|----:|
            | `a|b` | 2 |

            ~~~py
            # not a heading
            ~~~

                indented code

            > quoted
            > - with a list
            """;
        var blocks = MarkdownBlockParser.Parse(source, 0);
        await Assert.That(blocks.Select(b => b.Type)).IsEquivalentTo(new[]
        {
            MarkdownBlockType.Heading, MarkdownBlockType.BulletList, MarkdownBlockType.OrderedList,
            MarkdownBlockType.Table, MarkdownBlockType.CodeBlock, MarkdownBlockType.CodeBlock, MarkdownBlockType.BlockQuote,
        });
        var list = blocks[1].ListItems!;
        await Assert.That(list[0].Blocks[1].Type).IsEqualTo(MarkdownBlockType.BulletList);
        await Assert.That(list[1].Task).IsEqualTo(true);
        await Assert.That(list[2].Task).IsEqualTo(false);
        await Assert.That(blocks[2].StartNumber).IsEqualTo(3);
        await Assert.That(blocks[3].Rows![1][0]).IsEqualTo("`a|b`");
        await Assert.That(blocks[3].Alignments![1]).IsEqualTo(MarkdownTableAlign.Right);
        await Assert.That(blocks[4].Language).IsEqualTo("py");
        await Assert.That(blocks[5].Text).IsEqualTo("indented code");
        await Assert.That(blocks[6].Children!.Select(b => b.Type)).IsEquivalentTo(new[] { MarkdownBlockType.Paragraph, MarkdownBlockType.BulletList });
    }

    [Test]
    public async Task ExtractHeadings_SkipsCodeBlocks_AndNumbersRepeats()
    {
        var headings = MarkdownParser.ExtractHeadings("# Intro\n```sh\n# comment\n```\n## Intro\n## **Setup** steps");
        await Assert.That(headings.Select(h => h.AnchorId)).IsEquivalentTo(new[] { "intro", "intro-1", "setup-steps" });
        await Assert.That(headings[2].Title).IsEqualTo("Setup steps");
    }

    // ── Layout and input ─────────────────────────────────────────────

    [Test]
    public async Task Text_WrapsToTheWidth_AndNeverBreaksAWordBetweenSpans()
    {
        var text = new MarkdownText(Inline("some **bold**ly styled words that go on and on until they wrap"), 14f);
        var wide = text.Layout(2000f, LayoutSolver.DefaultFontPath);
        var narrow = text.Layout(120f, LayoutSolver.DefaultFontPath);
        await Assert.That(narrow.Height).IsGreaterThan(wide.Height * 2f);
        await Assert.That(narrow.Width).IsLessThanOrEqualTo(121f);

        // "bold" and "ly" are one word: whichever line has one has the other right after it.
        var bold = text.Fragments.Single(f => text.Spans[f.Span].Text == "bold");
        var ly = text.Fragments.First(f => f.Text.StartsWith("ly", StringComparison.Ordinal));
        await Assert.That(ly.Y).IsEqualTo(bold.Y);
        await Assert.That(Math.Abs(ly.X - (bold.X + bold.Width))).IsLessThan(0.5f);
    }

    [Test]
    public async Task QuoteBar_RunsTheHeightOfTheQuote()
    {
        // A Row asked to stretch, with unbounded height, makes its children as tall as the tallest.
        var md = new Markdown("> first line\n>\n> - a\n> - b");
        using var app = new RenderHarness(() => new ScrollView(md).Height(200).Width(300));
        app.Frame();
        var quote = Walk(md.Tree).OfType<Row>().First(r => r.CrossAxisAlignment == CrossAxisAlignment.Stretch);
        float rowHeight = quote.LayoutData.Bounds.Height;
        await Assert.That(rowHeight).IsGreaterThan(40f);
        await Assert.That(quote.Children[0].LayoutData.Bounds.Height).IsEqualTo(rowHeight);
    }

    [Test]
    public async Task ClickingALink_CallsTheHandler()
    {
        string? clicked = null;
        var md = new Markdown("Go to [the site](https://example.com) now.").OnLinkClick(url => clicked = url);
        using var app = new RenderHarness(() => md.Width(380));
        app.Frame();

        var text = First<MarkdownText>(md);
        await Assert.That(HitTester.TryGetAbsoluteBounds(md, text, out var at)).IsTrue();
        var link = text.Fragments.First(f => text.Spans[f.Span].Link is not null);
        app.Click(at.X + link.X + link.Width / 2f, at.Y + link.Y + link.Height / 2f);
        await Assert.That(clicked).IsEqualTo("https://example.com");

        clicked = null;
        app.Click(at.X + 2f, at.Y + link.Y + link.Height / 2f); // "Go", not a link
        await Assert.That(clicked).IsNull();
    }

    [Test]
    public async Task CodeBlock_LineNumbers_Copy_Wrap_AndAHeightThatFitsShortCode()
    {
        string longCode = string.Join('\n', Enumerable.Range(1, 40).Select(i => $"var x{i} = {i};"));
        var md = new Markdown($"```csharp\n{longCode}\n```\n\n```\nshort\n```").CodeBlock(lineNumbers: true, maxHeight: 120f, copyButtonLabel: "Copy it");
        using var app = new RenderHarness(() => md.Width(380));
        app.Frame();

        var views = Walk(md.Tree).OfType<ScrollView>().ToList();
        await Assert.That(views.Count).IsEqualTo(2);
        await Assert.That(views[0].LayoutData.Bounds.Height).IsEqualTo(120f);          // capped
        await Assert.That(views[1].LayoutData.Bounds.Height).IsLessThan(40f);         // short code fits
        await Assert.That(views[0].ScrollDirection).IsEqualTo(ScrollDirection.Both);
        var gutter = Walk(md.Tree).OfType<MarkdownText>().Where(t => t.Monospace && t.PlainText == "40");
        await Assert.That(gutter.Count()).IsEqualTo(1);
        await Assert.That(Walk(md.Tree).OfType<Button>().Count(b => b.Label.Resolve() == "Copy it")).IsEqualTo(2);

        // Keywords are coloured.
        var keyword = Walk(md.Tree).OfType<MarkdownText>().SelectMany(t => t.Spans).First(s => s.Text == "var");
        await Assert.That(keyword.Color).IsNotEqualTo(MarkdownHighlighter.For(ThemeSwitcher.Current).Plain);

        var wrapped = new Markdown("```\n" + new string('w', 300) + "\n```").CodeBlock(wrapLines: true, showCopyButton: false);
        await Assert.That(Walk(wrapped.Tree).OfType<ScrollView>().Any()).IsFalse();
    }

    [Test]
    public async Task Images_ResolveAgainstTheBaseUrl_WithTheirOptions()
    {
        var md = new Markdown("![logo](img/logo.png) ![abs](https://cdn.x/y.png)")
            .BaseUrl("https://site.org/docs").Images(lazyLoad: false, maxWidth: 200f, borderRadius: 8f);
        var images = Walk(md.Tree).OfType<Image>().ToList();
        await Assert.That(images.Select(i => i.Url)).IsEquivalentTo(new[] { "https://site.org/docs/img/logo.png", "https://cdn.x/y.png" });
        await Assert.That(images[0].LazyLoadEnabled).IsFalse();
        await Assert.That(images[0].LayoutData.MaxWidthMod).IsEqualTo(200f);
        await Assert.That(images[0].LayoutData.CornerRadiusValue).IsEqualTo(8f);

        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"md-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            string file = System.IO.Path.Combine(folder, "readme.md");
            await File.WriteAllTextAsync(file, "![pic](pic.png)");
            var fromFile = new Markdown(file);
            using var app = new RenderHarness(() => fromFile.Width(300));
            for (int i = 0; i < 50 && !Walk(fromFile.Tree).OfType<Image>().Any(); i++)
            {
                await Task.Delay(10);
                app.Frame();
            }
            await Assert.That(Walk(fromFile.Tree).OfType<Image>().Single().Path).IsEqualTo(System.IO.Path.Combine(folder, "pic.png"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public async Task Streaming_RebuildsOnlyTheLastBlock()
    {
        var first = new Markdown(new Bindable<string>("# Title\n\nPara one.\n\nPara tw", _ => { })).StreamingMode(true);
        var titleNode = First<MarkdownText>(first);
        await Assert.That(first.FullBuilds).IsEqualTo(1);

        var next = new Markdown(new Bindable<string>("# Title\n\nPara one.\n\nPara two is done.\n\n- item", _ => { })).StreamingMode(true);
        ((IPumpedNode)next).AdoptFrom(first);
        var blocks = next.GetParsedBlocks();
        await Assert.That(next.FullBuilds).IsEqualTo(1);
        await Assert.That(First<MarkdownText>(next)).IsSameReferenceAs(titleNode);
        await Assert.That(blocks.Select(b => b.Type)).IsEquivalentTo(new[] { MarkdownBlockType.Heading, MarkdownBlockType.Paragraph, MarkdownBlockType.Paragraph, MarkdownBlockType.BulletList });
        await Assert.That(blocks[2].Text).IsEqualTo("Para two is done.");
        await Assert.That(next.Headings.Count).IsEqualTo(1);
    }

    /// <summary>A document growing in a ScrollView, as a chat answer streams in.</summary>
    private sealed class Chat(MarkdownAutoScrollMode mode) : Component
    {
        internal string Text = string.Join("\n\n", Enumerable.Range(0, 8).Select(i => $"Paragraph {i}."));
        internal ScrollView? View;

        protected override Node Render() =>
            View = new ScrollView(new Markdown(new Bindable<string>(Text, _ => { })).StreamingMode(true).AutoScroll(mode)).Height(200).Width(300);

        internal void Append(string more)
        {
            Text += more;
            Invalidate();
        }
    }

    [Test]
    public async Task AutoScroll_KeepsTheEnclosingViewAtTheBottom()
    {
        var chat = new Chat(MarkdownAutoScrollMode.Always);
        using var app = new RenderHarness(() => chat);
        app.Frame();
        app.Frame();
        float before = chat.View!.MaxY;
        chat.Append("\n\n" + string.Join("\n\n", Enumerable.Range(0, 6).Select(i => $"More {i}.")));
        app.Frame();
        app.Frame();
        await Assert.That(chat.View!.MaxY).IsGreaterThan(before);
        await Assert.That(chat.View.OffsetY).IsEqualTo(chat.View.MaxY);

        var still = new Chat(MarkdownAutoScrollMode.Never);
        using var other = new RenderHarness(() => still);
        other.Frame();
        still.Append("\n\nMore.\n\nMore.\n\nMore.");
        other.Frame();
        other.Frame();
        await Assert.That(still.View!.OffsetY).IsEqualTo(0f);
    }

    [Test]
    public async Task Headings_ReportWhenVisible_AndAnchorLinksScrollToThem()
    {
        var seen = new List<string>();
        string filler = string.Join("\n\n", Enumerable.Range(0, 12).Select(i => $"Filler {i}."));
        var md = new Markdown($"[Jump](#far-away)\n\n# Near\n\n{filler}\n\n# Far away\n\n{filler}")
            .HeadingAnchors(true)
            .OnHeadingVisible(h => seen.Add(h.AnchorId));
        var sv = new ScrollView(md);
        using var app = new RenderHarness(() => sv.Height(200).Width(300));
        app.Frame();
        app.Frame();
        await Assert.That(seen).IsEquivalentTo(new[] { "near" });

        var jump = First<MarkdownText>(md);
        await Assert.That(HitTester.TryGetAbsoluteBounds(sv, jump, out var at)).IsTrue();
        var link = jump.Fragments[0];
        app.Click(at.X + link.X + link.Width / 2f, at.Y + link.Y + link.Height / 2f);
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(15);
            app.Frame();
        }
        var far = md.Headings.Single(h => h.Heading.AnchorId == "far-away").Node;
        await Assert.That(HitTester.TryGetAbsoluteBounds(sv, far, out var farAt)).IsTrue();
        await Assert.That(Math.Abs(farAt.Y)).IsLessThan(2f); // at the top of the view
        await Assert.That(seen).Contains("far-away");
        await Assert.That(md.ScrollToHeading("nope")).IsFalse();
    }
}
