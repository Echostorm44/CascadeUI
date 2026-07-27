using System.Text.RegularExpressions;
using Cascade.UI;
using static OnboardingWizard.FormHelpers;

namespace OnboardingWizard;

// ── Wizard state ──────────────────────────────────────────────────────────────

internal sealed record WizardData
{
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Password { get; init; } = "";

    public string Handle { get; init; } = "";
    public DateOnly? DateOfBirth { get; init; } = null;
    public IReadOnlyList<string> Interests { get; init; } = [];

    public string PlanId { get; init; } = "starter";
    public string BillingPeriod { get; init; } = "monthly";
}

// ── Page shell ────────────────────────────────────────────────────────────────

internal sealed class OnboardingPage : Component
{
    private int step = 0;
    private WizardData data = new();
    private bool submitting = false;
    private string submitError = "";

    private static readonly IReadOnlyList<Step> Steps =
    [
        new Step("Account"),
        new Step("Profile"),
        new Step("Plan"),
        new Step("Confirm")
    ];

    protected override Node Render()
    {
        return new ErrorBoundary(
            content: () => WizardBody(),
            fallback: ex => new Column(spacing: 20, children:
            [
                new Label("Something went wrong").Bold().FontSize(20),
                new Label($"Error: {ex.Message}")
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                new Button("Try Again", onClick: () => { Invalidate(); })
                    .Variant("outline")
            ]).Alignment(Alignment.Center).Padding(40)
        );
    }

    private Node WizardBody() =>
        new Column(spacing: 0, children:
        [
            new StepIndicator(
                currentStep: new Bindable<int>(step, v => { step = v; Invalidate(); }),
                steps: Steps
            )
            .Clickable(s => s < step)
            .OnStepClick(i => { step = i; Invalidate(); })
            .Padding(horizontal: 40, vertical: 24),

            new Separator(),

            new ScrollView(
                new Column(spacing: 32, children:
                [
                    StepContent(),
                    submitError.Length > 0
                        ? ErrorBanner(submitError)
                        : Node.Empty,
                    StepNavigation()
                ])
                .Padding(horizontal: 40, vertical: 32)
                .MaxWidth(560)
                .Alignment(Alignment.Center)
            ).Grow(1)
        ]);

    private Node StepContent() =>
        step switch
        {
            0 => new AccountStep { Data = data, OnNext = AdvanceWithData },
            1 => new ProfileStep { Data = data, OnNext = AdvanceWithData, OnBack = GoBack },
            2 => new PlanStep { Data = data, OnNext = AdvanceWithData, OnBack = GoBack },
            3 => new ConfirmationStep { Data = data },
            _ => Node.Empty
        };

    // Steps 0–2 render their own Back/Next row (so both buttons share one
    // baseline). Only the final Confirm step has no in-step buttons, so the
    // page supplies its Back + Create Account row here.
    private Node StepNavigation()
    {
        if (step != Steps.Count - 1)
        {
            return Node.Empty;
        }

        return new Row(children:
        [
            new Button("Back", onClick: GoBack)
                .Variant("outline"),
            new Spacer(),
            new Button(
                label: submitting ? "Submitting…" : "Create Account",
                onClick: () => { _ = OnSubmitAsync(); }
            )
            .Variant("primary")
            .Disabled(submitting)
            .Loading(submitting)
        ]);
    }

    private void GoBack()
    {
        step--;
        submitError = "";
        Invalidate();
    }

    private void AdvanceWithData(WizardData updated)
    {
        data = updated;
        step++;
        submitError = "";
        Invalidate();
    }

    private async Task OnSubmitAsync()
    {
        submitting = true;
        submitError = "";
        Invalidate();

        try
        {
            await Task.Delay(1500);
            submitting = false;
            submitError = "";
            Invalidate();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            submitError = "Something went wrong. Please try again.";
            submitting = false;
            Invalidate();
        }
    }

    private static Node ErrorBanner(string message)
    {
        var colors = ThemeSwitcher.ActiveColors;
        return new Row(spacing: 12, children:
        [
            new Label("⚠").FontSize(18).Color(colors.Danger),
            new Label(message).Color(colors.Danger)
        ])
        .Padding(14)
        .Background(colors.DangerSubtle)
        .CornerRadius(8);
    }
}

