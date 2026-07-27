namespace Cascade.UI.Tests;

/// <summary>
/// Regression net for TextArea vertical navigation across blank lines and line
/// starts. In the fast (Latin) layout path a line's trailing '\n' is folded into
/// its <c>TextLength</c>, so its [TextStart, TextStart + TextLength] range overlaps
/// the next line's start and <see cref="TextLayoutResult.GetLineIndexForOffset"/>
/// returns the PREVIOUS line for a caret at the start of a line (including a blank
/// line). That made Up/Down (and Home/End) resolve the wrong current line, so they
/// did nothing on blank lines / line starts while Right still advanced.
/// <see cref="InputDispatcher.TextAreaVisualLineIndex"/> resolves the caret to the
/// line it visually sits on, matching the painter.
/// </summary>
public sealed class TextAreaVerticalNavTests
{
    private static string InterPath()
    {
        string p = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");
        if (!File.Exists(p))
        {
            throw new FileNotFoundException(p);
        }
        return p;
    }

    private static TextLayoutResult Layout(string text)
    {
        var opts = new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 17f,
            MaxWidth = 4000f,
            MaxLines = 0,
            Overflow = TextOverflow.Clip,
        };
        return TextLayoutEngine.Layout(text, opts);
    }

    // "Hello\n\nWorld": Hello=[0..5), '\n'@5, blank line start=6, '\n'@6, World start=7.
    private const string Sample = "Hello\n\nWorld";

    [Test]
    public async Task GetLineIndexForOffset_ReturnsPreviousLine_AtBoundary_DocumentsBug()
    {
        var layout = Layout(Sample);

        // The blank line's caret offset (6) resolves to line 0 (the previous line),
        // and the start of "World" (7) resolves to line 1 — off by one each time.
        int blank = layout.GetLineIndexForOffset(6);
        int world = layout.GetLineIndexForOffset(7);

        await Assert.That(blank).IsEqualTo(0);
        await Assert.That(world).IsEqualTo(1);
    }

    [Test]
    public async Task VisualLineIndex_ResolvesBlankLineAndLineStart()
    {
        var layout = Layout(Sample);

        int atHelloMid = InputDispatcher.TextAreaVisualLineIndex(layout, Sample, 3); // inside "Hello"
        int atBlank = InputDispatcher.TextAreaVisualLineIndex(layout, Sample, 6);    // blank line
        int atWorldStart = InputDispatcher.TextAreaVisualLineIndex(layout, Sample, 7); // start of "World"
        int atWorldMid = InputDispatcher.TextAreaVisualLineIndex(layout, Sample, 9);  // inside "World"

        await Assert.That(atHelloMid).IsEqualTo(0);
        await Assert.That(atBlank).IsEqualTo(1);
        await Assert.That(atWorldStart).IsEqualTo(2);
        await Assert.That(atWorldMid).IsEqualTo(2);
    }

    [Test]
    public async Task VisualLineIndex_TrailingNewline_ReturnsPhantomRow()
    {
        const string text = "abc\n";
        var layout = Layout(text);

        // Caret after the final '\n' sits on a phantom row the engine emits no line
        // for; the helper returns Lines.Count so Up lands on the last real line.
        int phantom = InputDispatcher.TextAreaVisualLineIndex(layout, text, 4);

        await Assert.That(phantom).IsEqualTo(layout.Lines.Count);
        await Assert.That(layout.Lines.Count).IsEqualTo(1);
    }

    [Test]
    public async Task VisualLineIndex_ConsecutiveBlankLines_EachResolvesToOwnRow()
    {
        // "a\n\n\nb": a=[0..1) '\n'@1, blank@2 '\n'@2, blank@3 '\n'@3, b start@4.
        const string text = "a\n\n\nb";
        var layout = Layout(text);

        int firstBlank = InputDispatcher.TextAreaVisualLineIndex(layout, text, 2);
        int secondBlank = InputDispatcher.TextAreaVisualLineIndex(layout, text, 3);
        int bStart = InputDispatcher.TextAreaVisualLineIndex(layout, text, 4);

        await Assert.That(firstBlank).IsEqualTo(1);
        await Assert.That(secondBlank).IsEqualTo(2);
        await Assert.That(bStart).IsEqualTo(3);
    }
}
