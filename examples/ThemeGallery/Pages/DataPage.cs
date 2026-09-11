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
        // More rows than the 180px box can show, so the section also demonstrates that the table
        // scrolls its own rows (CONTROLS-005) rather than clipping the overflow.
        (string Name, string Email)[] people =
        [
            ("Grace", "grace@example.com"), ("Heidi", "heidi@example.com"),
            ("Ivan", "ivan@example.com"), ("Judy", "judy@example.com"),
            ("Mallory", "mallory@example.com"), ("Niaj", "niaj@example.com"),
            ("Olivia", "olivia@example.com"), ("Peggy", "peggy@example.com"),
            ("Rupert", "rupert@example.com"), ("Sybil", "sybil@example.com"),
            ("Trent", "trent@example.com"), ("Victor", "victor@example.com"),
            ("Walter", "walter@example.com"), ("Wendy", "wendy@example.com"),
            ("Yves", "yves@example.com"), ("Zoe", "zoe@example.com"),
        ];
        string[] roles = ["Owner", "Admin", "Viewer"];

        var items = new List<SampleRow>(people.Length);
        for (int i = 0; i < people.Length; i++)
        {
            items.Add(new SampleRow(
                people[i].Name,
                people[i].Email,
                roles[i % roles.Length],
                i % 4 != 3));
        }

        return Section("DataTable",
            "Read-only data table with typed columns, a fill column, custom cell nodes, and scrolling.",
            new DataTable<SampleRow>(items,
            [
                // A custom cell returning a background-filled Column. Before RENDER-006 the
                // custom-cell painter understood only Sparkline/Label/Row-of-Labels, so this
                // painted nothing at all.
                DataColumn<SampleRow>.Custom("", r => new Column(children: [])
                    .Width(3f)
                    .Height(18f)
                    .Background(r.Active ? new ColorValue("#30D158") : new ColorValue("#FF453A"))
                    .CornerRadius(2f)).Width(18f),
                // Auto: sized to its own content (header + a sample of the rows).
                DataColumn<SampleRow>.Text("Name", r => r.Name).Width(DataColumnWidth.Auto),
                // A custom cell mixing an icon with a label in a Row — the icon used to be dropped
                // (only Label children were drawn), and then, once it drew, it escaped the cell
                // clip and repeated down the page until RENDER-009.
                DataColumn<SampleRow>.Custom("Role", r => new Row(
                    spacing: 6f,
                    crossAxisAlignment: CrossAxisAlignment.Center,
                    children:
                    [
                        new IconView(RoleIcon, size: 14),
                        new Label(r.Role),
                    ])).Width(120f),
                // Fill + MinWidth: silently ignored before CONTROLS-001. Long text ellipsises
                // now that custom cells go through the normal label path.
                DataColumn<SampleRow>.Text("Email", r => r.Email)
                    .Width(DataColumnWidth.Fill).MinWidth(140f),
                DataColumn<SampleRow>.Bool("Active", r => r.Active).Width(70f),
            ]).Height(180));
    }


    // Lucide "shield" — stands in for a role badge in the custom cell demo.
    private static readonly Icon RoleIcon = new(
        ["M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"],
        new Size(24, 24), 14f, "Role");

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
