using Cascade.UI;

namespace NoteEditor;

// ── Models ───────────────────────────────────────────────────────────────────

internal sealed record NoteHeader(
    string Id,
    string Title,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    string Preview
);

internal enum NoteSortOrder
{
    ModifiedDesc,
    ModifiedAsc,
    TitleAsc,
    TitleDesc,
    CreatedDesc
}

// ── Mergeable text command ───────────────────────────────────────────────────

internal sealed class TypeTextCommand : IMergeableCommand
{
    private readonly Func<string> getter;
    private readonly Action<string> setter;
    private string oldValue;
    private string newValue;
    private DateTime timestamp;

    public TypeTextCommand(
        string description,
        Func<string> getter,
        Action<string> setter,
        string newValue)
    {
        this.getter = getter;
        this.setter = setter;
        oldValue = getter();
        this.newValue = newValue;
        timestamp = DateTime.UtcNow;
        Description = description;
    }

    public string Description { get; private set; }

    public void Execute()
    {
        setter(newValue);
    }

    public void Undo()
    {
        setter(oldValue);
    }

    public bool CanMerge(IUndoCommand previous)
    {
        if (previous is not TypeTextCommand prev)
        {
            return false;
        }

        if ((timestamp - prev.timestamp).TotalSeconds > 2)
        {
            return false;
        }

        if (newValue.Length > 0 && (newValue[^1] == ' ' || newValue[^1] == '\n'))
        {
            return false;
        }

        return true;
    }

    public void Merge(IUndoCommand previous)
    {
        var prev = (TypeTextCommand)previous;
        prev.newValue = newValue;
        prev.timestamp = timestamp;
        prev.Description = "Type text";
    }
}

// ── Note editor page ─────────────────────────────────────────────────────────

#pragma warning disable CA1812 // Instantiated via App.Run<T>()
internal sealed class NoteEditorPage : Component
{
    // Simplified icon SVG paths (M/L/H/V/Z only for the simple SVG renderer)
    private static readonly Icon PlusIcon = new(
        "M12 5v14M5 12h14",
        new Size(24, 24), 20f, "New note");

    private static readonly Icon SaveIcon = new(
        ["M19 21H5V5h11l5 5v11z",
         "M17 21v-8H7v8", "M7 3v5h8"],
        new Size(24, 24), 20f, "Save");

    private static readonly Icon FileTextIcon = new(
        ["M14 2H6v20h12V8z",
         "M14 2v6h6", "M16 13H8", "M16 17H8", "M10 9H8"],
        new Size(24, 24), 24f, "File");

    private readonly UndoStack undoStack = new(maxDepth: 500);

    private string? selectedNoteId;
    private NoteSortOrder sortOrder = NoteSortOrder.ModifiedDesc;
    private bool showWordCount = true;

    private List<NoteHeader> notes = [];
    private string currentTitle = "";
    private string currentBody = "";
    private bool justSaved;

    protected override Task OnMounted()
    {
        // Seed some sample notes
        var now = DateTime.Now;
        notes =
        [
            new NoteHeader("1", "Welcome to Note Editor",
                now.AddDays(-7), now.AddHours(-1),
                "This is a demo of the Cascade UI note editor..."),
            new NoteHeader("2", "Shopping List",
                now.AddDays(-3), now.AddDays(-1),
                "Milk, eggs, bread, butter, coffee..."),
            new NoteHeader("3", "Meeting Notes",
                now.AddDays(-1), now.AddMinutes(-30),
                "Discussed Q1 roadmap. Action items: review..."),
            new NoteHeader("4", "Ideas",
                now.AddDays(-14), now.AddDays(-5),
                "Build a note editor as a golden example...")
        ];

        // Load the first note
        LoadNote("1");
        return Task.CompletedTask;
    }

    private void LoadNote(string noteId)
    {
        var header = notes.Find(n => n.Id == noteId);
        if (header is null)
        {
            return;
        }

        selectedNoteId = noteId;
        currentTitle = header.Title;
        currentBody = GetBodyForNote(noteId);
        undoStack.Clear();
        undoStack.MarkSaved();
        Invalidate();
    }

