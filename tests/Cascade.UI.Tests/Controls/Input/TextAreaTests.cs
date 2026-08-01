#pragma warning disable CA2000, CA1812

using System;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class TextAreaTests
{
    private static Bindable<string> CreateStringBinding(string initial)
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    private static TextArea CreateDefaultArea()
    {
        return new TextArea(CreateStringBinding("value"));
    }

    private sealed class TestNode : Node
    {
    }

    [Test]
    public async Task Constructor_SetsBindingAndLines()
    {
        var binding = CreateStringBinding("notes");
        var area = new TextArea(binding, placeholder: "Enter", minLines: 2, maxLines: 5, label: "Notes");

        string value = area.Value.Value;
        string placeholder = area.Placeholder.Value;
        int minLines = area.MinLines;
        int? maxLines = area.MaxLines;
        string label = area.Label.Value;

        await Assert.That(value).IsEqualTo("notes");
        await Assert.That(placeholder).IsEqualTo("Enter");
        await Assert.That(minLines).IsEqualTo(2);
        await Assert.That(maxLines).IsEqualTo(5);
        await Assert.That(label).IsEqualTo("Notes");
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var area = CreateDefaultArea()
            .Validate(text => string.IsNullOrWhiteSpace(text)
                ? ValidationResult.Error("Required")
                : ValidationResult.Ok);

        int rules = area.ValidationRules.Count;
        await Assert.That(rules).IsEqualTo(1);
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var area = CreateDefaultArea()
            .Validate(_ => ValidationResult.Ok)
            .Validate(_ => ValidationResult.Error("Too long"));

        var result = area.RunValidation();
        string? message = result.Message;

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(message).IsEqualTo("Too long");
    }

    [Test]
    public async Task MaxLength_ModifierStoresValue()
    {
        var area = CreateDefaultArea().MaxLength(500);

        int? maxLength = area.MaxLengthValue;
        await Assert.That(maxLength).IsEqualTo(500);
    }

    [Test]
    public async Task ShowCharacterCount_StoresStyle()
    {
        var area = CreateDefaultArea().ShowCharacterCount(CountStyle.Fraction);

        var style = area.CharacterCountStyle;
        await Assert.That(style).IsEqualTo(CountStyle.Fraction);
    }

    [Test]
    public async Task Lines_SetsFixedLines()
    {
        var area = CreateDefaultArea().Lines(4);

        int minLines = area.MinLines;
        int? maxLines = area.MaxLines;
        int? fixedLines = area.FixedLines;

        await Assert.That(minLines).IsEqualTo(4);
        await Assert.That(maxLines).IsEqualTo(4);
        await Assert.That(fixedLines).IsEqualTo(4);
    }

    [Test]
    public async Task AutoGrow_SetsFlagsAndBounds()
    {
        var area = CreateDefaultArea().AutoGrow(minLines: 2, maxLines: 6);

        bool enabled = area.AutoGrowEnabled;
        int? minLines = area.AutoGrowMinLines;
        int? maxLines = area.AutoGrowMaxLines;

        await Assert.That(enabled).IsTrue();
        await Assert.That(minLines).IsEqualTo(2);
        await Assert.That(maxLines).IsEqualTo(6);
    }

    [Test]
    public async Task Resizable_SetsDirection()
    {
        var area = CreateDefaultArea().Resizable(ResizeDirection.Both);

        var direction = area.ResizeDir;
        await Assert.That(direction).IsEqualTo(ResizeDirection.Both);
    }

    [Test]
    public async Task PrefixSuffix_ModifiersStoreNodes()
    {
        var prefix = new TestNode();
        var suffix = new TestNode();
        var area = CreateDefaultArea()
            .Prefix(prefix)
            .Suffix(suffix);

        var storedPrefix = area.PrefixNode;
        var storedSuffix = area.SuffixNode;

        await Assert.That(storedPrefix).IsEqualTo(prefix);
        await Assert.That(storedSuffix).IsEqualTo(suffix);
    }

    [Test]
    public async Task Debounce_ModifierStoresDelay()
    {
        var area = CreateDefaultArea().Debounce(TimeSpan.FromMilliseconds(400));

        TimeSpan? delay = area.DebounceDelay;
        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(400));
    }

    [Test]
    public async Task Disabled_ReadOnly_SetFlags()
    {
        var area = CreateDefaultArea()
            .Disabled()
            .ReadOnly();

        bool disabled = area.IsDisabled;
        bool readOnly = area.IsReadOnly;

        await Assert.That(disabled).IsTrue();
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var area = CreateDefaultArea().AccessibleLabel("Notes input");

        string label = area.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Notes input");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var area = CreateDefaultArea();

        var chained = area
            .MaxLength(200)
            .ShowCharacterCount(CountStyle.Remaining)
            .AutoGrow()
            .Resizable(ResizeDirection.Vertical)
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Description");

        bool same = ReferenceEquals(area, chained);
        await Assert.That(same).IsTrue();
    }
}
