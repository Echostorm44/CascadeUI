using System.Numerics;
using TUnit.Core;

namespace Cascade.UI.Tests;

/// <summary>
/// Tests for <see cref="NodePainter"/> — the tree-walking paint pass that
/// reads laid-out nodes and issues DrawContext calls.
/// </summary>
public class NodePainterTests
{
    private readonly FluentTheme theme = new();

    // ── Helper to create a DrawContext without a backend ────────────────

    private static DrawContext CreateTestContext(float width = 800, float height = 600)
    {
        var ctx = new DrawContext
        {
            Size = new Size(width, height),
            PixelRatio = 1f
        };
        return ctx;
    }

    // ── Empty / invisible nodes ────────────────────────────────────────

    [Test]
    public async Task Paint_EmptyNode_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        // Should not throw — Node.Empty has IsLayoutEmpty = true
        await Assert.That(() => painter.Paint(Node.Empty)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_InvisibleNode_Skipped()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("hidden");
        label.LayoutData.IsVisible = false;
        label.LayoutData.Bounds = new Rect(0, 0, 100, 30);

        // Should not throw — invisible nodes are skipped
        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_ZeroSizeNode_Skipped()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("zero");
        label.LayoutData.Bounds = new Rect(10, 10, 0, 0);

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    // ── Label ──────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Label_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Hello World");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_LabelWithLocKey_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label(new LocKey("test.label"));
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_MultiLineLabel_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Line one\n\nLine three\nLine four");
        label.LayoutData.Bounds = new Rect(10, 10, 300, 200);

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    // ── PaintText single- vs multi-line decision ───────────────────────
    // Regression guard: a plain Label with no MaxLines paints with maxLines == 0
    // (the "unlimited" sentinel). Treating that as single-line centred a multi-line
    // body's FIRST line on the vertical middle of the full text box, leaving a large
    // gap above the text. Only maxLines == 1 is truly single-line; unlimited text is
    // multi-line when it has a newline or overflows the available width.

    [Test]
    public async Task ShouldCenterAsSingleLine_UnlimitedWithNewline_IsMultiLine()
    {
        // maxLines == 0, explicit newline → must NOT be treated as single-line.
        bool single = NodePainter.ShouldCenterAsSingleLine(
            maxLines: 0, text: "Hi all,\n\nBody text", singleLineWidth: 40f, availableWidth: 300f);

        await Assert.That(single).IsFalse();
    }

    [Test]
    public async Task ShouldCenterAsSingleLine_UnlimitedThatWraps_IsMultiLine()
    {
        // maxLines == 0, no newline but wider than the box → wraps → multi-line.
        bool single = NodePainter.ShouldCenterAsSingleLine(
            maxLines: 0, text: "a very long single run of text", singleLineWidth: 500f, availableWidth: 120f);

        await Assert.That(single).IsFalse();
    }

    [Test]
    public async Task ShouldCenterAsSingleLine_UnlimitedThatFits_IsSingleLine()
    {
        // maxLines == 0, no newline, fits → genuinely one line → single-line centring
        // (so folder names, list rows and other plain labels stay visually centred).
        bool single = NodePainter.ShouldCenterAsSingleLine(
            maxLines: 0, text: "Inbox", singleLineWidth: 42f, availableWidth: 200f);

        await Assert.That(single).IsTrue();
    }

    [Test]
    public async Task ShouldCenterAsSingleLine_MaxLinesOne_IsAlwaysSingleLine()
    {
        // maxLines == 1 clamps to one line even when the raw text is wide.
        bool single = NodePainter.ShouldCenterAsSingleLine(
            maxLines: 1, text: "clamped to one line and truncated", singleLineWidth: 999f, availableWidth: 100f);

        await Assert.That(single).IsTrue();
    }

    [Test]
    public async Task ShouldCenterAsSingleLine_BoundedMultiLineWithNewline_IsMultiLine()
    {
        // maxLines == 3 with a newline → multi-line flow, not single-line centring.
        bool single = NodePainter.ShouldCenterAsSingleLine(
            maxLines: 3, text: "first\nsecond", singleLineWidth: 30f, availableWidth: 300f);

        await Assert.That(single).IsFalse();
    }

    // ── Button ─────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Button_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var button = new Button("Click Me", () => { });
        button.LayoutData.Bounds = new Rect(10, 10, 120, 36);

        await Assert.That(() => painter.Paint(button)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_DisabledButton_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var button = new Button("Disabled", () => { }).Disabled();
        button.LayoutData.Bounds = new Rect(10, 10, 120, 36);

        await Assert.That(() => painter.Paint(button)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_ButtonWithVariant_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        // Unknown variant falls back to default theme
        var button = new Button("Ghost", () => { }).Variant("ghost");
        button.LayoutData.Bounds = new Rect(10, 10, 120, 36);

        await Assert.That(() => painter.Paint(button)).ThrowsNothing();
    }

    // ── TextInput ──────────────────────────────────────────────────────

    [Test]
    public async Task Paint_TextInput_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var input = new TextInput(new Bindable<string>("hello", _ => { }));
        input.LayoutData.Bounds = new Rect(10, 10, 200, 36);

        await Assert.That(() => painter.Paint(input)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_TextInputWithPlaceholder_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var input = new TextInput(new Bindable<string>("", _ => { }),
            placeholder: "Enter name");
        input.LayoutData.Bounds = new Rect(10, 10, 200, 36);

        await Assert.That(() => painter.Paint(input)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_DisabledTextInput_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var input = new TextInput(new Bindable<string>("text", _ => { })).Disabled();
        input.LayoutData.Bounds = new Rect(10, 10, 200, 36);

        await Assert.That(() => painter.Paint(input)).ThrowsNothing();
    }

    // ── Checkbox ───────────────────────────────────────────────────────

    [Test]
    public async Task Paint_UncheckedCheckbox_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var cb = new Checkbox(new Bindable<bool>(false, _ => { }), "Accept terms");
        cb.LayoutData.Bounds = new Rect(10, 10, 200, 24);

        await Assert.That(() => painter.Paint(cb)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_CheckedCheckbox_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var cb = new Checkbox(new Bindable<bool>(true, _ => { }), "Selected");
        cb.LayoutData.Bounds = new Rect(10, 10, 200, 24);

        await Assert.That(() => painter.Paint(cb)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_IndeterminateCheckbox_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var cb = new Checkbox(CheckboxValue.Indeterminate, _ => { }, "Partial");
        cb.LayoutData.Bounds = new Rect(10, 10, 200, 24);

        await Assert.That(() => painter.Paint(cb)).ThrowsNothing();
    }

    // ── Toggle ─────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Toggle_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var toggle = new Toggle(new Bindable<bool>(true, _ => { }), "Dark Mode");
        toggle.LayoutData.Bounds = new Rect(10, 10, 200, 24);

        await Assert.That(() => painter.Paint(toggle)).ThrowsNothing();
    }

    // ── ProgressBar ────────────────────────────────────────────────────

    [Test]
    public async Task Paint_DeterminateProgressBar_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var pb = new ProgressBar(0.65f);
        pb.LayoutData.Bounds = new Rect(10, 10, 300, 20);

        await Assert.That(() => painter.Paint(pb)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_IndeterminateProgressBar_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var pb = new ProgressBar(ProgressMode.Indeterminate);
        pb.LayoutData.Bounds = new Rect(10, 10, 300, 20);

        await Assert.That(() => painter.Paint(pb)).ThrowsNothing();
    }

    // ── Separator ──────────────────────────────────────────────────────

    [Test]
    public async Task Paint_HorizontalSeparator_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var sep = new Separator(Orientation.Horizontal);
        sep.LayoutData.Bounds = new Rect(0, 50, 400, 1);

        await Assert.That(() => painter.Paint(sep)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_VerticalSeparator_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var sep = new Separator(Orientation.Vertical, 2f, new ColorValue("#FF0000"));
        sep.LayoutData.Bounds = new Rect(200, 0, 2, 400);

        await Assert.That(() => painter.Paint(sep)).ThrowsNothing();
    }

    // ── Card ───────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Card_PaintsBackgroundAndContent()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var content = new Label("Card content");
        content.LayoutData.Bounds = new Rect(26, 26, 248, 30);

        var card = new Card(content);
        card.LayoutData.Bounds = new Rect(10, 10, 300, 200);

        await Assert.That(() => painter.Paint(card)).ThrowsNothing();
    }

    // ── Spinner ────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Spinner_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var spinner = new Spinner();
        spinner.LayoutData.Bounds = new Rect(10, 10, 32, 32);

        await Assert.That(() => painter.Paint(spinner)).ThrowsNothing();
    }

    // ── Slider ─────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_Slider_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var slider = new Slider(new Bindable<float>(0.5f, _ => { }), 0f, 1f);
        slider.LayoutData.Bounds = new Rect(10, 10, 300, 30);

        await Assert.That(() => painter.Paint(slider)).ThrowsNothing();
    }

