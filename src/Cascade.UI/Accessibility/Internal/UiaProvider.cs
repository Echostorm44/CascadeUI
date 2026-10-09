using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI;

/// <summary>
/// Windows UI Automation bridge. Exposes a window's accessibility tree to Narrator, NVDA, JAWS and
/// any other UIA client: answers <c>WM_GETOBJECT</c> with a fragment-root provider
/// (<see cref="UiaRootElement"/>), raises focus, menu, property-change and notification events, and
/// routes UIA patterns (Invoke, Value, Toggle, Selection…) to the controls.
/// </summary>
/// <remarks>
/// <para>
/// Zero cost until assistive technology asks: nothing is built before the first
/// <c>WM_GETOBJECT</c> for <c>UiaRootObjectId</c>, and after it the per-frame work is one call to
/// <c>UiaClientsAreListening</c> while nobody listens. The tree itself is rebuilt only when a client
/// asks a question after the UI changed.
/// </para>
/// <para>
/// COM objects are produced by source-generated COM (<c>[GeneratedComClass]</c> with
/// <see cref="StrategyBasedComWrappers"/>), so the bridge is NativeAOT and trimming safe.
/// </para>
/// </remarks>
internal sealed class UiaProvider : IPlatformAccessibilityBridge
{
    private nint windowHandle;
    private bool initialized;
    private UiaContext? context;

    // Set once UIA has asked this window for its provider; until then no event is raised.
    private bool connected;

    // What the last frame reported, to raise events only on change.
    private FocusKey lastFocus;
    private int lastToggle = -1;
    private int lastExpand = -1;
    private string? lastValue;
    private int lastOverlayCount;

    public string PlatformName => "Windows UIA";

    /// <summary>Whether a UIA client has asked this window for its tree (tests and diagnostics).</summary>
    internal bool IsConnected => connected;

    /// <summary>The window's UIA tree, once attached.</summary>
    internal UiaContext? Context => context;

    public void Initialize(nint handle)
    {
        windowHandle = handle;
        initialized = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    /// <summary>Connects the bridge to the window's live tree.</summary>
    internal void Attach(UiaContext treeContext)
    {
        context = treeContext;
    }

    public void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        if (connected && windowHandle != 0)
        {
            // Releases the providers UIA holds for this window (documented for WM_DESTROY).
            _ = UiaNative.UiaReturnRawElementProvider(windowHandle, 0, 0, 0);
            _ = UiaNative.UiaDisconnectAllProviders();
        }

        connected = false;
        context = null;
        windowHandle = 0;
        initialized = false;
    }

    /// <summary>
    /// Answers <c>WM_GETOBJECT</c>. Returns the LRESULT to hand back, or null to let the window's
    /// default procedure answer (requests for MSAA objects and the like).
    /// </summary>
    internal nint? HandleGetObject(nuint wParam, nint lParam)
    {
        if (!initialized || context is null || (int)(long)lParam != UiaIds.UiaRootObjectId)
        {
            return null;
        }

        connected = true;
        nint simple = UiaComObjects.Simple(context.Root);
        try
        {
            return UiaNative.UiaReturnRawElementProvider(windowHandle, wParam, lParam, simple);
        }
        finally
        {
            if (simple != 0)
            {
                Marshal.Release(simple);
            }
        }
    }

    public void OnTreeChanged()
    {
        context?.Invalidate();
    }

    public void OnFrameCompleted(bool reRendered)
    {
        if (context is null)
        {
            return;
        }

        context.Invalidate();
        if (!connected || !UiaNative.UiaClientsAreListening())
        {
            return;
        }

        RaiseChangeEvents();
    }

    public void OnNodeReplaced(Node from, Node to)
    {
        context?.NodeReplaced(from, to);
    }

    public void Announce(string message, AnnouncePriority priority)
    {
        if (!initialized || context is null)
        {
            return;
        }

        if (!Dispatcher.IsOnUiThread && Dispatcher.IsInitialized)
        {
            Dispatcher.Post(() =>
            {
                Announce(message, priority);
            });
            return;
        }

        if (!UiaNative.UiaClientsAreListening())
        {
            return;
        }

        int processing = priority switch
        {
            AnnouncePriority.High => UiaIds.NotificationProcessing_ImportantAll,
            AnnouncePriority.Low => UiaIds.NotificationProcessing_MostRecent,
            _ => UiaIds.NotificationProcessing_All,
        };

        nint root = UiaComObjects.Simple(context!.Root);
        nint display = UiaNative.SysAllocString(message);
        nint activity = UiaNative.SysAllocString("Cascade.Announce");
        try
        {
            _ = UiaNative.UiaRaiseNotificationEvent(root, UiaIds.NotificationKind_Other, processing, display, activity);
        }
        finally
        {
            UiaNative.SysFreeString(display);
            UiaNative.SysFreeString(activity);
            Marshal.Release(root);
        }
    }

