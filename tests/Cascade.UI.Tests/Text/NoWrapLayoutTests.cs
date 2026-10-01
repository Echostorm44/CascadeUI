namespace Cascade.UI.Tests.Text;

/// <summary>
/// <see cref="TextLayoutOptions.NoWrap"/>: lines break only at hard line breaks, and an ellipsis
/// cuts at a character rather than at the last word that fits (a URL used to collapse to
/// "https://github.com/…" because '/' is a break opportunity).
/// </summary>
public class NoWrapLayoutTests
{
    private const string Url = "https://github.com/Echostorm44/CascadeUI";

    private static string InterPath() => System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf");

    private static TextLayoutResult Layout(string text, float maxWidth, bool noWrap, TextOverflow overflow, int maxLines = 0)
    {
        return TextLayoutEngine.Layout(text, new TextLayoutOptions
        {
            FontPath = InterPath(),
            FontSize = 13f,
            MaxWidth = maxWidth,
            MaxLines = maxLines,
            Overflow = overflow,
            NoWrap = noWrap,
        });
    }

    [Test]
    public async Task Wrapping_SplitsTheUrl_NoWrapKeepsOneLine()
    {
        var wrapped = Layout(Url, 120f, noWrap: false, TextOverflow.Clip);
        var single = Layout(Url, 120f, noWrap: true, TextOverflow.Clip);

        await Assert.That(wrapped.Lines.Count).IsGreaterThan(1);
        await Assert.That(single.Lines.Count).IsEqualTo(1);
        await Assert.That(single.Lines[0].TextLength).IsEqualTo(Url.Length);
    }

    [Test]
    public async Task NoWrapEllipsis_CutsAtACharacter_WithinWidth()
    {
        var full = Layout(Url, float.PositiveInfinity, noWrap: true, TextOverflow.Clip);
        float width = full.Lines[0].Width * 0.7f;

        var cut = Layout(Url, width, noWrap: true, TextOverflow.Ellipsis);
        var byWord = Layout(Url, width, noWrap: false, TextOverflow.Ellipsis, maxLines: 1);

        await Assert.That(cut.Lines.Count).IsEqualTo(1);
        await Assert.That(cut.Lines[0].Width).IsLessThanOrEqualTo(width);
        // Character cut keeps more of the text than the word-boundary cut.
        await Assert.That(cut.Lines[0].TextLength).IsGreaterThan(byWord.Lines[0].TextLength);
        await Assert.That(cut.Lines[0].TextLength).IsGreaterThan("https://github.com/".Length);
    }

    [Test]
    public async Task NoWrap_StillBreaksAtHardNewlines()
    {
        var layout = Layout("first line\nsecond", 1000f, noWrap: true, TextOverflow.Clip);

        await Assert.That(layout.Lines.Count).IsEqualTo(2);
    }
}
