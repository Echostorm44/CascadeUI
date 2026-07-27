#pragma warning disable CA2000, CA1812

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class TimePickerTests
{
    private static Bindable<TimeOnly?> CreateBinding(TimeOnly? initial = null)
    {
        TimeOnly? captured = initial;
        return new Bindable<TimeOnly?>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_SetsValueBinding()
    {
        var time = new TimeOnly(14, 30);
        var binding = CreateBinding(time);
        var picker = new TimePicker(binding);

        var value = picker.Value.Value;
        await Assert.That(value).IsEqualTo(time);
    }

    [Test]
    public async Task Constructor_DefaultFormatIsHour24()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding);

        var format = picker.Format;
        await Assert.That(format).IsEqualTo(TimeFormat.Hour24);
    }

    [Test]
    public async Task Constructor_CustomFormatHour12()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding, format: TimeFormat.Hour12);

        var format = picker.Format;
        await Assert.That(format).IsEqualTo(TimeFormat.Hour12);
    }

    [Test]
    public async Task Constructor_DefaultStepIsOneMinute()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding);

        var step = picker.Step;
        await Assert.That(step).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    [Test]
    public async Task Constructor_CustomStep()
    {
        var binding = CreateBinding();
        var step = TimeSpan.FromMinutes(15);
        var picker = new TimePicker(binding, step: step);

        var storedStep = picker.Step;
        await Assert.That(storedStep).IsEqualTo(step);
    }

    [Test]
    public async Task PopupStyle_SetsDisplayStyle()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding)
            .PopupStyle(TimePickerPopupStyle.Grid);

        var style = picker.PopupDisplayStyle;
        await Assert.That(style).IsEqualTo(TimePickerPopupStyle.Grid);
    }

    [Test]
    public async Task PopupStyle_DefaultIsScrollWheel()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding);

        var style = picker.PopupDisplayStyle;
        await Assert.That(style).IsEqualTo(TimePickerPopupStyle.ScrollWheel);
    }

    [Test]
    public async Task PopupStyle_ReturnsSameInstance()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding);

        var result = picker.PopupStyle(TimePickerPopupStyle.Input);
        var same = ReferenceEquals(picker, result);
        await Assert.That(same).IsTrue();
    }

    [Test]
    public async Task PopupStyle_InputMode()
    {
        var binding = CreateBinding();
        var picker = new TimePicker(binding)
            .PopupStyle(TimePickerPopupStyle.Input);

        var style = picker.PopupDisplayStyle;
        await Assert.That(style).IsEqualTo(TimePickerPopupStyle.Input);
    }

    [Test]
    public async Task ValueBinding_OnChangeUpdatesCapture()
    {
        TimeOnly? captured = null;
        var binding = new Bindable<TimeOnly?>(null, v => { captured = v; });
        var picker = new TimePicker(binding);

        var newTime = new TimeOnly(9, 45, 30);
        picker.Value.OnChange(newTime);

        await Assert.That(captured).IsEqualTo(newTime);
    }
}
