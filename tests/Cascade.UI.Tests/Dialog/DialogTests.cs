#pragma warning disable CA2000, CA1812, CA2025

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Cascade.UI.Tests.OverlayTestKit;

namespace Cascade.UI.Tests;

/// <summary>
/// Dialogs end to end through a real frame loop (layout, focus, input; nothing painted): they
/// show over the page, are modal, trap and restore focus, answer Escape/Enter, stack, and the
/// awaited task completes with the right result — every await bounded by a timeout, so a
/// regression to the old never-completing stub fails instead of hanging.
/// </summary>
[NotInParallel("Dialog")]
public sealed class DialogTests
{
    private sealed class Page : Component
    {
        public Page()
        {
            Instance = this;
        }

        public static Page? Instance { get; private set; }

        public int MainClicks { get; private set; }

        protected override Node Render()
        {
            return new Column(spacing: 8, children:
            [
                new Button("Main", () => { MainClicks++; }).Width(200).Height(40),
                new Button("Other", () => { }).Width(200).Height(40),
            ]);
        }
    }

    private sealed class ValueDialog : Component
    {
        protected override Node Render()
        {
            return new Column(children:
            [
                new Label("Pick a value"),
                new Button("Forty-two", () => { DialogContext.Close(42); }),
                new Button("Seven", () => { Dialog.Return(7); }),
            ]).Padding(16);
        }
    }

    private sealed class NamedDialog : Component
    {
        private readonly string name;

        public NamedDialog(string name)
        {
            this.name = name;
        }

        protected override Node Render()
        {
            return new Button($"Return {name}", () => { DialogContext.Close(name); }).Padding(16);
        }
    }

    private sealed class StackDialog : Component
    {
        public StackDialog()
        {
            Instance = this;
        }

        public static StackDialog? Instance { get; private set; }

        public Task<bool>? Nested { get; private set; }

        public DialogContext Context => DialogContext;

        protected override Node Render()
        {
            return new Row(spacing: 8, children:
            [
                new Button("More", () => { Nested = Dialog.ConfirmAsync("Are you sure?", "Really?"); }),
                new Button("Done", () => { DialogContext.Close("done"); }),
            ]).Padding(16);
        }
    }

    private sealed class LockedDialog : Component
    {
        protected override Node Render()
        {
            return new Button("Finish", () => { DialogContext.Close(); }).Padding(16);
        }
    }

    private static Button PageButton(FrameOrchestrator orch, string label)
    {
        return FindButton(orch.RootHost!.RenderedTree!, label);
    }

    // ── Showing ───────────────────────────────────────────────────────

    [Test]
    public async Task Confirm_ShowsCenteredModalDialog_LabelledByItsTitle()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Delete clip?", "This cannot be undone.", "Delete", "Keep", style: DialogStyle.Destructive);
        orch.Tick();

