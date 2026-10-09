using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI;

// UI Automation provider interfaces, declared for source-generated COM (AOT and trimming safe: no
// built-in COM marshalling, no reflection). Vtable order and IIDs are taken from the Windows SDK's
// um\UIAutomationCore.idl. Every method is [PreserveSig] so HRESULTs are explicit and no exception
// crosses into UIA; object results are raw COM pointers produced by UiaComObjects.

/// <summary>IRawElementProviderSimple (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal partial interface IRawElementProviderSimple
{
    [PreserveSig]
    int GetProviderOptions(out int options);

    [PreserveSig]
    int GetPatternProvider(int patternId, out nint provider);

    [PreserveSig]
    int GetPropertyValue(int propertyId, out UiaVariant value);

    [PreserveSig]
    int GetHostRawElementProvider(out nint provider);
}

/// <summary>IRawElementProviderFragment (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("f7063da8-8359-439c-9297-bbc5299a7d87")]
internal partial interface IRawElementProviderFragment
{
    [PreserveSig]
    int Navigate(int direction, out nint fragment);

    [PreserveSig]
    int GetRuntimeId(out nint safeArray);

    [PreserveSig]
    int GetBoundingRectangle(out UiaRect rect);

    [PreserveSig]
    int GetEmbeddedFragmentRoots(out nint safeArray);

    [PreserveSig]
    int SetFocus();

    [PreserveSig]
    int GetFragmentRoot(out nint root);
}

/// <summary>IRawElementProviderFragmentRoot (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("620ce2a5-ab8f-40a9-86cb-de3c75599b58")]
internal partial interface IRawElementProviderFragmentRoot
{
    [PreserveSig]
    int ElementProviderFromPoint(double x, double y, out nint fragment);

    [PreserveSig]
    int GetFocus(out nint fragment);
}

/// <summary>IInvokeProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("54fcb24b-e18e-47a2-b4d3-eccbe77599a2")]
internal partial interface IInvokeProvider
{
    [PreserveSig]
    int Invoke();
}

/// <summary>IValueProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("c7935180-6fb3-4201-b174-7df73adbf64a")]
internal partial interface IValueProvider
{
    [PreserveSig]
    int SetValue(nint value);

    [PreserveSig]
    int GetValue(out nint bstr);

    [PreserveSig]
    int GetIsReadOnly(out int readOnly);
}

/// <summary>IRangeValueProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("36dc7aef-33e6-4691-afe1-2be7274b3d33")]
internal partial interface IRangeValueProvider
{
    [PreserveSig]
    int SetValue(double value);

    [PreserveSig]
    int GetValue(out double value);

    [PreserveSig]
    int GetIsReadOnly(out int readOnly);

    [PreserveSig]
    int GetMaximum(out double value);

    [PreserveSig]
    int GetMinimum(out double value);

    [PreserveSig]
    int GetLargeChange(out double value);

    [PreserveSig]
    int GetSmallChange(out double value);
}

/// <summary>IToggleProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("56d00bd0-c4f4-433c-a836-1a52a57e0892")]
internal partial interface IToggleProvider
{
    [PreserveSig]
    int Toggle();

    [PreserveSig]
    int GetToggleState(out int state);
}

/// <summary>ISelectionProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("fb8b03af-3bdf-48d4-bd36-1a65793be168")]
internal partial interface ISelectionProvider
{
    [PreserveSig]
    int GetSelection(out nint safeArray);

    [PreserveSig]
    int GetCanSelectMultiple(out int value);

    [PreserveSig]
    int GetIsSelectionRequired(out int value);
}

/// <summary>ISelectionItemProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("2acad808-b2d4-452d-a407-91ff1ad167b2")]
internal partial interface ISelectionItemProvider
{
    [PreserveSig]
    int Select();

    [PreserveSig]
    int AddToSelection();

    [PreserveSig]
    int RemoveFromSelection();

    [PreserveSig]
    int GetIsSelected(out int value);

    [PreserveSig]
    int GetSelectionContainer(out nint provider);
}

/// <summary>IExpandCollapseProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("d847d3a5-cab0-4a98-8c32-ecb45c59ad24")]
internal partial interface IExpandCollapseProvider
{
    [PreserveSig]
    int Expand();

    [PreserveSig]
    int Collapse();

    [PreserveSig]
    int GetExpandCollapseState(out int state);
}

