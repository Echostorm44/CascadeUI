#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SelectTests
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
            new("a", "Alpha"),
            new("b", "Beta"),
            new("c", "Gamma")
        };
    }

    [Test]
    public async Task Constructor_FlatOptions_StoresOptions()
    {
        var binding = CreateStringBinding("a");
        var options = CreateOptions();
        var select = new Select<string>(binding, options);

        int count = select.Options!.Count;
        var expected = 3;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_FlatOptions_GroupsIsNull()
    {
        var binding = CreateStringBinding("a");
        var options = CreateOptions();
        var select = new Select<string>(binding, options);

        bool isNull = select.Groups == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_Groups_StoresGroups()
    {
        var binding = CreateStringBinding("a");
        var groups = new List<SelectGroup<string>>
        {
            new("Group 1", new[] { new SelectOption<string>("a", "Alpha") })
        };
        var select = new Select<string>(binding, groups);

        int count = select.Groups!.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_Groups_OptionsIsNull()
    {
        var binding = CreateStringBinding("a");
        var groups = new List<SelectGroup<string>>
        {
            new("Group 1", new[] { new SelectOption<string>("a", "Alpha") })
        };
        var select = new Select<string>(binding, groups);

        bool isNull = select.Options == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_StoresPlaceholder()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions(), placeholder: "Pick one");

        string placeholder = select.Placeholder.Value;
        await Assert.That(placeholder).IsEqualTo("Pick one");
    }

    [Test]
    public async Task Constructor_StoresLabel()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions(), label: "Country");

        string label = select.Label.Value;
        await Assert.That(label).IsEqualTo("Country");
    }

    [Test]
    public async Task Searchable_SetsFlag()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions()).Searchable();

        bool searchable = select.IsSearchable;
        await Assert.That(searchable).IsTrue();
    }

    [Test]
    public async Task Searchable_Async_SetsSource()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .Searchable(query => Task.FromResult<IEnumerable<SelectOption<string>>>(
                new[] { new SelectOption<string>("x", query) }));

        bool hasSource = select.AsyncSearchSource != null;
        bool searchable = select.IsSearchable;
        await Assert.That(hasSource).IsTrue();
        await Assert.That(searchable).IsTrue();
    }

    [Test]
    public async Task RenderOption_SetsRenderer()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .RenderOption(v => Node.Empty);

        bool hasRenderer = select.OptionRenderer != null;
        await Assert.That(hasRenderer).IsTrue();
    }

    [Test]
    public async Task RenderSelected_SetsRenderer()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .RenderSelected(v => Node.Empty);

        bool hasRenderer = select.SelectedRenderer != null;
        await Assert.That(hasRenderer).IsTrue();
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .Validate(v => string.IsNullOrEmpty(v) ? ValidationResult.Error("Required") : ValidationResult.Ok);

        int count = select.ValidationRules.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .ValidateOn(ValidationTrigger.Submit);

        var trigger = select.ValidationTriggerMode;
        var expected = ValidationTrigger.Submit;
        await Assert.That(trigger).IsEqualTo(expected);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions()).Disabled();

        bool disabled = select.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions()).ReadOnly();

        bool readOnly = select.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions())
            .AccessibleLabel("Country selector");

        string label = select.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Country selector");
    }

    [Test]
    public async Task RunValidation_PassesWithNoRules()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions());

        var result = select.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateStringBinding("a");
        var select = new Select<string>(binding, CreateOptions());

        var result = select
            .Searchable()
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Test");

        bool same = ReferenceEquals(select, result);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task SelectOption_StoresValueAndLabel()
    {
        var option = new SelectOption<int>(42, "Forty-two");

        int value = option.Value;
        LocKey label = option.Label;
        var expectedValue = 42;
        await Assert.That(value).IsEqualTo(expectedValue);
        await Assert.That(label).IsEqualTo("Forty-two");
    }

    [Test]
    public async Task SelectGroup_StoresHeaderAndOptions()
    {
        var options = new[] { new SelectOption<string>("a", "Alpha") };
        var group = new SelectGroup<string>("Letters", options);

        LocKey header = group.Header;
        int count = group.Options.Count();
        var expectedCount = 1;
        await Assert.That(header).IsEqualTo("Letters");
        await Assert.That(count).IsEqualTo(expectedCount);
    }
}