    private static string GetBodyForNote(string noteId)
    {
        return noteId switch
        {
            "1" => "This is a demo of the Cascade UI note editor.\n\n"
                 + "It demonstrates UndoStack with mergeable commands, "
                 + "MenuBar with keyboard shortcuts, SplitView sidebar, "
                 + "and StatusBar with word count.\n\n"
                 + "Try editing text — consecutive typing merges into a "
                 + "single undo step. Press Ctrl+Z to undo.",
            "2" => "Milk\nEggs\nBread\nButter\nCoffee\nOrange juice\nCheese",
            "3" => "Discussed Q1 roadmap.\n\n"
                 + "Action items:\n"
                 + "- Review design docs by Friday\n"
                 + "- Schedule follow-up with platform team\n"
                 + "- Update backlog priorities",
            "4" => "Build a note editor as a golden example for Cascade UI.\n\n"
                 + "Key features:\n"
                 + "- Undo/redo with mergeable text commands\n"
                 + "- Sidebar note list with sort options\n"
                 + "- Auto-save with dirty tracking\n"
                 + "- Menu bar integration",
            _ => ""
        };
    }

    private void SaveCurrentNote()
    {
        if (selectedNoteId is null || !undoStack.IsDirty)
        {
            return;
        }

        var index = notes.FindIndex(n => n.Id == selectedNoteId);
        if (index >= 0)
        {
            var preview = currentBody.Length > 100
                ? currentBody[..100]
                : currentBody;
            notes[index] = notes[index] with
            {
                Title = currentTitle,
                ModifiedAt = DateTime.Now,
                Preview = preview.Replace('\n', ' ')
            };
        }

        undoStack.MarkSaved();
        justSaved = true;
        Invalidate();

        // Clear the "just saved" indicator after a delay
        _ = ClearSavedIndicatorAsync();
    }

    private async Task ClearSavedIndicatorAsync()
    {
        await Task.Delay(1500);
        justSaved = false;
        Invalidate();
    }

