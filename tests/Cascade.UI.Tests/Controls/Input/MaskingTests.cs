#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class MaskingTests
{
    [Test]
    public async Task PhonePattern_FormatsValue()
    {
        var pattern = MaskPattern.PhoneUs;

        string formatted = pattern.Apply("5558675309");
        var expected = "(555) 867-5309";

        await Assert.That(formatted).IsEqualTo(expected);
    }

    [Test]
    public async Task SsnPattern_FormatsValue()
    {
        var pattern = MaskPattern.SSN;

        string formatted = pattern.Apply("123456789");
        var expected = "123-45-6789";

        await Assert.That(formatted).IsEqualTo(expected);
    }

    [Test]
    public async Task DatePattern_FormatsValue()
    {
        var pattern = MaskPattern.Date;

        string formatted = pattern.Apply("01012026");
        var expected = "01/01/2026";

        await Assert.That(formatted).IsEqualTo(expected);
    }

    [Test]
    public async Task CreditCardPattern_InputSlotCountMatchesDigits()
    {
        var pattern = MaskPattern.CreditCard;

        int slots = pattern.InputSlotCount;
        var expected = 16;

        await Assert.That(slots).IsEqualTo(expected);
    }

    [Test]
    public async Task PostalCodePattern_StripsLiterals()
    {
        var pattern = MaskPattern.PostalCodeUs;
        string formatted = "12345-6789";

        string raw = pattern.StripLiterals(formatted);
        var expected = "123456789";

        await Assert.That(raw).IsEqualTo(expected);
    }

    [Test]
    public async Task RegisterAndLookup_CustomPattern()
    {
        var custom = new MaskPattern("AA-###");
        string name = $"TestPattern-{Guid.NewGuid()}";
        MaskPattern.Register(name, custom);

        var retrieved = MaskPattern.GetRegistered(name);
        await Assert.That(retrieved).IsEqualTo(custom);
    }

    [Test]
    public async Task CustomMask_FormatParseRoundtrip()
    {
        var mask = new CustomMask(
            format: raw => $"[{raw}]",
            parse: formatted => formatted.Trim('[', ']'),
            allowedChars: CharSet.AlphaNumeric,
            placeholder: 'X');

        string formatted = mask.Format("ABC123");
        string raw = mask.Parse(formatted);
        char placeholder = mask.Placeholder;

        await Assert.That(formatted).IsEqualTo("[ABC123]");
        await Assert.That(raw).IsEqualTo("ABC123");
        await Assert.That(placeholder).IsEqualTo('X');
    }

    [Test]
    public async Task IsSlotMatch_ValidatesCharacters()
    {
        bool digitMatch = MaskPattern.IsSlotMatch('#', '5');
        bool letterMatch = MaskPattern.IsSlotMatch('A', 'Z');
        bool alphanumericMatch = MaskPattern.IsSlotMatch('*', '7');
        bool optionalMatch = MaskPattern.IsSlotMatch('?', '!');
        bool literalMatch = MaskPattern.IsSlotMatch('-', '-');

        await Assert.That(digitMatch).IsTrue();
        await Assert.That(letterMatch).IsTrue();
        await Assert.That(alphanumericMatch).IsTrue();
        await Assert.That(optionalMatch).IsTrue();
        await Assert.That(literalMatch).IsTrue();
    }

    [Test]
    public async Task Apply_PadsWithPlaceholderForMissingInput()
    {
        var pattern = new MaskPattern("##/##", placeholderChar: '_');

        string formatted = pattern.Apply("12");
        var expected = "12/__";

        await Assert.That(formatted).IsEqualTo(expected);
    }
}
