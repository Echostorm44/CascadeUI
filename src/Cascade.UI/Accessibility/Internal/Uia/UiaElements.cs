using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI;

/// <summary>
/// The logic shared by the window root and every element below it: resolving to a live tree
/// entry, navigation, bounds. COM entry points live on the two [GeneratedComClass] subclasses.
/// </summary>
internal abstract class UiaFragment
{
    protected UiaFragment(UiaContext context)
    {
        Context = context;
    }

    internal UiaContext Context { get; }

    /// <summary>This element's entry in <paramref name="tree"/>, or <see cref="AccessibleTree.None"/> when it is gone.</summary>
    internal abstract int Resolve(AccessibleTree tree);

    /// <summary>Navigation by UIA's NavigateDirection; null at the edges.</summary>
    internal abstract UiaFragment? Navigate(int direction);

    /// <summary>Window-logical bounds; empty when the element is gone.</summary>
    internal abstract Rect LogicalBounds();

    internal static UiaFragment? EntryNeighbour(UiaContext context, AccessibleTree tree, int index, int direction)
    {
        ref readonly var entry = ref tree[index];
        int target = direction switch
        {
            UiaIds.NavigateDirection_Parent => entry.Parent,
            UiaIds.NavigateDirection_NextSibling => entry.NextSibling,
            UiaIds.NavigateDirection_PreviousSibling => entry.PreviousSibling,
            UiaIds.NavigateDirection_FirstChild => entry.FirstChild,
            UiaIds.NavigateDirection_LastChild => entry.LastChild,
            _ => AccessibleTree.None,
        };
        return target == AccessibleTree.None ? null : context.ElementFor(tree, target);
    }
}

/// <summary>
/// The window: UIA's fragment root, hosted in the HWND (UIA takes the window's name, bounds and
/// runtime id from the HWND's own provider and merges our children under it).
/// </summary>
[GeneratedComClass]
internal sealed partial class UiaRootElement : UiaFragment, IRawElementProviderSimple, IRawElementProviderFragment, IRawElementProviderFragmentRoot
{
    internal UiaRootElement(UiaContext context)
        : base(context)
    {
    }

    internal override int Resolve(AccessibleTree tree)
    {
        return 0;
    }

    internal override UiaFragment? Navigate(int direction)
    {
        if (direction is not (UiaIds.NavigateDirection_FirstChild or UiaIds.NavigateDirection_LastChild))
        {
            return null;
        }

        return EntryNeighbour(Context, Context.Tree, 0, direction);
    }

    internal override Rect LogicalBounds()
    {
        return Context.Tree.Root.Bounds;
    }

    /// <summary>The deepest element at a screen point (physical pixels), or null for the window itself.</summary>
    internal UiaFragment? ElementAt(double x, double y)
    {
        var point = Context.Host.FromScreen(x, y);
        var tree = Context.Tree;
        int index = tree.HitTest(point);
        if (index == 0)
        {
            return null;
        }

        var element = Context.ElementFor(tree, index);
        if (element is UiaElement list && tree[index].Node is IListViewNode { SectionCount: 0 } listNode)
        {
            var listBounds = tree[index].Bounds;
            int row = listNode.RowIndexAt(point.Y - listBounds.Y - list.ContentInsetTop());
            if (row >= 0)
            {
                return list.Row(row);
            }
        }
        return element;
    }

    // ── IRawElementProviderSimple ─────────────────────────────────────

    public int GetProviderOptions(out int options)
    {
        options = UiaIds.ProviderOptions_ServerSideProvider;
        return UiaIds.S_OK;
    }

    public int GetPatternProvider(int patternId, out nint provider)
    {
        provider = 0;
        return UiaIds.S_OK;
    }

    public int GetPropertyValue(int propertyId, out UiaVariant value)
    {
        value = propertyId == UiaIds.FrameworkIdProperty ? UiaVariant.From("Cascade") : default;
        return UiaIds.S_OK;
    }

    public int GetHostRawElementProvider(out nint provider)
    {
        provider = 0;
        nint handle = Context.Host.Handle;
        return handle == 0 ? UiaIds.S_OK : UiaNative.UiaHostProviderFromHwnd(handle, out provider);
    }

    // ── IRawElementProviderFragment ───────────────────────────────────

    public int Navigate(int direction, out nint fragment)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            result = UiaComObjects.Fragment(Navigate(direction));
            return UiaIds.S_OK;
        });
        fragment = result;
        return hr;
    }

    public int GetRuntimeId(out nint safeArray)
    {
        // A root hosted in an HWND takes its runtime id from the window.
        safeArray = 0;
        return UiaIds.S_OK;
    }

    public int GetBoundingRectangle(out UiaRect rect)
    {
        // Likewise its bounds.
        rect = default;
        return UiaIds.S_OK;
    }

    public int GetEmbeddedFragmentRoots(out nint safeArray)
    {
        safeArray = 0;
        return UiaIds.S_OK;
    }

    public int SetFocus()
    {
        return UiaIds.S_OK;
    }

    public int GetFragmentRoot(out nint root)
    {
        root = UiaComObjects.FragmentRoot(this);
        return UiaIds.S_OK;
    }

    // ── IRawElementProviderFragmentRoot ───────────────────────────────

    public int ElementProviderFromPoint(double x, double y, out nint fragment)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            result = UiaComObjects.Fragment(ElementAt(x, y));
            return UiaIds.S_OK;
        });
        fragment = result;
        return hr;
    }

    public int GetFocus(out nint fragment)
    {
        nint result = 0;
        int hr = UiaContext.Run(() =>
        {
            result = UiaComObjects.Fragment(Context.FocusedElement());
            return UiaIds.S_OK;
        });
        fragment = result;
        return hr;
    }
}
