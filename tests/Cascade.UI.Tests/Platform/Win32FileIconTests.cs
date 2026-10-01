#pragma warning disable CA2000 // ImageSource implements IDisposable; test instances are short-lived

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// <see cref="ImageSource.FromFileIcon"/> against real shell icons: an executable's embedded icon and
/// a document's associated icon, at the requested size, with real transparency.
/// </summary>
public class Win32FileIconTests
{
    [Test]
    [Arguments(16)]
    [Arguments(32)]
    [Arguments(48)]
    public async Task ExecutableIcon_HasRequestedSize_AndTransparentCorners(int size)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string exe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        using ImageSource? icon = ImageSource.FromFileIcon(exe, size);

        await Assert.That(icon).IsNotNull();
        await Assert.That(icon!.Width).IsEqualTo(size);
        await Assert.That(icon.Height).IsEqualTo(size);
        byte[] rgba = icon.Pixels.ToArray();
        int opaque = 0;
        for (int i = 3; i < rgba.Length; i += 4)
        {
            if (rgba[i] == 255)
            {
                opaque++;
            }
        }
        await Assert.That(rgba[3]).IsEqualTo((byte)0); // top-left corner of the folder glyph is clear
        await Assert.That(opaque).IsGreaterThan(size * size / 4);
    }

    [Test]
    public async Task DocumentIcon_ComesFromItsAssociation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cascade-icon-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(file, "x");
        try
        {
            using ImageSource? icon = ImageSource.FromFileIcon(file, 32);

            await Assert.That(icon).IsNotNull();
            await Assert.That(icon!.Width).IsEqualTo(32);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public async Task MissingPath_ReturnsNull()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        await Assert.That(ImageSource.FromFileIcon(@"Z:\definitely\not\here.exe", 32)).IsNull();
    }
}
