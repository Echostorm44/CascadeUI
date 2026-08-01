namespace Cascade.UI;

/// <summary>
/// Determines the evaluated strength of a password.
/// </summary>
public enum PasswordStrength
{
    /// <summary>Password is very weak — low entropy, common, or trivially crackable.</summary>
    Weak,

    /// <summary>Password has moderate entropy but could still be improved.</summary>
    Fair,

    /// <summary>Password has strong entropy with good length and diversity.</summary>
    Good,

    /// <summary>Password has very high entropy — long, diverse, and unpredictable.</summary>
    Strong,
}

/// <summary>
/// Evaluates password strength using Shannon entropy estimation with penalties
/// for common passwords, repetition, sequential runs, and keyboard walks.
/// <para>
/// Philosophy: a 25-character lowercase passphrase is far more secure than "Password1!"
/// because entropy scales with length × log₂(charset). We penalize predictable patterns
/// rather than demanding arbitrary character-class checkboxes.
/// </para>
/// </summary>
public static class PasswordStrengthEvaluator
{
    private const double WeakBits = 28;
    private const double ModerateBits = 45;
    private const double StrongBits = 70;
    private const double CeilingBits = 130;

    /// <summary>
    /// Returns a score from 0–100 based on effective entropy (raw entropy minus penalties).
    /// <list type="table">
    ///   <item><term>0–25</term><description>Weak (red)</description></item>
    ///   <item><term>26–50</term><description>Fair (orange/yellow)</description></item>
    ///   <item><term>51–75</term><description>Good (light green)</description></item>
    ///   <item><term>76–100</term><description>Very strong (green)</description></item>
    /// </list>
    /// </summary>
    public static int CalculateScore(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }

        double rawEntropy = CalculateRawEntropy(password);
        double penaltyFactor = CalculatePenaltyFactor(password);
        double effectiveEntropy = rawEntropy * (1.0 - penaltyFactor);

