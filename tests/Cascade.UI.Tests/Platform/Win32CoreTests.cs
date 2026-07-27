using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Unit tests for the Win32 public API wiring: AppArgs, Dispatcher pre-init,
/// AppWindow pre-init, and FilePickerResult.FileName.
/// </summary>
public class Win32CoreTests
{
    // ── AppArgs.Has ──────────────────────────────────────────────────

    [Test]
    public async Task AppArgs_Has_ReturnsTrueForPresentFlag()
    {
        var args = new AppArgs();
        args.SetRaw(["--debug", "--verbose"]);

        await Assert.That(args.Has("--debug")).IsTrue();
    }

    [Test]
    public async Task AppArgs_Has_ReturnsTrueForSecondFlag()
    {
        var args = new AppArgs();
        args.SetRaw(["--debug", "--verbose"]);

        await Assert.That(args.Has("--verbose")).IsTrue();
    }

    [Test]
    public async Task AppArgs_Has_ReturnsFalseForAbsentFlag()
    {
        var args = new AppArgs();
        args.SetRaw(["--debug", "--verbose"]);

        await Assert.That(args.Has("--release")).IsFalse();
    }

    [Test]
    public async Task AppArgs_Has_ReturnsFalseWhenEmpty()
    {
        var args = new AppArgs();
        args.SetRaw([]);

        await Assert.That(args.Has("--debug")).IsFalse();
    }

    [Test]
    public async Task AppArgs_Has_IsCaseSensitive()
    {
        var args = new AppArgs();
        args.SetRaw(["--Debug"]);

        await Assert.That(args.Has("--debug")).IsFalse();
    }

    // ── AppArgs.Get ──────────────────────────────────────────────────

    [Test]
    public async Task AppArgs_Get_ReturnsValueAfterFlag()
    {
        var args = new AppArgs();
        args.SetRaw(["--port", "8080"]);

        await Assert.That(args.Get("--port")).IsEqualTo("8080");
    }

    [Test]
    public async Task AppArgs_Get_ReturnsNullWhenFlagNotFound()
    {
        var args = new AppArgs();
        args.SetRaw(["--port", "8080"]);

        await Assert.That(args.Get("--host")).IsNull();
    }

    [Test]
    public async Task AppArgs_Get_ReturnsNullWhenFlagIsLast()
    {
        var args = new AppArgs();
        args.SetRaw(["--port"]);

        await Assert.That(args.Get("--port")).IsNull();
    }

    [Test]
    public async Task AppArgs_Get_ReturnsNullWhenEmpty()
    {
        var args = new AppArgs();
        args.SetRaw([]);

        await Assert.That(args.Get("--port")).IsNull();
    }

    [Test]
    public async Task AppArgs_Get_ReturnsCorrectValueAmongMultipleFlags()
    {
        var args = new AppArgs();
        args.SetRaw(["--host", "localhost", "--port", "9000"]);

        await Assert.That(args.Get("--port")).IsEqualTo("9000");
        await Assert.That(args.Get("--host")).IsEqualTo("localhost");
    }

    // ── Dispatcher pre-init ──────────────────────────────────────────

    [Test]
    public async Task Dispatcher_IsOnUiThread_ReturnsFalseWhenNotInitialized()
    {
        // Save and clear the message loop so we test the uninitialized state.
        var saved = Dispatcher.messageLoop;
        Dispatcher.messageLoop = null;

        try
        {
            await Assert.That(Dispatcher.IsOnUiThread).IsFalse();
        }
        finally
        {
            Dispatcher.messageLoop = saved;
        }
    }

    [Test]
    public async Task Dispatcher_Post_ThrowsWhenNotInitialized()
    {
        var saved = Dispatcher.messageLoop;
        Dispatcher.messageLoop = null;

        try
        {
            await Assert.That(() => Dispatcher.Post(() => { }))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            Dispatcher.messageLoop = saved;
        }
    }

    // ── AppWindow before initialization ──────────────────────────────

    [Test]
    public async Task AppWindow_IsMaximized_ReturnsFalseBeforeInit()
    {
        // App.Window uses App.nativeWindow which is null before Run.
        await Assert.That(App.Window.IsMaximized).IsFalse();
    }

    [Test]
    public async Task AppWindow_IsMinimized_ReturnsFalseBeforeInit()
    {
        await Assert.That(App.Window.IsMinimized).IsFalse();
    }

    [Test]
    public async Task AppWindow_Bounds_ReturnsDefaultBeforeInit()
    {
        Rect bounds = App.Window.Bounds;
        await Assert.That(bounds).IsEqualTo(default(Rect));
    }

    [Test]
    public async Task AppWindow_Minimize_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.Minimize());
    }

    [Test]
    public async Task AppWindow_Maximize_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.Maximize());
    }

    [Test]
    public async Task AppWindow_Close_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.Close());
    }

    [Test]
    public async Task AppWindow_SetSize_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.SetSize(800, 600));
    }

    [Test]
    public async Task AppWindow_CenterOnScreen_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.CenterOnScreen());
    }

    [Test]
    public async Task AppWindow_CenterOnParent_DoesNotThrowBeforeInit()
    {
        await Task.Run(() => App.Window.CenterOnParent());
    }

    // ── FilePickerResult.FileName ─────────────────────────────────────

    [Test]
    public async Task FilePickerResult_FileName_ExtractsFileNameFromPath()
    {
        var result = new FilePickerResult { Path = @"C:\Users\alice\documents\report.pdf", Size = 0 };
        await Assert.That(result.FileName).IsEqualTo("report.pdf");
    }

    [Test]
    public async Task FilePickerResult_FileName_HandlesFileInRoot()
    {
        var result = new FilePickerResult { Path = @"C:\file.txt", Size = 0 };
        await Assert.That(result.FileName).IsEqualTo("file.txt");
    }

    [Test]
    public async Task FilePickerResult_FileName_HandlesUnixStylePath()
    {
        var result = new FilePickerResult { Path = "/home/alice/photo.png", Size = 0 };
        await Assert.That(result.FileName).IsEqualTo("photo.png");
    }

    [Test]
    public async Task FilePickerResult_FileName_HandlesNoExtension()
    {
        var result = new FilePickerResult { Path = @"C:\tools\myapp", Size = 0 };
        await Assert.That(result.FileName).IsEqualTo("myapp");
    }
}
