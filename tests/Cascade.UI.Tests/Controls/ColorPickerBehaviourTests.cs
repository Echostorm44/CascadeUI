#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// The colour picker's modes, formats, opacity, eye dropper and swatches, which were stored and
/// never used: it only ever showed the square and a hex label, and lost the alpha.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ColorPickerBehaviourTests
{
    /// <summary>A picker bound to component state, re-rendered on every change as an app would be.</summary>
    private sealed class Host(Func<Host, ColorPicker> build) : Component
    {
        internal ColorValue Color = ColorValue.FromRgba(1f, 0f, 0f);
        internal IReadOnlyList<ColorValue> Saved = [];
        internal ColorPicker? Picker;

        internal Bindable<ColorValue> ColorBinding => new(Color, c => { Color = c; Invalidate(); });
        internal Bindable<IReadOnlyList<ColorValue>> SavedBinding => new(Saved, v => { Saved = v; Invalidate(); });

        protected override Node Render() => Picker = build(this);

        internal void SetColor(ColorValue color)
        {
            Color = color;
            Invalidate();
        }
    }

    private static (RenderHarness App, Host Host) Open(Func<Host, ColorPicker> build)
    {
        ColorPicker.ClearRecentForTests();
        FocusManager.ClearFocus();
        var host = new Host(build);
        var app = new RenderHarness(() => host);
        app.Frame();
        app.Frame();
        return (app, host);
    }

    private static ColorPickerGeometry Geometry(Host host) =>
        ColorPickerGeometry.Arrange(host.Picker!, host.Picker!.LayoutData.Bounds.Width);

    private static (float R, float G, float B, float A) Srgb(ColorValue c) => ColorMath.ToSrgb(c);

    [Test]
    public async Task Formats_RoundTrip()
    {
        foreach (var format in Enum.GetValues<ColorFormat>())
        {
            string text = ColorMath.Format(format, 200f, 0.6f, 0.8f, 0.5f);
            var back = ColorMath.Parse(text, format);
            await Assert.That(back).IsNotNull().Because(text);
            var (h, s, b, a) = back!.Value;
            await Assert.That(Math.Abs(h - 200f)).IsLessThan(1.5f).Because(text);
            await Assert.That(Math.Abs(s - 0.6f)).IsLessThan(0.02f).Because(text);
            await Assert.That(Math.Abs(b - 0.8f)).IsLessThan(0.02f).Because(text);
            await Assert.That(Math.Abs(a - 0.5f)).IsLessThan(0.01f).Because(text);
        }
        await Assert.That(ColorMath.Format(ColorFormat.Hex, 0f, 1f, 1f, 1f)).IsEqualTo("#FF0000");
        await Assert.That(ColorMath.Parse("#0f0", ColorFormat.Hex)!.Value.H).IsEqualTo(120f);
        await Assert.That(ColorMath.Parse("not a colour", ColorFormat.RGB)).IsNull();
    }

    [Test]
    public async Task Square_AndHueBar_SetTheColour_AndTheDragEndIsARecentColour()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding));
        using (app)
        {
            var g = Geometry(host);
            app.Click(g.Canvas.X + g.Canvas.Width * 0.25f, g.Canvas.Y + g.Canvas.Height * 0.5f);
            var (r, gg, b, _) = Srgb(host.Color);
            await Assert.That(host.Picker!.Saturation).IsBetween(0.23f, 0.27f);
            await Assert.That(host.Picker.Brightness).IsBetween(0.48f, 0.52f);
            await Assert.That(r).IsGreaterThan(gg); // still red, paler and darker
            await Assert.That(Math.Abs(gg - b)).IsLessThan(0.01f);

            app.Click(g.HueBar.X + g.HueBar.Width / 3f, g.HueBar.Y + g.HueBar.Height / 2f);
            await Assert.That(host.Picker!.Hue).IsBetween(118f, 122f);
            await Assert.That(host.Picker.RecentShown.Count).IsEqualTo(2);
        }
    }

    [Test]
    public async Task Opacity_SetsAlpha_AndMakesThePickerTaller()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding).ShowOpacity(true).Modes([ColorPickerMode.Sliders]));
        using (app)
        {
            var g = Geometry(host);
            await Assert.That(g.Opacity.Width).IsGreaterThan(0f);
            float plain = ColorPickerGeometry.Arrange(new ColorPicker(host.ColorBinding).Modes([ColorPickerMode.Sliders]), 260f).Height;
            await Assert.That(g.Height).IsGreaterThan(plain);
            app.Click(g.Opacity.X + g.Opacity.Width * 0.3f, g.Opacity.Y + g.Opacity.Height / 2f);
            await Assert.That(host.Color.A).IsBetween(0.28f, 0.32f);
        }
    }

    [Test]
    public async Task Modes_SwitchToTheWheel_AndItsRingAndTriangleWork()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding).Modes([ColorPickerMode.HueSaturation, ColorPickerMode.Wheel, ColorPickerMode.Sliders]));
        using (app)
        {
            var g = Geometry(host);
            await Assert.That(g.Tabs.Count).IsEqualTo(3);
            app.Click(g.Tabs[1].X + 10f, g.Tabs[1].Y + 10f);
            await Assert.That(host.Picker!.Mode).IsEqualTo(ColorPickerMode.Wheel);

            g = Geometry(host);
            // The ring's right-hand side is a quarter turn from red: hue 90.
            float mid = (g.WheelOuter + g.WheelInner) / 2f;
            app.Click(g.WheelCenter.X + mid, g.WheelCenter.Y);
            await Assert.That(host.Picker!.Hue).IsBetween(88f, 92f);

            // The white corner of the triangle: no saturation, full brightness.
            var white = g.Triangle.White;
            app.Click(white.X + 1f, white.Y - 1f);
            await Assert.That(host.Picker!.Saturation).IsLessThan(0.05f);
            await Assert.That(host.Picker.Brightness).IsGreaterThan(0.95f);
        }
    }

    [Test]
    public async Task Sliders_SetOneChannel()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding).Modes([ColorPickerMode.Sliders]));
        using (app)
        {
            var g = Geometry(host);
            var green = g.Channels[1];
            app.Click(green.Right, green.Y + green.Height / 2f);
            var (r, gg, b, _) = Srgb(host.Color);
            await Assert.That(r).IsGreaterThan(0.99f);
            await Assert.That(gg).IsGreaterThan(0.99f);
            await Assert.That(b).IsLessThan(0.01f);
        }
    }

    [Test]
    public async Task TheValue_CyclesFormats_AndTakesTypedText()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding).Formats([ColorFormat.Hex, ColorFormat.RGB]).Modes([ColorPickerMode.Sliders]));
        using (app)
        {
            var g = Geometry(host);
            await Assert.That(host.Picker!.ValueText).IsEqualTo("#FF0000");
            app.Click(g.Format.X + 5f, g.Format.Y + 5f);
            await Assert.That(host.Picker!.ValueText).IsEqualTo("rgb(255, 0, 0)");

            // Clicking the value and typing replaces it; Enter applies.
            app.Click(g.Value.X + 10f, g.Value.Y + 10f);
            await Assert.That(FocusManager.FocusedElement is ColorPicker).IsTrue();
            foreach (char c in "0, 0, 255")
            {
                app.Press(Key.None, character: c);
            }
            await Assert.That(host.Picker!.ValueText).IsEqualTo("0, 0, 255");
            app.Press(Key.Enter);
            var (r, _, b, _) = Srgb(host.Color);
            await Assert.That(b).IsGreaterThan(0.99f);
            await Assert.That(r).IsLessThan(0.01f);
            await Assert.That(host.Picker!.EditText).IsNull();

            // Not a colour: Enter keeps it for correcting; Escape drops it.
            app.Press(Key.None, character: '#');
            app.Press(Key.None, character: 'z');
            app.Press(Key.Enter);
            await Assert.That(host.Picker!.EditText).IsEqualTo("#z");
            app.Press(Key.Escape);
            await Assert.That(host.Picker!.EditText).IsNull();
        }
    }

    [Test]
    public async Task Keys_MoveSaturationBrightnessAndHue()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding));
        using (app)
        {
            FocusManager.RequestFocus(host.Picker!);
            host.SetColor(ColorMath.FromHsb(100f, 0.5f, 0.5f, 1f));
            app.Frame();
            app.Press(Key.Right);
            await Assert.That(host.Picker!.Saturation).IsBetween(0.505f, 0.515f);
            app.Press(Key.Up, ModifierKeys.Shift);
            await Assert.That(host.Picker!.Brightness).IsBetween(0.595f, 0.605f);
            app.Press(Key.PageUp);
            await Assert.That(host.Picker!.Hue).IsBetween(109.5f, 110.5f);
        }
    }

    [Test]
    public async Task Swatches_AreSavedAndChosen_AndRecentOnesAreCapped()
    {
        var (app, host) = Open(h => new ColorPicker(h.ColorBinding).Swatches(h.SavedBinding).MaxRecent(2).Modes([ColorPickerMode.Sliders]));
        using (app)
        {
            var g = Geometry(host);
            app.Click(g.AddSwatch.X + 5f, g.AddSwatch.Y + 5f);
            await Assert.That(host.Saved.Count).IsEqualTo(1);
            await Assert.That(host.Saved[0].ToHex()).IsEqualTo(ColorValue.FromRgba(1f, 0f, 0f).ToHex());

            // Another colour, then the saved one chosen again.
            var blue = Geometry(host).Channels[2];
            app.Click(blue.Right, blue.Y + blue.Height / 2f);
            await Assert.That(host.Picker!.ValueText).IsEqualTo("#FF00FF");
            g = Geometry(host);
            app.Click(g.Saved[0].X + 5f, g.Saved[0].Y + 5f);
            await Assert.That(host.Picker!.ValueText).IsEqualTo("#FF0000");

            for (int i = 0; i < 3; i++)
            {
                var red = Geometry(host).Channels[0];
                app.Click(red.X + red.Width * (0.2f + i * 0.2f), red.Y + red.Height / 2f);
            }
            await Assert.That(host.Picker!.RecentShown.Count).IsEqualTo(2);
        }
    }

    [Test]
    public async Task EyeDropper_TakesTheColourUnderThePointer_OrGivesUpOnEscape()
    {
        ScreenColor.OverrideForTests = () => ColorValue.FromRgba(0f, 128f / 255f, 1f);
        try
        {
            var (app, host) = Open(h => new ColorPicker(h.ColorBinding).EyeDropper(true).Modes([ColorPickerMode.Sliders]));
            using (app)
            {
                var g = Geometry(host);
                await Assert.That(g.EyeDropper.Width).IsGreaterThan(0f);
                app.Click(g.EyeDropper.X + 5f, g.EyeDropper.Y + 5f);
                await Assert.That(host.Picker!.IsPickingFromScreen).IsTrue();
                app.Click(390f, 190f); // anywhere
                await Assert.That(host.Picker!.IsPickingFromScreen).IsFalse();
                await Assert.That(host.Picker.ValueText).IsEqualTo("#0080FF");

                app.Click(g.EyeDropper.X + 5f, g.EyeDropper.Y + 5f);
                app.Press(Key.Escape);
                await Assert.That(host.Picker!.IsPickingFromScreen).IsFalse();
            }
        }
        finally
        {
            ScreenColor.OverrideForTests = null;
        }
    }
}