// ── Step 1 — Account ──────────────────────────────────────────────────────────

internal sealed class AccountStep : Component
{
    public required WizardData Data { get; init; }
    public required Action<WizardData> OnNext { get; init; }
    public Action? OnBack { get; init; }

    private string name = "";
    private string email = "";
    private string password = "";

    private readonly FormScope form = new();

    protected override Task OnMounted()
    {
        name = Data.FullName;
        email = Data.Email;
        password = Data.Password;
        return Task.CompletedTask;
    }

    protected override Node Render() =>
        new Column(spacing: 24, children:
        [
            new Label("Create your account").Bold().FontSize(22),
            new Label("Enter your details to get started.")
                .Color(ThemeSwitcher.ActiveColors.TextMuted),

            new FormValidator(form,
                content: new Column(spacing: 20, children:
                [
                    FormFieldRow("Full Name",
                        new TextInput(
                            new Bindable<string>(name, v => { name = v; Invalidate(); }),
                            placeholder: "Enter your full name",
                            inputType: InputType.Text
                        )
                        .Validate(v => v.Trim().Length >= 2
                            ? ValidationResult.Ok
                            : ValidationResult.Error("Name must be at least 2 characters"))
                        .ValidateOn(ValidationTrigger.Blur)
                        .AccessibleLabel("Full Name")
                    ),

                    FormFieldRow("Email",
                        new TextInput(
                            new Bindable<string>(email, v => { email = v; Invalidate(); }),
                            placeholder: "you@example.com",
                            inputType: InputType.Email
                        )
                        .Validate(v =>
                        {
                            bool hasAt = v.Contains('@', StringComparison.Ordinal);
                            bool hasDot = v.Contains('.', StringComparison.Ordinal);
                            return hasAt && hasDot
                                ? ValidationResult.Ok
                                : ValidationResult.Error("Please enter a valid email address");
                        })
                        .ValidateOn(ValidationTrigger.Blur)
                        .AccessibleLabel("Email")
                    ),

                    FormFieldRow("Password",
                        new TextInput(
                            new Bindable<string>(password, v => { password = v; Invalidate(); }),
                            placeholder: "At least 8 characters",
                            inputType: InputType.Password
                        )
                        .Validate(v => v.Length >= 8
                            ? ValidationResult.Ok
                            : ValidationResult.Error("Password must be at least 8 characters"))
                        .ValidateOn(ValidationTrigger.Blur)
                        .AccessibleLabel("Password"),
                        hint: "Use 8 or more characters with a mix of letters, numbers, and symbols."
                    )
                ])
            ),

            new Row(children:
            [
                OnBack is not null
                    ? new Button("Back", onClick: () => OnBack()).Variant("outline")
                    : Node.Empty,
                new Spacer(),
                new Button("Next", onClick: OnNextClicked)
                    .Variant("primary")
                    .Disabled(!form.IsValid)
            ])
        ]);

    private void OnNextClicked()
    {
        if (!form.ValidateAll())
        {
            return;
        }

        OnNext(Data with
        {
            FullName = name.Trim(),
            Email = email.Trim(),
            Password = password
        });
    }
}

// ── Step 2 — Profile ──────────────────────────────────────────────────────────

internal sealed class ProfileStep : Component
{
    public required WizardData Data { get; init; }
    public required Action<WizardData> OnNext { get; init; }
    public Action? OnBack { get; init; }

    private string handle = "";
    private DateOnly? dateOfBirth = null;
    private IReadOnlyList<string> interests = [];

    private readonly FormScope form = new();

    private static readonly IReadOnlyList<SelectOption<string>> InterestOptions =
    [
        new("design", "Design"),
        new("engineering", "Engineering"),
        new("product", "Product"),
        new("data", "Data & Analytics"),
        new("marketing", "Marketing"),
        new("devops", "DevOps"),
        new("mobile", "Mobile"),
        new("security", "Security"),
    ];

    protected override Task OnMounted()
    {
        handle = Data.Handle;
        dateOfBirth = Data.DateOfBirth;
        interests = Data.Interests;
        return Task.CompletedTask;
    }

