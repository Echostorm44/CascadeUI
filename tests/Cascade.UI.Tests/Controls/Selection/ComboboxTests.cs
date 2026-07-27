#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ComboboxTests
{
    private static Bindable<string> CreateStringBinding(string initial)
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    private static IReadOnlyList<SelectOption<string>> CreateOptions()
    {
        return new List<SelectOption<string>>
        {
            new("red", "Red"),
            new("green", "Green"),
            new("blue", "Blue")
        };
    }

    [Test]
    public async Task Constructor_StoresValue()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding);

        string value = combobox.Value.Value;
        await Assert.That(value).IsEqualTo("red");
    }

    [Test]
    public async Task Constructor_StoresStaticOptions()
    {
        var binding = CreateStringBinding("red");
        var options = CreateOptions();
        var combobox = new Combobox<string>(binding, options);

        int count = combobox.StaticOptions!.Count;
        var expected = 3;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_NullOptionsWhenNotProvided()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding);

        bool isNull = combobox.StaticOptions == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_StoresPlaceholder()
    {
        var binding = CreateStringBinding("");
        var combobox = new Combobox<string>(binding, placeholder: "Type a color");

        string placeholder = combobox.Placeholder.Value;
        await Assert.That(placeholder).IsEqualTo("Type a color");
    }

    [Test]
    public async Task Constructor_StoresLabel()
    {
        var binding = CreateStringBinding("");
        var combobox = new Combobox<string>(binding, label: "Color");

        string label = combobox.Label.Value;
        await Assert.That(label).IsEqualTo("Color");
    }

    [Test]
    public async Task Options_SetsAsyncSource()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding)
            .Options(query => Task.FromResult<IEnumerable<SelectOption<string>>>(
                new[] { new SelectOption<string>(query, query) }));

        bool hasSource = combobox.AsyncOptionSource != null;
        await Assert.That(hasSource).IsTrue();
    }

    [Test]
    public async Task Debounce_SetsDelay()
    {
        var binding = CreateStringBinding("red");
        var delay = TimeSpan.FromMilliseconds(300);
        var combobox = new Combobox<string>(binding).Debounce(delay);

        var actual = combobox.DebounceDelay;
        await Assert.That(actual).IsEqualTo(delay);
    }

    [Test]
    public async Task RenderOption_SetsRenderer()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding)
            .RenderOption(v => Node.Empty);

        bool hasRenderer = combobox.OptionRenderer != null;
        await Assert.That(hasRenderer).IsTrue();
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding)
            .Validate(v => string.IsNullOrEmpty(v) ? ValidationResult.Error("Required") : ValidationResult.Ok);

        int count = combobox.ValidationRules.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding)
            .ValidateOn(ValidationTrigger.Debounced);

        var trigger = combobox.ValidationTriggerMode;
        var expected = ValidationTrigger.Debounced;
        await Assert.That(trigger).IsEqualTo(expected);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding).Disabled();

        bool disabled = combobox.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding).ReadOnly();

        bool readOnly = combobox.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding)
            .AccessibleLabel("Color picker");

        string label = combobox.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Color picker");
    }

    [Test]
    public async Task RunValidation_PassesWithNoRules()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding);

        var result = combobox.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var binding = CreateStringBinding("");
        var combobox = new Combobox<string>(binding)
            .Validate(v => string.IsNullOrEmpty(v) ? ValidationResult.Error("Required") : ValidationResult.Ok);

        var result = combobox.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateStringBinding("red");
        var combobox = new Combobox<string>(binding);

        var result = combobox
            .Debounce(TimeSpan.FromMilliseconds(200))
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Test");

        bool same = ReferenceEquals(combobox, result);
        await Assert.That(same).IsTrue();
    }
}