    private void CreateNewNote()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.Now;
        notes.Insert(0, new NoteHeader(id, "Untitled", now, now, ""));
        LoadNote(id); // LoadNote invalidates
    }

    private void DeleteCurrentNote()
    {
        if (selectedNoteId is null)
        {
            return;
        }

        notes.RemoveAll(n => n.Id == selectedNoteId);
        selectedNoteId = null;
        currentTitle = "";
        currentBody = "";
        undoStack.Clear();
        Invalidate();
    }

    private int WordCount =>
        string.IsNullOrWhiteSpace(currentBody)
            ? 0
            : currentBody.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private List<NoteHeader> SortedNotes => sortOrder switch
    {
        NoteSortOrder.ModifiedAsc => [.. notes.OrderBy(n => n.ModifiedAt)],
        NoteSortOrder.TitleAsc => [.. notes.OrderBy(n => n.Title)],
        NoteSortOrder.TitleDesc => [.. notes.OrderByDescending(n => n.Title)],
        NoteSortOrder.CreatedDesc => [.. notes.OrderByDescending(n => n.CreatedAt)],
        _ => [.. notes.OrderByDescending(n => n.ModifiedAt)]
    };

    // ── Undo commands ────────────────────────────────────────────────────

    private void OnTitleChanged(string newTitle)
    {
        var oldTitle = currentTitle;
        undoStack.Execute(UndoCommand.Create(
            description: "Edit title",
            execute: () => { currentTitle = newTitle; },
            undo: () => { currentTitle = oldTitle; }
        ));
        Invalidate();
    }

    private void OnBodyChanged(string newBody)
    {
        undoStack.Execute(new TypeTextCommand(
            description: "Type text",
            getter: () => currentBody,
            setter: text => { currentBody = text; },
            newValue: newBody
        ));
        Invalidate();
    }

    // ── Render ───────────────────────────────────────────────────────────

    protected override Node Render()
    {
        return new Column(
            children:
            [
                AppMenuBar(),
                new SplitView(
                    first: NoteList(),
                    second: selectedNoteId is not null ? Editor() : EmptyState()
                )
                .FirstSize(SplitSize.Fraction(0.3f))
                .FirstMin(180)
                .SecondMin(300)
                .Grow(1),
                showWordCount && selectedNoteId is not null
                    ? new StatusBar(
                        left: new Label($"{WordCount} words")
                            .FontSize(12)
                            .Opacity(0.6f),
                        right: undoStack.IsDirty
                            ? new Label("Unsaved changes")
                                .FontSize(12)
                                .Opacity(0.6f)
                            : justSaved
                                ? new Label("Saved")
                                    .FontSize(12)
                                    .Opacity(0.6f)
                                : new Label("All changes saved")
                                    .FontSize(12)
                                    .Opacity(0.4f)
                    )
                    : Node.Empty
            ]
        );
    }

    // ── Menu bar ─────────────────────────────────────────────────────────

    private Node AppMenuBar() =>
        new MenuBar(
            new Menu("File",
                MenuItem.Action("New Note",
                    Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.N),
                    CreateNewNote),
                MenuItem.Action("Save",
                    Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.S),
                    SaveCurrentNote,
                    enabled: undoStack.IsDirty),
                MenuItem.Separator(),
                MenuItem.Action("Delete Note",
                    DeleteCurrentNote,
                    enabled: selectedNoteId is not null)
            ),
            new Menu("Edit",
                MenuItem.Action("Undo",
                    Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.Z),
                    () => { undoStack.Undo(); Invalidate(); },
                    enabled: undoStack.CanUndo),
                MenuItem.Action("Redo",
                    Hotkey.From(ModifierKeys.Ctrl, Cascade.UI.Key.Y),
                    () => { undoStack.Redo(); Invalidate(); },
                    enabled: undoStack.CanRedo)
            ),
            new Menu("View",
                MenuItem.Toggle("Word Count",
                    showWordCount,
                    v => { showWordCount = v; Invalidate(); }),
                MenuItem.Separator(),
                MenuItem.Action("Sort by Modified",
                    () => { sortOrder = NoteSortOrder.ModifiedDesc; Invalidate(); }),
                MenuItem.Action("Sort by Title",
                    () => { sortOrder = NoteSortOrder.TitleAsc; Invalidate(); }),
                MenuItem.Action("Sort by Created",
                    () => { sortOrder = NoteSortOrder.CreatedDesc; Invalidate(); })
            )
        );

    // ── Note list sidebar ────────────────────────────────────────────────

    private Node NoteList() =>
        new Column(
            children:
            [
                new Row(
                    spacing: 8,
                    children:
                    [
                        new Label("Notes")
                            .FontSize(18)
                            .Bold()
                            .Grow(1),
                        new IconButton(PlusIcon, CreateNewNote)
                    ]
                ).Padding(horizontal: 16, vertical: 12),
                new Separator(),
                new ScrollView(
                    new Column(
                        spacing: 4,
                        children: [.. SortedNotes.Select(NoteRow)]
                    ).Padding(horizontal: 8, vertical: 8)
                ).Grow(1)
            ]
        );

    private Node NoteRow(NoteHeader note)
    {
        bool isActive = note.Id == selectedNoteId;
        bool isDirty = isActive && undoStack.IsDirty;
        var accent = ThemeSwitcher.ActiveColors.Primary;

        var row = new Column(
            spacing: 3,
            children:
            [
                new Row(
                    children:
                    [
                        (isActive
                            ? new Label(note.Title.Length > 0 ? note.Title : "Untitled").Bold()
                            : new Label(note.Title.Length > 0 ? note.Title : "Untitled")
                        ).Grow(1),
                        isDirty
                            ? new Label("•").FontSize(18).Color(accent)
                            : Node.Empty
                    ]
                ),
                note.Preview.Length > 0
                    ? new Label(note.Preview)
                        .FontSize(12)
                        .Opacity(0.5f)
                        .MaxLines(1)
                    : Node.Empty,
                new Label(note.ModifiedAt.ToString("MMM d, h:mm tt"))
                    .FontSize(11)
                    .Opacity(0.4f)
            ]
        )
        .Padding(horizontal: 12, vertical: 9)
        .CornerRadius(8)
        .OnTap(() => { LoadNote(note.Id); });

        // A selected row reads like a focused text field: a thin accent OUTLINE with
        // matching rounded corners and NO background fill. The user dislikes the
        // mid-opacity blue wash — outline only, never a translucent accent fill.
        if (isActive)
        {
            row = row.Border(accent, 1.5f, 8f);
        }

        return row;
    }

    // ── Editor ───────────────────────────────────────────────────────────

    private Node Editor() =>
        new Column(
            children:
            [
                new Row(
                    spacing: 8,
                    children:
                    [
                        new TextInput(
                            new Bindable<string>(currentTitle, OnTitleChanged),
                            placeholder: "Note title..."
                        )
                        .Grow(1),
                        new IconButton(SaveIcon, SaveCurrentNote)
                            .Disabled(!undoStack.IsDirty)
                    ]
                ).Padding(horizontal: 24, vertical: 16),
                new Separator(),
                new TextArea(
                    new Bindable<string>(currentBody, OnBodyChanged),
                    placeholder: "Start writing..."
                )
                .Grow(1)
                .Padding(horizontal: 24, vertical: 16)
            ]
        );

    private Node EmptyState() =>
        new Center(
            new Column(
                spacing: 16,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new IconView(FileTextIcon, size: 64)
                        .Opacity(0.3f),
                    new Label("Select a note or create a new one")
                        .Opacity(0.5f),
                    new Button("New Note", onClick: CreateNewNote)
                ]
            )
        );
}
