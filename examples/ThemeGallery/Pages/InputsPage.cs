using Cascade.UI;

namespace ThemeGallery.Pages;

internal static class InputsPage
{
    internal static Node Render(ThemeGalleryPage host) =>
        new Column(spacing: 32, children:
        [
            TextInputSection(),
            TextAreaSection(),
            PasswordInputSection(),
            PinInputSection(),
            NumberInputSection(),
            TagInputSection(),
            MentionInputSection(),
            FormValidatorSection(),
        ]);

    // ── TextInput ────────────────────────────────────────────────────────

    static Node TextInputSection()
    {
        var normal = new Bindable<string>("", _ => { });
        var filled = new Bindable<string>("Hello World", _ => { });
        var error = new Bindable<string>("bad@", _ => { });
        var disabled = new Bindable<string>("Cannot edit", _ => { });
        var readOnly = new Bindable<string>("Read-only text", _ => { });

        return Section("TextInput",
            "Single-line text input with placeholder, label, validation, disabled, and read-only states.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new TextInput(normal, placeholder: "Enter text...").Width(220),
                    new TextInput(filled, label: "With Label").Width(220),
                ]),
                new Row(spacing: 16, children:
                [
                    new TextInput(error, label: "Email")
                        .Validate(v => v.Contains('@', StringComparison.Ordinal) && v.Contains('.', StringComparison.Ordinal)
                            ? ValidationResult.Ok
                            : ValidationResult.Error("Invalid email"))
                        .Width(220),
                    new TextInput(disabled, label: "Disabled").Disabled().Width(220),
                ]),
                new TextInput(readOnly, label: "Read Only").ReadOnly().Width(220),
            ]));
    }

    // ── TextArea ─────────────────────────────────────────────────────────

    static Node TextAreaSection()
    {
        var normal = new Bindable<string>("", _ => { });
        var filled = new Bindable<string>("This is a multi-line text area.\nIt supports multiple lines of content.", _ => { });
        var disabled = new Bindable<string>("Cannot edit this area.", _ => { });

        return Section("TextArea",
            "Multi-line text area with line counts, auto-grow, and character count.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new TextArea(normal, placeholder: "Write something...", minLines: 3, maxLines: 6)
                        .Width(280),
                    new TextArea(filled, label: "With Content", minLines: 3)
                        .ShowCharacterCount(CountStyle.Fraction)
                        .MaxLength(500)
                        .Width(280),
                ]),
                new TextArea(disabled, label: "Disabled", minLines: 2)
                    .Disabled()
                    .Width(280),
            ]));
    }

    // ── PasswordInput ────────────────────────────────────────────────────

    static Node PasswordInputSection()
    {
        var normal = new Bindable<string>("", _ => { });
        var withStrength = new Bindable<string>("", _ => { });
        var disabled = new Bindable<string>("secret", _ => { });

        return Section("PasswordInput",
            "Password field with visibility toggle, strength indicator, and disabled state.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new PasswordInput(normal, placeholder: "Enter password...")
                        .ShowToggle()
                        .Width(220),
                    new PasswordInput(withStrength, placeholder: "Strong password...")
                        .ShowToggle()
                        .StrengthIndicator()
                        .Width(220),
                ]),
                new PasswordInput(disabled, placeholder: "Disabled")
                    .ShowToggle()
                    .Disabled()
                    .Width(220),
            ]));
    }

    // ── PinInput ─────────────────────────────────────────────────────────

    static Node PinInputSection()
    {
        var numeric = new Bindable<string>("", _ => { });
        var masked = new Bindable<string>("", _ => { });
        var prefilled = new Bindable<string>("1234", _ => { });
        var disabled = new Bindable<string>("0000", _ => { });

        return Section("PinInput",
            "Pin/OTP input with fixed-length character boxes, numeric-only and masked modes.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new Column(spacing: 4, children:
                    [
                        new Label("4-digit numeric").FontSize(12).Color(ThemeHelper.SubtleText),
                        new PinInput(numeric, length: 4).Numeric(),
                    ]),
                    new Column(spacing: 4, children:
                    [
                        new Label("6-digit masked").FontSize(12).Color(ThemeHelper.SubtleText),
                        new PinInput(masked, length: 6).Numeric().Masked(),
                    ]),
                ]),
                new Row(spacing: 16, children:
                [
                    new Column(spacing: 4, children:
                    [
                        new Label("Pre-filled").FontSize(12).Color(ThemeHelper.SubtleText),
                        new PinInput(prefilled, length: 4).Numeric(),
                    ]),
                    new Column(spacing: 4, children:
                    [
                        new Label("Disabled").FontSize(12).Color(ThemeHelper.SubtleText),
                        new PinInput(disabled, length: 4).Numeric().Disabled(),
                    ]),
                ]),
            ]));
    }

    // ── NumberInput ──────────────────────────────────────────────────────

    static Node NumberInputSection()
    {
        var basic = new Bindable<int>(0, _ => { });
        var bounded = new Bindable<int>(50, _ => { });
        var withStep = new Bindable<double>(0.0, _ => { });
        var disabled = new Bindable<int>(42, _ => { });

        return Section("NumberInput",
            "Numeric input with min/max bounds, step increment, stepper buttons, and formatting.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new NumberInput<int>(basic, label: "Basic").Width(180),
                    new NumberInput<int>(bounded, min: 0, max: 100, label: "Bounded (0–100)")
                        .StepperButtons(StepperPosition.Right)
                        .Width(180),
                ]),
                new Row(spacing: 16, children:
                [
                    new NumberInput<double>(withStep, min: 0.0, max: 10.0, step: 0.5, format: "F1", label: "Step 0.5")
                        .StepperButtons(StepperPosition.Split)
                        .Width(180),
                    new NumberInput<int>(disabled, label: "Disabled")
                        .StepperButtons(StepperPosition.Right)
                        .Disabled()
                        .Width(180),
                ]),
            ]));
    }

    // ── TagInput ─────────────────────────────────────────────────────────

    static Node TagInputSection()
    {
        var basic = new Bindable<IReadOnlyList<string>>(["React", "Cascade"], _ => { });
        var limited = new Bindable<IReadOnlyList<string>>(["Alpha", "Beta"], _ => { });
        var disabled = new Bindable<IReadOnlyList<string>>(["Locked", "Tags"], _ => { });

        return Section("TagInput",
            "Tag/chip input with add/remove, max count, and suggestions.",
            new Column(spacing: 12, children:
            [
                new Row(spacing: 16, children:
                [
                    new TagInput(basic, placeholder: "Add tags...", label: "Tags").Width(300),
                    new TagInput(limited, placeholder: "Max 5...", maxTags: 5, label: "Limited")
                        .Width(300),
                ]),
                new TagInput(disabled, label: "Disabled").Disabled().Width(300),
            ]));
    }

    // ── MentionInput ─────────────────────────────────────────────────────

    static Node MentionInputSection()
    {
        var normal = new Bindable<string>("", _ => { });
        var disabled = new Bindable<string>("Hello @world", _ => { });

        return Section("MentionInput",
            "Text input with @mention trigger support and disabled state.",
            new Column(spacing: 12, children:
            [
                new MentionInput(normal, placeholder: "Type @ to mention...", label: "Mentions")
                    .Width(400),
                new MentionInput(disabled, label: "Disabled").Disabled().Width(400),
            ]));
    }

    // ── FormValidator ────────────────────────────────────────────────────

    static Node FormValidatorSection()
    {
        var name = new Bindable<string>("", _ => { });
        var email = new Bindable<string>("", _ => { });
        var form = new FormScope();

        return Section("FormValidator",
            "Form-level validation wrapping multiple fields. ValidateAll() triggers all field rules.",
            new FormValidator(form, content:
                new Column(spacing: 12, children:
                [
                    new TextInput(name, label: "Name", placeholder: "Required")
                        .Validate(v => string.IsNullOrWhiteSpace(v)
                            ? ValidationResult.Error("Name is required")
                            : ValidationResult.Ok)
                        .Width(300),
                    new TextInput(email, label: "Email", placeholder: "user@example.com")
                        .Validate(v => !v.Contains('@', StringComparison.Ordinal)
                            ? ValidationResult.Error("Invalid email address")
                            : ValidationResult.Ok)
                        .Width(300),
                    new Row(spacing: 12, children:
                    [
                        new Button("Validate All", onClick: () => { form.ValidateAll(); }),
                        new Button("Reset", onClick: () => { form.Reset(); }).Variant("outline"),
                    ]),
                ])
            ));
    }

    // ── Section Helper ───────────────────────────────────────────────────

    static Node Section(string title, string description, Node content) =>
        ThemeHelper.Section(title, description, content);
}
