#pragma warning disable CA2000, CA1812

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

// ═══════════════════════════════════════════════════════════════════
// ColorPicker Tests
// ═══════════════════════════════════════════════════════════════════

public class ColorPickerTests
{
    private static Bindable<ColorValue> CreateColorBinding(ColorValue initial = default)
    {
        ColorValue captured = initial;
        return new Bindable<ColorValue>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_SetsValueBinding()
    {
        var color = ColorValue.Transparent;
        var binding = CreateColorBinding(color);
        var picker = new ColorPicker(binding);

        var value = picker.Value.Value;
        await Assert.That(value).IsEqualTo(color);
    }

    [Test]
    public async Task Modes_SetsPickerModes()
    {
        var binding = CreateColorBinding();
        var modes = new List<ColorPickerMode> { ColorPickerMode.HueSaturation, ColorPickerMode.Wheel };
        var picker = new ColorPicker(binding).Modes(modes);

        var stored = picker.PickerModes;
        await Assert.That(stored).IsNotNull();
        var count = stored!.Count;
        await Assert.That(count).IsEqualTo(2);
        var first = stored[0];
        await Assert.That(first).IsEqualTo(ColorPickerMode.HueSaturation);
    }

    [Test]
    public async Task Formats_SetsDisplayFormats()
    {
        var binding = CreateColorBinding();
        var formats = new List<ColorFormat> { ColorFormat.Hex, ColorFormat.RGB, ColorFormat.HSL };
        var picker = new ColorPicker(binding).Formats(formats);

        var stored = picker.DisplayFormats;
        await Assert.That(stored).IsNotNull();
        var count = stored!.Count;
        await Assert.That(count).IsEqualTo(3);
    }

    [Test]
    public async Task ShowOpacity_SetsFlag()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding).ShowOpacity(true);

        var show = picker.ShowOpacitySlider;
        await Assert.That(show).IsTrue();
    }

    [Test]
    public async Task EyeDropper_SetsFlag()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding).EyeDropper(true);

        var enabled = picker.EnableEyeDropper;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task Swatches_SetsBinding()
    {
        var binding = CreateColorBinding();
        IReadOnlyList<ColorValue> swatchList = new List<ColorValue> { ColorValue.Transparent };
        var swatchBinding = new Bindable<IReadOnlyList<ColorValue>>(swatchList, _ => { });
        var picker = new ColorPicker(binding).Swatches(swatchBinding);

        var stored = picker.SavedSwatches;
        await Assert.That(stored).IsNotNull();
    }

    [Test]
    public async Task MaxRecent_DefaultIs12()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding);

        var maxRecent = picker.MaxRecentColors;
        await Assert.That(maxRecent).IsEqualTo(12);
    }

    [Test]
    public async Task MaxRecent_SetsValue()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding).MaxRecent(20);

        var maxRecent = picker.MaxRecentColors;
        await Assert.That(maxRecent).IsEqualTo(20);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding).Disabled();

        var disabled = picker.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding).AccessibleLabel("Pick a color");

        var label = picker.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Pick a color");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateColorBinding();
        var picker = new ColorPicker(binding);

        var chained = picker
            .Modes(new List<ColorPickerMode> { ColorPickerMode.Wheel })
            .Formats(new List<ColorFormat> { ColorFormat.Hex })
            .ShowOpacity(true)
            .EyeDropper(true)
            .MaxRecent(8)
            .Disabled()
            .AccessibleLabel("Color");

        var same = ReferenceEquals(picker, chained);
        await Assert.That(same).IsTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════
// DateTimePicker Tests
// ═══════════════════════════════════════════════════════════════════

