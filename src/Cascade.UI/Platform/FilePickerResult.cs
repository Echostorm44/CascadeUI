namespace Cascade.UI;

/// <summary>
/// The result of a file picker dialog. Contains the selected file's path
/// and metadata. Null is returned from picker methods when the user cancels.
/// </summary>
public sealed record FilePickerResult
{
    /// <summary>The absolute file path.</summary>
    public required string Path { get; init; }

    /// <summary>The file name with extension.</summary>
    public string FileName
    {
        get { return System.IO.Path.GetFileName(Path); }
    }

    /// <summary>The file size in bytes.</summary>
    public long Size { get; init; }
}