        return EntropyToScore(effectiveEntropy);
    }

    /// <summary>
    /// Evaluates the strength of the given password string as a tier classification.
    /// Uses <see cref="CalculateScore"/> internally.
    /// </summary>
    public static PasswordStrength Evaluate(string password)
    {
        int score = CalculateScore(password);
        return score switch
        {
            <= 25 => PasswordStrength.Weak,
            <= 50 => PasswordStrength.Fair,
            <= 75 => PasswordStrength.Good,
            _ => PasswordStrength.Strong,
        };
    }

    private static int EntropyToScore(double entropy)
    {
        if (entropy < WeakBits)
        {
            return (int)Math.Round(entropy / WeakBits * 25);
        }

        if (entropy < ModerateBits)
        {
            return 25 + (int)Math.Round((entropy - WeakBits) / (ModerateBits - WeakBits) * 25);
        }

        if (entropy < StrongBits)
        {
            return 50 + (int)Math.Round((entropy - ModerateBits) / (StrongBits - ModerateBits) * 25);
        }

        double aboveStrong = Math.Min(entropy - StrongBits, CeilingBits - StrongBits);
        return 75 + (int)Math.Round(aboveStrong / (CeilingBits - StrongBits) * 25);
    }

    private static double CalculateRawEntropy(string password)
    {
        int pool = EstimateCharsetSize(password);
        return password.Length * Math.Log(pool, 2);
    }

    /// <summary>
    /// Returns a penalty fraction in [0.0, 0.95] subtracted from raw entropy.
    /// Proportional scaling means it works correctly at any password length.
    /// </summary>
    private static double CalculatePenaltyFactor(string password)
    {
        if (IsExactCommonPassword(password))
        {
            return 0.95;
        }

        double penalty = 0.0;

        if (ContainsCommonPassword(password))
        {
            penalty += 0.25;
        }

        double uniqueRatio = (double)password.Distinct().Count() / password.Length;
        if (uniqueRatio < 0.4)
        {
            penalty += 0.5;
        }
        else if (uniqueRatio < 0.6)
        {
            penalty += 0.2;
        }

        if (LongestSequentialRun(password) >= 4)
        {
            penalty += 0.3;
        }

        string lower = password.ToUpperInvariant();
        if (lower.Contains("QWERTY", StringComparison.Ordinal)
            || lower.Contains("ASDFGH", StringComparison.Ordinal)
            || lower.Contains("ZXCVBN", StringComparison.Ordinal))
        {
            penalty += 0.2;
        }

        return Math.Min(penalty, 0.95);
    }

    private static int EstimateCharsetSize(string password)
    {
        bool hasLower = false, hasUpper = false, hasDigit = false, hasSymbol = false;

        foreach (char c in password)
        {
            if (char.IsLower(c))
            {
                hasLower = true;
            }
            else if (char.IsUpper(c))
            {
                hasUpper = true;
            }
            else if (char.IsDigit(c))
            {
                hasDigit = true;
            }
            else
            {
                hasSymbol = true;
            }
        }

        int size = 0;
        if (hasLower) { size += 26; }
        if (hasUpper) { size += 26; }
        if (hasDigit) { size += 10; }
        if (hasSymbol) { size += 33; }

        return size == 0 ? 1 : size;
    }

    /// <summary>
    /// Finds the longest run of consecutive ascending or descending characters.
    /// </summary>
    private static int LongestSequentialRun(string password)
    {
        int maxRun = 1;
        int runAsc = 1;
        int runDesc = 1;

        for (int i = 1; i < password.Length; i++)
        {
            int diff = password[i] - password[i - 1];

            runAsc = diff == 1 ? runAsc + 1 : 1;
            runDesc = diff == -1 ? runDesc + 1 : 1;

            maxRun = Math.Max(maxRun, Math.Max(runAsc, runDesc));
        }

        return maxRun;
    }

    private static bool IsExactCommonPassword(string password)
    {
        return CommonPasswords.Contains(password);
    }

    private static bool ContainsCommonPassword(string password)
    {
        return CommonPasswords.Any(cp => password.Contains(cp, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly System.Collections.Generic.HashSet<string> CommonPasswords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "password1", "password123",
            "admin", "administrator",
            "welcome", "welcome1",
            "letmein", "login",
            "123456", "1234567", "12345678", "123456789",
            "qwerty", "qwerty123",
            "abc123", "iloveyou",
            "monkey", "dragon", "master", "sunshine",
        };
}

/// <summary>
/// Specialized password input control with visibility toggle and strength indicator.
/// Built on top of <see cref="InputType.Password"/> semantics with dedicated
/// password-specific features like strength evaluation and reveal toggle.
/// </summary>
public sealed class PasswordInput : Node
{
    /// <summary>
    /// Creates a password input bound to a string field via two-way binding.
    /// </summary>
    /// <param name="value">Two-way binding to the password value.</param>
    /// <param name="placeholder">Placeholder text shown when the input is empty.</param>
    public PasswordInput(Bindable<string> value, LocKey placeholder = default)
    {
        Value = value;
        Placeholder = placeholder;
    }

    /// <summary>Two-way binding to the password value.</summary>
    public Bindable<string> Value { get; }

    /// <summary>Placeholder text shown when the input is empty.</summary>
    public LocKey Placeholder { get; }

    // ── Internal modifier state set by extension methods ──────────────

    internal bool ShowToggleButton { get; set; } = true;
    internal bool UseStrengthIndicator { get; set; }
    internal Func<string, PasswordStrength>? CustomStrengthEvaluator { get; set; }
    internal int? MaxLengthValue { get; set; }
    internal List<Func<string, ValidationResult>> ValidationRules { get; } = [];
    internal ValidationTrigger ValidationTriggerMode { get; set; } = ValidationTrigger.Blur;
    internal bool IsDisabled { get; set; }
    internal bool IsReadOnly { get; set; }

    /// <summary>Absolute viewport bounds, set by the painter each frame for input hit-testing.</summary>
    internal Rect AbsoluteBounds { get; set; }

    /// <summary>
    /// Runs all registered validation rules against the current value.
    /// Returns the first failing result or <see cref="ValidationResult.Ok"/>.
    /// </summary>
    internal ValidationResult RunValidation()
    {
        string currentValue = Value.Value ?? string.Empty;
        foreach (var rule in ValidationRules)
        {
            var result = rule(currentValue);
            if (!result.IsValid)
            {
                return result;
            }
        }

        return ValidationResult.Ok;
    }
}

/// <summary>
/// Extension methods for <see cref="PasswordInput"/> providing fluent modifiers.
/// </summary>
public static class PasswordInputExtensions
{
    /// <summary>Shows or hides the password visibility toggle button.</summary>
    public static PasswordInput ShowToggle(this PasswordInput input, bool show = true)
    {
        input.ShowToggleButton = show;
        return input;
    }

    /// <summary>Enables or disables the password strength indicator bar.</summary>
    public static PasswordInput StrengthIndicator(this PasswordInput input, bool enabled = true)
    {
        input.UseStrengthIndicator = enabled;
        return input;
    }

    /// <summary>Sets a custom password strength evaluator function.</summary>
    public static PasswordInput StrengthEvaluator(this PasswordInput input, Func<string, PasswordStrength> evaluator)
    {
        input.CustomStrengthEvaluator = evaluator;
        return input;
    }

    /// <summary>Sets the maximum character length for the password.</summary>
    public static PasswordInput MaxLength(this PasswordInput input, int maxLength)
    {
        input.MaxLengthValue = maxLength;
        return input;
    }

    /// <summary>Adds a validation rule to the input.</summary>
    public static PasswordInput Validate(this PasswordInput input, Func<string, ValidationResult> rule)
    {
        input.ValidationRules.Add(rule);
        return input;
    }

    /// <summary>Sets when validation fires.</summary>
    public static PasswordInput ValidateOn(this PasswordInput input, ValidationTrigger trigger)
    {
        input.ValidationTriggerMode = trigger;
        return input;
    }

    /// <summary>Disables the input control.</summary>
    public static PasswordInput Disabled(this PasswordInput input, bool disabled = true)
    {
        input.IsDisabled = disabled;
        return input;
    }

    /// <summary>Makes the input read-only (visible but not editable).</summary>
    public static PasswordInput ReadOnly(this PasswordInput input, bool readOnly = true)
    {
        input.IsReadOnly = readOnly;
        return input;
    }

    /// <summary>Sets the accessible label for screen readers.</summary>
    public static PasswordInput AccessibleLabel(this PasswordInput input, LocKey label)
    {
        input.LayoutData.A11yLabel = label.Resolve();
        return input;
    }
}