    // ── Image ──────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_ImageWithoutSource_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var img = new Image("nonexistent.png");
        img.LayoutData.Bounds = new Rect(10, 10, 200, 150);

        // Source is null for path-based Image that hasn't been loaded
        await Assert.That(() => painter.Paint(img)).ThrowsNothing();
    }

    // ── Layout containers ──────────────────────────────────────────────

    [Test]
    public async Task Paint_Row_PaintsChildren()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label1 = new Label("A");
        label1.LayoutData.Bounds = new Rect(0, 0, 50, 20);

        var label2 = new Label("B");
        label2.LayoutData.Bounds = new Rect(58, 0, 50, 20);

        var row = new Row(spacing: 8, children: [label1, label2]);
        row.LayoutData.Bounds = new Rect(0, 0, 108, 20);

        await Assert.That(() => painter.Paint(row)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_Column_PaintsChildren()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label1 = new Label("Top");
        label1.LayoutData.Bounds = new Rect(0, 0, 100, 20);

        var label2 = new Label("Bottom");
        label2.LayoutData.Bounds = new Rect(0, 28, 100, 20);

        var col = new Column(spacing: 8, children: [label1, label2]);
        col.LayoutData.Bounds = new Rect(0, 0, 100, 48);

        await Assert.That(() => painter.Paint(col)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_Stack_PaintsBottomToTop()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var bg = new Label("Background");
        bg.LayoutData.Bounds = new Rect(0, 0, 200, 100);

        var fg = new Label("Foreground");
        fg.LayoutData.Bounds = new Rect(10, 10, 180, 80);

        var stack = new Stack(bg, fg);
        stack.LayoutData.Bounds = new Rect(0, 0, 200, 100);

        await Assert.That(() => painter.Paint(stack)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_Center_PaintsChild()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Centered");
        label.LayoutData.Bounds = new Rect(50, 40, 100, 20);

        var center = new Center(label);
        center.LayoutData.Bounds = new Rect(0, 0, 200, 100);

        await Assert.That(() => painter.Paint(center)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_Spacer_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var spacer = new Spacer();
        spacer.LayoutData.Bounds = new Rect(0, 0, 100, 20);

        await Assert.That(() => painter.Paint(spacer)).ThrowsNothing();
    }

    // ── Visual modifiers ───────────────────────────────────────────────

    [Test]
    public async Task Paint_NodeWithBackground_DrawsBackground()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("With Background");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.BackgroundColor = new ColorValue("#FF0000");

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithOpacity_AppliesOpacity()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Transparent");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.Opacity = 0.5f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithTranslate_AppliesTranslate()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Shifted");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.TranslateX = 5f;
        label.LayoutData.TranslateY = -3f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithScale_AppliesScale()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Scaled");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.Scale = 1.5f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithClip_AppliesClip()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Clipped");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.ClipContent = true;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithRoundedClip_AppliesRoundedClip()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Rounded Clip");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.ClipContent = true;
        label.LayoutData.CornerRadiusValue = 8f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithBorder_DrawsBorder()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Bordered");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.BorderColor = new ColorValue("#333333");
        label.LayoutData.BorderWidth = 1f;
        label.LayoutData.BorderRadiusValue = 4f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_NodeWithPerSideBorder_DrawsPerSideBorders()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var label = new Label("Side borders");
        label.LayoutData.Bounds = new Rect(10, 10, 200, 30);
        label.LayoutData.BorderTopColor = new ColorValue("#FF0000");
        label.LayoutData.BorderTopWidth = 2f;
        label.LayoutData.BorderBottomColor = new ColorValue("#0000FF");
        label.LayoutData.BorderBottomWidth = 1f;

        await Assert.That(() => painter.Paint(label)).ThrowsNothing();
    }

    // ── Nested composition ─────────────────────────────────────────────

    [Test]
    public async Task Paint_NestedLayout_PaintsEntireTree()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        // Build a realistic mini-tree: Column with Card containing a Row of buttons
        var btn1 = new Button("OK", () => { });
        btn1.LayoutData.Bounds = new Rect(26, 76, 80, 36);

        var btn2 = new Button("Cancel", () => { });
        btn2.LayoutData.Bounds = new Rect(114, 76, 80, 36);

        var btnRow = new Row(spacing: 8, children: [btn1, btn2]);
        btnRow.LayoutData.Bounds = new Rect(26, 76, 168, 36);

        var title = new Label("Confirmation");
        title.LayoutData.Bounds = new Rect(26, 26, 248, 30);

        var content = new Column(spacing: 16, children: [title, btnRow]);
        content.LayoutData.Bounds = new Rect(26, 26, 248, 86);

        var card = new Card(content);
        card.LayoutData.Bounds = new Rect(10, 10, 300, 130);

        var root = new Column(spacing: 0, children: [card]);
        root.LayoutData.Bounds = new Rect(0, 0, 320, 150);

        await Assert.That(() => painter.Paint(root)).ThrowsNothing();
    }

    // ── Badge ──────────────────────────────────────────────────────────

    [Test]
    public async Task Paint_DotBadge_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var icon = new Label("📧");
        icon.LayoutData.Bounds = new Rect(10, 10, 24, 24);

        var badge = new Badge(true, icon);
        badge.LayoutData.Bounds = new Rect(10, 10, 24, 24);

        await Assert.That(() => painter.Paint(badge)).ThrowsNothing();
    }

    [Test]
    public async Task Paint_CountBadge_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var icon = new Label("📧");
        icon.LayoutData.Bounds = new Rect(10, 10, 24, 24);

        var badge = new Badge(5, icon);
        badge.LayoutData.Bounds = new Rect(10, 10, 24, 24);

        await Assert.That(() => painter.Paint(badge)).ThrowsNothing();
    }

    // ── LinkButton ─────────────────────────────────────────────────────

    [Test]
    public async Task Paint_LinkButton_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var link = new LinkButton("Learn more", () => { });
        link.LayoutData.Bounds = new Rect(10, 10, 100, 24);

        await Assert.That(() => painter.Paint(link)).ThrowsNothing();
    }

    // ── IconButton ─────────────────────────────────────────────────────

    [Test]
    public async Task Paint_IconButton_DoesNotThrow()
    {
        var ctx = CreateTestContext();
        var painter = new NodePainter(ctx, theme);

        var ib = new IconButton(default, () => { });
        ib.LayoutData.Bounds = new Rect(10, 10, 36, 36);

        await Assert.That(() => painter.Paint(ib)).ThrowsNothing();
    }
}
