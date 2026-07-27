// Golden Example 08 — Invoice Editor (DataGrid)
//
// User story:
//   An accounts user drafts an invoice. Invoice-level details (client, dates,
//   payment terms) live in the header. Line items live in an editable DataGrid on
//   the left; existing rows are edited inline. New rows are composed in the
//   "Add Line Item" form on the right and appended to the grid. The running total
//   updates live. One row is flagged locked (approved on a previous revision) and
//   cannot be deleted. Submitting the invoice files it and resets the editor for
//   the next one.
//
// Demonstrates:
//   - DataGrid with editable Text, Number, Select, Date, and Computed columns
//   - Per-column validation (quantity > 0, price ≥ 0)
//   - Per-row validation (cross-column: description required when price > 0)
//   - AggregateRow for a live subtotal
//   - A composer form that appends rows (DeleteRow / DuplicateRow manage them)
//   - ClipboardSupport for pasting rows from Excel
//   - Reactive computed properties driving the header "Amount due" and the totals
//   - SplitView: grid on the left, composer + tax + notes on the right
//   - Async submit with a guard clause, a status pill, and a reset-for-next flow
//   - LifetimeToken on async operations; no dependency injection
//
// DataGrid rule of thumb:
//   DataTable  = display and sort. No editing.
//   DataGrid   = editing, adding, deleting, pasting. Use when the user needs to
//                manipulate the data, not just read it.
//
// The Computed column (Total) is the key pattern: the developer does not store
// Total on the model. They give the grid a formula. The grid recalculates on
// every cell change and shows the live result in the AggregateRow too.

using Cascade.UI;

namespace InvoiceEditor;

// ── Data model ────────────────────────────────────────────────────────────────

internal sealed class LineItem
{
    public Guid     Id          { get; init; } = Guid.NewGuid();
    public string   Description { get; set; }  = "";
    public decimal  Quantity    { get; set; }  = 1m;
    public decimal  UnitPrice   { get; set; }  = 0m;
    public string   Category    { get; set; }  = "General";
    public DateOnly ServiceDate { get; set; }  = DateOnly.FromDateTime(DateTime.Today);
    public bool     IsLocked    { get; init; }
}

internal sealed class InvoiceDraft
{
    public string   InvoiceNumber { get; set; } = "INV-2024-0042";
    public string   ClientName    { get; set; } = "";
    public DateOnly IssueDate     { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly DueDate       { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
    public string   PaymentTerms  { get; set; } = "net30";
    public decimal  TaxRate       { get; set; } = 0.08m;
    public string   Notes         { get; set; } = "";
}

// ── Page ──────────────────────────────────────────────────────────────────────

internal sealed partial class InvoiceEditorPage : Component
{
    private InvoiceDraft invoice = new();
    private List<LineItem> lineItems = [];
    private bool loading = true;
    private bool saving = false;
    private bool submitted = false;
    private string saveError = "";
    private int invoiceSeq = 0;

    // "Add Line Item" composer state (right panel). Kept as strings for the numeric
    // fields and parsed on add, matching how a plain text field feeds a model.
    private string newDescription = "";
    private string newQuantity = "1";
    private string newUnitPrice = "0";
    private string newCategory = "General";
    private DateOnly newServiceDate = DateOnly.FromDateTime(DateTime.Today);

    private static readonly IReadOnlyList<object> Categories =
        ["General", "Labour", "Materials", "Travel", "Subcontractor", "Software"];

    private static readonly IReadOnlyList<SelectOption<string>> CategoryOptions =
    [
        new SelectOption<string>("General",       "General"),
        new SelectOption<string>("Labour",        "Labour"),
        new SelectOption<string>("Materials",     "Materials"),
        new SelectOption<string>("Travel",        "Travel"),
        new SelectOption<string>("Subcontractor", "Subcontractor"),
        new SelectOption<string>("Software",      "Software"),
    ];

    private static readonly IReadOnlyList<SelectOption<string>> PaymentTermOptions =
    [
        new SelectOption<string>("net7",  "Net 7 days"),
        new SelectOption<string>("net14", "Net 14 days"),
        new SelectOption<string>("net30", "Net 30 days"),
        new SelectOption<string>("net60", "Net 60 days"),
        new SelectOption<string>("due",   "Due on receipt"),
    ];

    protected override async Task OnMounted()
    {
        await Delay(Duration.Ms(600), LifetimeToken);

        invoice = new InvoiceDraft
        {
            InvoiceNumber = "INV-2024-0042",
            ClientName    = "Acme Corporation",
            IssueDate     = DateOnly.FromDateTime(DateTime.Today),
            DueDate       = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            TaxRate       = 0.08m,
        };
        lineItems =
        [
            new LineItem
            {
                Description = "Web Development — Sprint 14",
                Quantity    = 80m,
                UnitPrice   = 150m,
                Category    = "Labour",
                ServiceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
                IsLocked    = true,
            },
            new LineItem
            {
                Description = "Cloud Hosting (March)",
                Quantity    = 1m,
                UnitPrice   = 249.99m,
                Category    = "Software",
                ServiceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-3)),
            },
            new LineItem
            {
                Description = "UI/UX Design Review",
                Quantity    = 16m,
                UnitPrice   = 125m,
                Category    = "Labour",
                ServiceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            },
            new LineItem
            {
                Description = "Travel — Client Site Visit",
                Quantity    = 1m,
                UnitPrice   = 387.50m,
                Category    = "Travel",
            },
        ];
        loading = false;
        Invalidate();
    }

