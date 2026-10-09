#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Cascade.UI.Tests.OverlayTestKit;

namespace Cascade.UI.Tests.Controls;

/// <summary>
/// DataGrid's built-in batch editors: with <c>BatchEdit(true)</c>, the menu of a multi-row
/// selection ends with one "Set [Column] for selected rows…" item per visible editable column,
/// after the app's own <c>BatchActions</c>. Each opens a dialog with that column's editor,
/// prefilled with the value the rows share; Apply writes it to every selected row as one undo
/// step, Cancel changes nothing. Driven through a real <see cref="FrameOrchestrator"/> so the
/// dialog is the shipped overlay.
/// </summary>
[NotInParallel(["Dialog", "FocusManager"])]
public sealed class DataGridBatchEditTests
{
    private enum Priority
    {
        Low,
        Normal,
        High,
    }

    private sealed class Track
    {
        public string Title { get; set; } = "";
        public string Genre { get; set; } = "";
        public decimal Rating { get; set; }
        public DateOnly Added { get; set; }
        public Priority Priority { get; set; }
        public bool Favorite { get; set; }
        public string Length => Title.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static readonly IReadOnlyList<object> Priorities = [Priority.Low, Priority.Normal, Priority.High];

    private sealed class Page : Component
    {
        internal static Page? Instance;
        internal static readonly List<Track> Tracks = [];
        internal static readonly List<string> Changes = [];
        internal static bool WithBatchActions;
        internal static bool HideGenre;

        internal DataGrid<Track>? Grid;

        public Page()
        {
            Instance = this;
        }

        protected override Node Render()
        {
            var columns = new List<DataGridColumn<Track>>
            {
                DataGridColumn<Track>.Text("Title", t => t.Title, (t, v) => { t.Title = v; }),
                DataGridColumn<Track>.Text("Genre", t => t.Genre, (t, v) => { t.Genre = v; }),
                DataGridColumn<Track>.Number("Rating", t => t.Rating, (t, v) => { t.Rating = Convert.ToDecimal(v, System.Globalization.CultureInfo.InvariantCulture); }),
                DataGridColumn<Track>.Date("Added", t => t.Added, (t, v) => { t.Added = (DateOnly)v; }),
                DataGridColumn<Track>.Select("Priority", t => t.Priority, (t, v) => { t.Priority = (Priority)v; }, Priorities),
                DataGridColumn<Track>.Bool("Favorite", t => t.Favorite, (t, v) => { t.Favorite = v; }),
                DataGridColumn<Track>.Computed("Length", t => t.Length),
            };

            var grid = new DataGrid<Track>(new Bindable<IReadOnlyList<Track>>(Tracks, _ => { }), columns)
                .BatchEdit(true)
                .UndoEnabled(true)
                .OnChange(t => { Changes.Add(t.Title); });
            if (HideGenre)
            {
                grid.ColumnVisibility(new Dictionary<string, bool> { ["Genre"] = false });
            }

            if (WithBatchActions)
            {
                grid.BatchActions(rows => [ContextMenuItem.Action($"Delete {rows.Count}", () => { })]);
            }

            Grid = grid;
            return grid.Width(600).Height(400);
        }
    }

    [Before(Test)]
    public void SetUp()
    {
        Page.Tracks.Clear();
        Page.Tracks.AddRange(
        [
            new Track { Title = "One", Genre = "Jazz", Rating = 3m, Added = new DateOnly(2026, 1, 5), Priority = Priority.Normal },
            new Track { Title = "Two", Genre = "Jazz", Rating = 3m, Added = new DateOnly(2026, 1, 5), Priority = Priority.Normal },
            new Track { Title = "Three", Genre = "Rock", Rating = 4m, Added = new DateOnly(2026, 2, 1), Priority = Priority.Low },
        ]);
        Page.Changes.Clear();
        Page.WithBatchActions = false;
        Page.HideGenre = false;
    }

