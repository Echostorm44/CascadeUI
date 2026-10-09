namespace Cascade.UI.CliFixture;

/// <summary>
/// Fixture for the dialog/overlay integration tests (CASCADE_FIXTURE_VIEW=dialogs): buttons that
/// open a destructive Confirm, a Prompt with validation, a Confirm from a background thread, a
/// popover anchored to its button and a bottom sheet, beside a ScrollView (a retained layer the
/// overlays must paint over). "Last: …" shows the most recent result.
/// </summary>
internal sealed class DialogsView : Component
{
    private readonly NodeRef<Button> detailsButton = new();
    private string last = "none";

    protected override Node Render()
    {
        return new Row(spacing: 12, children:
        [
            new Column(spacing: 8, children:
            [
                new Label($"Last: {last}"),
                new Button("Delete clip", () => { _ = ConfirmDeleteAsync(); }),
                new Button("Rename clip", () => { _ = RenameAsync(); }),
                new Button("Worker confirm", () => { ConfirmFromBackground(); }),
                new Button("Show details", () => { ShowDetails(); }).Ref(detailsButton),
                new Button("Share", () => { _ = ShareAsync(); }),
            ]).Width(280),
            new ScrollView(new Column(spacing: 6, children: Rows())).Width(300).Height(420),
        ]).Padding(EdgeInsets.All(12));
    }

    private async Task ConfirmDeleteAsync()
    {
        bool confirmed = await Dialog.ConfirmAsync(
            "Delete clip?",
            "This permanently removes \"Meeting notes\" from your history.",
            confirmLabel: "Delete",
            cancelLabel: "Keep",
            style: DialogStyle.Destructive);
        Record(confirmed ? "deleted" : "kept");
    }

    private async Task RenameAsync()
    {
        string? name = await Dialog.PromptAsync(
            "Rename clip",
            "Give the clip a name you will recognise.",
            placeholder: "Clip name",
            value: "Meeting notes",
            confirmLabel: "Rename",
            validate: text => text.Trim().Length > 0 ? ValidationResult.Ok : ValidationResult.Error("A name cannot be empty."));
        Record(name is null ? "rename cancelled" : $"renamed to {name}");
    }

    // The overlay API marshals to the UI thread itself; only the result is posted back.
    private void ConfirmFromBackground()
    {
        _ = Task.Run(async () =>
        {
            bool confirmed = await Dialog.ConfirmAsync("Background check", "Opened from a worker thread.", "Proceed", "Stop").ConfigureAwait(false);
            Dispatcher.Post(() => { Record($"background {(confirmed ? "proceed" : "stop")}"); });
        });
    }

    private void ShowDetails()
    {
        if (detailsButton.Node is { } anchor)
        {
            Popover.Show<DetailsPopover>(anchor, new PopoverOptions { PreferredSide = PopoverSide.Right, AccessibleLabel = "Clip details" });
        }
    }

    private async Task ShareAsync()
    {
        string? action = await BottomSheet.ShowActionsAsync("Share clip", ["Copy link", "Send by email"]);
        Record(action is null ? "share cancelled" : $"share {action}");
    }

    private void Record(string result)
    {
        last = result;
        Invalidate();
    }

    private static Node[] Rows()
    {
        var rows = new Node[40];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Label($"History row {i + 1:D2} — under the overlay");
        }

        return rows;
    }

    private sealed class DetailsPopover : Component
    {
        protected override Node Render()
        {
            return new Column(spacing: 6, children:
            [
                new Label("Meeting notes"),
                new Label("Copied 3 minutes ago from Notepad"),
                new Button("Close", () => { DialogContext.Close(); }),
            ]).Padding(12);
        }
    }
}
