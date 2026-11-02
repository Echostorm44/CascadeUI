using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Label typography reaches the screen: weight, italic, line height, font family and decoration
/// were stored by their modifiers but nothing measured or drew with them. These render real frames
/// (the bundled Inter variable fonts, the CPU renderer) and measure the result.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class LabelTypographyTests
{
    private const uint Width = 700;
    private const uint Height = 160;

    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    [Test]
    public async Task FontWeight_DrawsEachWeight_HeavierWithEachStep()
    {
        FontWeight[] weights = [FontWeight.Thin, FontWeight.Light, FontWeight.Regular, FontWeight.SemiBold, FontWeight.Bold, FontWeight.Black];
        long previous = 0;
        foreach (var weight in weights)
        {
            var frame = Render(new Label("Hamburgefonts").FontSize(40).FontWeight(weight));
            long ink = frame.TotalInk();
            await Assert.That(ink).IsGreaterThan(previous + previous / 10).Because($"{weight} is heavier than the weight before it");
            previous = ink;
        }
    }

    [Test]
    public async Task FontWeight_IsMeasuredInTheFontItIsDrawnIn()
    {
        float regular = MeasuredWidth(new Label("Hamburgefonts").FontSize(40));
        float black = MeasuredWidth(new Label("Hamburgefonts").FontSize(40).FontWeight(FontWeight.Black));
        float thin = MeasuredWidth(new Label("Hamburgefonts").FontSize(40).FontWeight(FontWeight.Thin));

        await Assert.That(black).IsGreaterThan(regular + 10f);
        await Assert.That(thin).IsLessThan(regular - 5f);

        // And the drawn ink fits the measured box: nothing clipped, nothing short.
        var frame = Render(new Label("Hamburgefonts").FontSize(40).FontWeight(FontWeight.Black));
        (int left, int right) = frame.InkColumns();
        await Assert.That((float)(right - left + 1)).IsLessThanOrEqualTo(black + 1f);
        await Assert.That((float)(right - left + 1)).IsGreaterThan(black - 12f);
    }

    [Test]
    public async Task Bold_AndFontSize_KeepEachOther_InEitherOrder()
    {
        var sizeFirst = new Label("x").FontSize(30).Bold();
        var boldFirst = new Label("x").Bold().FontSize(30);

        await Assert.That(sizeFirst.TextStyleOverride!.Value.Size).IsEqualTo(30f);
        await Assert.That(sizeFirst.TextStyleOverride!.Value.Weight).IsEqualTo(FontWeight.Bold);
        await Assert.That(boldFirst.TextStyleOverride!.Value.Size).IsEqualTo(30f);
        await Assert.That(boldFirst.TextStyleOverride!.Value.Weight).IsEqualTo(FontWeight.Bold);
    }

    [Test]
    public async Task Italic_SlantsTheText()
    {
        var upright = Render(new Label("llllll").FontSize(48));
        var italic = Render(new Label("llllll").FontSize(48).Italic());

        // An italic stem leans right: its ink sits further right at the top than at the bottom.
        await Assert.That(Math.Abs(upright.Slant())).IsLessThan(1.0);
        await Assert.That(italic.Slant()).IsGreaterThan(4.0);
    }

    [Test]
    public async Task LineHeight_SetsTheDistanceBetweenLines()
    {
        float natural = MeasuredHeight(new Label("one\ntwo").FontSize(20));
        float tall = MeasuredHeight(new Label("one\ntwo").FontSize(20).LineHeight(3f));
        float styled = MeasuredHeight(new Label("one\ntwo").Style(new TextStyle(20, FontWeight.Regular, 2f)));

        await Assert.That(Math.Abs(tall - 2 * 3f * 20f)).IsLessThanOrEqualTo(1f);
        await Assert.That(Math.Abs(styled - 2 * 2f * 20f)).IsLessThanOrEqualTo(1f);
        await Assert.That(natural).IsLessThan(styled);
    }

    [Test]
    public async Task LineHeight_KeepsTheTextCentredInItsLines()
    {
        // Two lines of 3x line height: each line's ink sits in the middle of its 60 px line.
        var frame = Render(new Label("xx\nxx").FontSize(20).LineHeight(3f), crossAxisStart: true);
        (int top, int bottom) = frame.InkRows(0, 60);
        float inkMiddle = (top + bottom) / 2f;
        await Assert.That(Math.Abs(inkMiddle - 30f)).IsLessThanOrEqualTo(3f);
    }

    [Test]
    public async Task Underline_DrawsALineUnderTheTextAcrossItsWidth()
    {
        var plain = Render(new Label("i i i i i i i i").FontSize(32));
        var underlined = Render(new Label("i i i i i i i i").FontSize(32).Decoration(TextDecoration.Underline));
        (int top, int bottom) = plain.InkRows(0, (int)Height);

        (int row, int run) = underlined.LongestRun();
        await Assert.That(plain.LongestRun().Run).IsLessThan(20);
        await Assert.That(run).IsGreaterThan(120).Because("the underline spans the words and the gaps");
        // Below the baseline, which is the bottom of the dotless stems.
        await Assert.That(row).IsGreaterThan(bottom);
    }

    [Test]
    public async Task Strikethrough_DrawsALineThroughTheMiddleOfTheText()
    {
        var plain = Render(new Label("x x x x x x x").FontSize(32));
        var struck = Render(new Label("x x x x x x x").FontSize(32).Decoration(TextDecoration.Strikethrough));
        (int top, int bottom) = plain.InkRows(0, (int)Height);

        (int row, int run) = struck.LongestRun();
        await Assert.That(run).IsGreaterThan(120);
        await Assert.That(row).IsGreaterThan(top);
        await Assert.That(row).IsLessThan(bottom);
    }

    [Test]
    public async Task FontFamily_Monospace_GivesEveryCharacterTheSameAdvance()
    {
        var family = FontFamily.ForCategory(SystemFont.Monospace);
        float narrow = MeasuredWidth(new Label("iiiiiiii").FontSize(20).FontFamily(family));
        float wide = MeasuredWidth(new Label("MMMMMMMM").FontSize(20).FontFamily(family));
        float proportionalNarrow = MeasuredWidth(new Label("iiiiiiii").FontSize(20));
        float proportionalWide = MeasuredWidth(new Label("MMMMMMMM").FontSize(20));

        await Assert.That(Math.Abs(narrow - wide)).IsLessThanOrEqualTo(1f);
        await Assert.That(proportionalWide).IsGreaterThan(proportionalNarrow * 2f);
    }

    private static float MeasuredWidth(Label label) => Measure(label).Width;

    private static float MeasuredHeight(Label label) => Measure(label).Height;

    private static Size Measure(Label label)
    {
        Render(new Label("warm up"));  // mounts a themed frame, which sets the layout fonts
        return LayoutSolver.Measure(label, LayoutConstraints.Loose(new Size(Width, 1000)));
    }

    private static Frame Render(Label label, bool crossAxisStart = false)
    {
        content = () => new Column(
            crossAxisAlignment: crossAxisStart ? CrossAxisAlignment.Start : CrossAxisAlignment.Start,
            children: [label.Color(ColorValue.FromRgba(1f, 1f, 1f))]);
        ThemeSwitcher.Apply(new AppleTheme(ThemeMode.Dark));
        try
        {
            using var provider = new EtchBackendProvider();
            using var orchestrator = new FrameOrchestrator(() => { }, () => { })
            {
                RenderBackend = provider.Backend,
            };
            orchestrator.BeginFrameCallback = () => provider.BeginFrame(Width, Height);
            orchestrator.PresentFrameCallback = (frame, background) => provider.PresentFrame(frame, ColorValue.FromRgba(0f, 0f, 0f));
            orchestrator.EndFrameCallback = frame => provider.EndFrame(frame);
            orchestrator.MountRoot<Host>(Width, Height);
            orchestrator.Tick();
            return new Frame(provider.CpuRenderer!.CaptureFrame()!.Pixels);
        }
        finally
        {
            ThemeSwitcher.Reset();
        }
    }

    /// <summary>A rendered frame: white text on black, so a pixel's red channel is its ink.</summary>
    private sealed record Frame(byte[] Pixels)
    {
        private int InkAt(int x, int y) => Pixels[(y * (int)Width + x) * 4];

        public long TotalInk()
        {
            long sum = 0;
            for (int i = 0; i < Pixels.Length; i += 4)
            {
                sum += Pixels[i];
            }
            return sum;
        }

        public (int Left, int Right) InkColumns()
        {
            int left = int.MaxValue, right = -1;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (InkAt(x, y) > 40)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }
            return (left, right);
        }

        public (int Top, int Bottom) InkRows(int fromRow, int toRow)
        {
            int top = -1, bottom = -1;
            for (int y = fromRow; y < toRow; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (InkAt(x, y) > 40)
                    {
                        top = top < 0 ? y : top;
                        bottom = y;
                        break;
                    }
                }
            }
            return (top, bottom);
        }

        /// <summary>The row with the longest unbroken horizontal run of ink, and that run's length.</summary>
        public (int Row, int Run) LongestRun()
        {
            int bestRow = -1, best = 0;
            for (int y = 0; y < Height; y++)
            {
                int run = 0;
                for (int x = 0; x < Width; x++)
                {
                    run = InkAt(x, y) > 100 ? run + 1 : 0;
                    if (run > best)
                    {
                        best = run;
                        bestRow = y;
                    }
                }
            }
            return (bestRow, best);
        }

        /// <summary>Mean ink x in the top quarter of the ink minus that in the bottom quarter.</summary>
        public double Slant()
        {
            (int top, int bottom) = InkRows(0, (int)Height);
            int quarter = Math.Max(1, (bottom - top) / 4);
            return MeanX(top, top + quarter) - MeanX(bottom - quarter, bottom + 1);
        }

        private double MeanX(int fromRow, int toRow)
        {
            double weighted = 0, total = 0;
            for (int y = fromRow; y < toRow; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int ink = InkAt(x, y);
                    weighted += ink * x;
                    total += ink;
                }
            }
            return total > 0 ? weighted / total : 0;
        }
    }
}
