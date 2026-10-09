using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Cascade.UI;

/// <summary>Where a UIA tree lives: its window handle and the logical→screen mapping.</summary>
internal interface IUiaHost
{
    /// <summary>The HWND the root provider is hosted in (0 in tests).</summary>
    nint Handle { get; }

    /// <summary>Window-logical rectangle → screen rectangle in physical pixels.</summary>
    UiaRect ToScreen(Rect logical);

    /// <summary>Screen point in physical pixels → window-logical point.</summary>
    Point FromScreen(double x, double y);
}

/// <summary>
/// The UI Automation tree of one window: the semantic snapshot (rebuilt lazily, at most once per
/// frame and only when asked), the provider objects handed to UIA (kept stable across re-renders so
/// a screen reader's element survives an <c>Invalidate()</c>), and the rule that every provider call
/// runs on the UI thread.
/// </summary>
/// <remarks>
/// Nothing here exists until UIA first asks for the window's root provider; an app no assistive
/// technology looks at never builds a tree, allocates a provider or runs any of this per frame.
/// </remarks>
internal sealed class UiaContext
{
    private readonly IUiaHost host;
    private readonly Func<Node?> page;
    private readonly Func<InputDispatcher?> input;
    private readonly Func<Size> viewport;
    private readonly Dictionary<Node, UiaElement> nodeElements = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(MenuLevel Level, int Item), UiaElement> menuElements = [];
    private AccessibleTree? tree;
    private int treeVersion = -1;
    private int version;
    private int nextRuntimeId;
    private UiaRootElement? root;

    internal UiaContext(IUiaHost host, Func<Node?> page, Func<InputDispatcher?> input, Func<Size> viewport)
    {
        this.host = host;
        this.page = page;
        this.input = input;
        this.viewport = viewport;
    }

    internal IUiaHost Host => host;

    internal InputDispatcher? Input => input();

    /// <summary>The fragment root (the window), created on first use.</summary>
    internal UiaRootElement Root => root ??= new UiaRootElement(this);

    /// <summary>Whether the root was ever created (UIA asked for this window).</summary>
    internal bool HasRoot => root is not null;

    /// <summary>How many times the semantic tree was built (tests: proves nothing is built while nobody asks).</summary>
    internal int TreeBuilds { get; private set; }

    /// <summary>Marks the snapshot stale; the next question rebuilds it.</summary>
    internal void Invalidate()
    {
        version++;
    }

    /// <summary>The current semantic tree, rebuilt if the UI changed since it was last built.</summary>
    internal AccessibleTree Tree
    {
        get
        {
            if (tree is null || treeVersion != version)
            {
                tree = AccessibleTree.Build(page(), input(), viewport());
                TreeBuilds++;
                treeVersion = version;
                Prune(tree);
            }
            return tree;
        }
    }

    internal int NewRuntimeId()
    {
        return ++nextRuntimeId;
    }

    /// <summary>The provider for a tree entry (created and remembered on first use).</summary>
    internal UiaFragment ElementFor(AccessibleTree snapshot, int index)
    {
        ref readonly var entry = ref snapshot[index];
        switch (entry.Kind)
        {
            case AccessibleElementKind.Window:
                return Root;

            case AccessibleElementKind.Node:
                return ElementForNode(entry.Node!);

            default:
                var key = (entry.MenuLevel!, entry.Kind == AccessibleElementKind.Menu ? -1 : entry.Item);
                if (!menuElements.TryGetValue(key, out var element))
                {
                    element = entry.Kind == AccessibleElementKind.Menu
                        ? UiaElement.ForMenu(this, entry.MenuLevel!)
                        : UiaElement.ForMenuItem(this, entry.MenuLevel!, entry.Item);
                    menuElements[key] = element;
                }
                return element;
        }
    }

    /// <summary>The provider bound to <paramref name="node"/>.</summary>
    internal UiaElement ElementForNode(Node node)
    {
        if (!nodeElements.TryGetValue(node, out var element))
        {
            element = UiaElement.ForNode(this, node);
            nodeElements[node] = element;
        }
        return element;
    }

    /// <summary>A re-render replaced <paramref name="from"/>: its provider now speaks for <paramref name="to"/>.</summary>
    internal void NodeReplaced(Node from, Node to)
    {
        if (nodeElements.Count == 0 || !nodeElements.Remove(from, out var element))
        {
            return;
        }

        element.Rebind(to);
        nodeElements[to] = element;
    }

