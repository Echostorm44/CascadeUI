#pragma warning disable CA2000, CA1812

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Cascade.UI.Tests;

// ═══════════════════════════════════════════════════════════════════════
// PasswordInput Tests
// ═══════════════════════════════════════════════════════════════════════

public sealed class PasswordInputTests
{
    private static Bindable<string> CreateStringBinding(string initial = "")
    {
        string captured = initial;
        return new Bindable<string>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_SetsValueBindingAndPlaceholder()
    {
        var binding = CreateStringBinding("secret");
        var input = new PasswordInput(binding, placeholder: "Enter password");

        string value = input.Value.Value;
        string placeholder = input.Placeholder.Value;
        await Assert.That(value).IsEqualTo("secret");
        await Assert.That(placeholder).IsEqualTo("Enter password");
    }

    [Test]
    public async Task ShowToggle_DefaultIsTrue()
    {
        var input = new PasswordInput(CreateStringBinding());

        bool showToggle = input.ShowToggleButton;
        await Assert.That(showToggle).IsTrue();
    }

    [Test]
    public async Task ShowToggle_SetToFalse()
    {
        var input = new PasswordInput(CreateStringBinding()).ShowToggle(false);

        bool showToggle = input.ShowToggleButton;
        await Assert.That(showToggle).IsFalse();
    }

    [Test]
    public async Task StrengthIndicator_DefaultIsFalse()
    {
        var input = new PasswordInput(CreateStringBinding());

        bool enabled = input.UseStrengthIndicator;
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task StrengthIndicator_EnablesIndicator()
    {
        var input = new PasswordInput(CreateStringBinding()).StrengthIndicator(true);

        bool enabled = input.UseStrengthIndicator;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task StrengthEvaluator_SetsCustomEvaluator()
    {
        Func<string, PasswordStrength> evaluator = _ => PasswordStrength.Strong;
        var input = new PasswordInput(CreateStringBinding()).StrengthEvaluator(evaluator);

        bool hasEvaluator = input.CustomStrengthEvaluator is not null;
        await Assert.That(hasEvaluator).IsTrue();

        var result = input.CustomStrengthEvaluator!("any");
        await Assert.That(result).IsEqualTo(PasswordStrength.Strong);
    }

    [Test]
    public async Task MaxLength_SetsValue()
    {
        var input = new PasswordInput(CreateStringBinding()).MaxLength(128);

        int? maxLength = input.MaxLengthValue;
        await Assert.That(maxLength).IsEqualTo(128);
    }

    [Test]
    public async Task Validate_AddsRule()
    {
        var input = new PasswordInput(CreateStringBinding())
            .Validate(v => v.Length >= 8 ? ValidationResult.Ok : ValidationResult.Error("Too short"));

        int ruleCount = input.ValidationRules.Count;
        await Assert.That(ruleCount).IsEqualTo(1);
    }

    [Test]
    public async Task ValidateOn_SetsTrigger()
    {
        var input = new PasswordInput(CreateStringBinding()).ValidateOn(ValidationTrigger.Immediate);

        var trigger = input.ValidationTriggerMode;
        await Assert.That(trigger).IsEqualTo(ValidationTrigger.Immediate);
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var input = new PasswordInput(CreateStringBinding()).Disabled();

        bool disabled = input.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task ReadOnly_SetsFlag()
    {
        var input = new PasswordInput(CreateStringBinding()).ReadOnly();

        bool readOnly = input.IsReadOnly;
        await Assert.That(readOnly).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var input = new PasswordInput(CreateStringBinding()).AccessibleLabel("Password field");

        string label = input.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Password field");
    }

    [Test]
    public async Task RunValidation_PassingRule_ReturnsOk()
    {
        var binding = CreateStringBinding("ValidPass1!");
        var input = new PasswordInput(binding)
            .Validate(v => v.Length >= 8 ? ValidationResult.Ok : ValidationResult.Error("Too short"));

        var result = input.RunValidation();
        bool isValid = result.IsValid;
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task RunValidation_FailingRule_ReturnsError()
    {
        var binding = CreateStringBinding("abc");
        var input = new PasswordInput(binding)
            .Validate(v => v.Length >= 8 ? ValidationResult.Ok : ValidationResult.Error("Too short"));

        var result = input.RunValidation();
        bool isValid = result.IsValid;
        string? message = result.ErrorMessage;
        await Assert.That(isValid).IsFalse();
        await Assert.That(message).IsEqualTo("Too short");
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var input = new PasswordInput(CreateStringBinding());
        var chained = input
            .ShowToggle(true)
            .StrengthIndicator(true)
            .MaxLength(64)
            .Disabled()
            .ReadOnly();

        bool same = ReferenceEquals(input, chained);
        await Assert.That(same).IsTrue();
    }
}

// ═══════════════════════════════════════════════════════════════════════
// PasswordStrengthEvaluator Tests (entropy-based)
// ═══════════════════════════════════════════════════════════════════════

public sealed class PasswordStrengthEvaluatorTests
{
    [Test]
    public async Task EmptyString_ReturnsWeak()
    {
        var result = PasswordStrengthEvaluator.Evaluate(string.Empty);
        await Assert.That(result).IsEqualTo(PasswordStrength.Weak);
    }

    [Test]
    public async Task NullString_ReturnsWeak()
    {
        var result = PasswordStrengthEvaluator.Evaluate(null!);
        await Assert.That(result).IsEqualTo(PasswordStrength.Weak);
    }

    [Test]
    public async Task ShortPassword_ReturnsWeak()
    {
        var result = PasswordStrengthEvaluator.Evaluate("abc");
        await Assert.That(result).IsEqualTo(PasswordStrength.Weak);
    }

    [Test]
    public async Task CommonPassword_ReturnsWeak()
    {
        // "password" is in the common list — 95% entropy penalty crushes it
        var result = PasswordStrengthEvaluator.Evaluate("password");
        await Assert.That(result).IsEqualTo(PasswordStrength.Weak);
    }

    [Test]
    public async Task CommonPasswordVariant_StillPenalized()
    {
        // "Password1!" contains "password" — gets 25% substring penalty
        int variantScore = PasswordStrengthEvaluator.CalculateScore("Password1!");
        // Same length, same charset, no common password inside
        int cleanScore = PasswordStrengthEvaluator.CalculateScore("Br1ckL4y3r!");
        await Assert.That(cleanScore).IsGreaterThan(variantScore);
    }

    [Test]
    public async Task LongLowercasePassphrase_BeatsShortComplex()
    {
        // 25 lowercase = 25 × log₂(26) ≈ 117 bits raw entropy (strong!)
        int longSimple = PasswordStrengthEvaluator.CalculateScore("correcthorsebatterystaple");
        // 8 mixed chars = 8 × log₂(95) ≈ 52 bits (moderate)
        int shortComplex = PasswordStrengthEvaluator.CalculateScore("P@ss1w0d");
        await Assert.That(longSimple).IsGreaterThan(shortComplex);
    }

    [Test]
    public async Task LongPassphrase_IsStrong()
    {
        // Long lowercase passphrase should be rated strong
        var result = PasswordStrengthEvaluator.Evaluate("correcthorsebatterystaple");
        await Assert.That(result).IsEqualTo(PasswordStrength.Strong);
    }

    [Test]
    public async Task RepeatingChars_ReduceScore()
    {
        int repeated = PasswordStrengthEvaluator.CalculateScore("aaaaaaaaaa");
        int diverse = PasswordStrengthEvaluator.CalculateScore("abcxyzqrst");
        await Assert.That(diverse).IsGreaterThan(repeated);
    }

    [Test]
    public async Task SequentialRun_ReducesScore()
    {
        int sequential = PasswordStrengthEvaluator.CalculateScore("abcdefghij");
        int nonSequential = PasswordStrengthEvaluator.CalculateScore("qfztnbvmkx");
        await Assert.That(nonSequential).IsGreaterThan(sequential);
    }

    [Test]
    public async Task KeyboardWalk_ReducesScore()
    {
        int walkPassword = PasswordStrengthEvaluator.CalculateScore("qwertyasdf");
        int randomPassword = PasswordStrengthEvaluator.CalculateScore("xpfmgktwrn");
        await Assert.That(randomPassword).IsGreaterThan(walkPassword);
    }

    [Test]
    public async Task ScoreRange_ZeroToHundred()
    {
        int empty = PasswordStrengthEvaluator.CalculateScore("");
        int strong = PasswordStrengthEvaluator.CalculateScore("Xk9$mP2!wQ7@nL4#bR6&");
        await Assert.That(empty).IsEqualTo(0);
        await Assert.That(strong).IsGreaterThanOrEqualTo(75);
        await Assert.That(strong).IsLessThanOrEqualTo(100);
    }

    [Test]
    public async Task CalculateScore_MatchesEvaluateTier()
    {
        string pwd = "MyDog8Treats!";
        int score = PasswordStrengthEvaluator.CalculateScore(pwd);
        var tier = PasswordStrengthEvaluator.Evaluate(pwd);

        PasswordStrength expected = score switch
        {
            <= 25 => PasswordStrength.Weak,
            <= 50 => PasswordStrength.Fair,
            <= 75 => PasswordStrength.Good,
            _ => PasswordStrength.Strong,
        };
        await Assert.That(tier).IsEqualTo(expected);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// NotificationBell Tests
// ═══════════════════════════════════════════════════════════════════════

public sealed class NotificationBellTests
{
    private static Bindable<IReadOnlyList<AppNotification>> CreateNotificationBinding(
        IReadOnlyList<AppNotification>? initial = null)
    {
        IReadOnlyList<AppNotification> captured = initial ?? [];
        return new Bindable<IReadOnlyList<AppNotification>>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_StoresNotificationsBinding()
    {
        var notifications = new List<AppNotification>
        {
            new() { Id = "1", Title = "Test" },
        };
        var binding = CreateNotificationBinding(notifications);
        var bell = new NotificationBell(binding);

        int count = bell.Notifications.Value.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Constructor_StoresCallbacks()
    {
        bool readCalled = false;
        bool readAllCalled = false;
        bool clearCalled = false;

        var bell = new NotificationBell(
            CreateNotificationBinding(),
            onRead: _ => { readCalled = true; },
            onReadAll: () => { readAllCalled = true; },
            onClear: _ => { clearCalled = true; });

        bell.OnRead!(new AppNotification { Id = "1", Title = "T" });
        bell.OnReadAll!();
        bell.OnClear!(new AppNotification { Id = "1", Title = "T" });

        await Assert.That(readCalled).IsTrue();
        await Assert.That(readAllCalled).IsTrue();
        await Assert.That(clearCalled).IsTrue();
    }

    [Test]
    public async Task RenderNotification_SetsCustomRenderer()
    {
        var bell = new NotificationBell(CreateNotificationBinding())
            .RenderNotification(_ => Node.Empty);

        bool hasRenderer = bell.CustomRenderer is not null;
        await Assert.That(hasRenderer).IsTrue();
    }

    [Test]
    public async Task RingAnimation_DefaultIsTrue()
    {
        var bell = new NotificationBell(CreateNotificationBinding());

        bool enabled = bell.EnableRingAnimation;
        await Assert.That(enabled).IsTrue();
    }

    [Test]
    public async Task RingAnimation_SetToFalse()
    {
        var bell = new NotificationBell(CreateNotificationBinding()).RingAnimation(false);

        bool enabled = bell.EnableRingAnimation;
        await Assert.That(enabled).IsFalse();
    }

    [Test]
    public async Task MaxVisible_DefaultIs50()
    {
        var bell = new NotificationBell(CreateNotificationBinding());

        int maxVisible = bell.MaxVisibleCount;
        await Assert.That(maxVisible).IsEqualTo(50);
    }

    [Test]
    public async Task MaxVisible_SetsValue()
    {
        var bell = new NotificationBell(CreateNotificationBinding()).MaxVisible(25);

        int maxVisible = bell.MaxVisibleCount;
        await Assert.That(maxVisible).IsEqualTo(25);
    }

    [Test]
    public async Task EmptyState_SetsNode()
    {
        var bell = new NotificationBell(CreateNotificationBinding()).EmptyState(Node.Empty);

        bool isEmpty = bell.EmptyStateNode.IsLayoutEmpty;
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Disabled_SetsFlag()
    {
        var bell = new NotificationBell(CreateNotificationBinding()).Disabled();

        bool disabled = bell.IsDisabled;
        await Assert.That(disabled).IsTrue();
    }

    [Test]
    public async Task AccessibleLabel_SetsValue()
    {
        var bell = new NotificationBell(CreateNotificationBinding())
            .AccessibleLabel("Notifications");

        string label = bell.AccessibleLabelValue.Value;
        await Assert.That(label).IsEqualTo("Notifications");
    }

    [Test]
    public async Task AppNotification_HasCorrectDefaults()
    {
        var before = DateTimeOffset.UtcNow;
        var notification = new AppNotification { Id = "1", Title = "Test" };
        var after = DateTimeOffset.UtcNow;

        bool isRead = notification.IsRead;
        bool timestampInRange = notification.Timestamp >= before && notification.Timestamp <= after;
        string? body = notification.Body;

        await Assert.That(isRead).IsFalse();
        await Assert.That(timestampInRange).IsTrue();
        await Assert.That(body).IsNull();
    }

    [Test]
    public async Task FluentChaining_ReturnsSameInstance()
    {
        var bell = new NotificationBell(CreateNotificationBinding());
        var chained = bell
            .RingAnimation(true)
            .MaxVisible(10)
            .Disabled()
            .AccessibleLabel("Bell");

        bool same = ReferenceEquals(bell, chained);
        await Assert.That(same).IsTrue();
    }
}