public class DateTimePickerTests
{
    private static Bindable<DateTime?> CreateBinding(DateTime? initial = null)
    {
        DateTime? captured = initial;
        return new Bindable<DateTime?>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_SetsAllParams()
    {
        var dt = new DateTime(2024, 6, 15, 14, 30, 0);
        var binding = CreateBinding(dt);
        var minDate = new DateOnly(2024, 1, 1);
        var maxDate = new DateOnly(2024, 12, 31);
        var step = TimeSpan.FromMinutes(15);

        var picker = new DateTimePicker(binding,
            minDate: minDate,
            maxDate: maxDate,
            timeFormat: TimeFormat.Hour12,
            timeStep: step,
            format: "MMM d, yyyy h:mm tt");

        var value = picker.Value.Value;
        await Assert.That(value).IsEqualTo(dt);
        var min = picker.MinDate;
        await Assert.That(min).IsEqualTo(minDate);
        var max = picker.MaxDate;
        await Assert.That(max).IsEqualTo(maxDate);
        var fmt = picker.TimeFormatValue;
        await Assert.That(fmt).IsEqualTo(TimeFormat.Hour12);
        var storedStep = picker.TimeStep;
        await Assert.That(storedStep).IsEqualTo(step);
        var format = picker.Format;
        await Assert.That(format).IsEqualTo("MMM d, yyyy h:mm tt");
    }

    [Test]
    public async Task Constructor_DefaultTimeFormatIsHour24()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding);