/// <summary>IScrollItemProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("2360c714-4bf1-4b26-ba65-9b21316127eb")]
internal partial interface IScrollItemProvider
{
    [PreserveSig]
    int ScrollIntoView();
}

/// <summary>IGridProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("b17d6187-0907-464b-a168-0ef17a1572b1")]
internal partial interface IGridProvider
{
    [PreserveSig]
    int GetItem(int row, int column, out nint provider);

    [PreserveSig]
    int GetRowCount(out int value);

    [PreserveSig]
    int GetColumnCount(out int value);
}

/// <summary>IGridItemProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("d02541f1-fb81-4d64-ae32-f520f8a6dbd1")]
internal partial interface IGridItemProvider
{
    [PreserveSig]
    int GetRow(out int value);

    [PreserveSig]
    int GetColumn(out int value);

    [PreserveSig]
    int GetRowSpan(out int value);

    [PreserveSig]
    int GetColumnSpan(out int value);

    [PreserveSig]
    int GetContainingGrid(out nint provider);
}

/// <summary>ITableProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("9c860395-97b3-490a-b52a-858cc22af166")]
internal partial interface ITableProvider
{
    [PreserveSig]
    int GetRowHeaders(out nint safeArray);

    [PreserveSig]
    int GetColumnHeaders(out nint safeArray);

    [PreserveSig]
    int GetRowOrColumnMajor(out int value);
}

/// <summary>ITableItemProvider (UIAutomationCore.idl).</summary>
[GeneratedComInterface]
[Guid("b9734fa6-771f-4d78-9c90-2517999349cd")]
internal partial interface ITableItemProvider
{
    [PreserveSig]
    int GetRowHeaderItems(out nint safeArray);

    [PreserveSig]
    int GetColumnHeaderItems(out nint safeArray);
}

/// <summary>UiaRect: a rectangle in screen (physical pixel) coordinates.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UiaRect
{
    public double Left;
    public double Top;
    public double Width;
    public double Height;
}