    private decimal Subtotal  => lineItems.Sum(r => r.Quantity * r.UnitPrice);
    private decimal TaxAmount => Subtotal * invoice.TaxRate;
    private decimal Total     => Subtotal + TaxAmount;

    protected override Node Render()
    {
        if (loading)
        {
            return new Column(spacing: 16, children: [
                new Spinner(),
                new Label("Loading invoice...").FontSize(13).Color(ThemeSwitcher.ActiveColors.TextMuted)
            ]).Alignment(Alignment.Center);
        }

        return new Column(spacing: 0, children: [
            NavBar(),
            InvoiceHeader(),
            new SplitView(
                first:  GridPanel(),
                second: SidePanel()
            )
            .FirstSize(SplitSize.Fraction(0.62f))
            .FirstMin(480)
            .SecondMin(300)
            .SecondMax(440)
            .Grow(1)
        ]);
    }

    // ── Navigation bar ────────────────────────────────────────────────────────

    private Node NavBar()
    {
        var c = ThemeSwitcher.ActiveColors;
        return new Row(spacing: 16, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            new Column(spacing: 2, children: [
                new Label(invoice.InvoiceNumber)
                    .FontSize(18)
                    .Bold(),
                new Label(invoice.ClientName.Length > 0 ? invoice.ClientName : "New invoice")
                    .FontSize(12)
                    .Color(c.TextMuted)
            ]),
            StatusPill(),
            new Spacer(),
            new Column(spacing: 2, crossAxisAlignment: CrossAxisAlignment.End, children: [
                new Label("Amount due")
                    .FontSize(11)
                    .Color(c.TextMuted),
                new Label($"{Total:C2}")
                    .FontSize(18)
                    .Bold()
            ]),
            submitted
                ? Node.Empty
                : new Button(
                    label:   "Save Draft",
                    onClick: () => { _ = OnSaveDraft(); }
                ).Disabled(saving).Variant("outline"),
            submitted
                ? Node.Empty
                : new Button(
                    label:   "Submit",
                    onClick: () => { _ = OnSubmit(); }
                ).Disabled(saving || lineItems.Count == 0)
        ])
        .Padding(new EdgeInsets(14, 20, 14, 20))
        .Background(c.SurfaceAlt)
        .BorderBottom(c.Border, 1);
    }

    /// <summary>
    /// A small rounded status pill reflecting the draft/saving/submitted state,
    /// giving the header an at-a-glance status the way a real invoicing tool would.
    /// </summary>
    private Node StatusPill()
    {
        var c = ThemeSwitcher.ActiveColors;
        (string text, ColorValue bg, ColorValue fg) =
            submitted ? ("Submitted", c.SuccessSubtle, c.Success)
          : saving    ? ("Saving…",   c.WarningSubtle, c.Warning)
          :             ("Draft",     c.Surface,       c.TextMuted);

        return new Row(spacing: 0, children: [
            new Label(text)
                .FontSize(11)
                .Bold()
                .Color(fg)
        ])
        .Padding(new EdgeInsets(4, 10, 4, 10))
        .Background(bg)
        .CornerRadius(11);
    }

    // ── Invoice header (invoice-level fields) ───────────────────────────────────

    private Node InvoiceHeader()
    {
        var c = ThemeSwitcher.ActiveColors;
        return new Row(spacing: 16, crossAxisAlignment: CrossAxisAlignment.End, children: [
            HeaderField("Client Name",
                new TextInput(
                    new Bindable<string>(invoice.ClientName, v => { invoice.ClientName = v; Invalidate(); }),
                    placeholder: "Who is this invoice for?")
            ).Grow(2),
            HeaderField("Issue Date",
                new DatePicker(
                    new Bindable<DateOnly?>(invoice.IssueDate,
                        v => { if (v.HasValue) { invoice.IssueDate = v.Value; Invalidate(); } }),
                    format: "yyyy-MM-dd")
            ).Grow(1),
            HeaderField("Due Date",
                new DatePicker(
                    new Bindable<DateOnly?>(invoice.DueDate,
                        v => { if (v.HasValue) { invoice.DueDate = v.Value; Invalidate(); } }),
                    format: "yyyy-MM-dd")
            ).Grow(1),
            HeaderField("Payment Terms",
                new Select<string>(
                    new Bindable<string>(invoice.PaymentTerms, v => { invoice.PaymentTerms = v; Invalidate(); }),
                    options: PaymentTermOptions)
            ).Grow(1),
        ])
        .Padding(new EdgeInsets(12, 20, 12, 20))
        .Background(c.Surface)
        .BorderBottom(c.Border, 1);
    }

    // ── Grid panel ────────────────────────────────────────────────────────────

    private Node GridPanel()
    {
        return new Column(spacing: 0, crossAxisAlignment: CrossAxisAlignment.Stretch, children: [
            GridToolbar(),
            submitted
                ? SuccessBanner()
                : saveError.Length > 0
                    ? ErrorBanner(saveError)
                    : Node.Empty,
            InvoiceDataGrid()
        ]);
    }

    private Node GridToolbar()
    {
        return new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            new Label("Line Items")
                .FontSize(14)
                .Bold()
                .Grow(1),
            new Label($"{lineItems.Count} item{(lineItems.Count != 1 ? "s" : "")}")
                .FontSize(12)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            new Label("Ctrl+V to paste from Excel")
                .FontSize(11)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ])
        .Padding(new EdgeInsets(10, 16, 10, 16))
        .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
        .BorderBottom(ThemeSwitcher.ActiveColors.Border, 1);
    }

    private Node InvoiceDataGrid()
    {
        return new DataGrid<LineItem>(
            items:   new Bindable<IReadOnlyList<LineItem>>(lineItems, v => { lineItems = new List<LineItem>(v); Invalidate(); }),
            columns:
            [
                DataGridColumn<LineItem>.Text(
                    header: "Description",
                    get:    row => row.Description,
                    set:    (row, val) => { row.Description = val; }
                )
                .Width(DataColumnWidth.Fill)
                .MinWidth(280)
                .Validate(val => val?.ToString()?.Trim().Length > 0
                    ? ValidationResult.Ok
                    : ValidationResult.Error("Description is required"))
                .Pinned(ColumnPin.Left),

                DataGridColumn<LineItem>.Number(
                    header: "Qty",
                    get:    row => (object)row.Quantity,
                    set:    (row, val) => { if (val is decimal d) { row.Quantity = d; } },
                    format: "N2"
                )
                .Width(70)
                .Align(ColumnAlignment.Right)
                .Validate(val => val is decimal d && d > 0
                    ? ValidationResult.Ok
                    : ValidationResult.Error("Quantity must be > 0")),

                DataGridColumn<LineItem>.Number(
                    header: "Unit Price",
                    get:    row => (object)row.UnitPrice,
                    set:    (row, val) => { if (val is decimal d) { row.UnitPrice = d; } },
                    format: "C2"
                )
                .Width(110)
                .Align(ColumnAlignment.Right)
                .Validate(val => val is decimal d && d >= 0
                    ? ValidationResult.Ok
                    : ValidationResult.Error("Price must be ≥ 0")),

                DataGridColumn<LineItem>.Computed(
                    header:  "Total",
                    compute: row => (object)(row.Quantity * row.UnitPrice),
                    format:  "C2"
                )
                .Width(110)
                .Align(ColumnAlignment.Right),

                DataGridColumn<LineItem>.Select(
                    header:  "Category",
                    get:     row => (object)row.Category,
                    set:     (row, val) => { row.Category = val?.ToString() ?? "General"; },
                    options: Categories
                )
                .Width(110),

                DataGridColumn<LineItem>.Date(
                    header: "Date",
                    get:    row => (object)row.ServiceDate,
                    set:    (row, val) => { if (val is DateOnly d) { row.ServiceDate = d; } }
                )
                .Width(100),
            ]
        )
        .ValidateRow(row =>
        {
            if (row.UnitPrice > 0 && row.Description.Trim().Length == 0)
            {
                return ValidationResult.Error("Description required when price > 0");
            }
            return ValidationResult.Ok;
        })
        .DeleteRow(canDelete: row => !row.IsLocked)
        .DuplicateRow(clone: row => new LineItem
        {
            Description = row.Description,
            Quantity    = row.Quantity,
            UnitPrice   = row.UnitPrice,
            Category    = row.Category,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
        })
        .ClipboardSupport(true)
        .OnChange(_ => { saveError = ""; Invalidate(); })
        .EditMode(GridEditMode.ClickToEdit)
        .AggregateRow(AggregatePosition.Bottom, [
            new ColumnAggregate<LineItem>("Description", null),
            new ColumnAggregate<LineItem>("Qty",         null),
            new ColumnAggregate<LineItem>("Unit Price",  null),
            new ColumnAggregate<LineItem>("Total",
                items => (object)items.Sum(r => r.Quantity * r.UnitPrice),
                format: "C2"),
            new ColumnAggregate<LineItem>("Category",    null),
            new ColumnAggregate<LineItem>("Date",        null),
        ])
        .CellSelection(CellSelectionMode.MultiRange)
        .RowHeight(40)
        .Striped(true)
        .UndoEnabled(true)
        .Grow(1);
    }

    // ── Side panel: add-line composer + tax + notes ─────────────────────────────

    private Node SidePanel()
    {
        return new ScrollView(
            new Column(spacing: 24, children: [
                AddLineItemSection(),
                TaxSection(),
                NotesSection()
            ]).Padding(20)
        )
        .Background(ThemeSwitcher.ActiveColors.Surface);
    }

    private Node AddLineItemSection()
    {
        bool canAdd = newDescription.Trim().Length > 0;
        return new Column(spacing: 12, children: [
            new Label("Add Line Item")
                .FontSize(14)
                .Bold(),
            new Label("Compose a line, then add it to the invoice.")
                .FontSize(12)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            LabeledField("Description",
                new TextInput(
                    new Bindable<string>(newDescription, v => { newDescription = v; Invalidate(); }),
                    placeholder: "What was delivered?")
            ),
            new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.End, children: [
                LabeledField("Quantity",
                    new TextInput(new Bindable<string>(newQuantity, v => { newQuantity = v; Invalidate(); }))
                ).Grow(1),
                LabeledField("Unit Price",
                    new TextInput(new Bindable<string>(newUnitPrice, v => { newUnitPrice = v; Invalidate(); }))
                ).Grow(1),
            ]),
            LabeledField("Category",
                new Select<string>(
                    new Bindable<string>(newCategory, v => { newCategory = v; Invalidate(); }),
                    options: CategoryOptions)
            ),
            LabeledField("Service Date",
                new DatePicker(
                    new Bindable<DateOnly?>(newServiceDate,
                        v => { if (v.HasValue) { newServiceDate = v.Value; Invalidate(); } }),
                    format: "yyyy-MM-dd")
            ),
            new Button(
                label:   "Add to invoice",
                onClick: AddLineItem
            ).Disabled(!canAdd)
        ]);
    }

    private Node TaxSection()
    {
        return new Column(spacing: 12, children: [
            new Label("Tax & Totals")
                .FontSize(14)
                .Bold(),
            LabeledField("Tax Rate (%)",
                new TextInput(new Bindable<string>(
                    (invoice.TaxRate * 100).ToString("F0"),
                    v =>
                    {
                        if (decimal.TryParse(v, out decimal pct))
                        {
                            invoice.TaxRate = pct / 100m;
                            Invalidate();
                        }
                    }))
            ),
            new Column(spacing: 6, children: [
                TotalRow("Subtotal", Subtotal),
                TotalRow("Tax",      TaxAmount),
                new Separator(),
                TotalRow("Total",    Total, bold: true)
            ])
            .Padding(12)
            .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
            .CornerRadius(8)
        ]);
    }

    private Node NotesSection()
    {
        return new Column(spacing: 8, children: [
            new Label("Notes")
                .FontSize(14)
                .Bold(),
            new TextArea(new Bindable<string>(invoice.Notes,
                v => { invoice.Notes = v; Invalidate(); }),
                placeholder: "Add notes or special instructions..."
            ).Height(100)
        ]);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Node LabeledField(string label, Node child)
    {
        return new Column(spacing: 6, children: [
            new Label(label)
                .FontSize(12)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            child
        ]);
    }

    private static Node HeaderField(string label, Node child)
    {
        return new Column(spacing: 6, children: [
            new Label(label)
                .FontSize(11)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            child
        ]);
    }

    private static Node TotalRow(string label, decimal amount, bool bold = false)
    {
        var labelNode = new Label(label).FontSize(13);
        var valueNode = new Label($"{amount:C2}").FontSize(13);
        if (bold)
        {
            labelNode = labelNode.Bold();
            valueNode = valueNode.Bold();
        }
        else
        {
            labelNode = labelNode.Color(ThemeSwitcher.ActiveColors.TextMuted);
            valueNode = valueNode.Color(ThemeSwitcher.ActiveColors.TextMuted);
        }

        return new Row(spacing: 0, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            labelNode.Grow(1),
            valueNode
        ]);
    }

    private static Node ErrorBanner(string message)
    {
        return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            new Label("⚠").FontSize(16),
            new Label(message)
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.Danger)
        ])
        .Padding(new EdgeInsets(10, 16, 10, 16))
        .Background(ThemeSwitcher.ActiveColors.DangerSubtle)
        .CornerRadius(6);
    }

    private static Node SuccessBanner()
    {
        return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            new Label("✓").FontSize(16).Bold().Color(ThemeSwitcher.ActiveColors.Success),
            new Label("Invoice submitted — starting a new one…")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.Success)
        ])
        .Padding(new EdgeInsets(10, 16, 10, 16))
        .Background(ThemeSwitcher.ActiveColors.SuccessSubtle)
        .CornerRadius(6);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    /// <summary>Appends the composed line to the invoice and clears the form.</summary>
    private void AddLineItem()
    {
        if (newDescription.Trim().Length == 0)
        {
            return;
        }

        decimal quantity  = decimal.TryParse(newQuantity, out decimal q) && q > 0 ? q : 1m;
        decimal unitPrice = decimal.TryParse(newUnitPrice, out decimal p) && p >= 0 ? p : 0m;

        lineItems = new List<LineItem>(lineItems)
        {
            new LineItem
            {
                Description = newDescription.Trim(),
                Quantity    = quantity,
                UnitPrice   = unitPrice,
                Category    = newCategory,
                ServiceDate = newServiceDate,
            }
        };

        ResetComposer();
        saveError = "";
        Invalidate();
    }

    private void ResetComposer()
    {
        newDescription = "";
        newQuantity    = "1";
        newUnitPrice   = "0";
        newCategory    = "General";
        newServiceDate = DateOnly.FromDateTime(DateTime.Today);
    }

    /// <summary>Files the current invoice and resets the editor for the next one.</summary>
    private void ResetForNewInvoice()
    {
        invoiceSeq++;
        invoice = new InvoiceDraft
        {
            InvoiceNumber = $"INV-2024-{42 + invoiceSeq:D4}",
            ClientName    = "",
            IssueDate     = DateOnly.FromDateTime(DateTime.Today),
            DueDate       = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            PaymentTerms  = "net30",
            TaxRate       = 0.08m,
            Notes         = "",
        };
        lineItems = [];
        submitted = false;
        saving    = false;
        saveError = "";
        ResetComposer();
        Invalidate();
    }

    private async Task OnSaveDraft()
    {
        if (saving)
        {
            return;
        }

        saving = true;
        saveError = "";
        Invalidate();

        try
        {
            await Delay(Duration.Ms(800), LifetimeToken);
            saving = false;
            Invalidate();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            saveError = "Failed to save draft. Please try again.";
            saving = false;
            Invalidate();
        }
    }

    private async Task OnSubmit()
    {
        if (saving || submitted || lineItems.Count == 0)
        {
            return;
        }

        saving = true;
        saveError = "";
        Invalidate();

        try
        {
            await Delay(Duration.Ms(1000), LifetimeToken);
            saving = false;
            submitted = true;
            Invalidate();

            // Brief confirmation, then reset the editor for the next invoice.
            await Delay(Duration.Ms(1500), LifetimeToken);
            ResetForNewInvoice();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            saveError = "Failed to submit invoice. Please try again.";
            saving = false;
            submitted = false;
            Invalidate();
        }
    }
}
