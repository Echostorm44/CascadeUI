#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class ToggleTests
{
    private static Bindable<bool> CreateBoolBinding(bool initial)
    {
        bool captured = initial;
        return new Bindable<bool>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_StoresValue()
    {
        var binding = CreateBoolBinding(true);
        var toggle = new Toggle(binding);

        bool value = toggle.Value.Value;
        await Assert.That(value).IsTrue();
    }

    [Test]
    public async Task Constructor_StoresLabel()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding, label: "Dark mode");

        string label = toggle.Label.Value;
        await Assert.That(label).IsEqualTo("Dark mode");
    }

    [Test]
    public async Task Constructor_StoresDescription()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding, description: "Enable dark theme");

        string desc = toggle.Description.Value;
        await Assert.That(desc).IsEqualTo("Enable dark theme");
    }

    [Test]
    public async Task LabelPosition_SetsValue()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding).LabelPosition(ToggleLabelPosition.Left);

        var position = toggle.LabelPositionValue;
        var expected = ToggleLabelPosition.Left;
        await Assert.That(position).IsEqualTo(expected);
    }

    [Test]
    public async Task LabelPosition_DefaultIsRight()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding);

        var position = toggle.LabelPositionValue;
        var expected = ToggleLabelPosition.Right;
        await Assert.That(position).IsEqualTo(expected);
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var binding = CreateBoolBinding(true);
        var toggle = new Toggle(binding)
            .Validate(v => v ? ValidationResult.Ok : ValidationResult.Error("Must be on"));

        int count = toggle.ValidationRules.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding).ValidateOn(ValidationTrigger.Immediate);

        var trigger = toggle.ValidationTriggerMode;
        var expected = ValidationTrigger.Immediate;
        await Assert.That(trigger).IsEqualTo(expected);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding).Disabled();

        bool disabled = toggle.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding).ReadOnly();

        bool readOnly = toggle.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding).AccessibleLabel("Dark mode toggle");

        string label = toggle.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Dark mode toggle");
    }

    [Test]
    public async Task RunValidation_PassesWithNoRules()
    {
        var binding = CreateBoolBinding(true);
        var toggle = new Toggle(binding);

        var result = toggle.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RunValidation_ReturnsError()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding)
            .Validate(v => v ? ValidationResult.Ok : ValidationResult.Error("Required"));

        var result = toggle.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateBoolBinding(false);
        var toggle = new Toggle(binding);

        var result = toggle
            .LabelPosition(ToggleLabelPosition.Left)
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Test");

        bool same = ReferenceEquals(toggle, result);
        await Assert.That(same).IsTrue();
    }
}