/// <summary>UIA ids and constants (UIAutomationClient.h / UIAutomationCoreApi.h).</summary>
internal static class UiaIds
{
    internal const int S_OK = 0;
    internal const int E_INVALIDARG = unchecked((int)0x80070057);
    internal const int E_FAIL = unchecked((int)0x80004005);
    internal const int E_ACCESSDENIED = unchecked((int)0x80070005);
    internal const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);
    internal const int UIA_E_ELEMENTNOTENABLED = unchecked((int)0x80040200);
    internal const int UIA_E_INVALIDOPERATION = unchecked((int)0x80131509);

    internal const int UiaRootObjectId = -25;
    internal const int UiaAppendRuntimeId = 3;

    // ProviderOptions
    internal const int ProviderOptions_ServerSideProvider = 0x2;

    // NavigateDirection
    internal const int NavigateDirection_Parent = 0;
    internal const int NavigateDirection_NextSibling = 1;
    internal const int NavigateDirection_PreviousSibling = 2;
    internal const int NavigateDirection_FirstChild = 3;
    internal const int NavigateDirection_LastChild = 4;

    // Patterns
    internal const int InvokePattern = 10000;
    internal const int SelectionPattern = 10001;
    internal const int ValuePattern = 10002;
    internal const int RangeValuePattern = 10003;
    internal const int ExpandCollapsePattern = 10005;
    internal const int GridPattern = 10006;
    internal const int GridItemPattern = 10007;
    internal const int SelectionItemPattern = 10010;
    internal const int TablePattern = 10012;
    internal const int TableItemPattern = 10013;
    internal const int TogglePattern = 10015;
    internal const int ScrollItemPattern = 10017;

    // Properties
    internal const int ProcessIdProperty = 30002;
    internal const int ControlTypeProperty = 30003;
    internal const int NameProperty = 30005;
    internal const int AcceleratorKeyProperty = 30006;
    internal const int HasKeyboardFocusProperty = 30008;
    internal const int IsKeyboardFocusableProperty = 30009;
    internal const int IsEnabledProperty = 30010;
    internal const int AutomationIdProperty = 30011;
    internal const int ClassNameProperty = 30012;
    internal const int HelpTextProperty = 30013;
    internal const int IsControlElementProperty = 30016;
    internal const int IsContentElementProperty = 30017;
    internal const int IsPasswordProperty = 30019;
    internal const int IsOffscreenProperty = 30022;
    internal const int FrameworkIdProperty = 30024;
    internal const int ValueValueProperty = 30045;
    internal const int ValueIsReadOnlyProperty = 30046;
    internal const int RangeValueValueProperty = 30047;
    internal const int LocalizedControlTypeProperty = 30004;
    internal const int GridRowCountProperty = 30062;
    internal const int GridColumnCountProperty = 30063;
    internal const int GridItemRowProperty = 30064;
    internal const int GridItemColumnProperty = 30065;
    internal const int GridItemRowSpanProperty = 30066;
    internal const int GridItemColumnSpanProperty = 30067;
    internal const int ExpandCollapseStateProperty = 30070;
    internal const int SelectionItemIsSelectedProperty = 30079;
    internal const int ToggleStateProperty = 30086;
    internal const int LiveSettingProperty = 30135;
    internal const int PositionInSetProperty = 30152;
    internal const int SizeOfSetProperty = 30153;
    internal const int HeadingLevelProperty = 30173;
    internal const int IsDialogProperty = 30174;

    // Events
    internal const int StructureChangedEvent = 20002;
    internal const int MenuOpenedEvent = 20003;
    internal const int AutomationFocusChangedEvent = 20005;
    internal const int MenuClosedEvent = 20007;
    internal const int WindowOpenedEvent = 20016;
    internal const int InvokedEvent = 20009;

    // Control types
    internal const int ButtonControl = 50000;
    internal const int CheckBoxControl = 50002;
    internal const int ComboBoxControl = 50003;
    internal const int EditControl = 50004;
    internal const int HyperlinkControl = 50005;
    internal const int ImageControl = 50006;
    internal const int ListItemControl = 50007;
    internal const int ListControl = 50008;
    internal const int MenuControl = 50009;
    internal const int MenuBarControl = 50010;
    internal const int MenuItemControl = 50011;
    internal const int ProgressBarControl = 50012;
    internal const int RadioButtonControl = 50013;
    internal const int ScrollBarControl = 50014;
    internal const int SliderControl = 50015;
    internal const int TabControl = 50018;
    internal const int TabItemControl = 50019;
    internal const int TextControl = 50020;
    internal const int TreeControl = 50023;
    internal const int TreeItemControl = 50024;
    internal const int CustomControl = 50025;
    internal const int GroupControl = 50026;
    internal const int DataItemControl = 50029;
    internal const int WindowControl = 50032;
    internal const int PaneControl = 50033;
    internal const int HeaderControl = 50034;
    internal const int HeaderItemControl = 50035;
    internal const int TableControl = 50036;

    // RowOrColumnMajor
    internal const int RowOrColumnMajor_RowMajor = 0;

    // ToggleState
    internal const int ToggleState_Off = 0;
    internal const int ToggleState_On = 1;
    internal const int ToggleState_Indeterminate = 2;

    // ExpandCollapseState
    internal const int ExpandCollapseState_Collapsed = 0;
    internal const int ExpandCollapseState_Expanded = 1;
    internal const int ExpandCollapseState_LeafNode = 3;

    // StructureChangeType
    internal const int StructureChangeType_ChildrenInvalidated = 2;

    // LiveSetting
    internal const int LiveSetting_Polite = 1;
    internal const int LiveSetting_Assertive = 2;

    // HeadingLevel: HeadingLevel1 = 80051 … HeadingLevel9 = 80059.
    internal const int HeadingLevel1 = 80051;

    // NotificationKind / NotificationProcessing
    internal const int NotificationKind_Other = 4;
    internal const int NotificationProcessing_ImportantAll = 0;
    internal const int NotificationProcessing_All = 2;
    internal const int NotificationProcessing_MostRecent = 3;
}

/// <summary>UIAutomationCore.dll and oleaut32.dll entry points.</summary>
#pragma warning disable CA5392 // System DLLs, resolved from System32 by the loader.
internal static partial class UiaNative
{
    private const string Core = "UIAutomationCore.dll";
    private const string OleAut = "oleaut32.dll";

    internal const ushort VT_I4 = 3;
    internal const ushort VT_UNKNOWN = 13;

    [LibraryImport(Core)]
    internal static partial nint UiaReturnRawElementProvider(nint hwnd, nuint wParam, nint lParam, nint provider);

    [LibraryImport(Core)]
    internal static partial int UiaHostProviderFromHwnd(nint hwnd, out nint provider);

    [LibraryImport(Core)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UiaClientsAreListening();

    [LibraryImport(Core)]
    internal static partial int UiaRaiseAutomationEvent(nint provider, int eventId);

    [LibraryImport(Core)]
    internal static partial int UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId, UiaVariant oldValue, UiaVariant newValue);