    protected override Node Render() =>
        new Column(spacing: 24, children:
        [
            new Label("Set up your profile").Bold().FontSize(22),
            new Label("Tell us a bit about yourself. All fields are optional.")
                .Color(ThemeSwitcher.ActiveColors.TextMuted),

            new FormValidator(form,
                content: new Column(spacing: 20, children:
                [
                    FormFieldRow("Handle",
                        new TextInput(
                            new Bindable<string>(handle, v => { handle = v; Invalidate(); }),
                            placeholder: "@yourhandle",
                            inputType: InputType.Text
                        )
                        .Validate(v =>
                        {
                            if (v.Length == 0) { return ValidationResult.Ok; }
                            return Regex.IsMatch(v, @"^@?[a-zA-Z0-9_]{2,30}$")
                                ? ValidationResult.Ok
                                : ValidationResult.Error("Handle must be 2–30 alphanumeric characters or underscores");
                        })
                        .ValidateOn(ValidationTrigger.Blur)
                        .AccessibleLabel("Handle"),
                        hint: "Choose a unique handle for your public profile."
                    ),

                    FormFieldRow("Date of Birth",
                        new DatePicker(
                            new Bindable<DateOnly?>(dateOfBirth, v => { dateOfBirth = v; Invalidate(); }),
                            placeholder: "Select date",
                            max: DateOnly.FromDateTime(DateTime.Today.AddYears(-13))
                        )
                    ),

                    FormFieldRow("Interests",
                        new MultiSelect<string>(
                            new Bindable<IReadOnlyList<string>>(interests, v => { interests = v; Invalidate(); }),
                            options: InterestOptions,
                            placeholder: "Select your interests"
                        )
                        .AccessibleLabel("Interests"),
                        hint: "Pick topics you're interested in. We'll personalize your feed."
                    )
                ])
            ),

            new Row(children:
            [
                OnBack is not null
                    ? new Button("Back", onClick: () => OnBack()).Variant("outline")
                    : Node.Empty,
                new Spacer(),
                new Button("Next", onClick: OnNextClicked)
                    .Variant("primary")
            ])
        ]);

    private void OnNextClicked()
    {
        if (!form.ValidateAll()) { return; }

        OnNext(Data with
        {
            Handle = handle.Trim().TrimStart('@'),
            DateOfBirth = dateOfBirth,
            Interests = interests
        });
    }
}

// ── Step 3 — Plan ─────────────────────────────────────────────────────────────

internal sealed class PlanStep : Component
{
    public required WizardData Data { get; init; }
    public required Action<WizardData> OnNext { get; init; }
    public Action? OnBack { get; init; }

    private string planId = "starter";
    private string billingPeriod = "monthly";

    protected override Task OnMounted()
    {
        planId = Data.PlanId;
        billingPeriod = Data.BillingPeriod;
        return Task.CompletedTask;
    }

    protected override Node Render() =>
        new Column(spacing: 24, children:
        [
            new Label("Choose your plan").Bold().FontSize(22),
            new Label("Select the plan that works best for you.")
                .Color(ThemeSwitcher.ActiveColors.TextMuted),

            // Billing period
            new Column(spacing: 8, children:
            [
                new Label("Billing Period").Bold(),
                new RadioGroup<string>(
                    new Bindable<string>(billingPeriod, v => { billingPeriod = v; Invalidate(); }),
                    new Row(spacing: 16, children:
                    [
                        new RadioButton<string>("monthly", "Monthly"),
                        new RadioButton<string>("annual", new Row(spacing: 8, children:
                        [
                            new Label("Annual"),
                            new Label("Save 20%")
                                .FontSize(12)
                                .Color(ThemeSwitcher.ActiveColors.Success)
                                .Background(ThemeSwitcher.ActiveColors.SuccessSubtle)
                                .Padding(horizontal: 8, vertical: 2)
                                .CornerRadius(4)
                        ]))
                    ])
                ).AccessibleLabel("Billing Period")
            ]),

            // Plan cards
            new Column(spacing: 8, children:
            [
                new Label("Select Plan").Bold(),
                new RadioGroup<string>(
                    new Bindable<string>(planId, v => { planId = v; Invalidate(); }),
                    new Column(spacing: 10, children:
                    [
                        PlanCard("starter", "Starter", PlanPrice("starter"), "5 projects · 1 GB storage · Community support"),
                        PlanCard("pro", "Pro", PlanPrice("pro"), "Unlimited projects · 50 GB storage · Priority support", popular: true),
                        PlanCard("business", "Business", PlanPrice("business"), "Everything in Pro · SSO · Dedicated account manager")
                    ])
                ).AccessibleLabel("Select Plan")
            ]),

            new Row(children:
            [
                OnBack is not null
                    ? new Button("Back", onClick: () => OnBack()).Variant("outline")
                    : Node.Empty,
                new Spacer(),
                new Button("Next", onClick: OnNextClicked)
                    .Variant("primary")
            ])
        ]);

