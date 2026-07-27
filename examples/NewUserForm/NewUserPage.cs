// Golden Example 01 — New User Form
//
// Demonstrates: reactive fields, computed properties, two-way binding,
// form validation, async submit, conditional nodes, guard clauses,
// IconView for standalone icon display, Stack for layered decoration,
// and Particles for celebratory feedback.

using Cascade.UI;

namespace NewUserForm;

internal sealed class NewUserPage : Component
{
    // ── Reactive fields ──────────────────────────────────────────────
    private string name            = "";
    private string email           = "";
    private string password        = "";
    private string confirmPassword = "";
    private bool   submitting      = false;
    private bool   submitted       = false;
    private string submitError     = "";

    // ── Computed properties ──────────────────────────────────────────
    private bool NameValid      => name.Trim().Length >= 2;
    private bool EmailValid     => email.Contains('@', StringComparison.Ordinal)
                                && email.Contains('.', StringComparison.Ordinal);
    private bool PasswordValid  => password.Length >= 8;
    private bool PasswordsMatch => password == confirmPassword;
    private bool FormValid      => NameValid && EmailValid && PasswordValid && PasswordsMatch;

    // ── Text styles ──────────────────────────────────────────────────
    private static readonly TextStyle Heading1   = new(24f, FontWeight.Bold, 1.3f);
    private static readonly TextStyle BodyMuted  = new(14f, FontWeight.Regular, 1.5f);
    private static readonly TextStyle BodyLabel  = new(13f, FontWeight.Medium, 1.4f);
    private static readonly TextStyle BodyHint   = new(12f, FontWeight.Regular, 1.4f);
    private static readonly TextStyle BodyDanger = new(12f, FontWeight.Regular, 1.4f);
    private static readonly TextStyle Heading2   = new(20f, FontWeight.Bold, 1.3f);
    private static readonly TextStyle Body       = new(14f, FontWeight.Regular, 1.5f);

    // ── Colors — all from theme tokens ──────────────────────────────
    private static ColorValue DangerColor    => ThemeSwitcher.ActiveColors.Danger;
    private static ColorValue DangerSubtle   => ThemeSwitcher.ActiveColors.DangerSubtle;
    private static ColorValue SuccessColor   => ThemeSwitcher.ActiveColors.Success;
    private static ColorValue PrimaryColor   => ThemeSwitcher.ActiveColors.Primary;
    private static ColorValue MutedTextColor => ThemeSwitcher.ActiveColors.TextMuted;
    private static ColorValue GoldColor      => ThemeSwitcher.Current.Palette.Yellow;

    // ── Icons (Lucide-style SVG paths, 24×24 viewbox) ────────────────
    private static readonly Icon CheckCircleIcon = new(
        ["M22 11.08V12a10 10 0 1 1-5.93-9.14", "M22 4L12 14.01l-3-3"],
        new Size(24, 24), 24f, "Success");

    private static readonly Icon AlertCircleIcon = new(
        ["M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z",
         "M12 8v4", "M12 16h.01"],
        new Size(24, 24), 20f, "Alert");

    protected override Node Render()
    {
        if (submitted)
        {
            return SuccessView();
        }

        return new ScrollView(
            new Column(
                spacing: 32,
                children:
                [
                    PageHeader(),
                    FormBody(),
                    FormFooter()
                ]
            ).Padding(horizontal: 40, vertical: 32)
             .MaxWidth(480)
             .Alignment(Alignment.Center)
        );
    }

    private static Node PageHeader()
    {
        return new Column(
            spacing: 8,
            children:
            [
                new Label("Create Your Account").Style(Heading1),
                new Label("Fill in the details below to get started.")
                    .Style(BodyMuted).Color(MutedTextColor)
            ]);
    }

