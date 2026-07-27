using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class DataPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            DataGridSection(),
            DataTableSection(),
            ListViewSection(),
            TreeViewSection(),
            TimelineSection(),
            PropertyGridSection(),
        ]);

    // ── DataGrid ─────────────────────────────────────────────────────────

    static Node DataGridSection()
    {
        var items = new Bindable<IReadOnlyList<SampleRow>>(
        [
            new("Alice", "alice@example.com", "Admin", true),
            new("Bob", "bob@example.com", "Editor", true),
            new("Carol", "carol@example.com", "Viewer", false),
            new("Dave", "dave@example.com", "Editor", true),
            new("Eve", "eve@example.com", "Admin", false),
        ], _ => { });

        return Section("DataGrid",
            "Editable data grid with sortable columns, selection, and inline editing.",
            new DataGrid<SampleRow>(items,
            [
                DataGridColumn<SampleRow>.Text("Name", r => r.Name, (r, v) => { r.Name = v; }),
                DataGridColumn<SampleRow>.Text("Email", r => r.Email, (r, v) => { r.Email = v; }),
                DataGridColumn<SampleRow>.Text("Role", r => r.Role, (r, v) => { r.Role = v; }),
            ]).Height(220));
    }

    // ── DataTable ────────────────────────────────────────────────────────

    static Node DataTableSection()
    {
        IReadOnlyList<SampleRow> items =
        [
            new("Grace", "grace@example.com", "Owner", true),
            new("Heidi", "heidi@example.com", "Admin", true),
            new("Ivan", "ivan@example.com", "Viewer", false),
        ];

        return Section("DataTable",
            "Read-only data table with typed columns.",
            new DataTable<SampleRow>(items,
            [
                DataColumn<SampleRow>.Text("Name", r => r.Name),
                DataColumn<SampleRow>.Text("Email", r => r.Email),
                DataColumn<SampleRow>.Text("Role", r => r.Role),
                DataColumn<SampleRow>.Bool("Active", r => r.Active),
            ]).Height(180));
    }

    // ── ListView ─────────────────────────────────────────────────────────

    static Node ListViewSection()
    {
        string[] fruits = ["Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape"];

        return Section("ListView",
            "Templated list with selection mode.",
            new ListView<string>(fruits,
                render: item => new Label(item).Padding(8, 4),
                selectionMode: SelectionMode.Single,
                onSelect: _ => { }
            ).Height(200).Width(300));
    }

    // ── TreeView ─────────────────────────────────────────────────────────

    static Node TreeViewSection()
    {
        IReadOnlyList<TreeNode<string>> items =
        [
            new TreeNode<string>
            {
                Data = "Documents",
                Children =
                [
                    new TreeNode<string>
                    {
                        Data = "Work",
                        Children =
                        [
                            new TreeNode<string> { Data = "Report.docx", Children = [] },
                            new TreeNode<string> { Data = "Budget.xlsx", Children = [] },
                        ]
                    },
                    new TreeNode<string>
                    {
                        Data = "Personal",
                        Children =
                        [
                            new TreeNode<string> { Data = "Resume.pdf", Children = [] },
                        ]
                    },
                ],
                Expanded = true
            },
            new TreeNode<string>
            {
                Data = "Pictures",
                Children =
                [
                    new TreeNode<string>
                    {
                        Data = "Vacation",
                        Children =
                        [
                            new TreeNode<string> { Data = "Beach.jpg", Children = [] },
                            new TreeNode<string> { Data = "Mountain.jpg", Children = [] },
                        ]
                    },
                ]
            },
            new TreeNode<string> { Data = "Music", Children = [] },
        ];

        return Section("TreeView",
            "Hierarchical tree with expand/collapse and node rendering.",
            new TreeView<string>(items,
                render: item => new Label(item)
            ).Height(250).Width(350));
    }

    // ── Timeline ─────────────────────────────────────────────────────────

    static Node TimelineSection() =>
        Section("Timeline",
            "Chronological event timeline with timestamps.",
            new Timeline(
            [
                new TimelineEvent(new System.DateTime(2025, 1, 15, 9, 0, 0),
                    "Project Created", "Initial repository setup and scaffolding"),
                new TimelineEvent(new System.DateTime(2025, 3, 1, 14, 30, 0),
                    "Alpha Release", "First internal alpha with core controls"),
                new TimelineEvent(new System.DateTime(2025, 5, 10, 10, 0, 0),
                    "Beta Release", "Public beta with all 70+ controls"),
                new TimelineEvent(new System.DateTime(2025, 7, 1, 12, 0, 0),
                    "1.0 Release", "Production-ready release"),
            ]).Height(300));

    // ── PropertyGrid ─────────────────────────────────────────────────────

    static Node PropertyGridSection()
    {
        var title = "My Application";
        var opacity = 0.85f;
        var visible = true;
        var width = 800;
        var height = 600;
        var resizable = true;

        return Section("PropertyGrid",
            "Grouped property editor with typed fields.",
            new PropertyGrid(
            [
                new PropertyGroup("Appearance",
                [
                    Property.String("Title", () => title, v => { title = v; }),
                    Property.Float("Opacity", () => opacity, v => { opacity = v; }, min: 0f, max: 1f, step: 0.05f),
                    Property.Bool("Visible", () => visible, v => { visible = v; }),
                ]),
                new PropertyGroup("Layout",
                [
                    Property.Int("Width", () => width, v => { width = v; }, min: 100, max: 3840),
                    Property.Int("Height", () => height, v => { height = v; }, min: 100, max: 2160),
                    Property.Bool("Resizable", () => resizable, v => { resizable = v; }),
                ]),
            ]).Height(250));
    }

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);

    // ── Sample Data ──────────────────────────────────────────────────────

    private sealed class SampleRow(string name, string email, string role, bool active)
    {
        public string Name { get; set; } = name;
        public string Email { get; set; } = email;
        public string Role { get; set; } = role;
        public bool Active { get; set; } = active;
    }
}
