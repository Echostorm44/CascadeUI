#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class NumberInputTests
{
    private static Bindable<double> CreateDoubleBinding(double initial)
    {
        double captured = initial;
        return new Bindable<double>(captured, v => { captured = v; });
    }

    private static NumberInput<double> CreateDefaultInput()
    {
        return new NumberInput<double>(CreateDoubleBinding(1.5));
    }

    private sealed class TestNode : Node
    {
    }

    [Test]
    public async Task Constructor_SetsBoundariesAndFormat()
    {
        var binding = CreateDoubleBinding(2.0);
        var input = new NumberInput<double>(
            binding,
            min: 0.0,
            max: 10.0,
            step: 0.5,
            format: "N2",
            placeholder: "0",
            label: "Quantity");

        double value = input.Value.Value;
        double? min = input.Min;
        double? max = input.Max;
        double? step = input.Step;
        string? format = input.Format;
        string placeholder = input.Placeholder.Value;
        string label = input.Label.Value;

        await Assert.That(value).IsEqualTo(2.0);
        await Assert.That(min).IsEqualTo(0.0);
        await Assert.That(max).IsEqualTo(10.0);
        await Assert.That(step).IsEqualTo(0.5);
        await Assert.That(format).IsEqualTo("N2");
        await Assert.That(placeholder).IsEqualTo("0");
        await Assert.That(label).IsEqualTo("Quantity");
    }

    [Test]
    public async Task Clamp_RespectsMinAndMax()
    {
        var input = new NumberInput<double>(CreateDoubleBinding(5.0), min: 0.0, max: 10.0);

        double below = input.Clamp(-5.0);
        double above = input.Clamp(20.0);
        double inside = input.Clamp(6.0);

        await Assert.That(below).IsEqualTo(0.0);
        await Assert.That(above).IsEqualTo(10.0);
        await Assert.That(inside).IsEqualTo(6.0);
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var input = CreateDefaultInput()
            .Validate(value => value >= 0 ? ValidationResult.Ok : ValidationResult.Error("Negative"));

        int rules = input.ValidationRules.Count;
        await Assert.That(rules).IsEqualTo(1);
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var input = CreateDefaultInput()
            .Validate(_ => ValidationResult.Ok)
            .Validate(_ => ValidationResult.Error("Out of range"));

        var result = input.RunValidation(2.0);
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("Out of range");
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var input = CreateDefaultInput().ValidateOn(ValidationTrigger.Immediate);

        var trigger = input.ValidationTriggerMode;
        await Assert.That(trigger).IsEqualTo(ValidationTrigger.Immediate);
    }

    [Test]
    public async Task StepperButtons_SetsPosition()
    {
        var input = CreateDefaultInput().StepperButtons(StepperPosition.Split);

        var position = input.StepperPos;
        await Assert.That(position).IsEqualTo(StepperPosition.Split);
    }

    [Test]
    public async Task PrefixSuffix_ModifiersStoreNodes()
    {
        var prefix = new TestNode();
        var suffix = new TestNode();
        var input = CreateDefaultInput()
            .Prefix(prefix)
            .Suffix(suffix);

        var storedPrefix = input.PrefixNode;
        var storedSuffix = input.SuffixNode;

        await Assert.That(storedPrefix).IsEqualTo(prefix);
        await Assert.That(storedSuffix).IsEqualTo(suffix);
    }

    [Test]
    public async Task Disabled_ReadOnly_SetFlags()
    {
        var input = CreateDefaultInput()
            .Disabled()
            .ReadOnly();

        bool disabled = input.IsDisabled;
        bool readOnly = input.IsReadOnly;

        await Assert.That(disabled).IsTrue();
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var input = CreateDefaultInput().AccessibleLabel("Amount");

        string label = input.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Amount");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var input = CreateDefaultInput();

        var chained = input
            .Validate(_ => ValidationResult.Ok)
            .ValidateOn(ValidationTrigger.Submit)
            .StepperButtons(StepperPosition.Right)
            .Prefix(Node.Empty)
            .Suffix(Node.Empty)
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Chained");

        bool same = ReferenceEquals(input, chained);
        await Assert.That(same).IsTrue();
    }
}