        var top = Top(orch);
        var panel = top.PanelBounds;
        await Assert.That(top.Kind).IsEqualTo(OverlayKind.Dialog);
        await Assert.That(top.BlocksInput).IsTrue();
        await Assert.That(top.Tree!.LayoutData.A11yRole).IsEqualTo(AccessibleRole.AlertDialog);
        await Assert.That(top.Tree.LayoutData.A11yLabel).IsEqualTo("Delete clip?");
        await Assert.That(Math.Abs(panel.X - ((800f - panel.Width) / 2f))).IsLessThanOrEqualTo(1f);
        await Assert.That(Math.Abs(panel.Y - ((600f - panel.Height) / 2f))).IsLessThanOrEqualTo(1f);
        await Assert.That(panel.Width).IsEqualTo(ThemeSwitcher.Current.Dialog.MaxWidth);
        await Assert.That(FindButton(top.Tree, "Delete").VariantName).IsEqualTo("destructive");
        await Assert.That(task.IsCompleted).IsFalse();

        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
    }

    [Test]
    public async Task Sizes_FixedWidth_ShrinksToTheWindow_FullScreenFillsIt()
    {
        using var orch = Mount<Page>(width: 400f, height: 300f);
        var medium = Dialog.ShowAsync<ValueDialog>(new DialogOptions { Size = DialogSize.Medium });
        orch.Tick();
        await Assert.That(Top(orch).PanelBounds.Width).IsEqualTo(400f - (2f * OverlayManager.DialogMargin));

        Dialog.Dismiss();
        var full = Dialog.ShowAsync<ValueDialog>(new DialogOptions { Size = DialogSize.FullScreen });
        orch.Tick();
        await Assert.That(Top(orch).PanelBounds).IsEqualTo(new Rect(0f, 0f, 400f, 300f));

        Dialog.Dismiss();
        await Within(medium);
        await Within(full);
    }

    [Test]
    public async Task AnchoredDialog_SitsBelowItsAnchor()
    {
        using var orch = Mount<Page>();
        var anchor = PageButton(orch, "Main");
        HitTester.TryGetAbsoluteBounds(orch.RootHost!.RenderedTree!, anchor, out var anchorBounds);

        var task = Dialog.ShowAsync<ValueDialog>(new DialogOptions { Position = DialogPosition.Anchored(anchor) });
        orch.Tick();

        await Assert.That(Top(orch).PanelBounds.Y).IsEqualTo(anchorBounds.Bottom + OverlayManager.PopoverGap);
        Dialog.Dismiss();
        await Within(task);
    }

    // ── Results ───────────────────────────────────────────────────────

    [Test]
    public async Task Confirm_ClickingConfirm_CompletesTrue()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Title", "Message", "Yes", "No");
        orch.Tick();

        ClickInTop(orch, "Yes");

        await Assert.That(await Within(task)).IsTrue();
        await Assert.That(orch.Overlays.HasLiveEntries).IsFalse();
    }

    [Test]
    public async Task Confirm_ClickingCancel_CompletesFalse()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Title", "Message", "Yes", "No");
        orch.Tick();

        ClickInTop(orch, "No");

        await Assert.That(await Within(task)).IsFalse();
    }

    [Test]
    public async Task Alert_Button_Completes()
    {
        using var orch = Mount<Page>();
        var task = Dialog.AlertAsync("Saved", "Your clip was pinned.", "Got it");
        orch.Tick();

        ClickInTop(orch, "Got it");

        await Within(task);
        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task Custom_DialogContextClose_And_DialogReturn_DeliverTheValue()
    {
        using var orch = Mount<Page>();
        var first = Dialog.ShowAsync<ValueDialog, int>();
        orch.Tick();
        ClickInTop(orch, "Forty-two");
        await Assert.That(await Within(first)).IsEqualTo(42);

        orch.Tick();
        var second = Dialog.ShowAsync<ValueDialog, int>();
        orch.Tick();
        ClickInTop(orch, "Seven");
        await Assert.That(await Within(second)).IsEqualTo(7);
    }

    [Test]
    public async Task Custom_InstanceOverload_PassesInputs_AndCannotBeShownTwice()
    {
        using var orch = Mount<Page>();
        var dialog = new NamedDialog("hello");
        var task = Dialog.ShowAsync<string>(dialog);
        orch.Tick();

        await Assert.That(() => Dialog.ShowAsync<string>(dialog)).Throws<InvalidOperationException>();

        ClickInTop(orch, "Return hello");
        await Assert.That(await Within(task)).IsEqualTo("hello");
    }

    [Test]
    public async Task Dismiss_CompletesWithNull()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ShowAsync<ValueDialog, string>();
        orch.Tick();

        Dialog.Dismiss();

        await Assert.That(await Within(task)).IsNull();
    }

    // ── Keyboard ──────────────────────────────────────────────────────

    [Test]
    public async Task Escape_DismissesAsCancel()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Title", "Message");
        orch.Tick();

        Key(orch, Cascade.UI.Key.Escape);

        await Assert.That(await Within(task)).IsFalse();
    }

    [Test]
    public async Task Enter_ActivatesTheDefaultButton()
    {
        using var orch = Mount<Page>();
        var confirmed = Dialog.ConfirmAsync("Title", "Message", "Yes", "No");
        orch.Tick();
        await Assert.That(FocusManager.FocusedElement is Button { } b && b.Label.Resolve() == "Yes").IsTrue();
        PressEnter(orch);
        await Assert.That(await Within(confirmed)).IsTrue();

        orch.Tick();
        var cancelled = Dialog.ConfirmAsync("Title", "Message", "Yes", "No", defaultButton: DialogDefault.Cancel);
        orch.Tick();
        PressEnter(orch);
        await Assert.That(await Within(cancelled)).IsFalse();
    }

    [Test]
    public async Task NotDismissable_IgnoresEscapeAndBackdrop()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ShowAsync<LockedDialog>(new DialogOptions { Dismissable = false });
        orch.Tick();

        Key(orch, Cascade.UI.Key.Escape);
        Click(orch, new Point(5f, 595f));
        orch.Tick();

        await Assert.That(task.IsCompleted).IsFalse();
        await Assert.That(orch.Overlays.HasLiveEntries).IsTrue();

        ClickInTop(orch, "Finish");
        await Within(task);
    }

    // ── Modality and focus ────────────────────────────────────────────

    [Test]
    public async Task Modal_PageBeneathGetsNoClicks_BackdropClickDismisses()
    {
        using var orch = Mount<Page>();
        var main = CenterOf(orch.RootHost!.RenderedTree!, PageButton(orch, "Main"));
        var task = Dialog.ConfirmAsync("Title", "Message");
        orch.Tick();

        Click(orch, main);

        await Assert.That(Page.Instance!.MainClicks).IsEqualTo(0);
        await Assert.That(await Within(task)).IsFalse();

        // With the dialog gone the page is clickable again.
        orch.Tick();
        Click(orch, main);
        await Assert.That(Page.Instance.MainClicks).IsEqualTo(1);
    }

    [Test]
    public async Task Focus_MovesIn_TabCyclesInside_AndReturnsOnClose()
    {
        using var orch = Mount<Page>();
        var main = PageButton(orch, "Main");
        FocusManager.RequestFocus(main);

        var task = Dialog.ConfirmAsync("Title", "Message", "Yes", "No");
        orch.Tick();
        var tree = Top(orch).Tree!;
        var yes = FindButton(tree, "Yes");
        var no = FindButton(tree, "No");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(yes);

        var seen = new List<Node?>();
        for (int i = 0; i < 4; i++)
        {
            Key(orch, Cascade.UI.Key.Tab);
            seen.Add(FocusManager.FocusedElement);
        }

        await Assert.That(seen.All(n => ReferenceEquals(n, yes) || ReferenceEquals(n, no))).IsTrue();
        await Assert.That(seen.Any(n => ReferenceEquals(n, no))).IsTrue();

        Key(orch, Cascade.UI.Key.Tab, ModifierKeys.Shift);
        await Assert.That(ReferenceEquals(FocusManager.FocusedElement, yes) || ReferenceEquals(FocusManager.FocusedElement, no)).IsTrue();

        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(main);
    }

    [Test]
    public async Task Stacked_DialogOpensDialog_EscapeClosesOnlyTheTop()
    {
        using var orch = Mount<Page>();
        var outer = Dialog.ShowAsync<StackDialog, string>();
        orch.Tick();
        var outerContext = StackDialog.Instance!.Context;
        await Assert.That(outerContext.IsTopmost).IsTrue();

        ClickInTop(orch, "More");
        orch.Tick();

        await Assert.That(orch.Overlays.Entries.Count).IsEqualTo(2);
        await Assert.That(outerContext.IsTopmost).IsFalse();
        await Assert.That(outerContext.IsOpen).IsTrue();
        var nested = StackDialog.Instance.Nested!;

        Key(orch, Cascade.UI.Key.Escape);
        await Assert.That(await Within(nested)).IsFalse();
        await Assert.That(outer.IsCompleted).IsFalse();
        await Assert.That(outerContext.IsTopmost).IsTrue();

        // Focus went back into the outer dialog (to the button that opened the nested one).
        await Assert.That(FocusManager.FocusedElement is Button { } focused && focused.Label.Resolve() == "More").IsTrue();

        Settle(orch);
        ClickInTop(orch, "Done");
        await Assert.That(await Within(outer)).IsEqualTo("done");
        await Assert.That(outerContext.IsOpen).IsFalse();
    }

    // ── Prompt ────────────────────────────────────────────────────────

    [Test]
    public async Task Prompt_TypingAndEnter_ReturnsTheText()
    {
        using var orch = Mount<Page>();
        var task = Dialog.PromptAsync("Rename", "New name for the clip", value: "Clip");
        orch.Tick();
        await Assert.That(FocusManager.FocusedElement).IsTypeOf<TextInput>();

        Type(orch, " 2");
        orch.Tick();
        PressEnter(orch);

        await Assert.That(await Within(task)).IsEqualTo("Clip 2");
    }

    [Test]
    public async Task Prompt_Validation_BlocksConfirm_AndShowsItsMessage()
    {
        using var orch = Mount<Page>();
        var task = Dialog.PromptAsync(
            "Rename",
            "New name",
            confirmLabel: "Rename",
            validate: name => name.Trim().Length > 0 ? ValidationResult.Ok : ValidationResult.Error("Name cannot be empty."));
        orch.Tick();

        await Assert.That(FindButton(Top(orch).Tree!, "Rename").IsDisabled).IsTrue();
        PressEnter(orch);
        orch.Tick();
        await Assert.That(task.IsCompleted).IsFalse();
        await Assert.That(TryFind<Label>(Top(orch).Tree!, l => l.Text == "Name cannot be empty.")).IsNotNull();

        Type(orch, "ok");
        orch.Tick();
        await Assert.That(FindButton(Top(orch).Tree!, "Rename").IsDisabled).IsFalse();
        await Assert.That(TryFind<Label>(Top(orch).Tree!, l => l.Text == "Name cannot be empty.")).IsNull();

        ClickInTop(orch, "Rename");
        await Assert.That(await Within(task)).IsEqualTo("ok");
    }

    [Test]
    public async Task Prompt_Cancel_ReturnsNull()
    {
        using var orch = Mount<Page>();
        var task = Dialog.PromptAsync("Rename", "New name", value: "x");
        orch.Tick();

        ClickInTop(orch, "Cancel");

        await Assert.That(await Within(task)).IsNull();
    }

    // ── Progress ──────────────────────────────────────────────────────

    [Test]
    public async Task Progress_Updates_Cancels_AndClosesOnDispose()
    {
        using var orch = Mount<Page>();
        var progress = Dialog.ShowProgress("Exporting", "Starting", cancellable: true);
        int cancelled = 0;
        progress.OnCancelled += () => { cancelled++; };
        orch.Tick();

        progress.Update(0.5f, "Halfway");
        orch.Tick();
        var tree = Top(orch).Tree!;
        await Assert.That(Find<ProgressBar>(tree).Value).IsEqualTo(0.5f);
        await Assert.That(TryFind<Label>(tree, l => l.Text == "Halfway")).IsNotNull();

        // Not dismissable: Escape leaves it up.
        Key(orch, Cascade.UI.Key.Escape);
        await Assert.That(orch.Overlays.HasLiveEntries).IsTrue();

        ClickInTop(orch, "Cancel");
        await Assert.That(progress.IsCancelled).IsTrue();
        await Assert.That(cancelled).IsEqualTo(1);
        orch.Tick();
        await Assert.That(FindButton(Top(orch).Tree!, "Cancel").IsDisabled).IsTrue();

        progress.Dispose();
        await Assert.That(orch.Overlays.HasLiveEntries).IsFalse();
    }

    // ── Animation and idle ────────────────────────────────────────────

    [Test]
    public async Task OpenAnimation_HoldsFramesOnlyUntilItEnds()
    {
        bool reducedMotion = ThemeSwitcher.Current.Motion.ReducedMotion;
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Title", "Message");
        orch.Tick();

        if (!reducedMotion && !ThemeSwitcher.Current.Dialog.EnterTransition.Model.IsNoneModel)
        {
            await Assert.That(orch.Sentinels.OverlayAnimationsActive).IsTrue();
        }

        Settle(orch);

        // Other subsystems' process-wide state (left by other tests) may hold the loop; the
        // overlay must not be what does.
        await Assert.That(Top(orch).Visibility).IsEqualTo(1f);
        await Assert.That(orch.Sentinels.OverlayAnimationsActive).IsFalse();
        await Assert.That(orch.Sentinels.WouldHoldFrameLoop).IsEqualTo(OthersHold(orch.Sentinels));
        await Assert.That(orch.IsFrameRequested).IsEqualTo(OthersHold(orch.Sentinels));

        // Closing animates out, then the dialog is unmounted and the loop idles again.
        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
        Settle(orch);
        await Assert.That(orch.Overlays.HasEntries).IsFalse();
        await Assert.That(orch.IsFrameRequested).IsEqualTo(OthersHold(orch.Sentinels));
    }

    private static bool OthersHold(FrameLoopSentinels s)
    {
        return s.AnimationsActive || s.SharedAnimationsActive || s.RenderDirtyCount > 0 || s.TimedFramesHoldLoop
            || s.SpinnersActive || s.ChartAnimationsActive || s.ContinuousCanvasesActive
            || s.StateTransitionsActive;
    }

    [Test]
    public async Task WindowDeactivation_KeepsDialogs()
    {
        using var orch = Mount<Page>();
        var task = Dialog.ConfirmAsync("Title", "Message");
        orch.Tick();

        orch.Input.HandleWindowDeactivation();

        await Assert.That(orch.Overlays.HasLiveEntries).IsTrue();
        Key(orch, Cascade.UI.Key.Escape);
        await Within(task);
    }
}
