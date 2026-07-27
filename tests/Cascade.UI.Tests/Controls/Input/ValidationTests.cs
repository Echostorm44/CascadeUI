#pragma warning disable CA2000, CA1812

using System;
using System.Linq;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class ValidationTests
{
    private static Bindable<string> CreateStringBinding(string initial)
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    private static TextInput CreateInput()
    {
        return new TextInput(CreateStringBinding("value"));
    }

    [Test]
    public async Task ValidationTrigger_ContainsExpectedValues()
    {
        var values = Enum.GetValues<ValidationTrigger>();
        var expected = new[]
        {
            ValidationTrigger.Immediate,
            ValidationTrigger.Debounced,
            ValidationTrigger.Blur,
            ValidationTrigger.Submit,
            ValidationTrigger.Manual
        };

        bool containsAll = expected.All(values.Contains);
        int count = values.Length;

        await Assert.That(containsAll).IsTrue();
        await Assert.That(count).IsEqualTo(5);
    }

    [Test]
    public async Task ValidationResult_Ok_IsValid()
    {
        var result = ValidationResult.Ok;

        bool isValid = result.IsValid;
        string? message = result.Message;

        await Assert.That(isValid).IsTrue();
        await Assert.That(message).IsNull();
    }

    [Test]
    public async Task ValidationResult_Error_IsNotValid()
    {
        var result = ValidationResult.Error("Missing");

        bool isValid = result.IsValid;
        string? message = result.Message;

        await Assert.That(isValid).IsFalse();
        await Assert.That(message).IsEqualTo("Missing");
    }

    [Test]
    public async Task ValidationResult_Warning_IsValidButHasMessage()
    {
        var result = ValidationResult.Warning("Check this");

        bool isValid = result.IsValid;
        string? message = result.Message;

        await Assert.That(isValid).IsTrue();
        await Assert.That(message).IsEqualTo("Check this");
    }

    [Test]
    public async Task TextInput_RunValidation_UsesRules()
    {
        var input = CreateInput()
            .Validate(_ => ValidationResult.Error("First"))
            .Validate(_ => ValidationResult.Error("Second"));

        var result = input.RunValidation();
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("First");
    }

    [Test]
    public async Task TextArea_RunValidation_SucceedsWithOk()
    {
        var area = new TextArea(CreateStringBinding("notes"))
            .Validate(_ => ValidationResult.Ok);

        var result = area.RunValidation();

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task NumberInput_RunValidation_FailsOnRule()
    {
        var input = new NumberInput<double>(new Bindable<double>(-1, _ => { }))
            .Validate(value => value >= 0 ? ValidationResult.Ok : ValidationResult.Error("Negative"));

        var result = input.RunValidation(-1);
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("Negative");
    }

    [Test]
    public async Task PinInput_RunValidation_FailsOnLength()
    {
        var pin = new PinInput(CreateStringBinding("12"), length: 4)
            .Validate(value => value.Length == 4 ? ValidationResult.Ok : ValidationResult.Error("Length"));

        var result = pin.RunValidation();
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("Length");
    }
}
