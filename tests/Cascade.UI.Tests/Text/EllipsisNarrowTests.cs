namespace Cascade.UI.Tests;

/// <summary>
/// A one-line ellipsis layout narrower than the first character plus "…" kept no characters and
/// placed the ellipsis at the full line's width — far outside MaxWidth — so a narrow button
/// painted no caption at all. The lone ellipsis now sits at the start of the line, inside the box.
/// </summary>
public class EllipsisNarrowTests
{
    private const string Caption = "Worker confirm";

    private static TextLayoutResult Layout(float maxWidth, TextAlignment alignment)
    {
        var options = new TextLayoutOptions
        {
            FontPath = System.IO.Path.Combine(AppContext.BaseDirectory, "fonts", "Inter-Regular.ttf"),
            FontSize = 17f,
            MaxWidth = maxWidth,
            MaxLines = 1,
            NoWrap = true,
            Overflow = TextOverflow.Ellipsis,
            Alignment = alignment,
        };
        return TextLayoutEngine.Layout(Caption, options);
    }

    [Test]
    [Arguments(TextAlignment.Start)]
    [Arguments(TextAlignment.Center)]
    public async Task TooNarrowForAnyCharacter_DrawsTheEllipsisInsideTheBox(TextAlignment alignment)
    {
        const float maxWidth = 20f;
        var line = Layout(maxWidth, alignment).Lines[0];

        await Assert.That(line.TextLength).IsEqualTo(0);
        await Assert.That(line.Glyphs.Count).IsEqualTo(1);
        float right = line.X + line.Glyphs[0].X + line.Glyphs[0].AdvanceWidth;
        await Assert.That(line.X + line.Glyphs[0].X).IsGreaterThanOrEqualTo(0f);
        await Assert.That(right).IsLessThanOrEqualTo(maxWidth + 0.5f);
    }

    [Test]
    public async Task RoomForAPrefix_KeepsItAndFits()
    {
        const float maxWidth = 85f;
        var line = Layout(maxWidth, TextAlignment.Center).Lines[0];

        await Assert.That(line.TextLength).IsGreaterThan(0);
        await Assert.That(line.TextLength).IsLessThan(Caption.Length);
        await Assert.That(line.Width).IsLessThanOrEqualTo(maxWidth);
        var last = line.Glyphs[^1];
        await Assert.That(last.X + last.AdvanceWidth).IsLessThanOrEqualTo(maxWidth);
    }
}
