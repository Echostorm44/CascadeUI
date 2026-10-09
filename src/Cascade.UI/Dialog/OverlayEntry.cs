namespace Cascade.UI;

/// <summary>What an <see cref="OverlayEntry"/> is: it decides the chrome, placement and defaults.</summary>
internal enum OverlayKind
{
    /// <summary>A modal dialog (<see cref="Dialog"/>).</summary>
    Dialog,

    /// <summary>A modal sheet against the bottom edge (<see cref="BottomSheet"/>).</summary>
    Sheet,

    /// <summary>A light-dismiss panel anchored to a node or a point (<see cref="Popover"/>).</summary>
    Popover,
}

/// <summary>Completes the task an overlay's caller awaits.</summary>
internal interface IOverlayCompletion
{
    /// <summary>Completes with <paramref name="result"/> (null = dismissed). Later calls are ignored.</summary>
    void TryComplete(object? result);
}

/// <summary>
/// One open overlay: the content component, the chrome that hosts it, its resolved options and
/// its live state (placement, animation, focus bookkeeping). Owned by an
/// <see cref="OverlayManager"/>; everything here is touched on the UI thread only.
/// </summary>
internal sealed class OverlayEntry
{
    // Mutable structs: kept as fields so Advance/SetTarget mutate the entry's copy.
    internal AnimationChannel Presence;
    internal AnimationChannel DragOffset;

    internal OverlayEntry(OverlayManager manager, OverlayKind kind, Component content, IOverlayCompletion completion)
    {
        Manager = manager;
        Kind = kind;
        Content = content;
        Completion = completion;
        content.OverlayEntry = this;
        Context = new DialogContext(this);
        Chrome = new OverlayChrome(this);
    }

    internal OverlayManager Manager { get; }

    internal OverlayKind Kind { get; }

    /// <summary>The caller's component (a custom dialog, or a built-in Alert/Confirm/Prompt view).</summary>
    internal Component Content { get; }

    internal IOverlayCompletion Completion { get; }

    /// <summary>What <c>DialogContext</c> returns inside this overlay.</summary>
    internal DialogContext Context { get; }

    /// <summary>The framework component that wraps <see cref="Content"/> (title, sheet handle, accessibility role).</summary>
    internal OverlayChrome Chrome { get; }

    /// <summary>Hosts <see cref="Chrome"/> (and through it the content) while the overlay is open.</summary>
    internal ComponentHost? Host { get; set; }

    /// <summary>The laid-out panel: the chrome's rendered tree, whose bounds are window-logical.</summary>
    internal Node? Tree => Host?.RenderedTree;

    // ── Resolved options ─────────────────────────────────────────────

    /// <summary>Chrome title drawn above the content (dialogs and sheets), or null.</summary>
    internal string? Title { get; init; }

    /// <summary>The accessible name: the title, or what the caller supplied.</summary>
    internal string? AccessibleLabel { get; init; }

    internal AccessibleRole Role { get; init; } = AccessibleRole.Dialog;

    /// <summary>Escape and a click outside the panel close it.</summary>
    internal bool Dismissable { get; init; } = true;

    internal bool ShowBackdrop { get; init; }

    /// <summary>Backdrop opacity override; null uses the theme's backdrop colour as it is.</summary>
    internal float? BackdropOpacity { get; init; }

    /// <summary>
    /// Whether the overlay is modal: input outside its panel never reaches what is beneath, and
    /// keyboard focus cannot leave it. Dialogs and sheets always are; popovers when asked.
    /// </summary>
    internal bool BlocksInput { get; init; } = true;

    internal DialogSize Size { get; init; } = DialogSize.Auto;

    internal DialogPositionKind Position { get; init; } = DialogPositionKind.Center;

    /// <summary>The node a popover or anchored dialog sits against (kept current across re-renders).</summary>
    internal Node? Anchor { get; set; }

    /// <summary>The window point a popover sits against, when it has no anchor node.</summary>
    internal Point? AnchorPoint { get; init; }

    internal PopoverSide PreferredSide { get; init; } = PopoverSide.Auto;

    internal float OffsetX { get; init; }

    internal float OffsetY { get; init; }

    internal DialogAnimationKind Animation { get; init; } = DialogAnimationKind.Fade;

    internal AnimationModel? EnterModel { get; init; }

    internal AnimationModel? ExitModel { get; init; }

    // ── Live state ────────────────────────────────────────────────────

    /// <summary>Set once the overlay is mounted.</summary>
    internal bool IsOpened { get; set; }

    /// <summary>Closed: its result is delivered and it no longer takes input; it stays until its exit animation ends.</summary>
    internal bool IsClosing { get; set; }

    /// <summary>Laid out at least once, so <see cref="PanelBounds"/> is valid for hit-testing.</summary>
    internal bool IsLaidOut { get; set; }

    /// <summary>Initial focus has been placed (or deliberately left) after the first layout.</summary>
    internal bool FocusInitialized { get; set; }

    /// <summary>What had keyboard focus when the overlay opened; focus returns there on close.</summary>
    internal Node? PreviousFocus { get; set; }

    /// <summary>Whether that focus was keyboard focus (so the ring shows again on return).</summary>
    internal bool PreviousFocusWasKeyboard { get; set; }

    /// <summary>The panel, window-logical, at its resting position (before any animation transform).</summary>
    internal Rect PanelBounds { get; set; }

    /// <summary>The side a popover ended up on after flipping.</summary>
    internal PopoverSide ResolvedSide { get; set; } = PopoverSide.Bottom;

    /// <summary>A bottom sheet is being dragged by its handle.</summary>
    internal bool IsDragging { get; set; }

    /// <summary>Pointer Y where the sheet drag started.</summary>
    internal float DragStartY { get; set; }

    /// <summary>
    /// Presence 0..1 for painting: clamped animation progress (a spring may overshoot; the
    /// transform reads <see cref="AnimationChannel.Current"/> directly for that).
    /// </summary>
    internal float Visibility => Math.Clamp(Presence.Current, 0f, 1f);
}
