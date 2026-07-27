namespace Cascade.UI;

/// <summary>
/// A file type filter for use with <see cref="FilePicker"/> dialogs.
/// Each filter has a display label and one or more wildcard patterns.
/// </summary>
/// <param name="Label">
/// The display name shown in the filter dropdown (e.g. "Images", "All Files").
/// </param>
/// <param name="Patterns">
/// One or more wildcard patterns (e.g. "*.png", "*.jpg", "*.*").
/// </param>
public sealed record FileFilter(string Label, params string[] Patterns);
