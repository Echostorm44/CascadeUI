using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI.Integration.Uia;

// A minimal out-of-process UI Automation client over the UIA COM API (UIAutomationClient.idl),
// declared with source-generated COM so it runs on plain .NET (System.Windows.Automation needs the
// WindowsDesktop framework). Vtable order follows the Windows SDK's um\UIAutomationClient.idl;
// slots this client never calls are declared as placeholders so the order stays right.

#pragma warning disable CA1707, IDE1006 // COM-shaped names

/// <summary>IUIAutomationCondition — passed back to UIA only.</summary>
[GeneratedComInterface]
[Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
internal partial interface IUIAutomationCondition
{
}

/// <summary>IUIAutomationCacheRequest — never created; null is passed.</summary>
[GeneratedComInterface]
[Guid("b32a92b5-bc25-4078-9c08-d7ee95c48e03")]
internal partial interface IUIAutomationCacheRequest
{
}

/// <summary>IUIAutomation … IUIAutomation5, flattened (CUIAutomation8 implements all of them).</summary>
[GeneratedComInterface]
[Guid("25F700C8-D816-4057-A9DC-3CBDEE77E256")]
internal partial interface IUIAutomation5
{
    [PreserveSig] int CompareElements();
    [PreserveSig] int CompareRuntimeIds();
    [PreserveSig] int GetRootElement(out IUIAutomationElement root);
    [PreserveSig] int ElementFromHandle(nint hwnd, out IUIAutomationElement element);
    [PreserveSig] int ElementFromPoint();
    [PreserveSig] int GetFocusedElement(out IUIAutomationElement element);
    [PreserveSig] int GetRootElementBuildCache();
    [PreserveSig] int ElementFromHandleBuildCache();
    [PreserveSig] int ElementFromPointBuildCache();
    [PreserveSig] int GetFocusedElementBuildCache();
    [PreserveSig] int CreateTreeWalker();
    [PreserveSig] int get_ControlViewWalker();
    [PreserveSig] int get_ContentViewWalker();
    [PreserveSig] int get_RawViewWalker();
    [PreserveSig] int get_RawViewCondition();
    [PreserveSig] int get_ControlViewCondition();
    [PreserveSig] int get_ContentViewCondition();
    [PreserveSig] int CreateCacheRequest();
    [PreserveSig] int CreateTrueCondition(out IUIAutomationCondition condition);
    [PreserveSig] int CreateFalseCondition();
    [PreserveSig] int CreatePropertyCondition(int propertyId, ClientVariant value, out IUIAutomationCondition condition);
    [PreserveSig] int CreatePropertyConditionEx();
    [PreserveSig] int CreateAndCondition(IUIAutomationCondition first, IUIAutomationCondition second, out IUIAutomationCondition condition);
    [PreserveSig] int CreateAndConditionFromArray();
    [PreserveSig] int CreateAndConditionFromNativeArray();
    [PreserveSig] int CreateOrCondition();
    [PreserveSig] int CreateOrConditionFromArray();
    [PreserveSig] int CreateOrConditionFromNativeArray();
    [PreserveSig] int CreateNotCondition();
    [PreserveSig] int AddAutomationEventHandler(int eventId, IUIAutomationElement element, int scope, IUIAutomationCacheRequest? cacheRequest, IUIAutomationEventHandler handler);
    [PreserveSig] int RemoveAutomationEventHandler(int eventId, IUIAutomationElement element, IUIAutomationEventHandler handler);
    [PreserveSig] int AddPropertyChangedEventHandlerNativeArray();
    [PreserveSig] int AddPropertyChangedEventHandler();
    [PreserveSig] int RemovePropertyChangedEventHandler();
    [PreserveSig] int AddStructureChangedEventHandler();
    [PreserveSig] int RemoveStructureChangedEventHandler();
    [PreserveSig] int AddFocusChangedEventHandler(IUIAutomationCacheRequest? cacheRequest, IUIAutomationFocusChangedEventHandler handler);
    [PreserveSig] int RemoveFocusChangedEventHandler(IUIAutomationFocusChangedEventHandler handler);
    [PreserveSig] int RemoveAllEventHandlers();
    [PreserveSig] int IntNativeArrayToSafeArray();
    [PreserveSig] int IntSafeArrayToNativeArray();
    [PreserveSig] int RectToVariant();
    [PreserveSig] int VariantToRect();
    [PreserveSig] int SafeArrayToRectNativeArray();
    [PreserveSig] int CreateProxyFactoryEntry();
    [PreserveSig] int get_ProxyFactoryMapping();
    [PreserveSig] int GetPropertyProgrammaticName();
    [PreserveSig] int GetPatternProgrammaticName();
    [PreserveSig] int PollForPotentialSupportedPatterns();
    [PreserveSig] int PollForPotentialSupportedProperties();
    [PreserveSig] int CheckNotSupported();
    [PreserveSig] int get_ReservedNotSupportedValue();
    [PreserveSig] int get_ReservedMixedAttributeValue();
    [PreserveSig] int ElementFromIAccessible();
    [PreserveSig] int ElementFromIAccessibleBuildCache();

    // IUIAutomation2
    [PreserveSig] int get_AutoSetFocus();
    [PreserveSig] int put_AutoSetFocus();
    [PreserveSig] int get_ConnectionTimeout();
    [PreserveSig] int put_ConnectionTimeout();
    [PreserveSig] int get_TransactionTimeout();
    [PreserveSig] int put_TransactionTimeout();

    // IUIAutomation3
    [PreserveSig] int AddTextEditTextChangedEventHandler();
    [PreserveSig] int RemoveTextEditTextChangedEventHandler();

    // IUIAutomation4
    [PreserveSig] int AddChangesEventHandler();
    [PreserveSig] int RemoveChangesEventHandler();

    // IUIAutomation5
    [PreserveSig] int AddNotificationEventHandler(IUIAutomationElement element, int scope, IUIAutomationCacheRequest? cacheRequest, IUIAutomationNotificationEventHandler handler);
    [PreserveSig] int RemoveNotificationEventHandler(IUIAutomationElement element, IUIAutomationNotificationEventHandler handler);
}

/// <summary>IUIAutomationElement.</summary>
[GeneratedComInterface]
[Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
internal partial interface IUIAutomationElement
{
    [PreserveSig] int SetFocus();
    [PreserveSig] int GetRuntimeId();
    [PreserveSig] int FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement? found);
    [PreserveSig] int FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray found);
    [PreserveSig] int FindFirstBuildCache();
    [PreserveSig] int FindAllBuildCache();
    [PreserveSig] int BuildUpdatedCache();
    [PreserveSig] int GetCurrentPropertyValue(int propertyId, out ClientVariant value);
    [PreserveSig] int GetCurrentPropertyValueEx();
    [PreserveSig] int GetCachedPropertyValue();
    [PreserveSig] int GetCachedPropertyValueEx();
    [PreserveSig] int GetCurrentPatternAs();
    [PreserveSig] int GetCachedPatternAs();
    [PreserveSig] int GetCurrentPattern(int patternId, out nint pattern);
    [PreserveSig] int GetCachedPattern();
    [PreserveSig] int GetCachedParent();
    [PreserveSig] int GetCachedChildren();
    [PreserveSig] int get_CurrentProcessId(out int value);
    [PreserveSig] int get_CurrentControlType(out int value);
    [PreserveSig] int get_CurrentLocalizedControlType(out nint value);
    [PreserveSig] int get_CurrentName(out nint value);
    [PreserveSig] int get_CurrentAcceleratorKey();
    [PreserveSig] int get_CurrentAccessKey();
    [PreserveSig] int get_CurrentHasKeyboardFocus(out int value);
    [PreserveSig] int get_CurrentIsKeyboardFocusable();
    [PreserveSig] int get_CurrentIsEnabled(out int value);
    [PreserveSig] int get_CurrentAutomationId();
    [PreserveSig] int get_CurrentClassName();
    [PreserveSig] int get_CurrentHelpText();
    [PreserveSig] int get_CurrentCulture();
    [PreserveSig] int get_CurrentIsControlElement();
    [PreserveSig] int get_CurrentIsContentElement();
    [PreserveSig] int get_CurrentIsPassword();
    [PreserveSig] int get_CurrentNativeWindowHandle();
    [PreserveSig] int get_CurrentItemType();
    [PreserveSig] int get_CurrentIsOffscreen(out int value);
    [PreserveSig] int get_CurrentOrientation();
    [PreserveSig] int get_CurrentFrameworkId(out nint value);
    [PreserveSig] int get_CurrentIsRequiredForForm();
    [PreserveSig] int get_CurrentItemStatus();
    [PreserveSig] int get_CurrentBoundingRectangle(out ClientRect value);
}