    [LibraryImport(Core)]
    internal static unsafe partial int UiaRaiseStructureChangedEvent(nint provider, int structureChangeType, int* runtimeId, int runtimeIdLength);

    [LibraryImport(Core)]
    internal static partial int UiaRaiseNotificationEvent(nint provider, int notificationKind, int notificationProcessing, nint displayString, nint activityId);

    [LibraryImport(Core)]
    internal static partial int UiaDisconnectProvider(nint provider);

    [LibraryImport(Core)]
    internal static partial int UiaDisconnectAllProviders();

    [LibraryImport(OleAut)]
    internal static partial nint SafeArrayCreateVector(ushort vt, int lowerBound, uint elements);

    [LibraryImport(OleAut)]
    internal static unsafe partial int SafeArrayPutElement(nint safeArray, int* index, nint value);

    [LibraryImport(OleAut, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint SysAllocString(string value);

    [LibraryImport(OleAut)]
    internal static partial void SysFreeString(nint bstr);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ClientToScreen(nint hwnd, ref Win32.POINT point);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ScreenToClient(nint hwnd, ref Win32.POINT point);
}
#pragma warning restore CA5392

/// <summary>
/// A blittable VARIANT (24 bytes on 64-bit, 16 on 32-bit, as in oaidl.h) holding the few types
/// UIA properties use. <c>ComVariant</c> is not blittable while runtime marshalling is enabled, and
/// disabling it would change every P/Invoke in the assembly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UiaVariant
{
    internal const ushort VT_EMPTY = 0;
    internal const ushort VT_I4 = 3;
    internal const ushort VT_R8 = 5;
    internal const ushort VT_BSTR = 8;
    internal const ushort VT_BOOL = 11;

    public ushort Type;
    private ushort reserved1;
    private ushort reserved2;
    private ushort reserved3;
    private nint data1;
    private nint data2;

    internal static UiaVariant Empty => default;

    internal static UiaVariant From(int value) => FromInt(value);

    internal static UiaVariant From(bool value) => FromBool(value);

    internal static UiaVariant From(double value) => FromDouble(value);

    internal static UiaVariant From(string value) => FromString(value);

    internal static UiaVariant FromInt(int value)
    {
        var variant = new UiaVariant { Type = VT_I4 };
        *(int*)variant.Payload = value;
        return variant;
    }

    internal static UiaVariant FromBool(bool value)
    {
        var variant = new UiaVariant { Type = VT_BOOL };
        *(short*)variant.Payload = value ? (short)-1 : (short)0;
        return variant;
    }

    internal static UiaVariant FromDouble(double value)
    {
        var variant = new UiaVariant { Type = VT_R8 };
        *(double*)variant.Payload = value;
        return variant;
    }

    /// <summary>A VT_BSTR; the receiver (UIA) frees the string.</summary>
    internal static UiaVariant FromString(string value)
    {
        return new UiaVariant { Type = VT_BSTR, data1 = UiaNative.SysAllocString(value) };
    }

    // The union starts at offset 8 on every platform.
    private byte* Payload => (byte*)Unsafe.AsPointer(ref data1);

    /// <summary>The value as a managed object (tests, diagnostics): string, int, bool, double or null.</summary>
    internal object? ToObject()
    {
        return Type switch
        {
            VT_I4 => *(int*)Payload,
            VT_BOOL => *(short*)Payload != 0,
            VT_R8 => *(double*)Payload,
            VT_BSTR => data1 == 0 ? "" : Marshal.PtrToStringBSTR(data1),
            _ => null,
        };
    }

    /// <summary>Frees a VT_BSTR this variant owns (VariantClear for the types used here).</summary>
    internal void Clear()
    {
        if (Type == VT_BSTR && data1 != 0)
        {
            UiaNative.SysFreeString(data1);
        }
        this = default;
    }
}


/// <summary>
/// A provider failure carrying the HRESULT UIA should see (UIA_E_ELEMENTNOTAVAILABLE and the
/// like); <see cref="UiaContext.Run"/> turns it into that HRESULT.
/// </summary>
#pragma warning disable CA1064 // Never escapes the provider: UiaContext.Run converts it to an HRESULT.
internal sealed class UiaException : Exception
#pragma warning restore CA1064
{
    public UiaException()
    {
    }

    public UiaException(string message)
        : base(message)
    {
    }

    public UiaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public UiaException(string message, int hresult)
        : base(message)
    {
        HResult = hresult;
    }
}
