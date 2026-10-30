using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Cascade.UI;

namespace Cascade.UI.Tests;

/// <summary>
/// App.Exit from a thread other than the UI thread. Win32's quit message goes to the calling
/// thread's queue, so an off-thread Exit used to be lost and the app kept running.
/// </summary>
[NotInParallel]
public class AppExitTests
{
    [Test]
    public async Task Exit_FromAnotherThread_QuitsTheLoopOnTheUiThread()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var ready = new ManualResetEventSlim();
        Win32MessageLoop? loop = null;
        int? quitCode = null;
        bool quitOnUiThread = false;

        // A stand-in UI thread: owns the loop and drains its dispatch queue (no window, so no
        // WM_DISPATCH wake-ups; it polls instead).
        var ui = new Thread(() =>
        {
            loop = new Win32MessageLoop();
            Dispatcher.Initialize(loop);
            ready.Set();
            var deadline = Environment.TickCount64 + 5000;
            while (loop.RequestedExitCode is null && Environment.TickCount64 < deadline)
            {
                loop.HandleDispatchMessage();
                Thread.Sleep(2);
            }
            quitCode = loop.RequestedExitCode;
            quitOnUiThread = loop.QuitOnMainThread;
        });

        try
        {
            ui.Start();
            ready.Wait();
            await Assert.That(App.IsRunning).IsTrue();

            App.Exit(7); // test thread, not the loop's

            ui.Join();
            await Assert.That(quitCode).IsEqualTo(7);
            await Assert.That(quitOnUiThread).IsTrue();
        }
        finally
        {
            Dispatcher.messageLoop = null;
            loop?.Dispose();
        }
        await Assert.That(App.IsRunning).IsFalse();
    }
}