    private static (FrameOrchestrator Orch, DataGrid<Track> Grid) MountWithSelection(params int[] rows)
    {
        var orch = Mount<Page>();
        var grid = Page.Instance!.Grid!;
        ITabularDataNode tdn = grid;
        tdn.SelectRow(rows[0], false, false);
        for (int i = 1; i < rows.Length; i++)
        {
            tdn.SelectRow(rows[i], true, false);
        }

        return (orch, grid);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + OverlayTestKit.Timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static string Labels(IReadOnlyList<ContextMenuItem> items) =>
        string.Join("|", items.Select(i => i.Label ?? "---"));

    private static ContextMenuItem Item(IReadOnlyList<ContextMenuItem> items, string label) =>
        items.First(i => i.Label == label);

    [Test]
    public async Task Menu_ForAMultiSelection_ListsEveryVisibleEditableColumn_AfterTheAppsBatchActions()
    {
        Page.WithBatchActions = true;
        var (orch, grid) = MountWithSelection(0, 1);
        using var _ = orch;

        var menu = ((ITabularDataNode)grid).GetRowContextMenu(0);

        await Assert.That(Labels(menu)).IsEqualTo(
            "Delete 2|---|Set Title for selected rows…|Set Genre for selected rows…|Set Rating for selected rows…"
            + "|Set Added for selected rows…|Set Priority for selected rows…|Set Favorite for selected rows…");
    }

    [Test]
    public async Task Menu_HiddenColumnsAndASingleRow_GetNoEditor_AndWithoutBatchActionsThereIsNoSeparator()
    {
        Page.HideGenre = true;
        var (orch, grid) = MountWithSelection(0, 2);
        using var _ = orch;
        ITabularDataNode tdn = grid;

        await Assert.That(tdn.HasRowContextMenu).IsTrue();
        var menu = tdn.GetRowContextMenu(2);
        await Assert.That(menu[0].Label).IsEqualTo("Set Title for selected rows…");
        await Assert.That(Labels(menu)).DoesNotContain("Genre");

        // A row outside the selection, or a lone row, has no batch menu.
        tdn.SelectRow(1, false, false);
        await Assert.That(tdn.GetRowContextMenu(1)).IsEmpty();
    }

    [Test]
    public async Task Text_DialogIsPrefilledWithTheSharedValue_ApplyWritesEveryRow_AsOneUndoStep()
    {
        var (orch, grid) = MountWithSelection(0, 1);
        using var _ = orch;

        Item(((ITabularDataNode)grid).GetRowContextMenu(0), "Set Genre for selected rows…").OnClick!();
        Settle(orch);

        var tree = Top(orch).Tree!;
        await Assert.That(Find<Label>(tree, l => l.Text == "Set Genre").Text).IsEqualTo("Set Genre");
        var input = Find<TextInput>(tree);
        await Assert.That(input.Value.Value).IsEqualTo("Jazz");
        await Assert.That(FocusManager.FocusedElement).IsSameReferenceAs(input);

        input.Value.OnChange("Blues");
        orch.Tick();
        PressEnter(orch);
        Settle(orch);

        await Assert.That(orch.Overlays.Topmost).IsNull();

        // The menu item starts the edit without awaiting it; without a UI synchronization context
        // the write after the dialog closes runs on the thread pool, so wait for it.
        await WaitUntil(() => Page.Changes.Count == 2);
        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Genre))).IsEqualTo("Blues,Blues,Rock");
        await Assert.That(string.Join(",", Page.Changes)).IsEqualTo("One,Two");
        await Assert.That(((ITabularDataNode)grid).GetCellText(0, 1)).IsEqualTo("Blues");

