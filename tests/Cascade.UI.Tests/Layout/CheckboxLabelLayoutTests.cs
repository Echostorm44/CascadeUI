using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

/// <summary>
/// CONTROLS-004. <c>MeasureCheckbox</c> and <c>MeasureRadioButton</c> hardcoded the box size and
/// label gap (18/8 and 22/8) while the painter used the active theme's tokens. Apple's checkbox is
/// 20px, so a checkbox laid out at its own natural width asked for 26px of box + gap while the
/// painter reserved 28 — leaving the label 2px short and dropping its last word entirely, with no
/// ellipsis to show it had happened. These pin the measurement to the same tokens the painter uses.
/// </summary>
public class CheckboxLabelLayoutTests
{
    private readonly LayoutEngine engine = new();

    private static Checkbox LabelledCheckbox() =>
        new(new Bindable<bool>(false, _ => { }), "Show pruned sessions");

    [Test]
    public async Task CheckboxWidthTracksTheThemeBoxSize()
    {
        float originalSize = LayoutSolver.CheckboxSize;
        float originalGap = LayoutSolver.CheckboxLabelGap;

        try
        {
            LayoutSolver.CheckboxSize = 18f;
            LayoutSolver.CheckboxLabelGap = 8f;
            var narrow = LabelledCheckbox();
            engine.Layout(narrow, LayoutConstraints.Loose(new Size(4000, 600)));
            float narrowWidth = narrow.LayoutData.MeasuredSize.Width;

            // A larger box must widen the control by exactly the difference. When the size was a
            // const this stayed put, and the painter's extra 2px came out of the label.
            LayoutSolver.CheckboxSize = 28f;
            var wide = LabelledCheckbox();
            engine.Layout(wide, LayoutConstraints.Loose(new Size(4000, 600)));
            float wideWidth = wide.LayoutData.MeasuredSize.Width;

            await Assert.That(wideWidth - narrowWidth).IsEqualTo(10f).Within(0.01f);
        }
        finally
        {
            LayoutSolver.CheckboxSize = originalSize;
            LayoutSolver.CheckboxLabelGap = originalGap;
        }
    }

    [Test]
    public async Task CheckboxWidthTracksTheThemeLabelGap()
    {
        float originalGap = LayoutSolver.CheckboxLabelGap;

        try
        {
            LayoutSolver.CheckboxLabelGap = 4f;
            var tight = LabelledCheckbox();
            engine.Layout(tight, LayoutConstraints.Loose(new Size(4000, 600)));
            float tightWidth = tight.LayoutData.MeasuredSize.Width;

            LayoutSolver.CheckboxLabelGap = 20f;
            var loose = LabelledCheckbox();
            engine.Layout(loose, LayoutConstraints.Loose(new Size(4000, 600)));
            float looseWidth = loose.LayoutData.MeasuredSize.Width;

            await Assert.That(looseWidth - tightWidth).IsEqualTo(16f).Within(0.01f);
        }
        finally
        {
            LayoutSolver.CheckboxLabelGap = originalGap;
        }
    }

    [Test]
    public async Task CheckboxNaturalWidthLeavesRoomForItsWholeLabel()
    {
        var checkbox = LabelledCheckbox();
        engine.Layout(checkbox, LayoutConstraints.Loose(new Size(4000, 600)));

        // The painter lays the label out in (width - CheckboxSize - CheckboxLabelGap). Measuring
        // with the same tokens is what guarantees the remainder is enough for the label; the bug
        // was that the two disagreed by the 2px difference in box size.
        float labelSpace = checkbox.LayoutData.MeasuredSize.Width
            - LayoutSolver.CheckboxSize
            - LayoutSolver.CheckboxLabelGap;

        await Assert.That(labelSpace).IsGreaterThan(0f);
    }

    [Test]
    public async Task RadioButtonWidthTracksTheThemeCircleSize()
    {
        float originalSize = LayoutSolver.RadioSize;

        try
        {
            LayoutSolver.RadioSize = 22f;
            var small = new RadioButton<string>("a", "Pick this one");
            engine.Layout(small, LayoutConstraints.Loose(new Size(4000, 600)));
            float smallWidth = small.LayoutData.MeasuredSize.Width;

            LayoutSolver.RadioSize = 32f;
            var large = new RadioButton<string>("a", "Pick this one");
            engine.Layout(large, LayoutConstraints.Loose(new Size(4000, 600)));
            float largeWidth = large.LayoutData.MeasuredSize.Width;

            await Assert.That(largeWidth - smallWidth).IsEqualTo(10f).Within(0.01f);
        }
        finally
        {
            LayoutSolver.RadioSize = originalSize;
        }
    }
}
