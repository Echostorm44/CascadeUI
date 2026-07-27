#pragma warning disable CA2000, CA1812

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class DatePickerTests
{
    private static Bindable<DateOnly?> CreateBinding(DateOnly? initial = null)
    {
        DateOnly? captured = initial;
        return new Bindable<DateOnly?>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_SetsValueBinding()
    {
        var date = new DateOnly(2024, 6, 15);
        var binding = CreateBinding(date);
        var picker = new DatePicker(binding);

        var value = picker.Value.Value;
        await Assert.That(value).IsEqualTo(date);
    }

    [Test]
    public async Task Constructor_SetsPlaceholder()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding, placeholder: "Pick a date");

        var placeholder = picker.Placeholder.Value;
        await Assert.That(placeholder).IsEqualTo("Pick a date");
    }

    [Test]
    public async Task Constructor_DefaultMinIsNull()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding);

        var min = picker.Min;
        await Assert.That(min).IsNull();
    }

    [Test]
    public async Task Constructor_DefaultMaxIsNull()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding);

        var max = picker.Max;
        await Assert.That(max).IsNull();
    }

    [Test]
    public async Task Constructor_DefaultFormatIsNull()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding);

        var format = picker.Format;
        await Assert.That(format).IsNull();
    }

    [Test]
    public async Task Constructor_CustomFormat()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding, format: "yyyy-MM-dd");

        var format = picker.Format;
        await Assert.That(format).IsEqualTo("yyyy-MM-dd");
    }

    [Test]
    public async Task Constructor_MinMaxConstraints()
    {
        var binding = CreateBinding();
        var minDate = new DateOnly(2024, 1, 1);
        var maxDate = new DateOnly(2024, 12, 31);
        var picker = new DatePicker(binding, min: minDate, max: maxDate);

        var min = picker.Min;
        var max = picker.Max;
        await Assert.That(min).IsEqualTo(minDate);
        await Assert.That(max).IsEqualTo(maxDate);
    }

    [Test]
    public async Task DisabledDates_WithPredicate_StoresPredicate()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding)
            .DisabledDates(d => d.DayOfWeek == DayOfWeek.Sunday);

        var predicate = picker.DisabledDatesPredicate;
        var sunday = new DateOnly(2024, 6, 16);
        var monday = new DateOnly(2024, 6, 17);

        await Assert.That(predicate).IsNotNull();
        var sundayDisabled = predicate!(sunday);
        var mondayDisabled = predicate(monday);
        await Assert.That(sundayDisabled).IsTrue();
        await Assert.That(mondayDisabled).IsFalse();
    }

    [Test]
    public async Task DisabledDates_WithList_CreatesPredicateFromDates()
    {
        var binding = CreateBinding();
        var holiday = new DateOnly(2024, 12, 25);
        var newYear = new DateOnly(2025, 1, 1);
        var regularDay = new DateOnly(2024, 6, 15);
        var picker = new DatePicker(binding)
            .DisabledDates(new List<DateOnly> { holiday, newYear });

        var predicate = picker.DisabledDatesPredicate;
        await Assert.That(predicate).IsNotNull();

        var holidayDisabled = predicate!(holiday);
        var newYearDisabled = predicate(newYear);
        var regularDisabled = predicate(regularDay);
        await Assert.That(holidayDisabled).IsTrue();
        await Assert.That(newYearDisabled).IsTrue();
        await Assert.That(regularDisabled).IsFalse();
    }

    [Test]
    public async Task HighlightedDates_StoresDatesAndColor()
    {
        var binding = CreateBinding();
        var dates = new List<DateOnly>
        {
            new DateOnly(2024, 6, 1),
            new DateOnly(2024, 6, 15)
        };
        var color = ColorValue.Transparent;
        var picker = new DatePicker(binding)
            .HighlightedDates(dates, color);

        var storedDates = picker.HighlightedDatesList;
        var storedColor = picker.HighlightedDatesColor;
        await Assert.That(storedDates).IsNotNull();
        await Assert.That(storedDates!.Count).IsEqualTo(2);
        await Assert.That(storedColor).IsEqualTo(color);
    }

    [Test]
    public async Task HighlightedDates_WithTooltip_StoresTooltip()
    {
        var binding = CreateBinding();
        var dates = new List<DateOnly> { new DateOnly(2024, 6, 1) };
        Func<DateOnly, string> tooltipFn = d => $"Event on {d}";

        var picker = new DatePicker(binding)
            .HighlightedDates(dates, tooltip: tooltipFn);

        var tooltip = picker.HighlightedDatesTooltip;
        await Assert.That(tooltip).IsNotNull();
        var result = tooltip!(new DateOnly(2024, 6, 1));
        await Assert.That(result).IsEqualTo("Event on 6/1/2024");
    }

    [Test]
    public async Task ExtensionMethods_ReturnSameInstance()
    {
        var binding = CreateBinding();
        var picker = new DatePicker(binding);

        var afterDisabled = picker.DisabledDates(d => false);
        var afterHighlighted = afterDisabled.HighlightedDates(new List<DateOnly>());

        var sameAfterDisabled = ReferenceEquals(picker, afterDisabled);
        var sameAfterHighlighted = ReferenceEquals(picker, afterHighlighted);
        await Assert.That(sameAfterDisabled).IsTrue();
        await Assert.That(sameAfterHighlighted).IsTrue();
    }

    [Test]
    public async Task ValueBinding_OnChangeUpdatesCapture()
    {
        DateOnly? captured = null;
        var binding = new Bindable<DateOnly?>(null, v => { captured = v; });
        var picker = new DatePicker(binding);

        var newDate = new DateOnly(2024, 7, 4);
        picker.Value.OnChange(newDate);

        await Assert.That(captured).IsEqualTo(newDate);
    }
}