    // ── Change events ─────────────────────────────────────────────────

    /// <summary>
    /// What has focus, compared frame to frame without building the tree: the focused node, the
    /// selected row of a focused list, the keyboard tab of a focused tab bar, or the highlighted
    /// item of an open menu.
    /// </summary>
    private readonly record struct FocusKey(Node? Node, int Row, MenuLevel? MenuLevel, int MenuItem, int Column = -1);

    private FocusKey CurrentFocus()
    {
        if (context!.Input?.Menu is { IsOpen: true } menu)
        {
            var level = menu.Levels[^1];
            return new FocusKey(null, -1, level, level.Highlighted);
        }

        var focused = FocusManager.FocusedElement;
        if (focused is ITabularDataNode table)
        {
            // The current cell of a grid (or the selected row of a table) has the focus: a move is a focus change.
            int column = table is ITabularCellGrid { CellNavigationEnabled: true } cells && table.RowActionStrip is not { FocusedIndex: >= 0 }
                ? cells.CurrentColumn
                : -1;
            return new FocusKey(focused, table.SelectedRowIndex, null, -1, column);
        }

        int row = focused switch
        {
            IListViewNode { SectionCount: 0 } list => list.SelectedIndex,
            TabBar bar => TabStripLayout.KeyboardPosition(bar),
            _ => -1,
        };
        return new FocusKey(focused, row, null, -1);
    }

    private void RaiseChangeEvents()
    {
        var focus = CurrentFocus();
        int overlays = context!.Input?.Overlays?.Entries.Count ?? 0;
        if (overlays > lastOverlayCount)
        {
            RaiseForTopOverlay();
        }
        lastOverlayCount = overlays;

        if (focus != lastFocus)
        {
            bool menuOpened = focus.MenuLevel is not null && lastFocus.MenuLevel is null;
            lastFocus = focus;
            RememberState(focus.Node);
            if (menuOpened)
            {
                RaiseForMenu(UiaIds.MenuOpenedEvent);
            }
            if (context.FocusedElement() is { } element)
            {
                Raise(element, UiaIds.AutomationFocusChangedEvent);
            }
            return;
        }

        // Same focus: tell the reader when the focused control's state changed under it.
        if (focus.Node is not { } node || focus.Row >= 0 || focus.MenuLevel is not null)
        {
            return;
        }

        int toggle = node is Checkbox or Toggle ? UiaElement.ToggleStateOf(node) : -1;
        int expand = UiaElement.ExpandStateOf(node);
        string? value = UiaElement.ValueOf(node);
        if (toggle != lastToggle && toggle >= 0)
        {
            RaiseProperty(node, UiaIds.ToggleStateProperty, UiaVariant.From(lastToggle), UiaVariant.From(toggle));
        }
        if (expand != lastExpand && expand >= 0)
        {
            RaiseProperty(node, UiaIds.ExpandCollapseStateProperty, UiaVariant.From(lastExpand), UiaVariant.From(expand));
        }
        if (value is not null && value != lastValue)
        {
            RaiseProperty(node, UiaIds.ValueValueProperty, UiaVariant.From(lastValue ?? ""), UiaVariant.From(value));
        }
        RememberState(node);
    }

    private void RememberState(Node? node)
    {
        if (node is null)
        {
            lastToggle = -1;
            lastExpand = -1;
            lastValue = null;
            return;
        }

        lastToggle = node is Checkbox or Toggle ? UiaElement.ToggleStateOf(node) : -1;
        lastExpand = UiaElement.ExpandStateOf(node);
        lastValue = UiaElement.ValueOf(node);
    }

    private void RaiseForMenu(int eventId)
    {
        var tree = context!.Tree;
        if (context.Input?.Menu is { IsOpen: true } menu && tree.IndexOfMenu(menu.Levels[0], -1) is var index && index != AccessibleTree.None)
        {
            Raise(context.ElementFor(tree, index), eventId);
        }
    }

