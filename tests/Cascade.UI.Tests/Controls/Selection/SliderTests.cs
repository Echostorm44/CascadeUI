#pragma warning disable CA2000, CA1812

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class SliderTests
{
    private static Bindable<float> CreateFloatBinding(float initial)
    {
        float captured = initial;
        return new Bindable<float>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_StoresBinding()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        float value = slider.Bind.Value;
        var expected = 0.5f;
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_DefaultMinMax()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        float min = slider.Min;
        float max = slider.Max;
        var expectedMin = 0f;
        var expectedMax = 1f;
        await Assert.That(min).IsEqualTo(expectedMin);
        await Assert.That(max).IsEqualTo(expectedMax);
    }

    [Test]
    public async Task Constructor_CustomMinMax()
    {
        var binding = CreateFloatBinding(50f);
        var slider = new Slider(binding, min: 0f, max: 100f);

        float min = slider.Min;
        float max = slider.Max;
        var expectedMin = 0f;
        var expectedMax = 100f;
        await Assert.That(min).IsEqualTo(expectedMin);
        await Assert.That(max).IsEqualTo(expectedMax);
    }

    [Test]
    public async Task Constructor_WithStep()
    {
        var binding = CreateFloatBinding(50f);
        var slider = new Slider(binding, min: 0f, max: 100f, step: 10f);

        float? step = slider.Step;
        var expected = 10f;
        await Assert.That(step).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_StepNullByDefault()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        bool isNull = slider.Step == null;
        await Assert.That(isNull).IsTrue();
    }

    [Test]
    public async Task Constructor_StoresLabel()
    {
        var binding = CreateFloatBinding(50f);
        var slider = new Slider(binding, label: "Volume");

        string label = slider.Label.Value;
        await Assert.That(label).IsEqualTo("Volume");
    }

    [Test]
    public async Task Format_SetsFormatString()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).Format("P0");

        string? format = slider.FormatString;
        await Assert.That(format).IsEqualTo("P0");
    }

    [Test]
    public async Task ShowValueLabel_SetsFlag()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).ShowValueLabel();

        bool show = slider.ShowValueLabelValue;
        await Assert.That(show).IsTrue();
    }

    [Test]
    public async Task ShowTicks_SetsFlag()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).ShowTicks();

        bool show = slider.ShowTicksValue;
        await Assert.That(show).IsTrue();
    }

    [Test]
    public async Task Orientation_SetsValue()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).Orientation(Orientation.Vertical);

        var orientation = slider.OrientationValue;
        var expected = Orientation.Vertical;
        await Assert.That(orientation).IsEqualTo(expected);
    }

    [Test]
    public async Task Orientation_DefaultIsHorizontal()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        var orientation = slider.OrientationValue;
        var expected = Orientation.Horizontal;
        await Assert.That(orientation).IsEqualTo(expected);
    }

    [Test]
    public async Task Width_SetsValue()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).Width(300f);

        float? width = slider.WidthValue;
        var expected = 300f;
        await Assert.That(width).IsEqualTo(expected);
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding)
            .Validate(v => v < 0 ? ValidationResult.Error("Must be positive") : ValidationResult.Ok);

        int count = slider.ValidationRules.Count;
        var expected = 1;
        await Assert.That(count).IsEqualTo(expected);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).ValidateOn(ValidationTrigger.Manual);

        var trigger = slider.ValidationTriggerMode;
        var expected = ValidationTrigger.Manual;
        await Assert.That(trigger).IsEqualTo(expected);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).Disabled();

        bool disabled = slider.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).ReadOnly();

        bool readOnly = slider.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding).AccessibleLabel("Volume slider");

        string label = slider.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Volume slider");
    }

    [Test]
    public async Task RunValidation_PassesWithNoRules()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        var result = slider.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RunValidation_ReturnsError()
    {
        var binding = CreateFloatBinding(-1f);
        var slider = new Slider(binding)
            .Validate(v => v < 0 ? ValidationResult.Error("Negative") : ValidationResult.Ok);

        var result = slider.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateFloatBinding(0.5f);
        var slider = new Slider(binding);

        var result = slider
            .Format("F1")
            .ShowValueLabel()
            .ShowTicks()
            .Orientation(Orientation.Vertical)
            .Width(200f)
            .Disabled()
            .ReadOnly()
            .AccessibleLabel("Test");

        bool same = ReferenceEquals(slider, result);
        await Assert.That(same).IsTrue();
    }
}
