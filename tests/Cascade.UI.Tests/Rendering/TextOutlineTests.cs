using System.Numerics;
using Cascade.UI.Backend.Etch;

namespace Cascade.UI.Tests.Rendering;

/// <summary>
/// Outlined text (<see cref="Label.Outline"/>, <see cref="TextOutline"/>): each glyph grown by
/// the outline width, drawn under the text in the outline colour.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class TextOutlineTests
{
    private static readonly ColorValue OutlineColor = ColorValue.FromRgba(0f, 0f, 0f);
    private static readonly ColorValue TextColor = ColorValue.FromRgba(1f, 1f, 1f);

    private static Func<Node> content = () => Node.Empty;

    private sealed class Host : Component
    {
        protected override Node Render()
        {
            return content();
        }
    }

    [Test]
    public async Task OutlinedLabel_DrawsEveryLinesOutlineBeforeAnyOfItsText()
    {
        var glyphs = Paint(new Label("Outlined first line\nand a second").Color(TextColor).Outline(OutlineColor, 3f));

        var outlines = glyphs.Where(op => op.OutlineWidth > 0f).ToList();
        var text = glyphs.Where(op => op.OutlineWidth == 0f).ToList();
        await Assert.That(outlines.Count).IsGreaterThanOrEqualTo(2).Because("one outline run per line");
        await Assert.That(outlines.Count).IsEqualTo(text.Count);
        await Assert.That(outlines.All(op => op.OutlineWidth == 3f && op.Color == OutlineColor)).IsTrue();
        await Assert.That(text.All(op => op.Color == TextColor)).IsTrue();
        // A thick outline must not cover the line above it: all outlines come first.
        int lastOutline = glyphs.FindLastIndex(op => op.OutlineWidth > 0f);
        int firstText = glyphs.FindIndex(op => op.OutlineWidth == 0f);
        await Assert.That(lastOutline).IsLessThan(firstText);
    }

    [Test]
    public async Task Label_WithoutAnOutline_OrWithAZeroWidthOne_DrawsOnlyItsText()
    {
        var plain = Paint(new Label("Plain"));
        var removed = Paint(new Label("Removed").Outline(OutlineColor, 3f).Outline(OutlineColor, 0f));
        var transparent = Paint(new Label("Transparent").Outline(ColorValue.FromRgba(0f, 0f, 0f, 0f), 3f));

        await Assert.That(plain.Any(op => op.OutlineWidth > 0f)).IsFalse();
        await Assert.That(removed.Any(op => op.OutlineWidth > 0f)).IsFalse();
        await Assert.That(transparent.Any(op => op.OutlineWidth > 0f)).IsFalse();
        await Assert.That(plain.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Outline_RingsTheGlyph_AtTheOutlineWidth()
    {
        int ring = OutlineRingWidth(scale: 1f, outlineWidth: 3f);

        // The anti-aliased pixels at either edge of the ring are neither black nor white.
        await Assert.That(ring).IsGreaterThanOrEqualTo(2);
        await Assert.That(ring).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task Outline_ScalesWithTheDisplay_LikeTheText()
    {
        int atTwo = OutlineRingWidth(scale: 2f, outlineWidth: 3f);

        // 3 logical pixels are 6 device pixels at 200% (less an anti-aliased edge pixel or two).
        await Assert.That(atTwo).IsGreaterThanOrEqualTo(5);
        await Assert.That(atTwo).IsLessThanOrEqualTo(6);
    }

    /// <summary>
    /// Draws a white, black-outlined "I" (Inter's is a plain stem) on mid grey through the CPU path
    /// and counts the black pixels left of the stem on a row through its middle.
    /// </summary>
    private static int OutlineRingWidth(float scale, float outlineWidth)
    {
        const float FontSize = 40f;
        int size = (int)(100 * scale);
        byte[] font = CpuFrame.InterRegular();
        byte[] pixels = CpuFrame.Render(size, size, ColorValue.FromRgba(0.5f, 0.5f, 0.5f), backend =>
        {
            backend.PushTransform(1, Matrix3x2.CreateScale(scale));
            ulong fontHandle = backend.LoadFont(font, 0);
            var face = backend.GetOrCreateFontFace(fontHandle, FontSize)
                ?? throw new InvalidOperationException("Failed to create font face for test font.");
            if (!face.TryGetGlyph('I', out uint glyphId))
            {
                throw new InvalidOperationException("Test font has no 'I'.");
            }
            Span<ushort> glyphIds = [(ushort)glyphId];
            Span<float> positions = [40f, 70f];
            backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, OutlineColor, outlineWidth);
            backend.DrawGlyphs(1, fontHandle, glyphIds, positions, FontSize, TextColor);
        });

        int row = (int)(55 * scale);
        int black = 0;
        for (int x = 0; x < size; x++)
        {
            int i = (row * size + x) * 4;
            if (pixels[i] > 200)
            {
                return black;
            }
            if (pixels[i] < 30)
            {
                black++;
            }
        }
        throw new InvalidOperationException("The white stem was not found on the row.");
    }

    private static List<EtchBackend.GlyphOp> Paint(Label label)
    {
        content = () => new Column(children: [label]);
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
}
