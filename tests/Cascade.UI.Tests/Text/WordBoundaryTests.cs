namespace Cascade.UI.Tests;

/// <summary>
/// Tests for word boundary detection — Latin, CJK, emoji, and mixed scripts.
/// </summary>
public class WordBoundaryTests
{
    // ── Latin words ─────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_Latin_FindsWordStart()
    {
        var doc = new TextDocument("Hello World");
        int result = TextBoundary.PreviousWordBoundary(doc, 11);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_FromMiddleOfWord_FindsStart()
    {
        var doc = new TextDocument("Hello World");
        int result = TextBoundary.PreviousWordBoundary(doc, 8);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(6);
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_FromWhitespace_FindsPreviousWord()
    {
        var doc = new TextDocument("Hello   World");
        int result = TextBoundary.PreviousWordBoundary(doc, 6);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_AtStart_ReturnsZero()
    {
        var doc = new TextDocument("Hello");
        int result = TextBoundary.PreviousWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(0);
    }

    [TUnit.Core.Test]
    public async Task NextWordBoundary_Latin_FindsWordEnd()
    {
        var doc = new TextDocument("Hello World");
        int result = TextBoundary.NextWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task NextWordBoundary_FromMiddleOfWord_FindsEnd()
    {
        var doc = new TextDocument("Hello World");
        int result = TextBoundary.NextWordBoundary(doc, 2);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(5);
    }

    [TUnit.Core.Test]
    public async Task NextWordBoundary_FromWhitespace_SkipsToEndOfNextWord()
    {
        var doc = new TextDocument("Hello   World");
        int result = TextBoundary.NextWordBoundary(doc, 5);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(13);
    }

    [TUnit.Core.Test]
    public async Task NextWordBoundary_AtEnd_ReturnsLength()
    {
        var doc = new TextDocument("Hello");
        int result = TextBoundary.NextWordBoundary(doc, 5);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(5);
    }

    // ── WordAt ──────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WordAt_InMiddleOfWord_ReturnsWordRange()
    {
        var doc = new TextDocument("Hello World");
        var (start, end) = TextBoundary.WordAt(doc, 7);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(6);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(11);
    }

    [TUnit.Core.Test]
    public async Task WordAt_OnWhitespace_ReturnsWhitespaceRange()
    {
        var doc = new TextDocument("Hello   World");
        var (start, end) = TextBoundary.WordAt(doc, 6);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(5);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(8);
    }

    [TUnit.Core.Test]
    public async Task WordAt_EmptyDocument_ReturnsZeroRange()
    {
        var doc = new TextDocument();
        var (start, end) = TextBoundary.WordAt(doc, 0);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(0);
    }

    // ── CJK (every character is a word) ─────────────────────────────────

    [TUnit.Core.Test]
    public async Task NextWordBoundary_CJK_AdvancesOneCharacter()
    {
        // 你好世界 = "Hello World" in Chinese
        var doc = new TextDocument("\u4F60\u597D\u4E16\u754C");
        int result = TextBoundary.NextWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(1);
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_CJK_RetreatOneCharacter()
    {
        var doc = new TextDocument("\u4F60\u597D\u4E16\u754C");
        int result = TextBoundary.PreviousWordBoundary(doc, 3);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2);
    }

    [TUnit.Core.Test]
    public async Task WordAt_CJK_ReturnsSingleCharacter()
    {
        var doc = new TextDocument("\u4F60\u597D\u4E16\u754C");
        var (start, end) = TextBoundary.WordAt(doc, 1);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(2);
    }

    // ── Emoji ───────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WordAt_Emoji_ReturnsSingleEmoji()
    {
        // 😀😂 = two emoji (each is a surrogate pair)
        var doc = new TextDocument("\U0001F600\U0001F602");
        var (start, end) = TextBoundary.WordAt(doc, 0);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(2); // 2 UTF-16 code units for surrogate pair
    }

    [TUnit.Core.Test]
    public async Task NextWordBoundary_Emoji_AdvancesOneEmoji()
    {
        var doc = new TextDocument("\U0001F600\U0001F602");
        int result = TextBoundary.NextWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2); // past the first surrogate pair
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_Emoji_RetreatsOneEmoji()
    {
        var doc = new TextDocument("\U0001F600\U0001F602");
        int result = TextBoundary.PreviousWordBoundary(doc, 4);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2); // start of second emoji
    }

    // ── Mixed scripts ───────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task NextWordBoundary_MixedLatinCJK_StopsAtBoundary()
    {
        // "Hello你好" — Latin word followed by CJK
        var doc = new TextDocument("Hello\u4F60\u597D");
        int result = TextBoundary.NextWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(5); // end of "Hello"
    }

    [TUnit.Core.Test]
    public async Task PreviousWordBoundary_MixedCJKLatin_StopsAtBoundary()
    {
        // "你好Hello" — CJK followed by Latin
        var doc = new TextDocument("\u4F60\u597DHello");
        int result = TextBoundary.PreviousWordBoundary(doc, 7);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2); // start of "Hello"
    }

    // ── Numbers mixed with letters ──────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WordAt_AlphanumericIdentifier_TreatsAsOneWord()
    {
        var doc = new TextDocument("var1 = 42");
        var (start, end) = TextBoundary.WordAt(doc, 2);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(4);
    }

    // ── Punctuation ─────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task NextWordBoundary_Punctuation_TreatedAsSeparateWord()
    {
        var doc = new TextDocument("Hello, World");
        int result = TextBoundary.NextWordBoundary(doc, 0);
        await TUnit.Assertions.Assert.That(result).IsEqualTo(5); // stops at comma
    }

    // ── Underscore treated as letter ────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WordAt_Underscore_TreatedAsPartOfWord()
    {
        var doc = new TextDocument("my_variable = 10");
        var (start, end) = TextBoundary.WordAt(doc, 5);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(0);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(11);
    }

    // ── Hiragana ────────────────────────────────────────────────────────

    [TUnit.Core.Test]
    public async Task WordAt_Hiragana_ReturnsSingleCharacter()
    {
        // Hiragana あいう is treated as CJK (each char is a word)
        var doc = new TextDocument("\u3042\u3044\u3046");
        var (start, end) = TextBoundary.WordAt(doc, 1);
        await TUnit.Assertions.Assert.That(start).IsEqualTo(1);
        await TUnit.Assertions.Assert.That(end).IsEqualTo(2);
    }
}
