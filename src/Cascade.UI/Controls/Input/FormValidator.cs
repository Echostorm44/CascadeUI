namespace Cascade.UI;

/// <summary>
/// A scope node that collects the validity state of all input controls
/// within it. Manages form-level validation, submit button enabling,
/// and explicit submission with full validation.
/// </summary>
public sealed class FormValidator : Node
{
    /// <summary>
    /// Creates a form validator scope with an inline form scope reference.
    /// Use this overload when the submit button lives inside the FormValidator block.
    /// </summary>
    /// <param name="form">Output parameter receiving the form scope for querying validity.</param>
    /// <param name="content">The content node tree containing input controls.</param>
    public FormValidator(out FormScope form, Node content)
    {
        form = new FormScope();
        Scope = form;
        Content = content;
    }

    /// <summary>
    /// Creates a form validator scope with an externally-declared form scope.
    /// Use this overload when <c>form.IsValid</c> or <c>form.ValidateAll()</c>
    /// are needed from a handler or parent component outside <c>Render()</c>.
    /// </summary>
    /// <param name="form">An externally-declared form scope field.</param>
    /// <param name="content">The content node tree containing input controls.</param>
    public FormValidator(FormScope form, Node content)
    {
        Scope = form;
        Content = content;
    }

    /// <summary>The form scope managing validation state.</summary>
    public FormScope Scope { get; }

    /// <summary>The content node tree containing input controls.</summary>
    public Node Content { get; }
}

/// <summary>
/// A stable handle to a <see cref="FormValidator"/>'s validation state.
/// Declared as a field (same pattern as <see cref="NodeRef{T}"/>) when
/// event handlers outside <c>Render()</c> need access to form validity.
/// </summary>
public sealed class FormScope
{
    private readonly List<FormFieldEntry> fields = [];

    /// <summary>
    /// Whether all input controls within the scope currently pass validation.
    /// This is a reactive value — UI bound to it updates automatically.
    /// </summary>
    public bool IsValid
    {
        get
        {
            foreach (var entry in fields)
            {
                if (entry.LastResult is { IsValid: false })
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Triggers validation on all fields immediately, regardless of their
    /// <see cref="ValidationTrigger"/> setting. Returns <c>false</c> if any
    /// field fails validation.
    /// </summary>
    public bool ValidateAll()
    {
        bool allValid = true;

        foreach (var field in fields)
        {
            var result = field.Validate();
            field.LastResult = result;
            if (!result.IsValid)
            {
                allValid = false;
            }
        }

        return allValid;
    }

    /// <summary>
    /// Resets all validation state, clearing error and warning messages
    /// from all fields within the scope.
    /// </summary>
    public void Reset()
    {
        foreach (var field in fields)
        {
            field.LastResult = null;
        }
    }

    /// <summary>
    /// Registers a field's validation function with this scope.
    /// Called by the framework when input controls are mounted inside a FormValidator.
    /// </summary>
    internal void RegisterField(Func<ValidationResult> validate)
    {
        fields.Add(new FormFieldEntry(validate));
    }

    /// <summary>
    /// Removes all registered fields. Called when the form content is unmounted.
    /// </summary>
    internal void ClearFields()
    {
        fields.Clear();
    }

    /// <summary>
    /// Returns the number of registered fields.
    /// </summary>
    internal int FieldCount => fields.Count;

    /// <summary>
    /// Returns the number of fields that currently have validation errors.
    /// </summary>
    internal int ErrorCount
    {
        get
        {
            int count = 0;
            foreach (var entry in fields)
            {
                if (entry.LastResult is { IsValid: false })
                {
                    count++;
                }
            }

            return count;
        }
    }
}

/// <summary>
/// Tracks a single field's validation function and last result within a <see cref="FormScope"/>.
/// </summary>
internal sealed class FormFieldEntry
{
    internal FormFieldEntry(Func<ValidationResult> validate)
    {
        Validate = validate;
    }

    internal Func<ValidationResult> Validate { get; }
    internal ValidationResult? LastResult { get; set; }
}
