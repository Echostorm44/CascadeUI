namespace Cascade.UI;

/// <summary>
/// Base class for all visual elements in the Cascade UI tree.
/// Every element rendered by the framework — controls, layout containers,
/// components — is a Node.
/// </summary>
public abstract class Node
{
    /// <summary>
    /// Represents the absence of a node. Used instead of null throughout
    /// the framework. Conditional rendering uses Node.Empty for the
    /// "nothing here" branch.
    /// </summary>
    public static Node Empty { get; } = new EmptyNode();

    /// <summary>
    /// Internal per-node layout data: modifier values and computed bounds.
    /// </summary>
    internal LayoutNodeData LayoutData { get; } = new();

    // ── Source location (WP-3524, devtools) ──────────────────────────
#if CASCADE_DEVTOOLS
    /// <summary>
    /// Opt-in source-location capture (<c>CASCADE_CAPTURE_SOURCE=1</c>). A PDB
    /// stack walk per node construction has real cost, so it is off by default
    /// even in devtools builds — enable it only when inspecting by source file
    /// (e.g. cascade_find_nodes <c>source_file</c>). Read at static init so the
    /// per-node check is a single field test.
    /// </summary>
    internal static readonly bool SourceCaptureEnabled =
        Environment.GetEnvironmentVariable("CASCADE_CAPTURE_SOURCE") == "1";

    /// <summary>File where this node was constructed (first user-code frame), if captured.</summary>
    internal string? SourceFile { get; private set; }

    /// <summary>Line where this node was constructed, if captured.</summary>
    internal int SourceLine { get; private set; }

    protected Node()
    {
        if (SourceCaptureEnabled)
        {
            CaptureSourceLocation();
        }
    }

    /// <summary>
    /// Walks the construction stack for the first frame outside the framework
    /// assembly — the user's <c>new Control(...)</c> / component site.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming", "IL2026",
        Justification = "Devtools-only opt-in source capture (CASCADE_CAPTURE_SOURCE=1). " +
                        "Missing stack metadata only degrades the diagnostic, never functionality.")]
    private void CaptureSourceLocation()
    {
        var frameworkAsm = typeof(Node).Assembly;
        var trace = new System.Diagnostics.StackTrace(1, fNeedFileInfo: true);
        for (int i = 0; i < trace.FrameCount; i++)
        {
            var frame = trace.GetFrame(i);
            if (frame?.GetMethod()?.DeclaringType?.Assembly == frameworkAsm)
            {
                continue;
            }
            string? file = frame?.GetFileName();
            if (!string.IsNullOrEmpty(file))
            {
                SourceFile = file;
                SourceLine = frame!.GetFileLineNumber();
                return;
            }
        }
    }
#else
    // Fallbacks when source capture is compiled out — kept as instance properties to
    // match the CASCADE_CAPTURE_SOURCE signature (callers use node.SourceFile/Line).
#pragma warning disable CA1822
    internal string? SourceFile => null;
    internal int SourceLine => 0;
#pragma warning restore CA1822
#endif

    // ── Interaction state ─────────────────────────────────────────────

    /// <summary>
    /// True when the pointer is over this node. Set by <see cref="InputDispatcher"/>
    /// during mouse move events. Read by <see cref="Rendering.Internal.NodePainter"/>
    /// to apply theme hover styles.
    /// </summary>
    internal bool IsHovered { get; set; }

    /// <summary>
    /// True when this node is being pressed (mouse down, not yet released).
    /// Set by <see cref="InputDispatcher"/> during mouse down/up events.
    /// Read by <see cref="Rendering.Internal.NodePainter"/> to apply theme pressed styles.
    /// </summary>
    internal bool IsPressed { get; set; }

    /// <summary>
    /// True if this node is an empty placeholder that occupies no layout space.
    /// </summary>
    internal virtual bool IsLayoutEmpty => false;

    /// <summary>
    /// Optional reconciliation key for keyed list diffing. When set,
    /// the <see cref="NodeDiffer"/> matches old and new nodes by key
    /// rather than by position, enabling efficient list reordering.
    /// </summary>
    internal string? ReconciliationKey { get; set; }

    /// <summary>
    /// Hero transition key for this node, or null if not a hero element.
    /// Set by <see cref="NavigationModifiers.NavigationHero{T}"/>.
    /// </summary>
    internal HeroKey? HeroKeyValue { get; set; }

    /// <summary>
    /// The non-generic node ref attached to this node, if any.
    /// Set by <see cref="Ref{T}"/> and populated by the framework
    /// after the node is mounted and laid out.
    /// </summary>
    internal INodeRefInternal? AttachedRef { get; private set; }

    /// <summary>
    /// Attaches a <see cref="NodeRef{T}"/> to this node, allowing the
    /// owning component to obtain a typed reference after mount.
    /// </summary>
    /// <typeparam name="T">The node type. Must match this node's actual type.</typeparam>
    /// <param name="nodeRef">The ref instance to populate when this node is mounted.</param>
    /// <returns>This node, for fluent chaining.</returns>
    public T Ref<T>(NodeRef<T> nodeRef) where T : Node
    {
        ArgumentNullException.ThrowIfNull(nodeRef);

        if (this is not T typed)
        {
            throw new InvalidCastException(
                $"Cannot attach NodeRef<{typeof(T).Name}> to a node of type {GetType().Name}.");
        }

        AttachedRef = nodeRef;
        nodeRef.Node = typed;
        return typed;
    }

    /// <summary>
    /// Sets a reconciliation key on this node for keyed list diffing.
    /// Returns this node for fluent chaining.
    /// </summary>
    public Node Key(string key)
    {
        ReconciliationKey = key;
        return this;
    }

    /// <summary>
    /// A node that renders nothing and occupies no layout space.
    /// </summary>
    private sealed class EmptyNode : Node, NodeDiffer.EmptyNodeMarker
    {
        internal override bool IsLayoutEmpty => true;
    }
}

/// <summary>
/// Non-generic interface for node ref operations used internally
/// by the framework during mount and layout.
/// </summary>
internal interface INodeRefInternal
{
    void SetMounted(Rect bounds);
    void ClearMounted();
}