        var format = picker.TimeFormatValue;
        await Assert.That(format).IsEqualTo(TimeFormat.Hour24);
    }

    [Test]
    public async Task Constructor_MinDateMaxDateStoredCorrectly()
    {
        var binding = CreateBinding();
        var minDate = new DateOnly(2024, 3, 1);
        var maxDate = new DateOnly(2024, 9, 30);
        var picker = new DateTimePicker(binding, minDate: minDate, maxDate: maxDate);

        var min = picker.MinDate;
        var max = picker.MaxDate;
        await Assert.That(min).IsEqualTo(minDate);
        await Assert.That(max).IsEqualTo(maxDate);
    }

    [Test]
    public async Task DisabledDates_WorksWithPredicate()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding)
            .DisabledDates(d => d.DayOfWeek == DayOfWeek.Saturday);

        var predicate = picker.DisabledDatesPredicate;
        await Assert.That(predicate).IsNotNull();

        var saturday = new DateOnly(2024, 6, 15);
        var monday = new DateOnly(2024, 6, 17);
        var satResult = predicate!(saturday);
        var monResult = predicate(monday);
        await Assert.That(satResult).IsTrue();
        await Assert.That(monResult).IsFalse();
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding).Disabled();

        var disabled = picker.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task Placeholder_SetsText()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding).Placeholder("Select date and time");

        var placeholder = picker.PlaceholderText.Value;
        await Assert.That(placeholder).IsEqualTo("Select date and time");
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding).AccessibleLabel("Schedule");

        var label = picker.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Schedule");
    }

    [Test]
    public async Task Format_StoredCorrectly()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding, format: "yyyy-MM-dd HH:mm");

        var format = picker.Format;
        await Assert.That(format).IsEqualTo("yyyy-MM-dd HH:mm");
    }

    [Test]
    public async Task TimeStep_StoredCorrectly()
    {
        var binding = CreateBinding();
        var step = TimeSpan.FromMinutes(30);
        var picker = new DateTimePicker(binding, timeStep: step);

        var stored = picker.TimeStep;
        await Assert.That(stored).IsEqualTo(step);
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var binding = CreateBinding();
        var picker = new DateTimePicker(binding);

        var chained = picker
            .DisabledDates(d => false)
            .Disabled()
            .Placeholder("Pick")
            .AccessibleLabel("DT");

        var same = ReferenceEquals(picker, chained);
        await Assert.That(same).IsTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════
// MonthPicker Tests
// ═══════════════════════════════════════════════════════════════════

public class MonthPickerTests
{
    private static Bindable<YearMonth?> CreateBinding(YearMonth? initial = null)
    {
        YearMonth? captured = initial;
        return new Bindable<YearMonth?>(captured, v => { captured = v; });
    }

    [Test]
    public async Task YearMonth_ComparisonWorks()
    {
        var jan2024 = new YearMonth(2024, 1);
        var mar2024 = new YearMonth(2024, 3);
        var jan2025 = new YearMonth(2025, 1);

        var janVsMar = jan2024.CompareTo(mar2024);
        var marVsJan = mar2024.CompareTo(jan2024);
        var janVsJan25 = jan2024.CompareTo(jan2025);
        var same = jan2024.CompareTo(new YearMonth(2024, 1));

        await Assert.That(janVsMar).IsNegative();
        await Assert.That(marVsJan).IsPositive();
        await Assert.That(janVsJan25).IsNegative();
        await Assert.That(same).IsEqualTo(0);
    }

    [Test]
    public async Task YearMonth_ToStringFormat()
    {
        var ym = new YearMonth(2024, 3);

        var str = ym.ToString();
        await Assert.That(str).IsEqualTo("2024-03");
    }

    [Test]
    public async Task Constructor_SetsValueBinding()
    {
        var ym = new YearMonth(2024, 6);
        var binding = CreateBinding(ym);
        var picker = new MonthPicker(binding);

        var value = picker.Value.Value;
        await Assert.That(value).IsEqualTo(ym);
    }

    [Test]
    public async Task Min_SetsMinValue()
    {
        var binding = CreateBinding();
        var min = new YearMonth(2024, 1);
        var picker = new MonthPicker(binding).Min(min);

        var stored = picker.MinValue;
        await Assert.That(stored).IsEqualTo(min);
    }

    [Test]
    public async Task Max_SetsMaxValue()
    {
        var binding = CreateBinding();
        var max = new YearMonth(2025, 12);
        var picker = new MonthPicker(binding).Max(max);

        var stored = picker.MaxValue;
        await Assert.That(stored).IsEqualTo(max);
    }

    [Test]
    public async Task Format_SetsFormatString()
    {
        var binding = CreateBinding();
        var picker = new MonthPicker(binding).Format("MMMM yyyy");

        var stored = picker.FormatString;
        await Assert.That(stored).IsEqualTo("MMMM yyyy");
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var binding = CreateBinding();
        var picker = new MonthPicker(binding).Disabled();

        var disabled = picker.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var binding = CreateBinding();
        var picker = new MonthPicker(binding).AccessibleLabel("Select month");

        var label = picker.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Select month");
    }
}

// ═══════════════════════════════════════════════════════════════════
// EmojiPicker Tests
// ═══════════════════════════════════════════════════════════════════

public class EmojiPickerTests
{
    [Test]
    public async Task Constructor_SetsOnSelectCallback()
    {
        string? selected = null;
        var picker = new EmojiPicker(e => { selected = e; });

        picker.OnSelect("😀");
        await Assert.That(selected).IsEqualTo("😀");
    }

    [Test]
    public async Task Categories_FiltersSetsCategories()
    {
        var cats = new List<EmojiCategory> { EmojiCategory.SmileysAndPeople, EmojiCategory.Flags };
        var picker = new EmojiPicker(_ => { }).Categories(cats);

        var stored = picker.Categories;
        await Assert.That(stored).IsNotNull();
        var count = stored!.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task RecentEmoji_SetsBindingAndCount()
    {
        IReadOnlyList<string> recent = new List<string> { "😀", "🎉" };
        var binding = new Bindable<IReadOnlyList<string>>(recent, _ => { });
        var picker = new EmojiPicker(_ => { }).RecentEmoji(binding, 10);

        var stored = picker.RecentEmojiBind;
        await Assert.That(stored).IsNotNull();
        var maxCount = picker.MaxRecentCount;
        await Assert.That(maxCount).IsEqualTo(10);
    }

    [Test]
    public async Task SkinTone_SetsBinding()
    {
        var binding = new Bindable<SkinTone>(SkinTone.Medium, _ => { });
        var picker = new EmojiPicker(_ => { }).SkinTone(binding);

        var stored = picker.SkinToneBind;
        await Assert.That(stored).IsNotNull();
        var value = stored!.Value.Value;
        await Assert.That(value).IsEqualTo(SkinTone.Medium);
    }

    [Test]
    public async Task DefaultMaxRecentCount_Is24()
    {
        var picker = new EmojiPicker(_ => { });

        var maxRecent = picker.MaxRecentCount;
        await Assert.That(maxRecent).IsEqualTo(24);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var picker = new EmojiPicker(_ => { }).Disabled();

        var disabled = picker.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var picker = new EmojiPicker(_ => { }).AccessibleLabel("Emoji");

        var label = picker.LayoutData.A11yLabel!;
        await Assert.That(label).IsEqualTo("Emoji");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var picker = new EmojiPicker(_ => { });

        var chained = picker
            .Categories(new List<EmojiCategory> { EmojiCategory.SmileysAndPeople })
            .Disabled()
            .AccessibleLabel("Emoji");

        var same = ReferenceEquals(picker, chained);
        await Assert.That(same).IsTrue();
    }
}
