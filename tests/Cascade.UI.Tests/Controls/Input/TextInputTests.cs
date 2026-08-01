#pragma warning disable CA2000, CA1812

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

public sealed class TextInputTests
{
    private static Bindable<string> CreateStringBinding(string initial)
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    private static TextInput CreateDefaultInput()
    {
        return new TextInput(CreateStringBinding("value"));
    }

    private sealed class TestNode : Node
    {
    }

    [Test]
    public async Task Constructor_Bindable_SetsProperties()
    {
        var binding = CreateStringBinding("hello");
        var input = new TextInput(binding, placeholder: "Enter", inputType: InputType.Email, label: "Email");

        string value = input.Value.Value;
        string placeholder = input.Placeholder.Value;
        var inputType = input.InputType;
        string label = input.Label.Value;

        await Assert.That(value).IsEqualTo("hello");
        await Assert.That(placeholder).IsEqualTo("Enter");
        await Assert.That(inputType).IsEqualTo(InputType.Email);
        await Assert.That(label).IsEqualTo("Email");
    }

    [Test]
    public async Task MaxLength_ModifierStoresValue()
    {
        var input = CreateDefaultInput().MaxLength(120);

        var maxLength = input.MaxLengthValue;
        await Assert.That(maxLength).IsEqualTo(120);
    }

    [Test]
    public async Task Prefix_ModifierStoresNode()
    {
        var prefix = new TestNode();
        var input = CreateDefaultInput().Prefix(prefix);

        var stored = input.PrefixNode;
        await Assert.That(stored).IsEqualTo(prefix);
    }

    [Test]
    public async Task Suffix_ModifierStoresNode()
    {
        var suffix = new TestNode();
        var input = CreateDefaultInput().Suffix(suffix);

        var stored = input.SuffixNode;
        await Assert.That(stored).IsEqualTo(suffix);
    }

    [Test]
    public async Task Disabled_ModifierSetsFlag()
    {
        var input = CreateDefaultInput().Disabled();

        bool disabled = input.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_ModifierSetsFlag()
    {
        var input = CreateDefaultInput().ReadOnly();

        bool readOnly = input.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task Placeholder_ModifierOverridesValue()
    {
        var input = CreateDefaultInput().Placeholder("Override");

        string placeholder = input.Placeholder.Value;
        await Assert.That(placeholder).IsEqualTo("Override");
    }

    [Test]
    public async Task Debounce_ModifierStoresDelay()
    {
        var input = CreateDefaultInput().Debounce(TimeSpan.FromMilliseconds(250));

        TimeSpan? delay = input.DebounceDelay;
        await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(250));
    }

    [Test]
    public async Task AccessibleLabel_ModifierStoresValue()
    {
        var input = CreateDefaultInput().AccessibleLabel("Screen reader");

        string label = input.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Screen reader");
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var input = CreateDefaultInput()
            .Validate(v => string.IsNullOrWhiteSpace(v) ? ValidationResult.Error("Required") : ValidationResult.Ok);

        int count = input.ValidationRules.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var input = CreateDefaultInput()
            .Validate(_ => ValidationResult.Ok)
            .Validate(_ => ValidationResult.Error("Bad"));

        var result = input.RunValidation();
        bool isValid = result.IsValid;
        string? message = result.Message;

        await Assert.That(isValid).IsFalse();
        await Assert.That(message).IsEqualTo("Bad");
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var input = CreateDefaultInput().ValidateOn(ValidationTrigger.Submit);

        var trigger = input.ValidationTriggerMode;
        await Assert.That(trigger).IsEqualTo(ValidationTrigger.Submit);
    }

    [Test]
    public async Task MaskPattern_ModifierStoresPattern()
    {
        var pattern = MaskPattern.PhoneUs;
        var input = CreateDefaultInput().Mask(pattern);

        var stored = input.MaskPatternValue;
        await Assert.That(stored).IsEqualTo(pattern);
    }

    [Test]
    public async Task MaskString_ModifierCreatesPattern()
    {
        var input = CreateDefaultInput().Mask("###-##");

        string pattern = input.MaskPatternValue!.Pattern;
        await Assert.That(pattern).IsEqualTo("###-##");
    }

    [Test]
    public async Task MaskCustom_ModifierStoresMask()
    {
        var custom = new CustomMask(
            format: raw => raw.ToUpperInvariant(),
            parse: formatted => formatted,
            allowedChars: CharSet.Alpha);

        var input = CreateDefaultInput().Mask(custom);

        var stored = input.CustomMaskValue;
        await Assert.That(stored).IsEqualTo(custom);
    }

    [Test]
    public async Task Autocomplete_SyncSourceStored()
    {
        IEnumerable<string> Source(string query)
        {
            return new[] { query };
        }

        var input = CreateDefaultInput().Autocomplete(Source);

        var stored = input.AutocompleteSyncSource;
        var result = new List<string>(stored!("a"));
        string first = result[0];

        await Assert.That(first).IsEqualTo("a");
    }

    [Test]
    public async Task Autocomplete_AsyncSourceStored()
    {
        Task<IEnumerable<string>> Source(string query)
        {
            IEnumerable<string> values = new[] { query };
            return Task.FromResult(values);
        }

        var input = CreateDefaultInput().Autocomplete(Source);

        var stored = input.AutocompleteAsyncSource;
        var result = new List<string>(await stored!("b"));
        string first = result[0];

        await Assert.That(first).IsEqualTo("b");
    }

    [Test]
    public async Task Autocomplete_TypedSourceStored()
    {
        Task<IEnumerable<int>> Source(string query)
        {
            IEnumerable<int> values = new[] { query.Length };
            return Task.FromResult(values);
        }

        Node Render(int value)
        {
            return new TestNode();
        }

        string Select(int value)
        {
            return value.ToString();
        }

        var input = CreateDefaultInput().Autocomplete(Source, Render, Select);

        var typed = input.TypedAutocompleteSource;
        bool isTypedAutocomplete = typed is TypedAutocomplete<int>;

        await Assert.That(isTypedAutocomplete).IsTrue();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var input = CreateDefaultInput();

        var chained = input
            .MaxLength(10)
            .Prefix(Node.Empty)
            .Suffix(Node.Empty)
            .Disabled()
            .ReadOnly()
            .Placeholder("x")
            .Debounce(TimeSpan.FromMilliseconds(200))
            .AccessibleLabel("Label");

        bool same = ReferenceEquals(input, chained);
        await Assert.That(same).IsTrue();
    }
}