    /// <summary>
    /// What has keyboard focus as UIA sees it: the highlighted item of an open menu, the selected
    /// row of a focused list, or the focused node. Null when nothing in the window is focused.
    /// </summary>
    internal UiaFragment? FocusedElement()
    {
        var snapshot = Tree;
        if (input()?.Menu is { IsOpen: true } menu)
        {
            var level = menu.Levels[^1];
            int item = level.Highlighted >= 0 ? level.Highlighted : -1;
            int index = snapshot.IndexOfMenu(level, item);
            if (index == AccessibleTree.None)
            {
                index = snapshot.IndexOfMenu(level, -1);
            }
            return index == AccessibleTree.None ? null : ElementFor(snapshot, index);
        }

        if (FocusManager.FocusedElement is not { } focused)
        {
            return null;
        }

        int focusedIndex = snapshot.IndexOf(focused);
        if (focusedIndex == AccessibleTree.None)
        {
            return null;
        }

        var element = ElementForNode(focused);
        if (focused is IListViewNode { SectionCount: 0 } list && list.SelectedIndex >= 0 && list.SelectedIndex < list.ItemCount)
        {
            return element.Row(list.SelectedIndex);
        }

        return element;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on the UI thread and returns its HRESULT. UIA may call a
    /// provider from its own threads; the node tree is single-threaded, so such calls are marshalled
    /// (and time out rather than hang a screen reader if the UI thread is blocked).
    /// </summary>
    internal static int Run(Func<int> work)
    {
        if (Dispatcher.IsOnUiThread || !Dispatcher.IsInitialized)
        {
            return Guard(work);
        }

        int result = UiaIds.UIA_E_ELEMENTNOTAVAILABLE;
        // Not disposed on timeout: the posted work may still run later and signal it.
#pragma warning disable CA2000 // Disposed on success; on timeout ownership stays with the posted work.
        var done = new ManualResetEventSlim();
#pragma warning restore CA2000
        try
        {
            Dispatcher.Post(() =>
            {
                result = Guard(work);
                done.Set();
            });
        }
        catch (InvalidOperationException)
        {
            return UiaIds.UIA_E_ELEMENTNOTAVAILABLE;
        }

        // The UI thread may be shut down or stuck; never block a screen reader forever.
        if (!done.Wait(TimeSpan.FromSeconds(3)))
        {
            return UiaIds.UIA_E_ELEMENTNOTAVAILABLE;
        }

        done.Dispose();
        return result;
    }

#pragma warning disable CA1031 // A provider must never let an exception unwind into UIA.
    private static int Guard(Func<int> work)
    {
        try
        {
            return work();
        }
        catch (Exception ex)
        {
            return ex.HResult < 0 ? ex.HResult : UiaIds.E_FAIL;
        }
    }
#pragma warning restore CA1031

    /// <summary>Forgets providers whose element left the tree (UIA may still hold them; they then report "not available").</summary>
    private void Prune(AccessibleTree snapshot)
    {
        if (nodeElements.Count > 0)
        {
            List<Node>? gone = null;
            foreach (var node in nodeElements.Keys)
            {
                if (snapshot.IndexOf(node) == AccessibleTree.None)
                {
                    (gone ??= []).Add(node);
                }
            }

            if (gone is not null)
            {
                foreach (var node in gone)
                {
                    nodeElements.Remove(node);
                }
            }
        }

        if (menuElements.Count > 0)
        {
            List<(MenuLevel, int)>? goneMenus = null;
            foreach (var key in menuElements.Keys)
            {
                if (snapshot.IndexOfMenu(key.Level, key.Item) == AccessibleTree.None)
                {
                    (goneMenus ??= []).Add(key);
                }
            }

            if (goneMenus is not null)
            {
                foreach (var key in goneMenus)
                {
                    menuElements.Remove(key);
                }
            }
        }
    }
}

/// <summary>Creates the raw COM pointers providers hand to UIA.</summary>
internal static class UiaComObjects
{
    private static readonly StrategyBasedComWrappers Wrappers = new();

    private static readonly Guid IidSimple = new("d6dd68d1-86fd-4332-8666-9abedea2d24c");
    private static readonly Guid IidFragment = new("f7063da8-8359-439c-9297-bbc5299a7d87");
    private static readonly Guid IidFragmentRoot = new("620ce2a5-ab8f-40a9-86cb-de3c75599b58");

    /// <summary>An AddRef'd IUnknown for a [GeneratedComClass] object (ownership passes to the caller).</summary>
    internal static nint Unknown(object instance)
    {
        return Wrappers.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);
    }

    internal static nint Simple(object? instance)
    {
        return Query(instance, IidSimple);
    }

    internal static nint Fragment(object? instance)
    {
        return Query(instance, IidFragment);
    }

    internal static nint FragmentRoot(object? instance)
    {
        return Query(instance, IidFragmentRoot);
    }

    private static nint Query(object? instance, Guid iid)
    {
        if (instance is null)
        {
            return 0;
        }

        nint unknown = Unknown(instance);
        try
        {
            int hr = Marshal.QueryInterface(unknown, in iid, out nint result);
            return hr >= 0 ? result : 0;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    /// <summary>A SAFEARRAY of VT_I4 holding <paramref name="values"/>.</summary>
    internal static unsafe nint IntArray(ReadOnlySpan<int> values)
    {
        nint array = UiaNative.SafeArrayCreateVector(UiaNative.VT_I4, 0, (uint)values.Length);
        if (array == 0)
        {
            return 0;
        }

        for (int i = 0; i < values.Length; i++)
        {
            int value = values[i];
            int hr = UiaNative.SafeArrayPutElement(array, &i, (nint)(&value));
            if (hr < 0)
            {
                return 0;
            }
        }
        return array;
    }

    /// <summary>A SAFEARRAY of VT_UNKNOWN holding IRawElementProviderSimple pointers for <paramref name="providers"/>.</summary>
    internal static unsafe nint ProviderArray(IReadOnlyList<object> providers)
    {
        nint array = UiaNative.SafeArrayCreateVector(UiaNative.VT_UNKNOWN, 0, (uint)providers.Count);
        if (array == 0)
        {
            return 0;
        }

        for (int i = 0; i < providers.Count; i++)
        {
            nint simple = Simple(providers[i]);
            try
            {
                // SafeArrayPutElement AddRefs a VT_UNKNOWN element itself.
                _ = UiaNative.SafeArrayPutElement(array, &i, simple);
            }
            finally
            {
                if (simple != 0)
                {
                    Marshal.Release(simple);
                }
            }
        }
        return array;
    }
}
