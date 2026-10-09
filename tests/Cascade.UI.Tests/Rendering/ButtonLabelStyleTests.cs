using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// A button was measured at its theme's <see cref="ButtonTheme.TextStyle"/> size (and the base
/// theme's padding) but its label was painted at the Body size (with the variant's padding).
/// Under Material 3 that was 16px measured, 14px drawn. Layout and paint now take the label style
/// from one place (<see cref="ButtonLabelStyle"/>); these paint real frames in each built-in theme
/// and check the label is drawn at the size the button was measured for, in full.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ButtonLabelStyleTests
{
    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    private static CascadeTheme MakeTheme(string name)
    {
        return name switch
        {
            "fluent" => new FluentTheme(ThemeMode.Dark),
            "material" => new Material3Theme(ThemeMode.Dark),
            _ => new AppleTheme(ThemeMode.Dark),
        };
    }

    [Test]
    [Arguments("apple")]
    [Arguments("fluent")]
    [Arguments("material")]
    public async Task EveryVariant_IsPaintedInTheStyleItWasMeasuredWith(string themeName)
    {
        var theme = MakeTheme(themeName);
        string?[] variants = [null, .. theme.Button.Variants.Keys];
        foreach (string? variant in variants)
        {
            var button = new Button("Copy to Clipboard", () => { });
            if (variant is not null)
            {
                button.Variant(variant);
            }

            var runs = Paint(theme, button);
            var expected = ButtonLabelStyle.StyleFor(ButtonLabelStyle.ThemeFor(theme.Button, variant), null);

            await Assert.That(runs.Count).IsEqualTo(1).Because($"{theme.GetType().Name} {variant ?? "default"}: one label run");
            await Assert.That(runs[0].FontSize).IsEqualTo(expected.Size)
                .Because($"{theme.GetType().Name} {variant ?? "default"}: painted at the measured size");
            // Measured for the whole caption at that size, so it is drawn whole (no ellipsis).
            await Assert.That(runs[0].GlyphIds.Length).IsEqualTo("Copy to Clipboard".Length)
                .Because($"{theme.GetType().Name} {variant ?? "default"}: the caption fits the button it was measured into");
        }
    }

    [Test]
    [Arguments("apple")]
    [Arguments("fluent")]
    [Arguments("material")]
    public async Task StyleOverride_IsMeasuredAndPainted(string themeName)
    {
        var theme = MakeTheme(themeName);
        var small = new TextStyle(12, FontWeight.Regular, 1.4f);

        var runs = Paint(theme, new Button("Small style", () => { }).Style(small));

        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].FontSize).IsEqualTo(12f);
        await Assert.That(runs[0].GlyphIds.Length).IsEqualTo("Small style".Length);
    }

    [Test]
    public async Task Material3_ButtonLabelsUseLabelLarge()
    {
        var theme = new Material3Theme(ThemeMode.Dark);

        await Assert.That(theme.Button.TextStyle).IsEqualTo(new TextStyle(14, FontWeight.Medium, 1.43f));
        foreach (var variant in theme.Button.Variants.Values)
        {
            await Assert.That(variant.TextStyle).IsEqualTo(theme.Button.TextStyle);
        }
    }

    private static List<EtchBackend.GlyphOp> Paint(CascadeTheme theme, Button button)
    {
        content = () => new Column(children: [button]);
        ThemeSwitcher.Apply(theme);
        try
        {
            using var backend = new EtchBackend();
            using var orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                RenderBackend = backend,
            };
            orchestrator.BeginFrameCallback = () =>
            {
                backend.Reset();
                return (1UL, 600u, 200u);
            };
            orchestrator.MountRoot<Host>(600f, 200f);
            orchestrator.Tick();
            return [.. backend.GlyphCommands];
        }
        finally
        {
            ThemeSwitcher.Reset();
        }
    }
}