    private string PlanPrice(string plan)
    {
        bool annual = billingPeriod == "annual";
        return plan switch
        {
            "starter"  => "$0/mo",
            "pro"      => annual ? "$16/mo" : "$20/mo",
            "business" => annual ? "$40/mo" : "$50/mo",
            _          => ""
        };
    }

    private static Node PlanCard(
        string value,
        string name,
        string price,
        string features,
        bool popular = false)
    {
        return new RadioButton<string>(
            value: value,
            label: new Column(spacing: 6, children:
            [
                new Row(spacing: 8, children:
                [
                    new Label(name).Bold().Grow(1),
                    popular
                        ? new Label("Popular")
                            .FontSize(11)
                            .Color(ThemeSwitcher.ActiveColors.PrimaryText)
                            .Background(ThemeSwitcher.ActiveColors.Primary.Opacity(0.12f))
                            .Padding(horizontal: 8, vertical: 2)
                            .CornerRadius(4)
                        : Node.Empty,
                    new Label(price).Bold()
                ]),
                new Label(features)
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ]),
            style: RadioStyle.Card
        );
    }

    private void OnNextClicked()
    {
        OnNext(Data with
        {
            PlanId = planId,
            BillingPeriod = billingPeriod
        });
    }
}

// ── Step 4 — Confirmation ─────────────────────────────────────────────────────

internal sealed class ConfirmationStep : Component
{
    public required WizardData Data { get; init; }

    protected override Node Render() =>
        new Column(spacing: 24, children:
        [
            new Label("Review & confirm").Bold().FontSize(22),
            new Label("Please review your information before creating your account.")
                .Color(ThemeSwitcher.ActiveColors.TextMuted),

            SummaryCard("Account",
                SummaryRow("Full Name", Data.FullName),
                SummaryRow("Email", Data.Email),
                SummaryRow("Password", "••••••••")
            ),
            SummaryCard("Profile",
                SummaryRow("Handle",
                    Data.Handle.Length > 0 ? $"@{Data.Handle}" : "Not set"),
                SummaryRow("Date of Birth",
                    Data.DateOfBirth?.ToString("MMMM d, yyyy") ?? "Not set"),
                SummaryRow("Interests",
                    Data.Interests.Count > 0
                        ? string.Join(", ", Data.Interests)
                        : "Not set")
            ),
            SummaryCard("Plan",
                SummaryRow("Selected Plan", Data.PlanId switch
                {
                    "starter"  => "Starter",
                    "pro"      => "Pro",
                    "business" => "Business",
                    _          => Data.PlanId
                }),
                SummaryRow("Billing", Data.BillingPeriod switch
                {
                    "monthly" => "Monthly",
                    "annual"  => "Annual",
                    _         => Data.BillingPeriod
                })
            )
        ]);

    private static Node SummaryCard(string title, params Node[] rows) =>
        new Column(spacing: 0, children:
        [
            new Label(title).Bold().Padding(horizontal: 16, vertical: 10),
            new Separator(),
            new Column(children: rows)
        ])
        .Background(ThemeSwitcher.ActiveColors.Surface)
        .CornerRadius(8)
        .Border(color: ThemeSwitcher.ActiveColors.Border, width: 1);

    private static Node SummaryRow(string label, string value) =>
        new Row(spacing: 8, children:
        [
            new Label(label)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
                .Width(140),
            new Label(value).Grow(1)
        ])
        .Padding(horizontal: 16, vertical: 10);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

internal static class FormHelpers
{
    internal static Node FormFieldRow(string label, Node field, string? hint = null) =>
        new Column(spacing: 4, children:
        [
            new Label(label).Bold().FontSize(14),
            field,
            hint is not null
                ? new Label(hint).FontSize(12).Color(ThemeSwitcher.ActiveColors.TextMuted)
                : Node.Empty
        ]);
}