/// <summary>IUIAutomationElementArray.</summary>
[GeneratedComInterface]
[Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
internal partial interface IUIAutomationElementArray
{
    [PreserveSig] int get_Length(out int length);
    [PreserveSig] int GetElement(int index, out IUIAutomationElement element);
}

/// <summary>IUIAutomationInvokePattern.</summary>
[GeneratedComInterface]
[Guid("fb377fbe-8ea6-46d5-9c73-6499642d3059")]
internal partial interface IUIAutomationInvokePattern
{
    [PreserveSig] int Invoke();
}

/// <summary>IUIAutomationValuePattern.</summary>
[GeneratedComInterface]
[Guid("a94cd8b1-0844-4cd6-9d2d-640537ab39e9")]
internal partial interface IUIAutomationValuePattern
{
    [PreserveSig] int SetValue(nint bstr);
    [PreserveSig] int get_CurrentValue(out nint bstr);
}

/// <summary>IUIAutomationSelectionPattern.</summary>
[GeneratedComInterface]
[Guid("5ed5202e-b2ac-47a6-b638-4b0bf140d78e")]
internal partial interface IUIAutomationSelectionPattern
{
    [PreserveSig] int GetCurrentSelection(out IUIAutomationElementArray selection);
}

/// <summary>IUIAutomationSelectionItemPattern.</summary>
[GeneratedComInterface]
[Guid("a8efa66a-0fda-421a-9194-38021f3578ea")]
internal partial interface IUIAutomationSelectionItemPattern
{
    [PreserveSig] int Select();
}

/// <summary>IUIAutomationTogglePattern.</summary>
[GeneratedComInterface]
[Guid("94cf8058-9b8d-4ab9-8bfd-4cd0a33c8c70")]
internal partial interface IUIAutomationTogglePattern
{
    [PreserveSig] int Toggle();
    [PreserveSig] int get_CurrentToggleState(out int state);
}

/// <summary>IUIAutomationFocusChangedEventHandler.</summary>
[GeneratedComInterface]
[Guid("c270f6b5-5c69-4290-9745-7a7f97169468")]
internal partial interface IUIAutomationFocusChangedEventHandler
{
    [PreserveSig] int HandleFocusChangedEvent(IUIAutomationElement sender);
}

/// <summary>IUIAutomationEventHandler.</summary>
[GeneratedComInterface]
[Guid("146c3c17-f12e-4e22-8c27-f894b9b79c69")]
internal partial interface IUIAutomationEventHandler
{
    [PreserveSig] int HandleAutomationEvent(IUIAutomationElement sender, int eventId);
}

/// <summary>IUIAutomationNotificationEventHandler.</summary>
[GeneratedComInterface]
[Guid("C7CB2637-E6C2-4D0C-85DE-4948C02175C7")]
internal partial interface IUIAutomationNotificationEventHandler
{
    [PreserveSig] int HandleNotificationEvent(IUIAutomationElement sender, int notificationKind, int notificationProcessing, nint displayString, nint activityId);
}

/// <summary>RECT.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ClientRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

/// <summary>A blittable VARIANT for the types this client reads and writes (string, int, bool).</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct ClientVariant
{
    [LibraryImport("oleaut32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SafeArrayGetLBound(nint sa, uint dim, out int bound);

    [LibraryImport("oleaut32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SafeArrayGetUBound(nint sa, uint dim, out int bound);

    [LibraryImport("oleaut32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SafeArrayGetElement(nint sa, int* index, void* value);

    public ushort Type;
    private ushort reserved1;
    private ushort reserved2;
    private ushort reserved3;
    private nint data1;
    private nint data2;

    internal static ClientVariant FromString(string value)
    {
        return new ClientVariant { Type = 8, data1 = Marshal.StringToBSTR(value) };
    }

    internal static ClientVariant FromInt(int value)
    {
        var variant = new ClientVariant { Type = 3 };
        *(int*)Unsafe.AsPointer(ref variant.data1) = value;
        return variant;
    }

    internal static ClientVariant FromBool(bool value)
    {
        var variant = new ClientVariant { Type = 11 };
        *(short*)Unsafe.AsPointer(ref variant.data1) = value ? (short)-1 : (short)0;
        return variant;
    }

    internal object? ToObject()
    {
        if (Type == 0x2003 && data1 != 0)
        {
            nint sa = data1;
            _ = SafeArrayGetLBound(sa, 1, out int lo);
            _ = SafeArrayGetUBound(sa, 1, out int hi);
            var parts = new List<int>();
            for (int i = lo; i <= hi; i++)
            {
                int v;
                _ = SafeArrayGetElement(sa, &i, &v);
                parts.Add(v);
            }
            return string.Join(",", parts);
        }
        fixed (nint* payload = &data1)
        {
            return Type switch
            {
                3 => *(int*)payload,
                5 => *(double*)payload,
                8 => data1 == 0 ? "" : Marshal.PtrToStringBSTR(data1),
                11 => *(short*)payload != 0,
                _ => null,
            };
        }
    }

    internal void Clear()
    {
        if (Type == 8 && data1 != 0)
        {
            Marshal.FreeBSTR(data1);
        }
        this = default;
    }
}

/// <summary>Records focus changes, automation events and notifications raised by the app under test.</summary>
[GeneratedComClass]
internal sealed partial class UiaEventRecorder : IUIAutomationFocusChangedEventHandler, IUIAutomationNotificationEventHandler, IUIAutomationEventHandler
{
    private readonly List<string> focus = [];
    private readonly List<string> notifications = [];
    private readonly List<string> events = [];
    private readonly object gate = new();

    public int HandleFocusChangedEvent(IUIAutomationElement sender)
    {
        string entry = $"{UiaClient.ControlTypeName(UiaClient.ControlType(sender))}:{UiaClient.Name(sender)}";
        lock (gate)
        {
            focus.Add(entry);
        }
        return 0;
    }

    public int HandleNotificationEvent(IUIAutomationElement sender, int notificationKind, int notificationProcessing, nint displayString, nint activityId)
    {
        string text = displayString == 0 ? "" : Marshal.PtrToStringBSTR(displayString);
        lock (gate)
        {
            notifications.Add($"{text}|{notificationProcessing}");
        }
        return 0;
    }

    public int HandleAutomationEvent(IUIAutomationElement sender, int eventId)
    {
        string entry = $"{eventId}:{UiaClient.Name(sender)}";
        lock (gate)
        {
            events.Add(entry);
        }
        return 0;
    }

    internal string[] Focus()
    {
        lock (gate)
        {
            return [.. focus];
        }
    }

    internal string[] Notifications()
    {
        lock (gate)
        {
            return [.. notifications];
        }
    }

    internal string[] Events()
    {
        lock (gate)
        {
            return [.. events];
        }
    }
}

/// <summary>Convenience wrapper over <see cref="IUIAutomation5"/>.</summary>
internal sealed partial class UiaClient
{
    internal const int TreeScopeSubtree = 7;
    internal const int TreeScopeDescendants = 4;
    internal const int NameProperty = 30005;
    internal const int ControlTypeProperty = 30003;
    internal const int HasKeyboardFocusProperty = 30008;
    internal const int IsDialogProperty = 30174;
    internal const int ValueValueProperty = 30045;
    internal const int InvokePattern = 10000;
    internal const int SelectionPattern = 10001;
    internal const int ValuePattern = 10002;
    internal const int SelectionItemPattern = 10010;
    internal const int TogglePattern = 10015;
    internal const int ButtonControl = 50000;
    internal const int EditControl = 50004;
    internal const int ListControl = 50008;
    internal const int ListItemControl = 50007;
    internal const int MenuItemControl = 50011;
    internal const int WindowControl = 50032;
    internal const int TabControl = 50018;
    internal const int TabItemControl = 50019;
    internal const int MenuOpenedEvent = 20003;

    private static readonly Guid ClsidCUIAutomation8 = new("e22ad333-b25f-460c-83d0-0581107395c9");
    private static readonly Guid IidIUIAutomation5 = new("25F700C8-D816-4057-A9DC-3CBDEE77E256");
    private static readonly StrategyBasedComWrappers Wrappers = new();

    private UiaClient(IUIAutomation5 automation)
    {
        Automation = automation;
    }

    internal IUIAutomation5 Automation { get; }

    internal static UiaClient Create()
    {
        int hr = CoCreateInstance(in ClsidCUIAutomation8, 0, 1 /* CLSCTX_INPROC_SERVER */, in IidIUIAutomation5, out nint pointer);
        Marshal.ThrowExceptionForHR(hr);
        var automation = (IUIAutomation5)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.None);
        Marshal.Release(pointer);
        return new UiaClient(automation);
    }

    internal IUIAutomationElement ElementFromHandle(nint hwnd)
    {
        Check(Automation.ElementFromHandle(hwnd, out var element));
        return element;
    }

    internal IUIAutomationElement? FocusedElement()
    {
        return Automation.GetFocusedElement(out var element) >= 0 ? element : null;
    }

    internal IUIAutomationElement? FindByName(IUIAutomationElement root, string name)
    {
        var value = ClientVariant.FromString(name);
        try
        {
            Check(Automation.CreatePropertyCondition(NameProperty, value, out var condition));
            Check(root.FindFirst(TreeScopeDescendants, condition, out var found));
            return found;
        }
        finally
        {
            value.Clear();
        }
    }

    internal IUIAutomationElement? FindByControlType(IUIAutomationElement root, int controlType)
    {
        Check(Automation.CreatePropertyCondition(ControlTypeProperty, ClientVariant.FromInt(controlType), out var condition));
        Check(root.FindFirst(TreeScopeDescendants, condition, out var found));
        return found;
    }

    internal static T Pattern<T>(IUIAutomationElement element, int patternId) where T : class
    {
        Check(element.GetCurrentPattern(patternId, out nint pointer));
        if (pointer == 0)
        {
            throw new InvalidOperationException($"Pattern {patternId} is not supported by '{Name(element)}'.");
        }

        var pattern = (T)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.None);
        Marshal.Release(pointer);
        return pattern;
    }

    internal static string Name(IUIAutomationElement element)
    {
        return element.get_CurrentName(out nint bstr) >= 0 ? TakeBstr(bstr) : "";
    }

    internal static int ControlType(IUIAutomationElement element)
    {
        return element.get_CurrentControlType(out int type) >= 0 ? type : 0;
    }

    internal static object? Property(IUIAutomationElement element, int propertyId)
    {
        Check(element.GetCurrentPropertyValue(propertyId, out var value));
        try
        {
            return value.ToObject();
        }
        finally
        {
            value.Clear();
        }
    }

    internal static ClientRect Bounds(IUIAutomationElement element)
    {
        Check(element.get_CurrentBoundingRectangle(out var rect));
        return rect;
    }

    internal static string ValueOf(IUIAutomationValuePattern pattern)
    {
        Check(pattern.get_CurrentValue(out nint bstr));
        return TakeBstr(bstr);
    }

    internal static void SetValue(IUIAutomationValuePattern pattern, string text)
    {
        nint bstr = Marshal.StringToBSTR(text);
        try
        {
            Check(pattern.SetValue(bstr));
        }
        finally
        {
            Marshal.FreeBSTR(bstr);
        }
    }

    internal static List<string> SelectionNames(IUIAutomationSelectionPattern pattern)
    {
        Check(pattern.GetCurrentSelection(out var array));
        Check(array.get_Length(out int length));
        var names = new List<string>(length);
        for (int i = 0; i < length; i++)
        {
            Check(array.GetElement(i, out var element));
            names.Add(Name(element));
        }
        return names;
    }

    internal static string ControlTypeName(int controlType)
    {
        return controlType switch
        {
            ButtonControl => "Button",
            EditControl => "Edit",
            ListControl => "List",
            ListItemControl => "ListItem",
            MenuItemControl => "MenuItem",
            WindowControl => "Window",
            TabControl => "Tab",
            TabItemControl => "TabItem",
            50002 => "CheckBox",
            50020 => "Text",
            50009 => "Menu",
            _ => controlType.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    internal static void Check(int hr)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private static string TakeBstr(nint bstr)
    {
        if (bstr == 0)
        {
            return "";
        }

        string text = Marshal.PtrToStringBSTR(bstr);
        Marshal.FreeBSTR(bstr);
        return text;
    }

    [LibraryImport("ole32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
}
#pragma warning restore CA1707, IDE1006

/// <summary>Brings a window to the foreground from a background process (tests).</summary>
internal static partial class Foreground
{
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    /// <summary>
    /// Makes <paramref name="hwnd"/> the foreground window. Windows only lets the process that owns
    /// the foreground, or one that just received input, change it — a synthesized Alt press counts as
    /// input, which is the documented way for automation to take the foreground.
    /// </summary>
    internal static bool Activate(nint hwnd)
    {
        keybd_event(VK_MENU, 0, 0, 0);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
        return SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void keybd_event(byte vk, byte scan, uint flags, nuint extraInfo);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetForegroundWindow();
}
