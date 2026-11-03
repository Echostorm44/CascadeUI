#pragma warning disable CA2000 // components and harnesses are disposed with the harness

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// .Validate(rule) / .ValidateOn(trigger) and FormValidator: rules run when the trigger says, the
/// message shows below the control, and a form collects its fields. (None of this ran before: the
/// rules were stored and never called.)
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class ValidationTriggerTests
{
    private static ValidationResult NotEmpty(string text) =>
        string.IsNullOrWhiteSpace(text) ? ValidationResult.Error("Required") : ValidationResult.Ok;

    private static ValidationResult AtLeastThree(string text) =>
        text.Length < 3 ? ValidationResult.Error("At least 3 characters") : ValidationResult.Ok;

    /// <summary>A form of text fields, each re-rendering on change as an app's would.</summary>
    private sealed class TextForm(ValidationTrigger trigger, int fields = 1, FormScope? scope = null) : Component
    {
        private readonly string[] values = new string[fields];
        internal readonly List<TextInput> Inputs = [];
        internal readonly NodeRef<TextInput> First = new();

        protected override Node Render()
        {
            Inputs.Clear();
            var children = new List<Node>();
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                var input = new TextInput(Bind(values[index] ?? "", v => values[index] = v))
                    .Validate(AtLeastThree).ValidateOn(trigger).Width(200);
                if (i == 0)
                {
                    input = input.Ref(First);
                }
                Inputs.Add(input);
                children.Add(input);
            }
            children.Add(new Button("Other", () => { }));
            Node column = new Column(spacing: 8, children: [.. children]);
            return scope is null ? column : new FormValidator(scope, column);
        }
    }

    private static void Type(RenderHarness app, TextInput input, string text)
    {
        FocusManager.RequestFocus(input);
        foreach (char c in text)
        {
            app.Press(Key.A, character: c);
            app.Frame();
        }
    }

    private static bool ShowsMessage(TextInput input) => Validation.Shown(input) is not null;

    [Test]
    public async Task Immediate_ShowsTheMessageAsYouType_AndItGoesWhenFixed()
    {
        var form = new TextForm(ValidationTrigger.Immediate);
        using var app = new RenderHarness(() => form);
        app.Frame();
        float plainHeight = form.Inputs[0].LayoutData.Bounds.Height;

        Type(app, form.Inputs[0], "ab");
        await Assert.That(ShowsMessage(form.Inputs[0])).IsTrue();
        await Assert.That(Validation.Shown(form.Inputs[0])!.Message).IsEqualTo("At least 3 characters");
        // The message takes a line below the field.
        await Assert.That(form.Inputs[0].LayoutData.Bounds.Height).IsEqualTo(plainHeight + Validation.MessageHeight);

        Type(app, form.Inputs[0], "c");
        await Assert.That(ShowsMessage(form.Inputs[0])).IsFalse();
        await Assert.That(form.Inputs[0].LayoutData.Bounds.Height).IsEqualTo(plainHeight);
    }

    [Test]
    public async Task Immediate_DrawsTheMessageInTheDangerColour()
    {
        var form = new TextForm(ValidationTrigger.Immediate);
        using var app = new RenderHarness(() => form);
        app.Frame();
        Type(app, form.Inputs[0], "a");
        var frame = app.Frame();

        (byte r, byte g, byte b) = ThemeSwitcher.Current.Colors.Danger.ToSrgbBytes();
        int danger = frame.Count(p => Math.Abs(p.R - r) < 30 && Math.Abs(p.G - g) < 30 && Math.Abs(p.B - b) < 30);
        await Assert.That(danger).IsGreaterThan(100);
    }

    [Test]
    public async Task Blur_WaitsUntilFocusLeaves()
    {
        var form = new TextForm(ValidationTrigger.Blur);
        using var app = new RenderHarness(() => form);
        app.Frame();

        Type(app, form.Inputs[0], "ab");
        await Assert.That(ShowsMessage(form.Inputs[0])).IsFalse();

        app.Press(Key.Tab);
        app.Frame();
        await Assert.That(ShowsMessage(form.Inputs[0])).IsTrue();
    }

    [Test]
    public async Task Debounced_WaitsForTheValueToSettle()
    {
        var form = new TextForm(ValidationTrigger.Debounced);
        using var app = new RenderHarness(() => form);
        app.Frame();

        Type(app, form.Inputs[0], "ab");
        await Assert.That(ShowsMessage(form.Inputs[0])).IsFalse();

        await Task.Delay(350);
        app.Frame();
        await Assert.That(ShowsMessage(form.Inputs[0])).IsTrue();
    }

    [Test]
    public async Task Submit_WaitsForValidateAll_WhichChecksEveryField()
    {
        var scope = new FormScope();
        var form = new TextForm(ValidationTrigger.Submit, fields: 2, scope: scope);
        using var app = new RenderHarness(() => form);
        app.Frame();

        Type(app, form.Inputs[0], "abc");
        app.Press(Key.Tab);
        app.Frame();
        await Assert.That(scope.FieldCount).IsEqualTo(2);
        await Assert.That(ShowsMessage(form.Inputs[1])).IsFalse();
        // IsValid looks without showing anything: the second field is still empty.
        await Assert.That(scope.IsValid).IsFalse();
        await Assert.That(ShowsMessage(form.Inputs[1])).IsFalse();

        await Assert.That(scope.ValidateAll()).IsFalse();
        app.Frame();
        await Assert.That(ShowsMessage(form.Inputs[0])).IsFalse();
        await Assert.That(ShowsMessage(form.Inputs[1])).IsTrue();
        await Assert.That(scope.ErrorCount).IsEqualTo(1);

        scope.Reset();
        app.Frame();
        await Assert.That(ShowsMessage(form.Inputs[1])).IsFalse();
        await Assert.That(scope.ErrorCount).IsEqualTo(0);
    }

    [Test]
    public async Task Manual_RunsOnlyWhenAsked()
    {
        var form = new TextForm(ValidationTrigger.Manual);
        using var app = new RenderHarness(() => form);
        app.Frame();

        Type(app, form.Inputs[0], "a");
        app.Press(Key.Tab);
        app.Frame();
        await Assert.That(ShowsMessage(form.Inputs[0])).IsFalse();

        var result = form.First.Validate();
        app.Frame();
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(ShowsMessage(form.Inputs[0])).IsTrue();
    }

    [Test]
    public async Task OtherControls_ValidateToo()
    {
        // A required checkbox, validated on change.
        bool agreed = false;
        var box = new Checkbox(new Bindable<bool>(false, v => agreed = v), "I agree")
            .Validate(v => v ? ValidationResult.Ok : ValidationResult.Error("Please agree"))
            .ValidateOn(ValidationTrigger.Immediate);
        using var app = new RenderHarness(() => box);
        app.Frame();
        await Assert.That(Validation.Run((IValidatable)box).IsValid).IsFalse();
        app.Frame();
        await Assert.That(Validation.Shown(box)?.Message).IsEqualTo("Please agree");
        await Assert.That(agreed).IsFalse();
    }

    [Test]
    public async Task TagInput_ARejectedTagShowsTheRulesMessage()
    {
        IReadOnlyList<string> tags = [];
        var input = new TagInput(new Bindable<IReadOnlyList<string>>([], v => tags = v))
            .Validate(tag => tag.Contains(' ', StringComparison.Ordinal) ? ValidationResult.Error("No spaces") : ValidationResult.Ok);
        input.AddTag("two words");
        await Assert.That(tags.Count).IsEqualTo(0);
        await Assert.That(Validation.Shown(input)?.Message).IsEqualTo("No spaces");
    }

    [Test]
    public async Task Debounce_ReportsOnlyTheSettledText_AndFlushesOnBlur()
    {
        var seen = new List<string>();
        var input = new TextInput(new Bindable<string>("", _ => { })).Debounce(TimeSpan.FromMilliseconds(100)).OnChange(seen.Add);
        var other = new Button("Other", () => { });
        using var app = new RenderHarness(() => new Column(children: [input.Width(200), other]));
        app.Frame();

        Type(app, input, "abc");
        await Assert.That(seen.Count).IsEqualTo(0);
        await Task.Delay(150);
        app.Frame();
        await Assert.That(string.Join(",", seen)).IsEqualTo("abc");

        // Leaving the field delivers a pending change at once.
        Type(app, input, "d");
        FocusManager.RequestFocus(other);
        app.Press(Key.Tab);
        await Assert.That(string.Join(",", seen)).IsEqualTo("abc,abcd");
    }
}
