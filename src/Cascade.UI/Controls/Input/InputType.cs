namespace Cascade.UI;

/// <summary>
/// Specifies the semantic type of a text input, which affects keyboard
/// layout on mobile, browser autofill hints, and built-in behavior
/// such as masking for passwords or a clear button for search.
/// </summary>
public enum InputType
{
    /// <summary>Default single-line text entry.</summary>
    Text,

    /// <summary>Password entry with masked display and show/hide toggle.</summary>
    Password,

    /// <summary>Email entry with email keyboard on mobile and autofill hint.</summary>
    Email,

    /// <summary>Numeric-only entry. Prefer <see cref="NumberInput{T}"/> for full numeric UX.</summary>
    Number,

    /// <summary>Telephone entry with phone keyboard on mobile.</summary>
    Phone,

    /// <summary>URL entry with URL keyboard on mobile and autofill hint.</summary>
    Url,

    /// <summary>Search entry with clear button and search keyboard on mobile.</summary>
    Search
}