        await Assert.That(((ITabularDataNode)grid).UndoEdit()).IsTrue();
        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Genre))).IsEqualTo("Jazz,Jazz,Rock");
        await Assert.That(((ITabularDataNode)grid).GetCellText(1, 1)).IsEqualTo("Jazz");
    }

    [Test]
    public async Task Text_RowsThatDiffer_StartEmpty_AndCancelChangesNothing()
    {
        var (orch, grid) = MountWithSelection(0, 2);
        using var _ = orch;

        var task = grid.ShowBatchEditorAsync(1, [0, 2], [Page.Tracks[0], Page.Tracks[2]]);
        Settle(orch);
        var input = Find<TextInput>(Top(orch).Tree!);
        await Assert.That(input.Value.Value).IsEqualTo("");
        await Assert.That(input.Placeholder.Resolve()).IsEqualTo("Multiple values");

        ClickInTop(orch, "Cancel");
        await Within(task);
        Settle(orch);

        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Genre))).IsEqualTo("Jazz,Jazz,Rock");
        await Assert.That(Page.Changes).IsEmpty();
    }

    [Test]
    public async Task InlineEdit_CommittedUnchanged_DoesNotCopyIntoTheOtherSelectedRows()
    {
        var (orch, grid) = MountWithSelection(0, 1);
        using var _ = orch;
        ITabularDataNode tdn = grid;

        // A click on a selected row enters edit mode; leaving it without typing used to write the
        // row's own value into every selected row (BatchEdit) and raise OnChange for each.
        tdn.BeginEdit(0, 0);
        await Assert.That(tdn.CommitEdit()).IsTrue();

        await Assert.That(tdn.IsEditing).IsFalse();
        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Title))).IsEqualTo("One,Two,Three");
        await Assert.That(Page.Changes).IsEmpty();

        // A real change still goes to every selected row.
        tdn.BeginEdit(0, 1);
        tdn.HandleEditChar('!');
        tdn.CommitEdit();
        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Genre))).IsEqualTo("Jazz!,Jazz!,Rock");
    }

    [Test]
    public async Task Number_ApplyIsDisabledUntilTheTextParses()
    {
        var (orch, grid) = MountWithSelection(0, 1, 2);
        using var _ = orch;

        var task = grid.ShowBatchEditorAsync(2, [0, 1, 2], [.. Page.Tracks]);
        Settle(orch);
        var tree = Top(orch).Tree!;
        var input = Find<TextInput>(tree);

        input.Value.OnChange("lots");
        orch.Tick();
        await Assert.That(FindButton(Top(orch).Tree!, "Apply").IsDisabled).IsTrue();
        PressEnter(orch);
        orch.Tick();
        await Assert.That(task.IsCompleted).IsFalse();

        input = Find<TextInput>(Top(orch).Tree!);
        input.Value.OnChange("5");
        orch.Tick();
        ClickInTop(orch, "Apply");
        await Within(task);

        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Rating))).IsEqualTo("5,5,5");
    }

    [Test]
    public async Task Select_ShowsTheColumnsOptions_PrefilledWithTheSharedOne_AndAppliesTheChosenOption()
    {
        var (orch, grid) = MountWithSelection(0, 1);
        using var _ = orch;

        var task = grid.ShowBatchEditorAsync(4, [0, 1], [Page.Tracks[0], Page.Tracks[1]]);
        Settle(orch);
        var select = Find<Select<int>>(Top(orch).Tree!);
        await Assert.That(select.Value.Value).IsEqualTo(1);
        await Assert.That(string.Join(",", select.Options!.Select(o => o.Label.Resolve()))).IsEqualTo("Low,Normal,High");

        select.Value.OnChange(2);
        orch.Tick();
        ClickInTop(orch, "Apply");
        await Within(task);

        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Priority))).IsEqualTo("High,High,Low");
    }

    [Test]
    public async Task Bool_AndDate_UseAToggleAndADatePicker()
    {
        var (orch, grid) = MountWithSelection(0, 1);
        using var _ = orch;

        var toggleTask = grid.ShowBatchEditorAsync(5, [0, 1], [Page.Tracks[0], Page.Tracks[1]]);
        Settle(orch);
        var toggle = Find<Toggle>(Top(orch).Tree!);
        await Assert.That(toggle.Value.Value).IsFalse();
        toggle.Value.OnChange(true);
        orch.Tick();
        ClickInTop(orch, "Apply");
        await Within(toggleTask);
        Settle(orch);
        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Favorite))).IsEqualTo("True,True,False");

        var dateTask = grid.ShowBatchEditorAsync(3, [0, 1], [Page.Tracks[0], Page.Tracks[1]]);
        Settle(orch);
        var picker = Find<DatePicker>(Top(orch).Tree!);
        await Assert.That(picker.Value.Value).IsEqualTo(new DateOnly(2026, 1, 5));
        picker.Value.OnChange(new DateOnly(2026, 3, 9));
        orch.Tick();
        ClickInTop(orch, "Apply");
        await Within(dateTask);

        await Assert.That(string.Join(",", Page.Tracks.Select(t => t.Added.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))))
            .IsEqualTo("2026-03-09,2026-03-09,2026-02-01");
    }
}