    private void RaiseForTopOverlay()
    {
        if (context!.Input?.Overlays?.Topmost is not { Tree: { } panel })
        {
            return;
        }

        var tree = context.Tree;
        int index = tree.IndexOf(panel);
        if (index != AccessibleTree.None)
        {
            Raise(context.ElementFor(tree, index), UiaIds.WindowOpenedEvent);
        }
    }

    private void RaiseProperty(Node node, int propertyId, UiaVariant oldValue, UiaVariant newValue)
    {
        try
        {
            if (context!.Tree.IndexOf(node) == AccessibleTree.None)
            {
                return;
            }

            nint simple = UiaComObjects.Simple(context.ElementForNode(node));
            try
            {
                _ = UiaNative.UiaRaiseAutomationPropertyChangedEvent(simple, propertyId, oldValue, newValue);
            }
            finally
            {
                Marshal.Release(simple);
            }
        }
        finally
        {
            oldValue.Clear();
            newValue.Clear();
        }
    }

    private static void Raise(UiaFragment element, int eventId)
    {
        nint simple = UiaComObjects.Simple(element);
        if (simple == 0)
        {
            return;
        }

        try
        {
            _ = UiaNative.UiaRaiseAutomationEvent(simple, eventId);
        }
        finally
        {
            Marshal.Release(simple);
        }
    }

    // ── Accessibility context ─────────────────────────────────────────

    public AccessibilityContext GetAccessibilityContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateDefaultContext();
        }

        return new AccessibilityContext
        {
            DisplayScale = GetDisplayScale(),
            TextScale = Win32Accessibility.GetTextScaleFactor() / 100.0f,
            ReducedMotion = GetReducedMotion(),
            HighContrast = GetHighContrast(),
            ReducedTransparency = !Win32Accessibility.GetTransparencyEnabled(),
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = IsScreenReaderActive(),
        };
    }

    public bool IsScreenReaderActive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        int screenReaderRunning = 0;
        return Win32Accessibility.SystemParametersInfo(Win32Accessibility.SPI_GETSCREENREADER, 0, ref screenReaderRunning, 0)
            && screenReaderRunning != 0;
    }

    /// <summary>
    /// Maps a Cascade AccessibleRole to the Windows UIA ControlType ID (values from
    /// UIAutomationClient.h). A heading is Text with a HeadingLevel; a dialog panel is a Window
    /// with IsDialog; a table row or cell is a DataItem.
    /// </summary>
    internal static int MapRoleToUiaControlType(AccessibleRole role)
    {
        return role switch
        {
            AccessibleRole.Button => UiaIds.ButtonControl,
            AccessibleRole.Checkbox => UiaIds.CheckBoxControl,
            AccessibleRole.Link => UiaIds.HyperlinkControl,
            AccessibleRole.Heading => UiaIds.TextControl,
            AccessibleRole.Text => UiaIds.TextControl,
            AccessibleRole.TextBox => UiaIds.EditControl,
            AccessibleRole.Radio => UiaIds.RadioButtonControl,
            AccessibleRole.RadioGroup => UiaIds.GroupControl,
            AccessibleRole.ComboBox => UiaIds.ComboBoxControl,
            AccessibleRole.Slider => UiaIds.SliderControl,
            AccessibleRole.Switch => UiaIds.CheckBoxControl,
            AccessibleRole.List => UiaIds.ListControl,
            AccessibleRole.ListItem => UiaIds.ListItemControl,
            AccessibleRole.Table => UiaIds.TableControl,
            AccessibleRole.Row => UiaIds.DataItemControl,
            AccessibleRole.ColumnHeader => UiaIds.HeaderItemControl,
            AccessibleRole.Cell => UiaIds.DataItemControl,
            AccessibleRole.TabList => UiaIds.TabControl,
            AccessibleRole.Tab => UiaIds.TabItemControl,
            AccessibleRole.TabPanel => UiaIds.PaneControl,
            AccessibleRole.MenuBar => UiaIds.MenuBarControl,
            AccessibleRole.MenuItem => UiaIds.MenuItemControl,
            AccessibleRole.Menu => UiaIds.MenuControl,
            AccessibleRole.Dialog => UiaIds.WindowControl,
            AccessibleRole.AlertDialog => UiaIds.WindowControl,
            AccessibleRole.ProgressBar => UiaIds.ProgressBarControl,
            AccessibleRole.ScrollBar => UiaIds.ScrollBarControl,
            AccessibleRole.Image => UiaIds.ImageControl,
            AccessibleRole.Navigation => UiaIds.GroupControl,
            AccessibleRole.Main => UiaIds.GroupControl,
            AccessibleRole.Tree => UiaIds.TreeControl,
            AccessibleRole.TreeItem => UiaIds.TreeItemControl,
            AccessibleRole.Region => UiaIds.GroupControl,
            _ => UiaIds.PaneControl,
        };
    }

    // ─── Private platform queries ───────────────────────────────────

    private float GetDisplayScale()
    {
        if (windowHandle == 0)
        {
            return 1.0f;
        }

        uint dpi = Win32Accessibility.GetDpiForWindow(windowHandle);
        return dpi == 0 ? 1.0f : dpi / 96.0f;
    }

    private static bool GetReducedMotion()
    {
        int animationsEnabled = 1;
        return Win32Accessibility.SystemParametersInfo(Win32Accessibility.SPI_GETCLIENTAREAANIMATION, 0, ref animationsEnabled, 0)
            && animationsEnabled == 0;
    }

    private static bool GetHighContrast()
    {
        var hc = new Win32Accessibility.HIGHCONTRAST
        {
            cbSize = (uint)Marshal.SizeOf<Win32Accessibility.HIGHCONTRAST>(),
        };
        return Win32Accessibility.SystemParametersInfoHighContrast(Win32Accessibility.SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0)
            && (hc.dwFlags & Win32Accessibility.HCF_HIGHCONTRASTON) != 0;
    }

    private static AccessibilityContext CreateDefaultContext()
    {
        return new AccessibilityContext
        {
            DisplayScale = 1.0f,
            TextScale = 1.0f,
            ReducedMotion = false,
            HighContrast = false,
            ReducedTransparency = false,
            HasCursor = true,
            LayoutDensity = LayoutDensity.Standard,
            ScreenReaderActive = false,
        };
    }
}

