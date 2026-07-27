using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// Unit tests for file and folder picker result types, filter construction,
/// and picker method signatures. Does not invoke the native dialogs.
/// </summary>
public class Win32DialogTests
{
    // ── FilePickerResult ─────────────────────────────────────────────

    [Test]
    public async Task FilePickerResult_FileName_ExtractsFromPath()
    {
        var result = new FilePickerResult { Path = @"C:\Users\test\documents\report.pdf", Size = 1024 };
        await Assert.That(result.FileName).IsEqualTo("report.pdf");
    }

    [Test]
    public async Task FilePickerResult_FileName_HandlesRootPath()
    {
        var result = new FilePickerResult { Path = @"C:\file.txt", Size = 0 };
        await Assert.That(result.FileName).IsEqualTo("file.txt");
    }

    [Test]
    public async Task FilePickerResult_FileName_HandlesNestedDirectories()
    {
        var result = new FilePickerResult { Path = @"C:\a\b\c\d\deep.cs", Size = 500 };
        await Assert.That(result.FileName).IsEqualTo("deep.cs");
    }

    [Test]
    public async Task FilePickerResult_Path_IsStored()
    {
        var result = new FilePickerResult { Path = @"C:\test.txt", Size = 42 };
        await Assert.That(result.Path).IsEqualTo(@"C:\test.txt");
    }

    [Test]
    public async Task FilePickerResult_Size_IsStored()
    {
        var result = new FilePickerResult { Path = @"C:\test.txt", Size = 98765 };
        await Assert.That(result.Size).IsEqualTo(98765L);
    }

    [Test]
    public async Task FilePickerResult_IsRecord_SupportsEquality()
    {
        var a = new FilePickerResult { Path = @"C:\file.txt", Size = 10 };
        var b = new FilePickerResult { Path = @"C:\file.txt", Size = 10 };
        await Assert.That(a).IsEqualTo(b);
    }

    // ── FileFilter ───────────────────────────────────────────────────

    [Test]
    public async Task FileFilter_Label_IsStored()
    {
        var filter = new FileFilter("Images", "*.png", "*.jpg");
        await Assert.That(filter.Label).IsEqualTo("Images");
    }

    [Test]
    public async Task FileFilter_Patterns_AreStored()
    {
        var filter = new FileFilter("Documents", "*.pdf", "*.docx");
        await Assert.That(filter.Patterns).HasCount().EqualTo(2);
        await Assert.That(filter.Patterns[0]).IsEqualTo("*.pdf");
        await Assert.That(filter.Patterns[1]).IsEqualTo("*.docx");
    }

    [Test]
    public async Task FileFilter_SinglePattern_IsStored()
    {
        var filter = new FileFilter("Text Files", "*.txt");
        await Assert.That(filter.Patterns).HasCount().EqualTo(1);
        await Assert.That(filter.Patterns[0]).IsEqualTo("*.txt");
    }

    [Test]
    public async Task FileFilter_AllFiles_UsesWildcard()
    {
        var filter = new FileFilter("All Files", "*.*");
        await Assert.That(filter.Label).IsEqualTo("All Files");
        await Assert.That(filter.Patterns[0]).IsEqualTo("*.*");
    }

    // ── FilePicker method signatures ─────────────────────────────────

    [Test]
    public async Task FilePicker_OpenAsync_ExistsWithCorrectSignature()
    {
        // Verify the method exists and throws PlatformNotSupportedException on non-Windows
        // or returns a valid Task. Since we are in a test environment without a window,
        // we verify the method is callable.
        var method = typeof(FilePicker).GetMethod(
            nameof(FilePicker.OpenAsync),
            [typeof(string), typeof(IReadOnlyList<FileFilter>), typeof(string)]);
        await Assert.That(method).IsNotNull();
    }

    [Test]
    public async Task FilePicker_OpenMultipleAsync_ExistsWithCorrectSignature()
    {
        var method = typeof(FilePicker).GetMethod(
            nameof(FilePicker.OpenMultipleAsync),
            [typeof(string), typeof(IReadOnlyList<FileFilter>), typeof(string)]);
        await Assert.That(method).IsNotNull();
    }

    [Test]
    public async Task FilePicker_SaveAsync_ExistsWithCorrectSignature()
    {
        var method = typeof(FilePicker).GetMethod(
            nameof(FilePicker.SaveAsync),
            [typeof(string), typeof(string), typeof(IReadOnlyList<FileFilter>), typeof(string)]);
        await Assert.That(method).IsNotNull();
    }

    [Test]
    public async Task FolderPicker_OpenAsync_ExistsWithCorrectSignature()
    {
        var method = typeof(FolderPicker).GetMethod(
            nameof(FolderPicker.OpenAsync),
            [typeof(string), typeof(string)]);
        await Assert.That(method).IsNotNull();
    }

    // ── Return types ─────────────────────────────────────────────────

    [Test]
    public async Task FilePicker_OpenAsync_ReturnType_IsTaskOfNullableFilePickerResult()
    {
        var method = typeof(FilePicker).GetMethod(
            nameof(FilePicker.OpenAsync),
            [typeof(string), typeof(IReadOnlyList<FileFilter>), typeof(string)]);
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.Task<FilePickerResult?>));
    }

    [Test]
    public async Task FilePicker_OpenMultipleAsync_ReturnType_IsTaskOfList()
    {
        var method = typeof(FilePicker).GetMethod(
            nameof(FilePicker.OpenMultipleAsync),
            [typeof(string), typeof(IReadOnlyList<FileFilter>), typeof(string)]);
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.Task<IReadOnlyList<FilePickerResult>>));
    }

    [Test]
    public async Task FolderPicker_OpenAsync_ReturnType_IsTaskOfNullableString()
    {
        var method = typeof(FolderPicker).GetMethod(
            nameof(FolderPicker.OpenAsync),
            [typeof(string), typeof(string)]);
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(System.Threading.Tasks.Task<string?>));
    }
}
