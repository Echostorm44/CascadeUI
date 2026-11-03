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
    // The controls with rules inside the FormValidator, refreshed each layout (re-renders replace them).
    private IReadOnlyList<IValidatable> fields = [];

    /// <summary>
    /// Whether every field in the form passes its rules right now. It checks the current values
    /// without showing any message, so a Submit button can be enabled from it.
    /// </summary>
    public bool IsValid
    {
        get
        {
            foreach (var entry in fields)
            {
                if (!entry.RunValidation().IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Validates every field now, whatever its <see cref="ValidationTrigger"/>, and shows each
    /// field's message. Returns <c>false</c> if any field fails.
    /// </summary>
    public bool ValidateAll()
    {
        bool allValid = true;
        foreach (var entry in fields)
        {
            if (!Validation.Run(entry).IsValid)
            {
                allValid = false;
            }
        }

        return allValid;
    }

    /// <summary>Clears every field's message (after a form is submitted or cleared, say).</summary>
    public void Reset()
    {
        foreach (var entry in fields)
        {
            if (entry.ValidationState is { } state)
            {
                state.Result = null;
                state.Dirty = false;
            }
        }
        SharedScheduler.Instance.RequestFrame();
    }

    /// <summary>The form's fields this frame. Called by layout.</summary>
    internal void SetFields(IReadOnlyList<IValidatable> current) => fields = current;

    /// <summary>The number of fields in the form.</summary>
    internal int FieldCount => fields.Count;

    /// <summary>The number of fields showing an error.</summary>
    internal int ErrorCount
    {
        get
        {
            int count = 0;
            foreach (var entry in fields)
            {
                if (entry.ValidationState?.Result is { IsValid: false })
                {
                    count++;
                }
            }

            return count;
        }
    }
}