    private Node FormBody()
    {
        return new Column(
            spacing: 24,
            children:
            [
                FormField(
                    label:     "Full Name",
                    field:     new Bindable<string>(name, v => { name = v; Invalidate(); }),
                    inputType: InputType.Text,
                    hint:      "At least 2 characters",
                    errorText: "Name must be at least 2 characters.",
                    isValid:   NameValid),
                FormField(
                    label:     "Email Address",
                    field:     new Bindable<string>(email, v => { email = v; Invalidate(); }),
                    inputType: InputType.Email,
                    hint:      "We'll send a confirmation link",
                    errorText: "Please enter a valid email address.",
                    isValid:   EmailValid),
                FormField(
                    label:     "Password",
                    field:     new Bindable<string>(password, v => { password = v; Invalidate(); }),
                    inputType: InputType.Password,
                    hint:      "At least 8 characters",
                    errorText: "Password must be at least 8 characters.",
                    isValid:   PasswordValid),
                FormField(
                    label:     "Confirm Password",
                    field:     new Bindable<string>(confirmPassword, v => { confirmPassword = v; Invalidate(); }),
                    inputType: InputType.Password,
                    errorText: "Passwords do not match.",
                    isValid:   PasswordsMatch)
            ]);
    }

    private Node FormFooter()
    {
        return new Column(
            spacing: 16,
            children:
            [
                submitError.Length > 0
                    ? ErrorBanner(submitError)
                    : Node.Empty,
                new Button(
                    label:   submitting ? "Creating account…" : "Create Account",
                    onClick: () => { _ = OnSubmit(); })
                    .Disabled(!FormValid || submitting)
                    .Loading(submitting)
                    .AccessibleLabel("Create account"),
                new Row(
                    spacing: 8,
                    children:
                    [
                        new Label("Already have an account?")
                            .Style(BodyMuted).Color(MutedTextColor),
                        new LinkButton(
                            label:   "Sign in",
                            onClick: () => { /* Navigation.Push<SignInPage>() */ })
                    ]).Alignment(Alignment.Center)
            ]);
    }

    private Node SuccessView()
    {
        return new Column(
            spacing: 24,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
            [
                new Stack(
                    new IconView(CheckCircleIcon, size: 64)
                        .Color(SuccessColor),
                    ParticleFactory.Particles(
                        emitter: ParticleEmitter.Burst(count: 80),
                        shape:   ParticleShape.Circle(radius: 4),
                        colors:  [PrimaryColor, SuccessColor, GoldColor],
                        physics: ParticlePhysics.Confetti(gravity: 0.3f, spread: 360f))),
                new Label("Welcome aboard!").Style(Heading2),
                new Label($"We've sent a confirmation link to {email}. Check your inbox to get started.")
                    .Style(Body),
                new Button(
                    label:   "Continue",
                    onClick: () => { /* Navigation.Push<HomePage>() */ })
            ])
            .Padding(40);
    }

    private static Node ErrorBanner(string message)
    {
        return new Row(
            spacing: 12,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children:
            [
                new IconView(AlertCircleIcon, size: 20)
                    .Color(DangerColor),
                new Label(message).Style(BodyDanger).Color(DangerColor)
            ])
            .Padding(16)
            .Background(DangerSubtle)
            .CornerRadius(8);
    }

    private static Node FormField(
        string           label,
        Bindable<string> field,
        InputType        inputType,
        string           errorText,
        bool             isValid,
        string?          hint = null)
    {
        bool showError = field.Value.Length > 0 && !isValid;

        return new Column(
            spacing: 6,
            children:
            [
                new Label(label).Style(BodyLabel),
                new TextInput(field, inputType: inputType)
                    .AccessibleLabel(label),
                hint != null && !showError
                    ? new Label(hint).Style(BodyHint).Color(MutedTextColor)
                    : Node.Empty,
                showError
                    ? new Label(errorText).Style(BodyDanger).Color(DangerColor)
                    : Node.Empty
            ]);
    }

    private async Task OnSubmit()
    {
        if (!FormValid || submitting)
        {
            return;
        }

        submitting  = true;
        submitError = "";
        Invalidate();

        try
        {
            // Simulate async API call
            await Task.Delay(1500);

            submitted = true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception)
        {
            submitError = "Something went wrong. Please try again.";
        }
#pragma warning restore CA1031
        finally
        {
            submitting = false;
            Invalidate();
        }
    }
}
