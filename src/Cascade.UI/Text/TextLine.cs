namespace Cascade.UI;

/// <summary>
/// Information about a single line within a <see cref="TextDocument"/>.
/// </summary>
public readonly record struct TextLine(
    /// <summary>Offset into the document where the line starts.</summary>
    int Start,

    /// <summary>Character count excluding the line terminator.</summary>
    int Length,

    /// <summary>Character count including \n or \r\n.</summary>
    int LengthWithTerminator,

    /// <summary>Zero-based line number.</summary>
    int LineIndex
);
