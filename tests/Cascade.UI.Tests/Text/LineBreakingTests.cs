namespace Cascade.UI.Tests;

/// <summary>
/// Tests for the Unicode line break algorithm implementation.
/// </summary>
public class LineBreakingTests
{
    [TUnit.Core.Test]
    public async Task BreaksAtSpaces()
    {
        var breaks = LineBreaker.FindBreakOpportunities("Hello World".AsSpan());

        // Should have an optional break at position 6 (after space, before 'W')
        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks).HasCount().EqualTo(1);
        await TUnit.Assertions.Assert.That(optionalBreaks[0].Position).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task NoBreakInMiddleOfWord()
    {
        var breaks = LineBreaker.FindBreakOpportunities("NoBreaksHere".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks).HasCount().EqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task CjkCharactersCanBreakBetweenEachPair()
    {
        // 你好世界 — four CJK characters; break between each pair
        var breaks = LineBreaker.FindBreakOpportunities("你好世界".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks.Count).IsGreaterThanOrEqualTo(3);
    }

    [TUnit.Core.Test]
    public async Task RespectsNonBreakingSpace()
    {
        // "Non\u00A0breaking" — NBSP should not allow a break
        var breaks = LineBreaker.FindBreakOpportunities("Non\u00A0breaking".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks).HasCount().EqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task MandatoryBreakAtNewline()
    {
        var breaks = LineBreaker.FindBreakOpportunities("Line1\nLine2".AsSpan());

        var mandatory = breaks.Where(b => b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(mandatory).HasCount().EqualTo(1);
        await TUnit.Assertions.Assert.That(mandatory[0].Position).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task CrLfIsSingleMandatoryBreak()
    {
        var breaks = LineBreaker.FindBreakOpportunities("Line1\r\nLine2".AsSpan());

        var mandatory = breaks.Where(b => b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(mandatory).HasCount().EqualTo(1);
        await TUnit.Assertions.Assert.That(mandatory[0].Position).IsEqualTo(7); // after \r\n
    }

    [TUnit.Core.Test]
    public async Task BreaksAfterSoftHyphen()
    {
        // Soft hyphen (\u00AD) is classified as BA (break after)
        // "un\u00ADbreak" should have a break opportunity after the soft hyphen
        var breaks = LineBreaker.FindBreakOpportunities("un\u00ADbreak".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks.Count).IsGreaterThanOrEqualTo(1);
    }

    [TUnit.Core.Test]
    public async Task BreaksAtZeroWidthSpace()
    {
        // ZWSP (\u200B) should always allow break after
        var breaks = LineBreaker.FindBreakOpportunities("Hello\u200BWorld".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        await TUnit.Assertions.Assert.That(optionalBreaks.Count).IsGreaterThanOrEqualTo(1);
    }

    [TUnit.Core.Test]
    public async Task EmptyText_ReturnsNoBreaks()
    {
        var breaks = LineBreaker.FindBreakOpportunities(ReadOnlySpan<char>.Empty);

        await TUnit.Assertions.Assert.That(breaks).HasCount().EqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task MultipleSpaces_BreakAfterEachWordGap()
    {
        var breaks = LineBreaker.FindBreakOpportunities("Hello beautiful World".AsSpan());

        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        // Break after "Hello " (pos 6) and after "beautiful " (pos 16)
        await TUnit.Assertions.Assert.That(optionalBreaks.Count).IsGreaterThanOrEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task NoBreakBeforeClosingBracket()
    {
        var breaks = LineBreaker.FindBreakOpportunities("(test)".AsSpan());

        // Should not break between 't' and ')'
        var optionalBreaks = breaks.Where(b => !b.IsMandatory).ToList();
        bool hasBreakBeforeClose = optionalBreaks.Any(b => b.Position == 5);
        await TUnit.Assertions.Assert.That(hasBreakBeforeClose).IsFalse();
    }
}
