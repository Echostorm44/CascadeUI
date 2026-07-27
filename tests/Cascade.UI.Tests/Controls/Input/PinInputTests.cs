#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class PinInputTests
{
    private static Bindable<string> CreateStringBinding(string initial)
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    private static PinInput CreateDefaultPin()
    {
        return new PinInput(CreateStringBinding("1234"), length: 4);
    }

    [Test]
    public async Task Constructor_SetsValueLengthAndLabel()
    {
        var binding = CreateStringBinding("0000");
        var pin = new PinInput(binding, length: 6, label: "OTP");

        string value = pin.Value.Value;
        int length = pin.Length;
        string label = pin.Label.Value;

        await Assert.That(value).IsEqualTo("0000");
        await Assert.That(length).IsEqualTo(6);
        await Assert.That(label).IsEqualTo("OTP");
    }

    [Test]
    public async Task Numeric_ModifierEnforcesDigits()
    {
        var pin = CreateDefaultPin().Numeric();

        bool acceptsDigit = pin.AcceptsCharacter('5');
        bool acceptsLetter = pin.AcceptsCharacter('A');

        await Assert.That(acceptsDigit).IsTrue();
        await Assert.That(acceptsLetter).IsFalse();
    }

    [Test]
    public async Task Masked_ModifierSetsFlag()
    {
        var pin = CreateDefaultPin().Masked();

        bool masked = pin.IsMasked;
        await Assert.That(masked).IsTrue();
    }

    [Test]
    public async Task AutoSubmit_SetsHandler()
    {
        void Handler(string value)
        {
        }

        var pin = CreateDefaultPin().AutoSubmit(Handler);

        var handler = pin.AutoSubmitHandler;
        await Assert.That(handler).IsNotNull();
    }

    [Test]
    public async Task Separator_AddsUniquePosition()
    {
        var pin = CreateDefaultPin()
            .Separator(2)
            .Separator(2);

        int count = pin.SeparatorPositions.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task HandlePaste_DistributesCharacters()
    {
        var pin = new PinInput(CreateStringBinding(string.Empty), length: 3).Numeric();

        string result = pin.HandlePaste("12A34");
        await Assert.That(result).IsEqualTo("123");
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var pin = CreateDefaultPin()
            .Validate(value => value.Length == 4 ? ValidationResult.Ok : ValidationResult.Error("Length"));

        int rules = pin.ValidationRules.Count;
        await Assert.That(rules).IsEqualTo(1);
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var pin = CreateDefaultPin()
            .Validate(_ => ValidationResult.Ok)
            .Validate(_ => ValidationResult.Error("Invalid"));

        var result = pin.RunValidation();
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("Invalid");
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var pin = CreateDefaultPin().ValidateOn(ValidationTrigger.Submit);

        var trigger = pin.ValidationTriggerMode;
        await Assert.That(trigger).IsEqualTo(ValidationTrigger.Submit);
    }

    [Test]
    public async Task Disabled_ReadOnly_SetFlags()
    {
        var pin = CreateDefaultPin()
            .Disabled()
            .ReadOnly();

        bool disabled = pin.IsDisabled;
        bool readOnly = pin.IsReadOnly;

        await Assert.That(disabled).IsTrue();
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var pin = CreateDefaultPin().AccessibleLabel("Verification code");

        string label = pin.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Verification code");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var pin = CreateDefaultPin();

        var chained = pin
            .Numeric()
            .Masked()
            .Separator(2)
            .Validate(_ => ValidationResult.Ok)
            .ValidateOn(ValidationTrigger.Blur)
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Chain");

        bool same = ReferenceEquals(pin, chained);
        await Assert.That(same).IsTrue();
    }
}
