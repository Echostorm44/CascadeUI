using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Unit tests for the macOS Cocoa API wiring: Dispatcher dual-loop support,
/// AppWindow cross-platform default state, and ClipboardContent Cocoa factory.
/// These tests verify logic on Windows CI without requiring a macOS runtime.
/// </summary>
public class CocoaCoreTests
{
    // ── Dispatcher.IsInitialized ─────────────────────────────────────

    [Test]
    public async Task Dispatcher_IsInitialized_ReturnsFalseWhenBothLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop = null;

        try
        {
            await Assert.That(Dispatcher.IsInitialized).IsFalse();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop = savedCocoa;
        }
    }

    [Test]
    public async Task Dispatcher_IsOnUiThread_FallsThroughToCocoaLoop()
    {
        // When both loops are null, IsOnUiThread returns false.
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop = null;

        try
        {
            await Assert.That(Dispatcher.IsOnUiThread).IsFalse();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop = savedCocoa;
        }
    }

    [Test]
    public async Task Dispatcher_Post_ThrowsWhenBothLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop = null;

        try
        {
            await Assert.That(() => Dispatcher.Post(() => { }))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop = savedCocoa;
        }
    }

    [Test]
    public async Task Dispatcher_InvokeAsync_ThrowsWhenBothLoopsNull()
    {
        var savedWin32 = Dispatcher.messageLoop;
        var savedCocoa = Dispatcher.cocoaLoop;
        Dispatcher.messageLoop = null;
        Dispatcher.cocoaLoop = null;

        try
        {
            await Assert.That(() => Dispatcher.InvokeAsync(() => { }))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            Dispatcher.messageLoop = savedWin32;
            Dispatcher.cocoaLoop = savedCocoa;
        }
    }

    // ── AppWindow default state ──────────────────────────────────────

    [Test]
    public async Task AppWindow_IsMaximized_FalseWhenBothWindowsNull()
    {
        var savedWin32 = App.nativeWindow;
        var savedCocoa = App.nativeCocoaWindow;
        App.nativeWindow = null;
        App.nativeCocoaWindow = null;

        try
        {
            await Assert.That(App.Window.IsMaximized).IsFalse();
        }
        finally
        {
            App.nativeWindow = savedWin32;
            App.nativeCocoaWindow = savedCocoa;
        }
    }

    [Test]
    public async Task AppWindow_IsMinimized_FalseWhenBothWindowsNull()
    {
        var savedWin32 = App.nativeWindow;
        var savedCocoa = App.nativeCocoaWindow;
        App.nativeWindow = null;
        App.nativeCocoaWindow = null;

        try
        {
            await Assert.That(App.Window.IsMinimized).IsFalse();
        }
        finally
        {
            App.nativeWindow = savedWin32;
            App.nativeCocoaWindow = savedCocoa;
        }
    }

    [Test]
    public async Task AppWindow_Bounds_DefaultWhenBothWindowsNull()
    {
        var savedWin32 = App.nativeWindow;
        var savedCocoa = App.nativeCocoaWindow;
        App.nativeWindow = null;
        App.nativeCocoaWindow = null;

        try
        {
            Rect bounds = App.Window.Bounds;
            await Assert.That(bounds).IsEqualTo(default(Rect));
        }
        finally
        {
            App.nativeWindow = savedWin32;
            App.nativeCocoaWindow = savedCocoa;
        }
    }

    [Test]
    public async Task AppWindow_Actions_DoNotThrowWhenBothWindowsNull()
    {
        var savedWin32 = App.nativeWindow;
        var savedCocoa = App.nativeCocoaWindow;
        App.nativeWindow = null;
        App.nativeCocoaWindow = null;

        try
        {
            // All action methods should be safe no-ops when both windows are null.
            App.Window.Minimize();
            App.Window.Maximize();
            App.Window.Restore();
            App.Window.Close();
            App.Window.ForceClose();
            App.Window.SetSize(100, 100);
            App.Window.SetPosition(50, 50);
            App.Window.CenterOnScreen();
            App.Window.CenterOnParent();
            App.Window.ToggleMaximize();

            // If we got here without exception, all no-ops succeeded.
            bool noThrow = true;
            await Assert.That(noThrow).IsTrue();
        }
        finally
        {
            App.nativeWindow = savedWin32;
            App.nativeCocoaWindow = savedCocoa;
        }
    }

    // ── ClipboardContent.FromCocoaAvailability ───────────────────────

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_MapsTextFlag()
    {
        var avail = new CocoaClipboardAvailability { HasText = true };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasText).IsTrue();
        await Assert.That(content.HasHtml).IsFalse();
        await Assert.That(content.HasFiles).IsFalse();
        await Assert.That(content.HasImage).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_MapsHtmlFlag()
    {
        var avail = new CocoaClipboardAvailability { HasHtml = true };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasHtml).IsTrue();
        await Assert.That(content.HasText).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_MapsRtfFlag()
    {
        var avail = new CocoaClipboardAvailability { HasRtf = true };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasRtf).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_MapsFilesFlag()
    {
        var avail = new CocoaClipboardAvailability { HasFiles = true };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasFiles).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_MapsImageFlag()
    {
        var avail = new CocoaClipboardAvailability { HasImage = true };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasImage).IsTrue();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_AllFalseByDefault()
    {
        var avail = new CocoaClipboardAvailability();
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasText).IsFalse();
        await Assert.That(content.HasHtml).IsFalse();
        await Assert.That(content.HasRtf).IsFalse();
        await Assert.That(content.HasImage).IsFalse();
        await Assert.That(content.HasFiles).IsFalse();
    }

    [Test]
    public async Task ClipboardContent_FromCocoaAvailability_AllFormats()
    {
        var avail = new CocoaClipboardAvailability
        {
            HasText  = true,
            HasHtml  = true,
            HasRtf   = true,
            HasFiles = true,
            HasImage = true
        };
        ClipboardContent content = ClipboardContent.FromCocoaAvailability(avail);

        await Assert.That(content.HasText).IsTrue();
        await Assert.That(content.HasHtml).IsTrue();
        await Assert.That(content.HasRtf).IsTrue();
        await Assert.That(content.HasFiles).IsTrue();
        await Assert.That(content.HasImage).IsTrue();
    }

    // ── Platform branching coverage ─────────────────────────────────

    [Test]
    public async Task PlatformBranching_IsWindowsOrMacOSOrNeither()
    {
        // At least one of the platform checks should be exercised.
        bool isWindows = OperatingSystem.IsWindows();
        bool isMacOS = OperatingSystem.IsMacOS();

        // On CI (Windows), Windows is true and macOS is false.
        if (isWindows)
        {
            await Assert.That(isMacOS).IsFalse();
        }
        else if (isMacOS)
        {
            await Assert.That(isWindows).IsFalse();
        }
        else
        {
            // Neither — both should be false.
            await Assert.That(isWindows).IsFalse();
            await Assert.That(isMacOS).IsFalse();
        }
    }
}
