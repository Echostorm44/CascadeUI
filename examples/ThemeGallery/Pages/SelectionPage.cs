using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class SelectionPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            CheckboxSection(),
            ToggleSection(),
            RadioGroupSection(),
            SliderSection(),
            RangeSliderSection(),
            SelectSection(),
            MultiSelectSection(),
            ComboboxSection(),
            SegmentedControlSection(),
            RatingSection(),
        ]);

    // ── Checkbox ─────────────────────────────────────────────────────────

    static Node CheckboxSection()
    {
        var a = new Bindable<bool>(false, _ => { });
        var b = new Bindable<bool>(true, _ => { });
        var c = new Bindable<bool>(false, _ => { });

        return Section("Checkbox",
            "Standard checkboxes with checked, unchecked, and disabled states.",
            new Column(spacing: 8, children:
            [
                new Row(spacing: 24, children:
                [
                    new Checkbox(a, label: "Unchecked"),
                    new Checkbox(b, label: "Checked"),
                ]),
                new Row(spacing: 24, children:
                [
                    new Checkbox(c, label: "Disabled Unchecked").Disabled(),
                    new Checkbox(new Bindable<bool>(true, _ => { }), label: "Disabled Checked").Disabled(),
                ]),
            ]));
    }

    // ── Toggle ───────────────────────────────────────────────────────────

    static Node ToggleSection()
    {
        var on = new Bindable<bool>(true, _ => { });
        var off = new Bindable<bool>(false, _ => { });
        var withLabel = new Bindable<bool>(true, _ => { });

        return Section("Toggle",
            "On/off toggle switch with label, description, and disabled states.",
            new Column(spacing: 8, children:
            [
                new Row(spacing: 24, children:
                [
                    new Toggle(on, label: "Enabled"),
                    new Toggle(off, label: "Disabled State").Disabled(),
                ]),
                new Toggle(withLabel, label: "Notifications", description: "Receive email notifications for updates"),
            ]));
    }

    // ── RadioGroup ───────────────────────────────────────────────────────

    static Node RadioGroupSection()
    {
        var size = new Bindable<string>("medium", _ => { });
        var disabledBind = new Bindable<string>("a", _ => { });

        return Section("RadioGroup",
            "Mutually exclusive radio button selection in column layout.",
            new Row(spacing: 32, children:
            [
                new RadioGroup<string>(size, content:
                    new Column(spacing: 6, children:
                    [
                        new RadioButton<string>("small", "Small"),
                        new RadioButton<string>("medium", "Medium"),
                        new RadioButton<string>("large", "Large"),
                    ])),
                new RadioGroup<string>(disabledBind, content:
                    new Column(spacing: 6, children:
                    [
                        new RadioButton<string>("a", "Option A"),
                        new RadioButton<string>("b", "Option B"),
                        new RadioButton<string>("c", "Option C"),
                    ])).Disabled(),
            ]));
    }

    // ── Slider ───────────────────────────────────────────────────────────

    static Node SliderSection()
    {
        var basic = new Bindable<float>(0.5f, _ => { });
        var stepped = new Bindable<float>(50f, _ => { });
        var disabled = new Bindable<float>(0.3f, _ => { });

        return Section("Slider",
            "Continuous and stepped sliders with labels and disabled state.",
            new Column(spacing: 12, children:
            [
                new Slider(basic, label: "Volume").Width(300),
                new Slider(stepped, min: 0f, max: 100f, step: 10f, label: "Brightness (step 10)").Width(300),
                new Slider(disabled, label: "Disabled").Disabled().Width(300),
            ]));
    }

    // ── RangeSlider ──────────────────────────────────────────────────────

    static Node RangeSliderSection()
    {
        var minVal = new Bindable<float>(20f, _ => { });
        var maxVal = new Bindable<float>(80f, _ => { });
        var dMin = new Bindable<float>(30f, _ => { });
        var dMax = new Bindable<float>(70f, _ => { });

        return Section("RangeSlider",
            "Dual-thumb slider for selecting a range.",
            new Column(spacing: 12, children:
            [
                new RangeSlider(minVal, maxVal, min: 0f, max: 100f, label: "Price Range").Width(300),
                new RangeSlider(dMin, dMax, min: 0f, max: 100f, label: "Disabled").Disabled().Width(300),
            ]));
    }

    // ── Select ───────────────────────────────────────────────────────────

    static Node SelectSection()
    {
        var country = new Bindable<string>("", _ => { });
        var prefilled = new Bindable<string>("us", _ => { });
        var disabled = new Bindable<string>("uk", _ => { });

        SelectOption<string>[] options =
        [
            new("us", "United States"),
            new("uk", "United Kingdom"),
            new("de", "Germany"),
            new("jp", "Japan"),
            new("au", "Australia"),
        ];

        return Section("Select",
            "Dropdown selection with placeholder, prefilled, and disabled states.",
            new Row(spacing: 16, children:
            [
                new Select<string>(country, options, placeholder: "Choose country...", label: "Country")
                    .Width(220),
                new Select<string>(prefilled, options, label: "Prefilled")
                    .Width(220),
                new Select<string>(disabled, options, label: "Disabled")
                    .Disabled()
                    .Width(220),
            ]));
    }

    // ── MultiSelect ──────────────────────────────────────────────────────

    static Node MultiSelectSection()
    {
        var tags = new Bindable<IReadOnlyList<string>>(["react", "css"], _ => { });
        var disabled = new Bindable<IReadOnlyList<string>>(["go"], _ => { });

        SelectOption<string>[] options =
        [
            new("react", "React"),
            new("vue", "Vue"),
            new("angular", "Angular"),
            new("svelte", "Svelte"),
            new("css", "CSS"),
            new("go", "Go"),
        ];

        return Section("MultiSelect",
            "Multi-value dropdown with max selection limit and disabled state.",
            new Row(spacing: 16, children:
            [
                new MultiSelect<string>(tags, options, placeholder: "Select skills...", maxSelected: 4, label: "Skills")
                    .Width(280),
                new MultiSelect<string>(disabled, options, label: "Disabled")
                    .Disabled()
                    .Width(280),
            ]));
    }

    // ── Combobox ─────────────────────────────────────────────────────────

    static Node ComboboxSection()
    {
        var value = new Bindable<string>("", _ => { });
        var prefilled = new Bindable<string>("Cascade", _ => { });
        var disabled = new Bindable<string>("Locked", _ => { });

        SelectOption<string>[] options =
        [
            new("Cascade", "Cascade"),
            new("React", "React"),
            new("Angular", "Angular"),
            new("Vue", "Vue"),
        ];

        return Section("Combobox",
            "Searchable dropdown with type-ahead filtering and disabled state.",
            new Row(spacing: 16, children:
            [
                new Combobox<string>(value, options, placeholder: "Search frameworks...", label: "Framework")
                    .Width(220),
                new Combobox<string>(prefilled, options, label: "Prefilled")
                    .Width(220),
                new Combobox<string>(disabled, options, label: "Disabled")
                    .Disabled()
                    .Width(220),
            ]));
    }

    // ── SegmentedControl ─────────────────────────────────────────────────

    static Node SegmentedControlSection()
    {
        var view = new Bindable<string>("day", _ => { });
        var disabled = new Bindable<string>("a", _ => { });

        return Section("SegmentedControl",
            "Pill-bar segment selection with active highlight and disabled state.",
            new Column(spacing: 12, children:
            [
                new SegmentedControl<string>(view,
                [
                    new SegmentOption<string>("day", "Day"),
                    new SegmentOption<string>("week", "Week"),
                    new SegmentOption<string>("month", "Month"),
                    new SegmentOption<string>("year", "Year"),
                ]),
                new SegmentedControl<string>(disabled,
                [
                    new SegmentOption<string>("a", "Alpha"),
                    new SegmentOption<string>("b", "Beta"),
                    new SegmentOption<string>("c", "Gamma"),
                ]).Disabled(),
            ]));
    }

    // ── Rating ───────────────────────────────────────────────────────────

    static Node RatingSection()
    {
        var basic = new Bindable<float>(3.5f, _ => { });
        var full = new Bindable<float>(5f, _ => { });
        var disabled = new Bindable<float>(2f, _ => { });

        return Section("Rating",
            "Star rating with half-star support and disabled state.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 24, children:
                [
                    new Rating(basic, label: "Rating (3.5)"),
                    new Rating(full, max: 10, label: "Out of 10"),
                ]),
                new Rating(disabled, label: "Disabled").Disabled(),
            ]));
    }

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