/// <summary>Maps a Win32 window's client area to screen coordinates for UIA (physical pixels, per-monitor DPI).</summary>
internal sealed class Win32UiaHost : IUiaHost
{
    private readonly Win32Window window;

    internal Win32UiaHost(Win32Window window)
    {
        this.window = window;
    }

    public nint Handle => window.Handle;

    public UiaRect ToScreen(Rect logical)
    {
        var origin = new Win32.POINT();
        _ = UiaNative.ClientToScreen(window.Handle, ref origin);
        float scale = window.DpiScale;
        return new UiaRect
        {
            Left = origin.x + (logical.X * scale),
            Top = origin.y + (logical.Y * scale),
            Width = logical.Width * scale,
            Height = logical.Height * scale,
        };
    }

    public Point FromScreen(double x, double y)
    {
        var point = new Win32.POINT { x = (int)Math.Round(x), y = (int)Math.Round(y) };
        _ = UiaNative.ScreenToClient(window.Handle, ref point);
        float scale = window.DpiScale;
        return new Point(point.x / scale, point.y / scale);
    }
}

/// <summary>
/// P/Invoke declarations for Windows accessibility APIs.
/// Uses [LibraryImport] for NativeAOT compatibility.
/// </summary>
#pragma warning disable CA5392 // P/Invokes target well-known system DLLs (user32.dll)
internal static partial class Win32Accessibility
{
    internal const uint SPI_GETSCREENREADER = 0x0046;
    internal const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
    internal const uint SPI_GETHIGHCONTRAST = 0x0042;
    internal const uint HCF_HIGHCONTRASTON = 0x00000001;

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref int pvParam,
        uint fWinIni);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SystemParametersInfoHighContrast(
        uint uiAction,
        uint uiParam,
        ref HIGHCONTRAST pvParam,
        uint fWinIni);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    [StructLayout(LayoutKind.Sequential)]
    internal struct HIGHCONTRAST
    {
        public uint cbSize;
        public uint dwFlags;
        public nint lpszDefaultScheme;
    }

    /// <summary>
    /// The "Make text bigger" setting: HKCU\Software\Microsoft\Accessibility\TextScaleFactor,
    /// 100–225 (percent). 100 when unset or not on Windows.
    /// </summary>
    internal static int GetTextScaleFactor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 100;
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
        return key?.GetValue("TextScaleFactor") is int percent && percent is >= 100 and <= 225 ? percent : 100;
    }

    /// <summary>
    /// The "Transparency effects" setting:
    /// HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency.
    /// True (transparency on) when unset or not on Windows.
    /// </summary>
    internal static bool GetTransparencyEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") is not int enabled || enabled != 0;
    }
}
#pragma warning restore CA5392
