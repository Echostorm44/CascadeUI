namespace Cascade.UI.Testing;

/// <summary>
/// Links a test to a specific section of a specification document.
/// Used for traceability — ensures every spec requirement has a corresponding test.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class SpecRefAttribute : Attribute
{
    /// <summary>Creates a spec reference linking to a document and section.</summary>
    public SpecRefAttribute(string document, string section)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);
        Document = document;
        Section = section;
    }

    /// <summary>The specification document name (e.g. "architecture.md").</summary>
    public string Document { get; }

    /// <summary>The section within the document (e.g. "Reactivity").</summary>
    public string Section { get; }
}
