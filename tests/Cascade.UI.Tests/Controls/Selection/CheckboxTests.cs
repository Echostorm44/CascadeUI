#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class CheckboxTests
{
    private static Bindable<bool> CreateBoolBinding(bool initial)
    {
        bool captured = initial;
        return new Bindable<bool>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_BoolBinding_StoresValue()
    {
        var binding = CreateBoolBinding(true);
        var checkbox = new Checkbox(binding);

        bool value = checkbox.BoolValue!.Value.Value;
        await Assert.That(value).IsTrue();
    }

    [Test]
    public async Task Constructor_BoolBinding_ThreeStateIsNull()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding);

        bool isNull = checkbox.ThreeStateValue == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_ThreeState_StoresValue()
    {
        var checkbox = new Checkbox(CheckboxValue.Indeterminate, _ => { });

        var value = checkbox.ThreeStateValue;
        var expected = CheckboxValue.Indeterminate;
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_ThreeState_BoolValueIsNull()
    {
        var checkbox = new Checkbox(CheckboxValue.Checked, _ => { });

        bool isNull = checkbox.BoolValue == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_ThreeState_InvokesOnChange()
    {
        CheckboxValue received = CheckboxValue.Unchecked;
        var checkbox = new Checkbox(CheckboxValue.Checked, v => { received = v; });

        checkbox.OnChange!(CheckboxValue.Indeterminate);
        var expected = CheckboxValue.Indeterminate;
        await Assert.That(received).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_Label_StoresLabel()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding, label: "Accept terms");

        string label = checkbox.Label.Value;
        await Assert.That(label).IsEqualTo("Accept terms");
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var binding = CreateBoolBinding(true);
        var checkbox = new Checkbox(binding)
            .Validate(v => v ? ValidationResult.Ok : ValidationResult.Error("Required"));

        int count = checkbox.ValidationRules.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task Validate_MultipleRules()
    {
        var binding = CreateBoolBinding(true);
        var checkbox = new Checkbox(binding)
            .Validate(_ => ValidationResult.Ok)
            .Validate(_ => ValidationResult.Ok);

        int count = checkbox.ValidationRules.Count;
        var expected = 2;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding)
            .ValidateOn(ValidationTrigger.Submit);

        var trigger = checkbox.ValidationTriggerMode;
        var expected = ValidationTrigger.Submit;
        await Assert.That(trigger).IsEqualTo(expected);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding).Disabled();

        bool disabled = checkbox.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task Disabled_CanBeUnset()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding).Disabled(false);

        bool disabled = checkbox.IsDisabled;
        await Assert.That(disabled).IsFalse();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding).ReadOnly();

        bool readOnly = checkbox.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding).AccessibleLabel("Terms checkbox");

        string label = checkbox.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Terms checkbox");
    }

    [Test]
    public async Task RunValidation_PassesWithNoRules()
    {
        var binding = CreateBoolBinding(true);
        var checkbox = new Checkbox(binding);

        var result = checkbox.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RunValidation_ReturnsFirstError()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding)
            .Validate(v => v ? ValidationResult.Ok : ValidationResult.Error("Must accept"));

        var result = checkbox.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateBoolBinding(false);
        var checkbox = new Checkbox(binding);

        var result = checkbox
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Test");

        bool same = ReferenceEquals(checkbox, result);
        await Assert.That(same).IsTrue();
    }
}
