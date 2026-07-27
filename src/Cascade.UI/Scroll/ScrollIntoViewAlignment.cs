namespace Cascade.UI;

/// <summary>
/// Controls how an element is positioned within the viewport when scrolled into view.
/// </summary>
public enum ScrollIntoViewAlignment
{
    /// <summary>Minimum scroll to make the element fully visible.</summary>
    Nearest,

    /// <summary>Align element's start with viewport start.</summary>
    Start,

    /// <summary>Center element in viewport.</summary>
    Center,

    /// <summary>Align element's end with viewport end.</summary>
    End
}
