using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// Routes platform input events (mouse, keyboard, scroll) to the correct nodes
/// in the visual tree. Performs hit testing, tracks hover/press state, and invokes
/// gesture callbacks and focus management.
/// </summary>
internal sealed partial class InputDispatcher
{
    private const float ExpandIndicatorWidth = 24f;

    private static InputDispatcher? current;

    private Node? rootNode;
    private Node? hoveredNode;
    private Node? pressedNode;
    private Point lastMousePosition;

    // The pointer position before the event being handled (lastMousePosition is already the new one).
    private Point previousMousePosition;

    // Whether lastMousePosition is where the pointer was on the previous event inside the window
    // (false before the first event and after the pointer left), so a move can be told from a jump.
    private bool pointerPositionKnown;
    private bool isMouseDown;

    // Slider drag state — tracked across MouseDown/Move/Up
    private float sliderDragStartX;
    private float sliderDragStartY;

    // A zoomable Image being panned: where the pointer and the pan were when the drag began.
    private Image? imagePanTarget;
    private Point imagePanOrigin;
    private Point imagePanStart;

    // A TextArea being resized by its grip, and the pointer and size when the drag began.
    private TextArea? textAreaResizeTarget;
    private Rect textAreaResizeStart;
    private float sliderDragStartValue;

    // RangeSlider drag state — which thumb (true = max, false = min)
    private bool rangeSliderDragIsMax;
    private float rangeSliderDragStartValue;

    // Open Select dropdown state
    private ISelectNode? openSelect;

    // Open MultiSelect dropdown state
    private IMultiSelectNode? openMultiSelect;

    // Open Combobox dropdown state
    private IComboboxNode? openCombobox;

    // Open DatePicker calendar popup state
    private DatePicker? openDatePicker;

    // Open DateTimePicker popup state
    private DateTimePicker? openDateTimePicker;

    // Open TimePicker popup state
    private TimePicker? openTimePicker;

    // Open MonthPicker popup state
    private MonthPicker? openMonthPicker;


    // Open NotificationBell dropdown state
    private NotificationBell? openNotificationBell;

    // Open DateRangePicker calendar popup state
    private DateRangePicker? openDateRangePicker;

    // Open DataGrid overlay (select dropdown or date popup)
    private ITabularDataNode? openGridOverlay;

    // DataGrid column resize/reorder drag tracking
    private ITabularDataNode? columnResizeTdn;
    private ITabularDataNode? columnReorderTdn;
    private float reorderStartMouseX;
    private float reorderStartMouseY;
    // Pointer position at mouse-down, so a zero-movement release still counts as a tap even when the
    // node under it is a DIFFERENT instance than at press (virtualized ListView/TreeView rows are
    // rebuilt every frame, so ReferenceEquals to the pressed node fails for a legitimate tap).
    private float mouseDownX;
    private float mouseDownY;
    private int reorderPendingCol = -1;
    private bool reorderDragActive;

    // ListView drag-to-reorder tracking (control-level, like column reorder above)
    private IListViewNode? listReorderLv;
    private int listReorderStartRow = -1;
    private float listReorderStartY;
    private bool listReorderActive;

    // ListView swipe-actions tracking (control-level: horizontal drag reveals action
    // buttons; taps on the revealed buttons are hit-tested by computed rects).
    private IListViewNode? swipeLv;
    private int swipeRow = -1;
    private float swipeStartX;
    private float swipeStartY;
    private bool swipeActive;
    private float swipeRawDx;
    private bool swipeButtonPending;
    private bool swipeButtonPendingLeading;
    private int swipeButtonPendingIndex = -1;

    // Drag-and-drop state — tracks cross-node drag from .Draggable() to .DropTarget()
    private bool dragDropPending;       // mouse down on draggable, threshold not yet met
    private bool dragDropActive;        // drag is in progress (threshold exceeded)
    private Node? dragDropSourceNode;   // the node that has .Draggable()
    private object? dragDropPayload;    // the data being dragged
    private float dragDropStartX;       // mouse position at drag start
    private float dragDropStartY;
    private Node? dragDropCurrentTarget; // current drop target under pointer (or null)

    /// <summary>
    /// Current drag mouse position in absolute coordinates — used by NodePainter
    /// to render the drag preview overlay at the pointer location.
    /// </summary>
    internal static Point DragDropMousePosition { get; private set; }

    /// <summary>
    /// The source node being dragged — used by NodePainter to render preview.
    /// </summary>
    internal static Node? DragDropSourceNode { get; private set; }

    /// <summary>
    /// The current valid drop target (or null) — used by NodePainter for feedback.
    /// </summary>
    internal static Node? DragDropTargetNode { get; private set; }

    /// <summary>
    /// Whether a drag-and-drop operation is currently active — used by NodePainter.
    /// </summary>
    internal static bool IsDragDropActive { get; private set; }

    /// <summary>
    /// Current mouse position in viewport coordinates — used by NodePainter
    /// for cursor proximity effects on controls.
    /// </summary>
    internal static Point CurrentMousePosition { get; private set; }

    // Last mouse modifier keys (for passing to click handlers)
    private ModifierKeys lastMouseModifiers;

    // TextInput editing buffer — maintained separately from the stale Bindable
    // because after Invalidate() → re-render, FocusManager still holds the OLD node
    // whose Bindable.Value is frozen at the value from the previous render.
    private string? textInputBuffer;

    // PinInput editing buffer — same reason as textInputBuffer. The Bindable is a
    // readonly struct so its Value freezes at creation time; after re-render the
    // FocusManager holds the OLD PinInput whose Bindable still has the stale value.

    /// <summary>
    /// The current editing buffer value, or null when no TextInput is being edited.
    /// Used by NodePainter to identify the focused TextInput after re-render
    /// replaces the node objects (making the FocusManager reference stale).
    /// </summary>
    internal static string? ActiveEditBuffer { get; private set; }

    // Caret blink timer — reset on focus and on each keystroke so the caret
    // is always visible immediately after user action, then starts blinking.
    internal static long CaretResetTimestamp { get; private set; }

    /// <summary>
    /// Restarts the caret blink (solid for one interval, then blinking until the platform's caret
    /// timeout) and repaints: called when the window is activated, as a native caret is recreated
    /// when its window regains focus. Without it a caret that stopped blinking at the timeout stayed
    /// solid after switching back until the next key press.
    /// </summary>
    internal void RestartCaretBlink()
    {
        CaretResetTimestamp = CaretSettings.Now();
        if (IsCaretActive)
        {
            RequestRepaint?.Invoke();
        }
    }

    // PropertyGrid inline editing state — stored here (not on the node) because
    // PropertyGrid is a Node that gets recreated on every re-render.
    internal static int PropertyGridEditingRow { get; set; } = -1;
    internal static string PropertyGridEditBuffer { get; set; } = "";
    internal static int PropertyGridEditCaret { get; set; }
    internal static PropertyDefinition? PropertyGridEditingProperty { get; set; }

    internal static void BeginPropertyGridEdit(int flatRow, PropertyDefinition prop, string initialValue)
    {
        PropertyGridEditingRow = flatRow;
        PropertyGridEditingProperty = prop;
        PropertyGridEditBuffer = initialValue;
        PropertyGridEditCaret = initialValue.Length;
        CaretResetTimestamp = Stopwatch.GetTimestamp();
    }

    internal static void CancelPropertyGridEdit()
    {
        PropertyGridEditingRow = -1;
        PropertyGridEditingProperty = null;
        PropertyGridEditBuffer = "";
        PropertyGridEditCaret = 0;
    }

    internal static bool CommitPropertyGridEdit()
    {
        var prop = PropertyGridEditingProperty;
        if (prop == null)
        {
            return false;
        }

        string value = PropertyGridEditBuffer;
        CancelPropertyGridEdit();

        switch (prop.EditorKind)
        {
            case PropertyEditorKind.String when prop.Setter is Action<string> setter:
                setter(value);
                return true;

            case PropertyEditorKind.Float when prop.Setter is Action<float> floatSetter:
                if (float.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out float fv))
                {
                    if (prop.MinValue.HasValue)
                    {
                        fv = Math.Max(fv, prop.MinValue.Value);
                    }
                    if (prop.MaxValue.HasValue)
                    {
                        fv = Math.Min(fv, prop.MaxValue.Value);
                    }
                    floatSetter(fv);
                    return true;
                }
                return false;

            case PropertyEditorKind.Int when prop.Setter is Action<int> intSetter:
                if (int.TryParse(value, out int iv))
                {
                    if (prop.MinIntValue.HasValue)
                    {
                        iv = Math.Max(iv, prop.MinIntValue.Value);
                    }
                    if (prop.MaxIntValue.HasValue)
                    {
                        iv = Math.Min(iv, prop.MaxIntValue.Value);
                    }
                    intSetter(iv);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// True when a text-editing control has focus and its caret should be animating.
    /// The frame loop keeps ticking while this is true so the caret blinks at a
    /// steady rate; it must cover every control that paints a blinking caret
    /// (previously only TextInput/MentionInput, which left TextArea, Password, Pin,
    /// Tag, and DataGrid cell edits blinking erratically as the loop woke only on
    /// sporadic events, or not at all).
    /// </summary>
    internal static bool IsCaretActive =>
        FocusManager.FocusedElement is TextInput or MentionInput or TextArea
            or PasswordInput or PinInput or TagInput
        || PropertyGridEditingRow >= 0
        || (FocusManager.FocusedElement is ITabularDataNode { IsEditing: true });

    /// <summary>True when a PasswordInput has focus and the caret should be animating.</summary>
    internal static bool IsPasswordCaretActive => FocusManager.FocusedElement is PasswordInput;

    /// <summary>
    /// The current PasswordInput editing buffer, or null when not editing. Setting it (focusing the
    /// field, or automation writing a value) puts the caret at the end with nothing selected; key
    /// edits go through <see cref="SetPasswordText"/>, which keeps the caret where the edit left it.
    /// </summary>
    internal static string? PasswordEditBuffer
    {
        get => passwordEditBuffer;
        private set
        {
            passwordEditBuffer = value;
            PasswordCaretIndex = value?.Length ?? 0;
            PasswordSelectionAnchor = PasswordCaretIndex;
        }
    }

    private static string? passwordEditBuffer;

    /// <summary>Character index of the caret within <see cref="PasswordEditBuffer"/>.</summary>
    internal static int PasswordCaretIndex { get; private set; }

    /// <summary>Selection anchor for the password field; text between it and the caret is selected.</summary>
    internal static int PasswordSelectionAnchor { get; private set; }

    /// <summary>Whether the password is currently revealed (show/hide toggle).</summary>
    internal static bool PasswordRevealed { get; private set; }

    /// <summary>
    /// Active cell index for the focused PinInput. Stored on the dispatcher
    /// (not the PinInput node) because re-render replaces node objects,
    /// resetting any state stored on them.
    /// </summary>
    internal static int PinActiveCellIndex { get; set; }

    /// <summary>
    /// The current PinInput editing buffer, or null when no PinInput is being edited.
    /// Used by NodePainter to identify the focused PinInput after re-render
    /// replaces the node objects (making the FocusManager reference stale).
    /// </summary>
    internal static string? PinEditBuffer { get; private set; }

    /// <summary>The current TextArea editing buffer, or null when not editing.</summary>
    internal static string? TextAreaEditBuffer { get; private set; }

    /// <summary>True when a TextArea has focus and the caret should be animating.</summary>
    internal static bool IsTextAreaCaretActive => FocusManager.FocusedElement is TextArea;

    /// <summary>Character index of the caret within TextAreaEditBuffer.</summary>
    internal static int TextAreaCaretIndex { get; private set; }

    /// <summary>Selection anchor for TextArea. When different from TextAreaCaretIndex, text is selected.</summary>
    internal static int TextAreaSelectionAnchor { get; private set; }

    /// <summary>Current vertical scroll offset for the focused TextArea (logical pixels).</summary>
    internal static float TextAreaScrollOffsetY { get; set; }

    /// <summary>Maximum vertical scroll offset for the focused TextArea (set by painter).</summary>
    internal static float TextAreaMaxScrollY { get; set; }

    /// <summary>Absolute bounds of the active TextArea, updated each paint frame by NodePainter.</summary>
    internal static Rect TextAreaAbsoluteBounds { get; set; }

    /// <summary>Last caret index that triggered auto-scroll. Prevents manual scroll from being overridden.</summary>
    internal static int TextAreaLastAutoScrollCaret { get; set; } = -1;

    /// <summary>Absolute bounds of the TextArea scrollbar track, set by NodePainter for drag hit testing.</summary>
    internal static Rect TextAreaScrollbarTrackBounds { get; set; }

    /// <summary>Height of the TextArea scrollbar thumb in logical pixels.</summary>
    internal static float TextAreaScrollbarThumbHeight { get; set; }

    // ── Wrapped-layout parameters stamped by the painter each frame ────────
    // The painter is the source of truth for how the focused TextArea's text is
    // laid out (soft word-wrap via TextLayoutEngine). It stamps the exact layout
    // parameters here so caret hit-testing, vertical navigation, and Home/End in
    // this dispatcher rebuild an identical layout (a cache hit) — painter and
    // input never disagree on where lines wrap.

    /// <summary>Font path the painter used for the focused TextArea's text.</summary>
    internal static string? TextAreaFontPath { get; set; }

    /// <summary>Font size the painter used for the focused TextArea's text.</summary>
    internal static float TextAreaFontSize { get; set; }

    /// <summary>Content width (bounds width minus horizontal padding) the painter wrapped to.</summary>
    internal static float TextAreaContentWidth { get; set; }

    /// <summary>Horizontal content padding the painter used.</summary>
    internal static float TextAreaPaddingH { get; set; } = 12f;

    /// <summary>Vertical content padding the painter used.</summary>
    internal static float TextAreaPaddingV { get; set; } = 8f;

    /// <summary>
    /// Builds the wrapped text layout for the focused TextArea using the parameters
    /// the painter stamped. Returns null when no layout parameters are available yet
    /// (e.g. before the first paint) so callers can fall back gracefully.
    /// </summary>
    internal static TextLayoutResult? GetTextAreaLayout(string text)
    {
        string? fontPath = TextAreaFontPath ?? LayoutSolver.DefaultFontPath;
        if (string.IsNullOrEmpty(fontPath) || string.IsNullOrEmpty(text))
        {
            return null;
        }

        float fontSize = TextAreaFontSize > 0 ? TextAreaFontSize : 17f;
        float maxWidth = TextAreaContentWidth > 0 ? TextAreaContentWidth : float.PositiveInfinity;

        var options = new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = fontSize,
            MaxWidth = maxWidth,
            MaxLines = 0,
            Overflow = TextOverflow.Clip,
        };
        return TextLayoutEngine.Layout(text, options);
    }

    /// <summary>Character index of the caret within textInputBuffer.</summary>
    internal static int TextInputCaretIndex { get; private set; }

    /// <summary>Selection anchor for TextInput. When different from TextInputCaretIndex, text is selected.</summary>
    internal static int TextInputSelectionAnchor { get; private set; }

    /// <summary>Current horizontal scroll offset for the focused TextInput (logical pixels).</summary>
    internal static float TextInputScrollOffsetX { get; set; }

    /// <summary>
    /// The in-progress typing buffer for the focused NumberInput, or null when the user is not
    /// typing into one (value shown from the bound value; stepper/scroll edits leave this null).
    /// Committed to the bound value on Enter or blur; the painter renders it while non-null.
    /// </summary>
    internal static string? NumberEditBuffer { get; private set; }

    /// <summary>Caret index within <see cref="NumberEditBuffer"/>.</summary>
    internal static int NumberInputCaretIndex { get; private set; }

    /// <summary>Current horizontal scroll offset for the focused PasswordInput (logical pixels).</summary>
    internal static float PasswordScrollOffsetX { get; set; }

    // ── MentionInput editing state ────────────────────────────────────

    /// <summary>The current MentionInput editing buffer, or null when not editing.</summary>
    internal static string? MentionEditBuffer { get; private set; }

    /// <summary>Character index of the caret within MentionEditBuffer.</summary>
    internal static int MentionInputCaretIndex { get; private set; }

    /// <summary>Selection anchor for MentionInput.</summary>
    internal static int MentionInputSelectionAnchor { get; private set; }

    // ── ScrollView state ──────────────────────────────────────────────
    // Maintained across re-renders like other interaction state (openSelect, textInputBuffer).
    // Supports one active ScrollView — adequate for typical single-scroll layouts.

    /// <summary>Current vertical scroll offset in logical pixels.</summary>
    internal static float ScrollViewOffsetY { get; set; }

    /// <summary>Maximum vertical scroll offset (contentHeight − viewportHeight). Set by LayoutSolver.</summary>
    internal static float ScrollViewMaxY { get; set; }

    /// <summary>Absolute bounds of the scrollbar track, set by NodePainter for drag hit testing.</summary>
    internal static Rect ScrollbarTrackBounds { get; set; }

    /// <summary>Height of the scrollbar thumb in logical pixels.</summary>
    internal static float ScrollbarThumbHeight { get; set; }

    // Scrollbar drag state
    private bool scrollbarDragging;
    private float scrollbarDragStartY;
    private float scrollbarDragStartOffset;
    private ScrollView? scrollbarDragTarget;

    // Whether the scrollbar being dragged is the horizontal one (scrollbarDragStartY then holds the start x).
    private bool scrollbarDragHorizontal;
    private IListViewNode? listScrollbarDragTarget;

    // TextArea scrollbar drag state
    private bool textAreaScrollbarDragging;
    private float textAreaScrollbarDragStartY;
    private float textAreaScrollbarDragStartOffset;

    // SplitView divider drag state
    private bool splitDividerDragging;
    private float splitDragStartMousePos;
    private float splitDragStartFirstSize;
    private SplitOrientation splitDragOrientation;
    private int splitDragTargetIndex;

    /// <summary>
    /// Persisted split view first-pane sizes (in pixels), keyed by layout traversal index.
    /// Survives re-renders since node objects are recreated each frame.
    /// </summary>
    private static readonly Dictionary<int, float> splitViewPositions = new();

    /// <summary>
    /// Layout counter incremented by LayoutSolver for each SplitView encountered.
    /// Reset at the start of each layout pass via PerformLayout.
    /// </summary>
    internal static int SplitViewLayoutCounter { get; set; }

    /// <summary>
    /// Gets the overridden first-pane pixel size for a SplitView at the given layout index,
    /// or null if no override exists (user hasn't dragged this divider).
    /// </summary>
    internal static float? GetSplitViewOverride(int layoutIndex)
    {
        return splitViewPositions.TryGetValue(layoutIndex, out float val) ? val : null;
    }

    /// <summary>
    /// Callback to request a visual repaint when interaction state changes
    /// (hover enter/leave, press/release). Set by <see cref="FrameOrchestrator"/>
    /// during initialization.
    /// </summary>
    internal Action? RequestRepaint { get; set; }

    /// <summary>
    /// Callback to request a cursor change. The parameter is the cursor kind:
    /// 0 = default arrow, 1 = horizontal resize (SizeWE), 2 = vertical resize (SizeNS).
    /// Set by the platform window during initialization.
    /// </summary>
    internal Action<int>? RequestCursorChange { get; set; }

    /// <summary>
    /// Starts a window move that runs until the mouse button is released (a press on a
    /// <c>.WindowDragArea()</c>). Null where the window cannot be moved this way.
    /// </summary>
    internal Action? BeginWindowDrag { get; set; }

    /// <summary>Takes (true) or gives back the mouse, so presses outside the window come here (the eye dropper).</summary>
    internal Action<bool>? RequestPointerCapture { get; set; }

    // A colour picker whose eye dropper is waiting for a press.
    private ColorPicker? activeEyeDropper;

    private int currentCursorKind;

    /// <summary>
    /// Updates the root node reference. Called after each layout pass since
    /// the node tree may have changed.
    /// </summary>
    internal void SetRoot(Node? root)
    {
        rootNode = root;
        mainRoot = root;
        current = this;
    }

    /// <summary>
    /// Called by the reconciler when an old node is replaced by a new node of the
    /// same type. Updates internal pressed/hovered references so that click detection
    /// (which uses ReferenceEquals) continues to work across re-renders.
    /// </summary>
    internal static void NotifyNodeReplaced(Node oldNode, Node newNode)
    {
        var self = current;
        if (self is null)
        {
            return;
        }

        self.Overlays?.NotifyNodeReplaced(oldNode, newNode);
        TransferLabelSelection(oldNode, newNode);

        if (ReferenceEquals(self.pressedNode, oldNode))
        {
            self.pressedNode = newNode;
        }

        if (ReferenceEquals(self.hoveredNode, oldNode))
        {
            self.hoveredNode = newNode;
        }

        if (ReferenceEquals(self.dragDropSourceNode, oldNode))
        {
            self.dragDropSourceNode = newNode;
        }

        if (ReferenceEquals(self.dragDropCurrentTarget, oldNode))
        {
            self.dragDropCurrentTarget = newNode;
        }

        if (ReferenceEquals(self.imagePanTarget, oldNode) && newNode is Image newPanTarget)
        {
            self.imagePanTarget = newPanTarget;
        }

        if (ReferenceEquals(self.textAreaResizeTarget, oldNode) && newNode is TextArea newResizeTarget)
        {
            self.textAreaResizeTarget = newResizeTarget;
        }

        if (ReferenceEquals(self.openDatePicker, oldNode) && newNode is DatePicker newDp)
        {
            self.openDatePicker = newDp;
        }

        if (ReferenceEquals(self.openDateTimePicker, oldNode) && newNode is DateTimePicker newDtp)
        {
            self.openDateTimePicker = newDtp;
        }

        if (ReferenceEquals(self.openDateRangePicker, oldNode) && newNode is DateRangePicker newDrp)
        {
            self.openDateRangePicker = newDrp;
        }

        if (self.menu is { } openMenu && ReferenceEquals(openMenu.Owner, oldNode))
        {
            openMenu.Owner = newNode;
        }

        // A scrolling toolbar keeps its place across a re-render.
        if (oldNode is ToolBar oldToolBar && newNode is ToolBar newToolBar)
        {
            newToolBar.ScrollOffset = oldToolBar.ScrollOffset;
        }

        // A menu bar keeps its open menu, keyboard focus and underlines across a re-render.
        if (oldNode is MenuBar oldBar && newNode is MenuBar newBar)
        {
            newBar.AdoptStateFrom(oldBar);
            if (ReferenceEquals(self.activeMenuBar, oldBar))
            {
                self.activeMenuBar = newBar;
            }
        }

        // Controlled input: when the app itself changes the value bound to the focused TextInput
        // (clearing a search box, say), the edit buffer adopts it; otherwise the field would keep
        // showing, and later commit, the stale text. Only a change between renders counts, so a
        // binding that ignores edits does not wipe what the user is typing.
        if (oldNode is TextInput oldInput && newNode is TextInput newInput
            && self.textInputBuffer is { } buffer
            && ReferenceEquals(FocusManager.FocusedElement, newInput)
            && newInput.Value.Value is { } value
            && value != buffer
            && value != oldInput.Value.Value)
        {
            self.textInputBuffer = value;
            ActiveEditBuffer = value;
            TextInputCaretIndex = value.Length;
            TextInputSelectionAnchor = value.Length;
        }
    }

    // ── Mouse events ──────────────────────────────────────────────────

    /// <summary>
    /// Handles a mouse event from the platform layer.
    /// </summary>
    internal void HandleMouseEvent(NativeMouseEvent evt)
    {
        ForgetLiveBounds();
        if (rootNode == null)
        {
            return;
        }

        current = this;
        LastInputWasKeyboard = false;
        if (evt.Type == NativeMouseEventType.MouseDown)
        {
            NoteMenuBarPointerDown();
        }

        // An open menu is modal for the pointer: it hovers, activates or dismisses first.
        if (HandleMenuMouse(evt))
        {
            return;
        }

        // Open dialogs, sheets and popovers take the pointer next (see InputDispatcher.Overlays).
        if (RouteOverlayMouse(evt))
        {
            return;
        }

        DispatchMouse(evt);
    }

    /// <summary>
    /// Pointer dispatch against <see cref="rootNode"/> — the page, or an overlay's tree while
    /// <see cref="RouteOverlayMouse"/> has swapped it in.
    /// </summary>
    private void DispatchMouse(NativeMouseEvent evt)
    {
        if (rootNode is null)
        {
            return;
        }

        // A zoomable image (.ZoomPan): dragging pans it, a double-click zooms in (or back out).
        if (imagePanTarget is { } panning)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                panning.Pan = new Point(imagePanStart.X + evt.X - imagePanOrigin.X, imagePanStart.Y + evt.Y - imagePanOrigin.Y);
                RequestRepaint?.Invoke();
            }
            else if (evt.Type == NativeMouseEventType.MouseUp)
            {
                imagePanTarget = null;
            }
            return;
        }
        if (evt.Type == NativeMouseEventType.MouseDown && evt.Button == NativeMouseButton.Left
            && HitTester.HitTest(rootNode, evt.X, evt.Y) is Image { ZoomPanEnabled: true } zoomable)
        {
            if (evt.ClickCount >= 2)
            {
                if (Math.Abs(zoomable.Zoom - 1f) > 0.001f)
                {
                    zoomable.Zoom = 1f;
                    zoomable.Pan = default;
                }
                else
                {
                    zoomable.ZoomAbout(zoomable.DoubleTapZoom, evt.X, evt.Y);
                }
                RequestRepaint?.Invoke();
                return;
            }
            imagePanTarget = zoomable;
            imagePanOrigin = new Point(evt.X, evt.Y);
            imagePanStart = zoomable.Pan;
            return;
        }

        // Dragging a TextArea's resize grip (.Resizable) captures the pointer until release.
        if (textAreaResizeTarget is { } resizing)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                if (resizing.ResizeDir is ResizeDirection.Horizontal or ResizeDirection.Both)
                {
                    resizing.ResizedWidth = Math.Max(80f, textAreaResizeStart.Width + evt.X - textAreaResizeStart.X);
                }
                if (resizing.ResizeDir is ResizeDirection.Vertical or ResizeDirection.Both)
                {
                    resizing.ResizedHeight = Math.Max(38f, textAreaResizeStart.Height + evt.Y - textAreaResizeStart.Y);
                }
                RequestRepaint?.Invoke();
            }
            else if (evt.Type == NativeMouseEventType.MouseUp)
            {
                textAreaResizeTarget = null;
            }
            return;
        }
        if (evt.Type == NativeMouseEventType.MouseDown && evt.Button == NativeMouseButton.Left
            && HitTester.HitTest(rootNode, evt.X, evt.Y) is TextArea { IsResizable: true, IsDisabled: false } gripArea)
        {
            var area = Live(gripArea, gripArea.AbsoluteBounds);
            if (evt.X >= area.Right - TextArea.GripSize && evt.Y >= area.Bottom - TextArea.GripSize)
            {
                textAreaResizeTarget = gripArea;
                textAreaResizeStart = new Rect(evt.X, evt.Y, area.Width, area.Height);
                return;
            }
        }

        // A ListView's scrollbar drag: the same, against the list's own offset.
        if (scrollbarDragging && listScrollbarDragTarget is { } dragList)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                float trackRange = dragList.ScrollbarTrackBounds.Height - dragList.ScrollbarThumbHeight;
                if (trackRange > 0)
                {
                    float newOffset = Math.Clamp(scrollbarDragStartOffset + (evt.Y - scrollbarDragStartY) / trackRange * dragList.MaxY, 0f, dragList.MaxY);
                    if (Math.Abs(newOffset - dragList.OffsetY) > 0.001f)
                    {
                        dragList.OffsetY = newOffset;
                        RequestRepaint?.Invoke();
                    }
                }
            }
            else if (evt.Type == NativeMouseEventType.MouseUp)
            {
                scrollbarDragging = false;
                listScrollbarDragTarget = null;
            }
            return;
        }

        // Scrollbar drag — intercept all mouse events while dragging
        if (scrollbarDragging && scrollbarDragTarget != null)
        {
            if (evt.Type == NativeMouseEventType.MouseMove && scrollbarDragHorizontal)
            {
                var across = scrollbarDragTarget;
                float range = across.HorizontalScrollbarTrackBounds.Width - across.HorizontalScrollbarThumbWidth;
                if (range > 0 && across.Control.MoveTo(scrollbarDragStartOffset + (evt.X - scrollbarDragStartY) / range * across.MaxX, across.OffsetY))
                {
                    RequestRepaint?.Invoke();
                }
                return;
            }

            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                float trackRange = scrollbarDragTarget.ScrollbarTrackBounds.Height - scrollbarDragTarget.ScrollbarThumbHeight;
                if (trackRange > 0)
                {
                    float deltaY = evt.Y - scrollbarDragStartY;
                    float ratio = deltaY / trackRange;
                    float newOffset = Math.Clamp(scrollbarDragStartOffset + ratio * scrollbarDragTarget.MaxY, 0f, scrollbarDragTarget.MaxY);
                    if (Math.Abs(newOffset - scrollbarDragTarget.OffsetY) > 0.001f)
                    {
                        scrollbarDragTarget.OffsetY = newOffset;
                        ScrollViewOffsetY = newOffset;
                        ScrollViewMaxY = scrollbarDragTarget.MaxY;
                        RequestRepaint?.Invoke();
                    }
                }

                return;
            }

            if (evt.Type == NativeMouseEventType.MouseUp)
            {
                scrollbarDragging = false;
                scrollbarDragTarget = null;
                return;
            }

            return;
        }

        // SplitView divider drag — intercept all mouse events while dragging
        if (splitDividerDragging)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                float currentPos = splitDragOrientation == SplitOrientation.Horizontal
                    ? evt.X : evt.Y;
                float delta = currentPos - splitDragStartMousePos;
                float newFirstSize = Math.Max(0f, splitDragStartFirstSize + delta);
                splitViewPositions[splitDragTargetIndex] = newFirstSize;
                RequestRepaint?.Invoke();
                return;
            }

            if (evt.Type == NativeMouseEventType.MouseUp)
            {
                splitDividerDragging = false;
                return;
            }

            return;
        }

        // TextArea scrollbar drag — intercept all mouse events while dragging
        if (textAreaScrollbarDragging)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                float trackRange = TextAreaScrollbarTrackBounds.Height - TextAreaScrollbarThumbHeight;
                if (trackRange > 0)
                {
                    float deltaY = evt.Y - textAreaScrollbarDragStartY;
                    float ratio = deltaY / trackRange;
                    float newOffset = Math.Clamp(textAreaScrollbarDragStartOffset + ratio * TextAreaMaxScrollY, 0f, TextAreaMaxScrollY);
                    if (Math.Abs(newOffset - TextAreaScrollOffsetY) > 0.001f)
                    {
                        TextAreaScrollOffsetY = newOffset;
                        CaretResetTimestamp = Stopwatch.GetTimestamp();
                        RequestRepaint?.Invoke();
                    }
                }

                return;
            }

            if (evt.Type == NativeMouseEventType.MouseUp)
            {
                textAreaScrollbarDragging = false;
                return;
            }

            return;
        }

        // Drag-and-drop intercept — route all events while drag is active
        if (dragDropActive)
        {
            if (evt.Type == NativeMouseEventType.MouseMove)
            {
                DragDropMousePosition = new Point(evt.X, evt.Y);

                // Find drop target under pointer
                var dropTarget = HitTester.FindDropTargetAt(rootNode, evt.X, evt.Y);

                // Validate the target accepts this payload
                if (dropTarget != null && dragDropPayload != null)
                {
                    var dragData = dropTarget.LayoutData.DragData;
                    if (dragData?.Accepts != null && !dragData.Accepts(dragDropPayload))
                    {
                        dropTarget = null;
                    }
                }

                if (!ReferenceEquals(dropTarget, dragDropCurrentTarget))
                {
                    dragDropCurrentTarget = dropTarget;
                    DragDropTargetNode = dropTarget;
                    DragState.UpdateDragOver(dropTarget);
                }

                RequestRepaint?.Invoke();
                return;
            }

            if (evt.Type == NativeMouseEventType.MouseUp)
            {
                // Execute drop if over a valid target
                if (dragDropCurrentTarget != null && dragDropPayload != null)
                {
                    var dragData = dragDropCurrentTarget.LayoutData.DragData;
                    if (dragData?.OnDrop != null)
                    {
                        var targetBounds = Live(dragDropCurrentTarget, dragData.AbsoluteBounds);
                        var dropPos = new DropPosition
                        {
                            Point = new Point(evt.X - targetBounds.X, evt.Y - targetBounds.Y),
                            Index = 0
                        };
                        dragData.OnDrop(dragDropPayload, dropPos);
                    }
                }

                // End drag
                DragState.EndDrag();
                dragDropActive = false;
                dragDropPending = false;
                dragDropSourceNode = null;
                dragDropPayload = null;
                dragDropCurrentTarget = null;
                DragDropSourceNode = null;
                DragDropTargetNode = null;
                IsDragDropActive = false;
                isMouseDown = false;
                pressedNode = null;
                RequestRepaint?.Invoke();
                return;
            }

            return;
        }

        // Start TextArea scrollbar drag on mouse down in TextArea scrollbar track
        if (evt.Type == NativeMouseEventType.MouseDown
            && TextAreaScrollbarTrackBounds.Width > 0
            && TextAreaScrollbarTrackBounds.Contains(new Point(evt.X, evt.Y)))
        {
            textAreaScrollbarDragging = true;
            textAreaScrollbarDragStartY = evt.Y;
            textAreaScrollbarDragStartOffset = TextAreaScrollOffsetY;
            return;
        }

        // Start scrollbar drag on mouse down in a scrollbar track area.
        // Find the ScrollView under the cursor and check its track bounds.
        if (evt.Type == NativeMouseEventType.MouseDown)
        {
            var targetSv = HitTester.FindScrollViewAt(rootNode, evt.X, evt.Y);
            if (targetSv != null && targetSv.HorizontalScrollbarTrackBounds.Width > 0
                && targetSv.HorizontalScrollbarTrackBounds.Contains(new Point(evt.X, evt.Y)))
            {
                scrollbarDragging = true;
                scrollbarDragHorizontal = true;
                scrollbarDragStartY = evt.X;
                scrollbarDragStartOffset = targetSv.OffsetX;
                scrollbarDragTarget = targetSv;
                return;
            }
            if (targetSv != null && targetSv.ScrollbarTrackBounds.Width > 0
                && targetSv.ScrollbarTrackBounds.Contains(new Point(evt.X, evt.Y)))
            {
                scrollbarDragHorizontal = false;
                scrollbarDragging = true;
                scrollbarDragStartY = evt.Y;
                scrollbarDragStartOffset = targetSv.OffsetY;
                scrollbarDragTarget = targetSv;
                return;
            }

            if (HitTester.FindScrollableListViewAt(rootNode, evt.X, evt.Y) is { } targetList
                && targetList.ScrollbarTrackBounds.Width > 0
                && targetList.ScrollbarTrackBounds.Contains(new Point(evt.X, evt.Y)))
            {
                scrollbarDragging = true;
                scrollbarDragStartY = evt.Y;
                scrollbarDragStartOffset = targetList.OffsetY;
                listScrollbarDragTarget = targetList;
                return;
            }
        }

        // Real pointer movement, as opposed to a move Windows reports at an unchanged position (a
        // window shown or re-stacked under a still pointer) or the first move after entering.
        bool pointerMoved = evt.Type == NativeMouseEventType.MouseMove && pointerPositionKnown
            && (evt.X != lastMousePosition.X || evt.Y != lastMousePosition.Y);
        // Where this event moved from: pan deltas and PointerEventArgs.Delta are measured against it.
        previousMousePosition = pointerPositionKnown ? lastMousePosition : new Point(evt.X, evt.Y);
        pointerPositionKnown = evt.Type != NativeMouseEventType.MouseLeave;

        lastMousePosition = new Point(evt.X, evt.Y);
        CurrentMousePosition = lastMousePosition;
        var hitNode = HitTester.HitTest(rootNode, evt.X, evt.Y);

        switch (evt.Type)
        {
            case NativeMouseEventType.MouseMove:
                HandleMouseMove(hitNode, evt);
                if (pointerMoved)
                {
                    SelectListRowUnderPointer(evt);
                }
                break;

            case NativeMouseEventType.MouseDown:
                HandleMouseDown(hitNode, evt);
                break;

            case NativeMouseEventType.MouseUp:
                HandleMouseUp(hitNode, evt);
                break;

            case NativeMouseEventType.MouseEnter:
                HandleMouseMove(hitNode, evt);
                break;

            case NativeMouseEventType.MouseLeave:
                HandleMouseLeave();
                break;
        }
    }

    /// <summary>
    /// <see cref="ListView{T}.SelectOnHover"/>: the pointer moved over a row of such a list, so that
    /// row becomes the selection (unless it already is, or a button is held for a drag).
    /// </summary>
    private void SelectListRowUnderPointer(NativeMouseEvent evt)
    {
        if (rootNode is null || isMouseDown
            || HitTester.FindSelectableListViewAt(rootNode, evt.X, evt.Y) is not { SelectsOnHover: true } list)
        {
            return;
        }
        int row = list.RowIndexAt(evt.Y - list.ReorderBounds.Y);
        if (row < 0 || row == list.SelectedIndex)
        {
            return;
        }
        list.SelectIndex(row);
        RequestRepaint?.Invoke();
    }

    private void HandleMouseMove(Node? hitNode, NativeMouseEvent evt)
    {
        if (TryExtendLabelSelection(evt))
        {
            UpdateHoverCursor(hitNode, evt.X, evt.Y);
            return;
        }

        // Update dropdown hover highlighting if a Select is open
        if (openSelect != null)
        {
            UpdateSelectDropdownHover(evt.X, evt.Y);
        }

        // Update dropdown hover highlighting if a MultiSelect is open
        if (openMultiSelect != null)
        {
            UpdateMultiSelectDropdownHover(evt.X, evt.Y);
        }

        // Update dropdown hover highlighting if a Combobox is open
        if (openCombobox != null)
        {
            UpdateComboboxDropdownHover(evt.X, evt.Y);
        }

        // Update calendar day hover highlighting if a DatePicker is open
        if (openDatePicker != null)
        {
            UpdateCalendarHover(openDatePicker, evt.X, evt.Y);
        }

        // Update DateTimePicker hover highlighting
        if (openDateTimePicker != null)
        {
            UpdateDateTimePickerHover(openDateTimePicker, evt.X, evt.Y);
        }

        // Update DateRangePicker hover highlighting
        if (openDateRangePicker != null)
        {
            UpdateDateRangeHover(openDateRangePicker, evt.X, evt.Y);
        }

        // Update MonthPicker hover highlighting
        if (openMonthPicker != null)
        {
            UpdateMonthPickerHover(openMonthPicker, evt.X, evt.Y);
        }

        // Update DataGrid overlay hover
        if (openGridOverlay != null)
        {
            if (openGridOverlay.IsSelectDropdownOpen)
            {
                UpdateDataGridSelectDropdownHover(openGridOverlay, evt.X, evt.Y);
            }
            else if (openGridOverlay.IsDatePopupOpen && openGridOverlay.DatePopupPicker != null)
            {
                UpdateCalendarHover(openGridOverlay.DatePopupPicker, evt.X, evt.Y);
            }
            else if (openGridOverlay.IsColumnChooserOpen)
            {
                UpdateColumnChooserHover(openGridOverlay, evt.X, evt.Y);
            }
        }

        // Update TreeView row hover (custom-painted rows aren't real nodes, so the
        // generic IsHovered path can't reach them).
        UpdateTreeViewHover(hitNode);

        // Evaluated on every move, not only when the hovered node changes: a column border
        // is a few pixels inside a table that is one node, so the pointer crosses onto and off
        // it without the hit node ever changing. Deciding the cursor only on enter/leave meant
        // the resize cursor could never appear for a table border.
        UpdateHoverCursor(hitNode, evt.X, evt.Y);

        // Inline row actions are painted inside the table node; their hover is tracked per action.
        UpdateRowActionHover(hitNode, hoveredNode, evt.X, evt.Y);

        // Tabs, close buttons and overflow buttons are parts of one TabBar node: tracked per part.
        UpdateTabBarHover(hitNode, evt.X, evt.Y);

        // Track enter/leave for hover
        if (!ReferenceEquals(hitNode, hoveredNode))
        {
            var previousHovered = hoveredNode;

            // Clear hover on the old hovered node
            if (hoveredNode != null)
            {
                hoveredNode.IsHovered = false;
                if (hoveredNode is ToolBar oldTb)
                {
                    oldTb.HoveredItemIndex = -1;
                }
                if (hoveredNode is ITabularDataNode oldTdn)
                {
                    oldTdn.HoveredRowIndex = -1;
                    oldTdn.HoveredColIndex = -1;
                }
                if (hoveredNode is INumberInput oldNi)
                {
                    oldNi.HoveredStepperButton = -1;
                }
                InvokePointerLeave(hoveredNode);
            }

            hoveredNode = hitNode;

            // Set hover on the new hovered node
            if (hitNode != null)
            {
                hitNode.IsHovered = true;
                InvokePointerEnter(hitNode);
            }

            // A control inside a cached ScrollView layer needs the layer recaptured to
            // show its hover feedback: hover uses an instant model (no ongoing animation
            // to keep the layer in direct-paint), so a plain repaint would just composite
            // the stale texture. Mark the layer dirty for the control entering AND leaving.
            if (rootNode != null)
            {
                if (previousHovered != null && IsInteractiveNode(previousHovered))
                {
                    MarkScrollViewLayersDirty(rootNode, previousHovered);
                }
                if (hitNode != null && IsInteractiveNode(hitNode))
                {
                    MarkScrollViewLayersDirty(rootNode, hitNode);
                }
            }

            // Hover changed — request a repaint for visual feedback
            RequestRepaint?.Invoke();
        }

        // Update per-button hover index on ToolBar
        if (hitNode is ToolBar tbHover)
        {
            int newIdx = ComputeToolBarItemIndex(tbHover, evt.X, evt.Y);
            if (newIdx != tbHover.HoveredItemIndex)
            {
                tbHover.HoveredItemIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }

        // Update per-label hover index on MenuBar (when closed)
        if (hitNode is MenuBar mbHover && !mbHover.IsOpen)
        {
            int newIdx = ComputeMenuBarLabelIndex(mbHover, evt.X, evt.Y);
            if (newIdx != mbHover.HoveredMenuIndex)
            {
                mbHover.HoveredMenuIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }
        else if (hoveredNode is MenuBar { HoveredMenuIndex: >= 0 } leftBar && !ReferenceEquals(hitNode, leftBar))
        {
            leftBar.HoveredMenuIndex = -1;
            RequestRepaint?.Invoke();
        }

        // Update hovered row on PropertyGrid
        if (hitNode is PropertyGrid pgHover)
        {
            int newRow = ComputePropertyGridRow(pgHover, evt.X, evt.Y);
            if (newRow != pgHover.HoveredRow)
            {
                pgHover.HoveredRow = newRow;
                RequestRepaint?.Invoke();
            }
        }


        // Update hovered item on NotificationBell dropdown
        if (openNotificationBell != null && openNotificationBell.IsOpen)
        {
            var dropBounds = openNotificationBell.DropdownBounds;
            float headerH = openNotificationBell.HeaderHeight;
            float itemH = openNotificationBell.ItemRowHeight;
            int newIdx = -1;

            if (dropBounds.Width > 0 && dropBounds.Contains(new Point(evt.X, evt.Y)))
            {
                if (evt.Y >= dropBounds.Y + headerH)
                {
                    newIdx = (int)((evt.Y - dropBounds.Y - headerH) / itemH);
                    var notifications = openNotificationBell.Notifications.Value;
                    int maxIdx = notifications != null ? Math.Min(notifications.Count, openNotificationBell.MaxVisibleCount) - 1 : -1;
                    if (newIdx > maxIdx)
                    {
                        newIdx = -1;
                    }
                }
            }

            // -2 = hovering bell icon itself
            if (Live(openNotificationBell, openNotificationBell.AbsoluteBounds).Contains(new Point(evt.X, evt.Y)))
            {
                newIdx = -2;
            }

            if (newIdx != openNotificationBell.HoveredIndex)
            {
                openNotificationBell.HoveredIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }
        else if (hitNode is NotificationBell { IsDisabled: false } nbHover)
        {
            int newIdx = Live(nbHover, nbHover.AbsoluteBounds).Contains(new Point(evt.X, evt.Y)) ? -2 : -1;
            if (newIdx != nbHover.HoveredIndex)
            {
                nbHover.HoveredIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }

        // Update day cell hover on inline Calendar
        if (hitNode is Calendar calHover)
        {
            HoverCalendar(calHover, evt.X, evt.Y);
        }

        // Update hovered cell on EmojiPicker
        if (hitNode is EmojiPicker { IsDisabled: false } epHover)
        {
            int newIdx = ComputeEmojiPickerHover(epHover, evt.X, evt.Y);
            if (newIdx != epHover.HoveredIndex)
            {
                epHover.HoveredIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }

        // Update hovered stepper button on NumberInput
        if (hitNode is INumberInput niHover && !niHover.IsDisabled)
        {
            int newBtn = ComputeNumberInputHoveredButton(niHover, evt.X, evt.Y);
            if (newBtn != niHover.HoveredStepperButton)
            {
                niHover.HoveredStepperButton = newBtn;
                RequestRepaint?.Invoke();
            }
        }

        // Update hovered row and column on DataGrid/DataTable
        if (hitNode is ITabularDataNode tdnHover && tdnHover.IsHoverHighlightEnabled)
        {
            int newRow = HitTestTabularRow(tdnHover, evt.X, evt.Y);
            var tdnBounds = Live(tdnHover, tdnHover.AbsoluteBounds);
            float relX = evt.X - tdnBounds.X;
            // Data rows are shifted right by the expand indicator when row detail is enabled
            float dataRelX = tdnHover.HasRowDetail ? relX - ExpandIndicatorWidth : relX;
            int newCol = newRow >= 0 ? HitTestTabularColumn(tdnHover, dataRelX, tdnBounds.Width) : -1;
            if (newRow != tdnHover.HoveredRowIndex || newCol != tdnHover.HoveredColIndex)
            {
                tdnHover.HoveredRowIndex = newRow;
                tdnHover.HoveredColIndex = newCol;
                RequestRepaint?.Invoke();
            }
        }
        else if (hoveredNode is not ITabularDataNode)
        {
            // Moved away from a tabular node — clear hover on any previously hovered one
            // (handled naturally by node leave above)
        }

        // Update hovered column header on DataGrid/DataTable
        if (hitNode is ITabularDataNode tdnHeaderHover)
        {
            var tdnBounds = Live(tdnHeaderHover, tdnHeaderHover.AbsoluteBounds);
            float relY = evt.Y - tdnBounds.Y;
            float headerH = tdnHeaderHover.GetRowHeight() + 4f;
            if (relY >= 0 && relY < headerH)
            {
                float relX = evt.X - tdnBounds.X;
                int newHeaderCol = HitTestTabularColumn(tdnHeaderHover, relX, tdnBounds.Width);
                bool nearBorder = HitTestColumnBorder(tdnHeaderHover, relX, tdnBounds.Width) >= 0;
                if (newHeaderCol != tdnHeaderHover.HoveredHeaderCol || nearBorder != tdnHeaderHover.IsNearColumnBorder)
                {
                    tdnHeaderHover.HoveredHeaderCol = newHeaderCol;
                    tdnHeaderHover.IsNearColumnBorder = nearBorder;
                    RequestRepaint?.Invoke();
                }
            }
            else if (tdnHeaderHover.HoveredHeaderCol >= 0)
            {
                tdnHeaderHover.HoveredHeaderCol = -1;
                tdnHeaderHover.IsNearColumnBorder = false;
                RequestRepaint?.Invoke();
            }
        }

        // Fire PointerMove on the current target
        if (hitNode != null)
        {
            InvokePointerGesture(hitNode, g => g.PointerMove, evt);
        }

        // DataGrid column resize drag
        if (isMouseDown && columnResizeTdn != null)
        {
            float delta = evt.X - columnResizeTdn.ResizeStartMouseX;
            float newWidth = columnResizeTdn.ResizeStartWidth + delta;
            columnResizeTdn.SetColumnWidth(columnResizeTdn.ResizingColumnIndex, newWidth);
            RequestRepaint?.Invoke();
        }
        // DataGrid column reorder drag — activate after threshold
        else if (isMouseDown && columnReorderTdn != null)
        {
            float dx = evt.X - reorderStartMouseX;
            float dy = evt.Y - reorderStartMouseY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (!reorderDragActive && dist < 5f)
            {
                // Haven't exceeded drag threshold — don't show reorder visuals yet
            }
            else
            {
                if (!reorderDragActive)
                {
                    // First time exceeding threshold — initialize reorder visual state
                    reorderDragActive = true;
                    var tdnBoundsInit = Live(columnReorderTdn, columnReorderTdn.AbsoluteBounds);
                    float headerH = columnReorderTdn.GetRowHeight() + 4f;
                    float colW = GetScaledColumnWidths(columnReorderTdn, tdnBoundsInit.Width)[reorderPendingCol];
                    columnReorderTdn.ReorderDragIndex = reorderPendingCol;
                    columnReorderTdn.ReorderDropIndex = reorderPendingCol;
                    columnReorderTdn.ReorderDragX = evt.X;
                    columnReorderTdn.ReorderDragWidth = colW;
                    columnReorderTdn.ReorderHeaderY = tdnBoundsInit.Y;
                    columnReorderTdn.ReorderHeaderHeight = headerH;
                }

                columnReorderTdn.ReorderDragX = evt.X;
                // Determine drop target based on mouse position
                var tdnBounds = Live(columnReorderTdn, columnReorderTdn.AbsoluteBounds);
                float relX = evt.X - tdnBounds.X;
                int dropIdx = HitTestTabularColumn(columnReorderTdn, relX, tdnBounds.Width);
                if (dropIdx < 0)
                {
                    dropIdx = columnReorderTdn.ReorderDragIndex;
                }
                columnReorderTdn.ReorderDropIndex = dropIdx;
                RequestRepaint?.Invoke();
            }
        }
        // ListView swipe actions — a horizontal-dominant drag reveals action buttons.
        else if (isMouseDown && swipeLv != null && !swipeButtonPending
            && (swipeActive
                || (MathF.Abs(evt.X - swipeStartX) >= 5f
                    && MathF.Abs(evt.X - swipeStartX) >= MathF.Abs(evt.Y - swipeStartY))))
        {
            if (!swipeActive)
            {
                swipeActive = true;
                swipeLv.SwipeRowIndex = swipeRow;
                pressedNode = null;        // a drag, not a tap
                listReorderLv = null;      // give up the competing reorder gesture
            }

            float dx = evt.X - swipeStartX;
            swipeRawDx = dx;
            float bw = swipeLv.SwipeButtonWidth;
            float maxTrail = swipeLv.TrailingActionCount(swipeRow) * bw;
            float maxLead = swipeLv.LeadingActionCount(swipeRow) * bw;
            swipeLv.SwipeOffsetX = Math.Clamp(dx, -maxTrail, maxLead);
            RequestRepaint?.Invoke();
        }
        // ListView drag-to-reorder — activates on a vertical-dominant drag.
        else if (isMouseDown && listReorderLv != null)
        {
            float dy = evt.Y - listReorderStartY;
            bool vertOk = listReorderActive
                || (MathF.Abs(dy) >= 5f
                    && (swipeLv == null || MathF.Abs(dy) > MathF.Abs(evt.X - swipeStartX)));
            if (vertOk)
            {
                if (!listReorderActive)
                {
                    listReorderActive = true;
                    listReorderLv.ReorderFromIndex = listReorderStartRow;
                    pressedNode = null; // a drag, not a tap — cancel the pending click/select
                    swipeLv = null;     // give up the competing swipe gesture
                }

                var b = listReorderLv.ReorderBounds;
                float ih = listReorderLv.GetItemHeight();
                // +OffsetY maps the on-screen Y back to an absolute row when scrolled.
                int to = ih > 0 ? (int)((evt.Y - b.Y + listReorderLv.OffsetY) / ih) : listReorderStartRow;
                listReorderLv.ReorderToIndex = Math.Clamp(to, 0, listReorderLv.ItemCount - 1);
                RequestRepaint?.Invoke();
            }
        }
        // If dragging a slider, update its value from the drag delta
        else if (isMouseDown && pressedNode is Slider slider && !slider.IsDisabled && !slider.IsReadOnly)
        {
            UpdateSliderFromDrag(slider, evt.X, evt.Y);
            if (rootNode != null)
            {
                MarkScrollViewLayersDirty(rootNode, slider);
            }
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && pressedNode is RangeSlider rsDrag && !rsDrag.IsDisabled && !rsDrag.IsReadOnly)
        {
            UpdateRangeSliderFromDrag(rsDrag, evt.X);
            if (rootNode != null)
            {
                MarkScrollViewLayersDirty(rootNode, rsDrag);
            }
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && pressedNode is TagInput { DragIndex: >= 0 } tagDrag && TryGetAbsoluteBounds(tagDrag, out var tagDragBounds))
        {
            var local = new Point(evt.X - tagDragBounds.X, evt.Y - tagDragBounds.Y);
            if (!tagDrag.Dragging && (MathF.Abs(local.X - tagDrag.DragStart.X) > 4f || MathF.Abs(local.Y - tagDrag.DragStart.Y) > 4f))
            {
                tagDrag.Dragging = true;
            }
            if (tagDrag.Dragging)
            {
                tagDrag.DropIndex = tagDrag.InsertionIndexAt(local);
                if (rootNode is not null)
                {
                    MarkScrollViewLayersDirty(rootNode, tagDrag);
                }
                RequestRepaint?.Invoke();
            }
        }
        else if (isMouseDown && pressedNode is Calendar { Drag: not null } calDrag)
        {
            DragCalendar(calDrag, evt.X, evt.Y);
        }
        else if (isMouseDown && pressedNode is ColorPicker cpDrag && !cpDrag.IsDisabled)
        {
            DragColorPicker(cpDrag, evt.X, evt.Y);
            if (rootNode != null)
            {
                MarkScrollViewLayersDirty(rootNode, cpDrag);
            }
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && FocusManager.FocusedElement is TextArea taDrag && TextAreaEditBuffer != null)
        {
            // Drag to extend text selection in TextArea
            PositionTextAreaCaretFromMouse(taDrag, evt);
            CaretResetTimestamp = Stopwatch.GetTimestamp();
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && FocusManager.FocusedElement is PasswordInput pwdDrag && PasswordEditBuffer != null)
        {
            // Drag to extend the selection in a password field
            PasswordCaretIndex = PasswordCaretIndexAt(pwdDrag, PasswordEditBuffer, evt.X);
            CaretResetTimestamp = Stopwatch.GetTimestamp();
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && FocusManager.FocusedElement is TextInput tiDrag && textInputBuffer != null)
        {
            // Drag to extend text selection in TextInput
            PositionTextInputCaretFromMouse(tiDrag, evt);
            CaretResetTimestamp = Stopwatch.GetTimestamp();
            RequestRepaint?.Invoke();
        }
        else if (isMouseDown && dragDropPending && !dragDropActive)
        {
            // Check if drag threshold exceeded to start drag-and-drop
            float dx = evt.X - dragDropStartX;
            float dy = evt.Y - dragDropStartY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist >= 5f && dragDropSourceNode != null)
            {
                dragDropActive = true;
                IsDragDropActive = true;
                DragDropSourceNode = dragDropSourceNode;
                DragDropMousePosition = new Point(evt.X, evt.Y);
                DragState.BeginDrag(dragDropPayload);
                RequestRepaint?.Invoke();
            }
        }
        else if (isMouseDown && pressedNode != null)
        {
            var delta = new Point(evt.X - previousMousePosition.X, evt.Y - previousMousePosition.Y);
            InvokeGesture(pressedNode, g => g.Pan, delta);
        }
    }

    private void HandleMouseDown(Node? hitNode, NativeMouseEvent evt)
    {
        isMouseDown = true;
        lastMouseModifiers = evt.Modifiers;
        mouseDownX = evt.X;
        mouseDownY = evt.Y;

        // Typed time applies when the press goes elsewhere (or is dropped if it is not a time).
        if (FocusManager.FocusedElement is TimePicker { InputText: not null } typingTime && !ReferenceEquals(hitNode, typingTime))
        {
            if (!typingTime.CommitInput())
            {
                typingTime.InputText = null;
            }
        }

        // The eye dropper is waiting: this press takes the colour under the pointer, wherever it is.
        if (activeEyeDropper is { } dropper && evt.Button == NativeMouseButton.Left)
        {
            FinishEyeDropper(dropper, take: true);
            isMouseDown = false;
            return;
        }

        // A press in a selectable label selects text there (and is not a tap on its container);
        // a press anywhere else drops the selection.
        if (TryBeginLabelSelection(evt))
        {
            return;
        }

        // A ListView swipe is open: a left click either invokes a revealed action
        // button (fired on release) or, anywhere else, dismisses the swipe.
        if (evt.Button == NativeMouseButton.Left && swipeLv != null && swipeLv.SwipeRowIndex >= 0)
        {
            var hitButton = HitSwipeButton(swipeLv, evt.X, evt.Y);
            if (hitButton is { } hb)
            {
                swipeButtonPending = true;
                swipeButtonPendingLeading = hb.leading;
                swipeButtonPendingIndex = hb.index;
                return;
            }

            CloseSwipe();
            RequestRepaint?.Invoke();
            return;
        }

        // Right-click on a row of a list with an item context menu: select it, open its menu.
        if (evt.Button == NativeMouseButton.Right && TryOpenListViewContextMenuAt(evt))
        {
            ResetPressState();
            return;
        }

        // Right-click on a DataTable/DataGrid row with a row menu: select it, open its menu.
        if (evt.Button == NativeMouseButton.Right && TryOpenTabularContextMenuAt(hitNode, evt))
        {
            ResetPressState();
            return;
        }

        // A press on a window drag area (not on a control inside it) moves the window. The move
        // loop runs to the button's release and consumes it, so the press is forgotten after.
        if (evt.Button == NativeMouseButton.Left && BeginWindowDrag is { } beginWindowDrag
            && rootNode is not null && HitTester.FindWindowDragAreaAt(rootNode, evt.X, evt.Y) is not null)
        {
            ResetPressState();
            beginWindowDrag();
            CancelPointerPress();
            return;
        }

        // Check if the click is on an active toast notification
        if (evt.Button == NativeMouseButton.Left && Toast.HitZones.Count > 0)
        {
            var clickPt = new Point(evt.X, evt.Y);
            for (int i = Toast.HitZones.Count - 1; i >= 0; i--)
            {
                var zone = Toast.HitZones[i];
                if (zone.Bounds.Contains(clickPt))
                {
                    // Check action button first
                    if (zone.OnAction != null && zone.ActionBounds.Width > 0
                        && zone.ActionBounds.Contains(clickPt))
                    {
                        var action = zone.OnAction;
                        Toast.Dismiss(zone.Id);
                        RequestRepaint?.Invoke();
                        action();
                    }
                    else
                    {
                        // Click anywhere else on the toast dismisses it
                        Toast.Dismiss(zone.Id);
                        RequestRepaint?.Invoke();
                    }
                    return;
                }
            }
        }

        // If CommandPalette is open, handle click (close on backdrop, execute on item)
        if (CommandPalette.IsOpen && CommandPalette.Instance != null && evt.Button == NativeMouseButton.Left)
        {
            var cp = CommandPalette.Instance;
            var clickPt2 = new Point(evt.X, evt.Y);

            if (cp.OverlayBounds.Width > 0 && cp.OverlayBounds.Contains(clickPt2))
            {
                // Click inside panel — check if it's on an item
                for (int i = 0; i < cp.ItemBounds.Count; i++)
                {
                    if (cp.ItemBounds[i].Contains(clickPt2))
                    {
                        cp.HighlightedIndex = cp.ScrollOffset + i;
                        cp.ExecuteHighlighted();
                        RequestRepaint?.Invoke();
                        return;
                    }
                }
                // Click in panel but not on an item — ignore (probably search area)
                return;
            }
            else
            {
                // Click outside panel — close
                CommandPalette.Close();
                cp.SearchText = "";
                RequestRepaint?.Invoke();
                return;
            }
        }

        // A click on a row of the focused field's suggestion list accepts it.
        if (evt.Button == NativeMouseButton.Left && FocusManager.FocusedElement is ISuggestionHost { Suggestions.IsOpen: true } clickedHost)
        {
            int row = clickedHost.Suggestions.RowAt(evt.X, evt.Y);
            if (row >= 0)
            {
                AcceptSuggestion(clickedHost, clickedHost.Suggestions.Items[row]);
                swallowNextMouseUp = true;
                RequestRepaint?.Invoke();
                return;
            }
        }

        // If a Select dropdown is open, check if the click is within it
        if (openSelect != null && evt.Button == NativeMouseButton.Left)
        {
            var dropdownBounds = openSelect.DropdownBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(clickPoint))
            {
                // Click is inside the dropdown — select the item (scroll-aware)
                float itemHeight = openSelect.DropdownItemHeight;
                if (itemHeight <= 0)
                {
                    itemHeight = dropdownBounds.Height / Math.Max(1, openSelect.OptionCount);
                }

                int visibleIndex = (int)((evt.Y - dropdownBounds.Y) / itemHeight);
                int index = openSelect.ScrollOffset + visibleIndex;
                if (index >= 0 && index < openSelect.OptionCount)
                {
                    openSelect.SelectIndex(index);
                }

                openSelect = null;
                RequestRepaint?.Invoke();
                return;
            }

            // Click is on the trigger itself — ToggleOpen will close it
            if (hitNode is ISelectNode sel && ReferenceEquals(sel, openSelect))
            {
                // Let the normal flow handle it (InvokeTap → ToggleOpen)
            }
            else
            {
                // Click is outside both trigger and dropdown — close it
                openSelect.Close();
                openSelect = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a MultiSelect dropdown is open, check if the click is within it
        if (openMultiSelect != null && evt.Button == NativeMouseButton.Left)
        {
            var dropdownBounds = openMultiSelect.DropdownBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(clickPoint))
            {
                // Click inside dropdown — toggle the item (stays open)
                float itemHeight = openMultiSelect.DropdownItemHeight;
                if (itemHeight <= 0)
                {
                    itemHeight = dropdownBounds.Height / Math.Max(1, openMultiSelect.OptionCount);
                }

                int visibleIndex = (int)((evt.Y - dropdownBounds.Y) / itemHeight);
                int index = openMultiSelect.ScrollOffset + visibleIndex;
                if (index >= 0 && index < openMultiSelect.OptionCount)
                {
                    openMultiSelect.ToggleItem(index);
                }

                RequestRepaint?.Invoke();
                return;
            }

            // Click is on the trigger itself — ToggleOpen will close it
            if (hitNode is IMultiSelectNode ms && ReferenceEquals(ms, openMultiSelect))
            {
                // Let the normal flow handle it (InvokeTap → ToggleOpen)
            }
            else
            {
                // Click is outside both trigger and dropdown — close it
                openMultiSelect.Close();
                openMultiSelect = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a Combobox dropdown is open, check if the click is within it
        if (openCombobox != null && evt.Button == NativeMouseButton.Left)
        {
            var dropdownBounds = openCombobox.DropdownBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(clickPoint))
            {
                // Click inside dropdown — select the item
                float itemHeight = openCombobox.DropdownItemHeight;
                if (itemHeight <= 0)
                {
                    itemHeight = dropdownBounds.Height / Math.Max(1, openCombobox.FilteredOptionCount);
                }

                int visibleIndex = (int)((evt.Y - dropdownBounds.Y) / itemHeight);
                int index = openCombobox.ScrollOffset + visibleIndex;
                if (index >= 0 && index < openCombobox.FilteredOptionCount)
                {
                    openCombobox.SelectFilteredIndex(index);
                }

                openCombobox = null;
                RequestRepaint?.Invoke();
                return;
            }

            // Click is on the trigger itself — ToggleOpen will close it
            if (hitNode is IComboboxNode cbHit && ReferenceEquals(cbHit, openCombobox))
            {
                // Let the normal flow handle it (InvokeTap → ToggleOpen)
            }
            else
            {
                // Click is outside — commit text and close
                openCombobox.CommitText();
                openCombobox = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a NotificationBell dropdown is open, check if the click is within it
        if (openNotificationBell != null && evt.Button == NativeMouseButton.Left)
        {
            var dropBounds = openNotificationBell.DropdownBounds;
            var bellBounds = Live(openNotificationBell, openNotificationBell.AbsoluteBounds);
            var click = new Point(evt.X, evt.Y);

            if (dropBounds.Width > 0 && dropBounds.Contains(click))
            {
                float headerH = openNotificationBell.HeaderHeight;
                float itemH = openNotificationBell.ItemRowHeight;
                var notifications = openNotificationBell.Notifications.Value;

                // Check if clicking "Mark all read" header area
                if (evt.Y < dropBounds.Y + headerH)
                {
                    if (evt.X > dropBounds.X + dropBounds.Width * 0.5f && openNotificationBell.OnReadAll != null)
                    {
                        openNotificationBell.OnReadAll();
                        openNotificationBell.Close();
                        openNotificationBell = null;
                        RequestRepaint?.Invoke();
                        return;
                    }
                    return;
                }

                // Click on a notification item
                if (notifications != null && notifications.Count > 0)
                {
                    float itemsTop = dropBounds.Y + headerH;
                    int idx = (int)((evt.Y - itemsTop) / itemH);
                    if (idx >= 0 && idx < Math.Min(notifications.Count, openNotificationBell.MaxVisibleCount))
                    {
                        var notif = notifications[idx];
                        if (!notif.IsRead)
                        {
                            openNotificationBell.OnRead?.Invoke(notif);
                        }
                        openNotificationBell.Close();
                        openNotificationBell = null;
                        RequestRepaint?.Invoke();
                        notif.OnClick?.Invoke();
                        return;
                    }
                }

                return;
            }

            // Click on the bell itself — toggle
            if (bellBounds.Contains(click))
            {
                openNotificationBell.Close();
                openNotificationBell = null;
                RequestRepaint?.Invoke();
                return;
            }

            // Click outside — close
            openNotificationBell.Close();
            openNotificationBell = null;
            RequestRepaint?.Invoke();
        }

        // If a DatePicker calendar is open, check if the click is within it
        if (openDatePicker != null && evt.Button == NativeMouseButton.Left)
        {
            var calBounds = openDatePicker.CalendarBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (calBounds.Width > 0 && calBounds.Contains(clickPoint))
            {
                HandleCalendarPopupClick(openDatePicker, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            // Click is on the DatePicker trigger itself — ToggleCalendar will close it
            if (hitNode is DatePicker dpHit && ReferenceEquals(dpHit, openDatePicker))
            {
                // Let the normal flow handle it (InvokeTap → ToggleCalendar)
            }
            else
            {
                // Click is outside both trigger and calendar — close it
                openDatePicker.CloseCalendar();
                openDatePicker = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a DateTimePicker popup is open, check if the click is within it
        if (openDateTimePicker != null && evt.Button == NativeMouseButton.Left)
        {
            var calBounds = openDateTimePicker.CalendarBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (calBounds.Width > 0 && calBounds.Contains(clickPoint))
            {
                HandleDateTimePopupClick(openDateTimePicker, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            if (hitNode is DateTimePicker dtpHit && ReferenceEquals(dtpHit, openDateTimePicker))
            {
                // Let the normal flow handle it (InvokeTap → ToggleCalendar)
            }
            else
            {
                openDateTimePicker.CloseCalendar();
                openDateTimePicker = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a TimePicker popup is open, check if the click is within it
        if (openTimePicker != null && evt.Button == NativeMouseButton.Left)
        {
            var popBounds = openTimePicker.PopupBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (popBounds.Width > 0 && popBounds.Contains(clickPoint))
            {
                HandleTimePickerPopupClick(openTimePicker, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            if (hitNode is TimePicker tpHit && ReferenceEquals(tpHit, openTimePicker))
            {
                // Let the normal flow handle it (InvokeTap → TogglePopup)
            }
            else
            {
                openTimePicker.ClosePopup();
                openTimePicker = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a MentionInput suggestion popup is open, check if click is within it
        if (FocusManager.FocusedElement is MentionInput openMention && openMention.IsPopupOpen
            && evt.Button == NativeMouseButton.Left)
        {
            var popBounds = openMention.PopupBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (popBounds.Width > 0 && popBounds.Contains(clickPoint))
            {
                HandleMentionPopupClick(openMention, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            // Click outside popup closes it
            openMention.ClosePopup();
            RequestRepaint?.Invoke();
            // Don't return — let normal click handling proceed
        }

        // If a MonthPicker popup is open, check if the click is within it
        if (openMonthPicker != null && evt.Button == NativeMouseButton.Left)
        {
            var popBounds = openMonthPicker.PopupBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (popBounds.Width > 0 && popBounds.Contains(clickPoint))
            {
                HandleMonthPickerPopupClick(openMonthPicker, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            if (hitNode is MonthPicker mpHit && ReferenceEquals(mpHit, openMonthPicker))
            {
                // Let the normal flow handle it (InvokeTap → TogglePopup)
            }
            else
            {
                openMonthPicker.ClosePopup();
                openMonthPicker = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a DateRangePicker calendar is open, check if the click is within it
        if (openDateRangePicker != null && evt.Button == NativeMouseButton.Left)
        {
            var calBounds = openDateRangePicker.CalendarBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (calBounds.Width > 0 && calBounds.Contains(clickPoint))
            {
                HandleDateRangePopupClick(openDateRangePicker, evt.X, evt.Y);
                RequestRepaint?.Invoke();
                return;
            }

            // Click is on the trigger itself — ToggleCalendar will close it
            if (hitNode is DateRangePicker drpHit && ReferenceEquals(drpHit, openDateRangePicker))
            {
                // Let the normal flow handle it
            }
            else
            {
                openDateRangePicker.CloseCalendar();
                openDateRangePicker = null;
                RequestRepaint?.Invoke();
            }
        }

        // If a DataGrid has an open overlay (select dropdown or date popup), handle clicks
        if (openGridOverlay != null && evt.Button == NativeMouseButton.Left)
        {
            var clickPoint = new Point(evt.X, evt.Y);

            // Select dropdown overlay
            if (openGridOverlay.IsSelectDropdownOpen)
            {
                var ddBounds = openGridOverlay.SelectDropdownBounds;
                if (ddBounds.Width > 0 && ddBounds.Contains(clickPoint))
                {
                    // Click inside dropdown — select option
                    var options = openGridOverlay.GetSelectOptions(openGridOverlay.SelectDropdownCol);
                    if (options != null && options.Count > 0)
                    {
                        float itemHeight = ddBounds.Height / Math.Max(1, options.Count);
                        int index = (int)((evt.Y - ddBounds.Y) / itemHeight);
                        if (index >= 0 && index < options.Count)
                        {
                            openGridOverlay.CommitSelectOption(index);
                            openGridOverlay = null;
                            RequestRepaint?.Invoke();
                            return;
                        }
                    }
                }
                else
                {
                    // Click outside dropdown — close it
                    openGridOverlay.CloseSelectDropdown();
                    openGridOverlay = null;
                    RequestRepaint?.Invoke();
                }
            }

            // Date popup overlay
            if (openGridOverlay != null && openGridOverlay.IsDatePopupOpen)
            {
                var dp = openGridOverlay.DatePopupPicker;
                if (dp != null)
                {
                    var calBounds = dp.CalendarBounds;
                    if (calBounds.Width > 0 && calBounds.Contains(clickPoint))
                    {
                        HandleDataGridCalendarClick(openGridOverlay, dp, evt.X, evt.Y);
                        RequestRepaint?.Invoke();
                        return;
                    }
                    else
                    {
                        // Click outside calendar — close it
                        openGridOverlay.CloseDatePopup();
                        openGridOverlay = null;
                        RequestRepaint?.Invoke();
                    }
                }
            }
        }

        // DataGrid column resize — detect drag start on column border in header
        if (hitNode is ITabularDataNode tdnResize && evt.Button == NativeMouseButton.Left)
        {
            var tdnBounds = Live(tdnResize, tdnResize.AbsoluteBounds);
            float relY = evt.Y - tdnBounds.Y;
            float headerH = tdnResize.GetRowHeight() + 4f;
            if (relY >= 0 && relY < headerH)
            {
                float relX = evt.X - tdnBounds.X;
                int borderCol = HitTestColumnBorder(tdnResize, relX, tdnBounds.Width);
                if (borderCol >= 0 && tdnResize.IsColumnResizable(borderCol))
                {
                    // Start column resize
                    tdnResize.ResizingColumnIndex = borderCol;
                    tdnResize.ResizeStartWidth = tdnResize.GetColumnWidth(
                        borderCol, tdnBounds.Width - TabularRowGeometry.ActionStripWidth(tdnResize, tdnBounds.Width));
                    tdnResize.ResizeStartMouseX = evt.X;
                    columnResizeTdn = tdnResize;
                    return;
                }

                // Column reorder — defer until drag threshold is exceeded.
                // On MouseUp without significant movement, this becomes a sort click.
                if (tdnResize.IsReorderingEnabled)
                {
                    int headerCol = HitTestTabularColumn(tdnResize, relX, tdnBounds.Width);
                    if (headerCol >= 0)
                    {
                        reorderPendingCol = headerCol;
                        reorderStartMouseX = evt.X;
                        reorderStartMouseY = evt.Y;
                        reorderDragActive = false;
                        columnReorderTdn = tdnResize;
                        return;
                    }
                }
            }
        }

        // Detect SplitView divider click — start drag without affecting focus
        if (hitNode is SplitView sv)
        {
            // When hitNode is a SplitView, the click landed in the divider gap
            // (child bounds fill their pane areas, so children aren't hit)
            splitDividerDragging = true;
            splitDragOrientation = sv.Orientation;
            splitDragTargetIndex = sv.LayoutIndex;
            if (sv.Orientation == SplitOrientation.Horizontal)
            {
                splitDragStartMousePos = evt.X;
                splitDragStartFirstSize = sv.First.LayoutData.Bounds.Width;
            }
            else
            {
                splitDragStartMousePos = evt.Y;
                splitDragStartFirstSize = sv.First.LayoutData.Bounds.Height;
            }
            return;
        }

        // Calendar — handle clicks directly in mouseDown (not through InvokeTap).
        // Hit zones are stored in node-local coordinates during painting.
        // AbsoluteBounds stores the viewport-space position, so we convert
        // viewport click coords to node-local by subtracting AbsoluteBounds origin.
        if (hitNode is Calendar calDown && evt.Button == NativeMouseButton.Left)
        {
            FocusManager.RequestFocus(calDown);
            PressCalendar(calDown, evt.X, evt.Y);
            pressedNode = calDown;
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, calDown);
            }
            RequestRepaint?.Invoke();
            return;
        }

        pressedNode = hitNode;

        // Click selects a row of a selectable list (and focuses the list for keyboard navigation);
        // the second press of a double-click activates it.
        Node? clickedList = null;
        if (rootNode != null && evt.Button == NativeMouseButton.Left
            && HitTester.FindSelectableListViewAt(rootNode, evt.X, evt.Y) is { } selLv)
        {
            clickedList = selLv as Node;
            int row = selLv.RowIndexAt(evt.Y - selLv.ReorderBounds.Y);
            if (row >= 0)
            {
                selLv.SelectIndex(row);
                if (selLv.FocusesOnClick && selLv is Node listNode)
                {
                    FocusManager.RequestFocus(listNode);
                }
                if (evt.ClickCount >= 2)
                {
                    selLv.ActivateIndex(row);
                }
                RequestRepaint?.Invoke();
            }
        }

        // Arm a potential ListView drag-to-reorder. It only activates if the pointer
        // then moves past the threshold (see HandleMouseMove); a plain click still
        // falls through to the row's tap/select/button below.
        if (rootNode != null)
        {
            var reLv = HitTester.FindReorderableListViewAt(rootNode, evt.X, evt.Y);
            if (reLv != null)
            {
                float ih = reLv.GetItemHeight();
                int startRow = ih > 0 ? (int)((evt.Y - reLv.ReorderBounds.Y + reLv.OffsetY) / ih) : -1;
                listReorderLv = reLv;
                listReorderStartRow = Math.Clamp(startRow, 0, Math.Max(0, reLv.ItemCount - 1));
                listReorderStartY = evt.Y;
                listReorderActive = false;
            }

            // Arm a potential swipe. The move handler arbitrates by dominant axis
            // (horizontal → swipe, vertical → reorder) when a list supports both.
            var swLv = HitTester.FindSwipeableListViewAt(rootNode, evt.X, evt.Y);
            if (swLv != null)
            {
                float ih = swLv.GetItemHeight();
                int startRow = ih > 0 ? (int)((evt.Y - swLv.ReorderBounds.Y + swLv.OffsetY) / ih) : -1;
                swipeLv = swLv;
                swipeRow = Math.Clamp(startRow, 0, Math.Max(0, swLv.ItemCount - 1));
                swipeStartX = evt.X;
                swipeStartY = evt.Y;
                swipeActive = false;
            }
        }

        if (hitNode != null)
        {
            hitNode.IsPressed = true;
            if (hitNode is ToolBar tbPress)
            {
                tbPress.PressedItemIndex = ComputeToolBarItemIndex(tbPress, evt.X, evt.Y);
            }
            if (hitNode is INumberInput niPress && !niPress.IsDisabled)
            {
                niPress.PressedStepperButton = ComputeNumberInputHoveredButton(niPress, evt.X, evt.Y);
            }
            if (hitNode is ITabularDataNode tdnPress && evt.Button == NativeMouseButton.Left)
            {
                PressRowAction(tdnPress, evt.X, evt.Y);
            }
            if (hitNode is TabBar tabBarPress)
            {
                PressTabBar(tabBarPress, evt);
            }
            RequestRepaint?.Invoke();
        }

        // A press on a tag (not its ×) of a reorderable tag input may become a drag.
        if (hitNode is TagInput { IsReorderEnabled: true, IsDisabled: false, IsReadOnly: false } tagPress && evt.Button == NativeMouseButton.Left
            && TryGetAbsoluteBounds(tagPress, out var tagBounds))
        {
            var local = new Point(evt.X - tagBounds.X, evt.Y - tagBounds.Y);
            tagPress.DragIndex = tagPress.PillAt(local);
            tagPress.DragStart = local;
            tagPress.Dragging = false;
            tagPress.DropIndex = -1;
        }

        if (hitNode is ColorPicker { IsDisabled: false } pickerPress && evt.Button == NativeMouseButton.Left)
        {
            PressColorPicker(pickerPress, evt.X, evt.Y);
        }

        // Record slider drag start state
        if (hitNode is Slider sl)
        {
            sliderDragStartX = evt.X;
            sliderDragStartY = evt.Y;
            sliderDragStartValue = sl.Bind.Value;
        }

        // Record range slider drag start state — pick nearest thumb
        if (hitNode is RangeSlider rs && !rs.IsDisabled && !rs.IsReadOnly)
        {
            sliderDragStartX = evt.X;
            float trackRange = rs.Max - rs.Min;
            float thumbW = 20f;
            // Where layout has it now (the painter's stamp lags a frame and is stale in a scrolled layer).
            var absBounds = TryGetAbsoluteBounds(rs, out var liveRange) ? liveRange : rs.AbsoluteBounds;
            float trackW = Math.Max(1f, absBounds.Width - thumbW);
            float relX = evt.X - absBounds.X - thumbW / 2f;
            float clickFrac = Math.Clamp(relX / trackW, 0f, 1f);
            float clickVal = rs.Min + clickFrac * trackRange;

            float distMin = MathF.Abs(clickVal - rs.MinBind.Value);
            float distMax = MathF.Abs(clickVal - rs.MaxBind.Value);
            rangeSliderDragIsMax = distMax <= distMin;
            rs.FocusedThumb = rangeSliderDragIsMax ? 1 : 0;
            rangeSliderDragStartValue = rangeSliderDragIsMax ? rs.MaxBind.Value : rs.MinBind.Value;
        }

        // A list that selects without taking focus (FocusOnClick(false)) leaves focus where it was:
        // neither the list nor the row's content takes it, and it is not cleared.
        bool keepFocus = clickedList is IListViewNode { FocusesOnClick: false };

        if (hitNode == null)
        {
            if (keepFocus)
            {
                return;
            }
            // A click on a list's empty area (or unlaid rows) keeps the list focused.
            if (clickedList is not null)
            {
                FocusManager.RequestFocus(clickedList);
            }
            else
            {
                FocusManager.ClearFocus();
            }
            return;
        }

        // Focus the clicked node if it's focusable
        var focusTarget = keepFocus ? null : FindFocusableNode(hitNode);
        if (focusTarget != null)
        {
            // Clear TagInput focus state if moving away from a TagInput
            if (FocusManager.FocusedElement is TagInput prevTag && !ReferenceEquals(prevTag, focusTarget))
            {
                prevTag.IsFocused = false;
            }

            // Commit a pending NumberInput edit when focus moves to a different control.
            if (FocusManager.FocusedElement is INumberInput prevNi && !ReferenceEquals(prevNi, focusTarget))
            {
                CommitNumberInputEdit(prevNi);
            }

            // Whether this very input already had focus (a click inside it keeps its selection anchor for
            // Shift+click); asked before RequestFocus, which re-seeds the edit buffers on a move.
            bool wasTextInputFocused = focusTarget is TextInput && ReferenceEquals(FocusManager.FocusedElement, focusTarget);

            FocusManager.RequestFocus(focusTarget);

            // Initialize text editing buffer when focusing a TextInput
            if (focusTarget is TextInput ti)
            {
                textInputBuffer = ti.Value.Value ?? string.Empty;
                ActiveEditBuffer = textInputBuffer;
                if (!wasTextInputFocused)
                {
                    TextInputCaretIndex = textInputBuffer.Length;
                    TextInputScrollOffsetX = 0f;
                }
                CaretResetTimestamp = Stopwatch.GetTimestamp();
                PositionTextInputCaretFromMouse(ti, evt);
                bool shiftHeld = evt.Modifiers.HasFlag(ModifierKeys.Shift);
                if (!shiftHeld || !wasTextInputFocused)
                {
                    TextInputSelectionAnchor = TextInputCaretIndex;
                }
                PinEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
            }
            else if (focusTarget is PasswordInput pwdFocus)
            {
                // Only initialize buffer on first focus, not on re-click
                bool wasEditing = PasswordEditBuffer != null;
                if (!wasEditing)
                {
                    PasswordEditBuffer = pwdFocus.Value.Value ?? string.Empty;
                    PasswordRevealed = false;
                    PasswordScrollOffsetX = 0f;
                }
                CaretResetTimestamp = Stopwatch.GetTimestamp();
                // The click places the caret (Shift extends the selection), like a TextInput.
                PasswordCaretIndex = PasswordCaretIndexAt(pwdFocus, PasswordEditBuffer!, evt.X);
                if (!evt.Modifiers.HasFlag(ModifierKeys.Shift) || !wasEditing)
                {
                    PasswordSelectionAnchor = PasswordCaretIndex;
                }
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
            }
            else if (focusTarget is PinInput pin)
            {
                // Set active cell to end of current value (or 0 if empty)
                string pinVal = pin.Value.Value ?? "";
                PinEditBuffer = pinVal;
                PinActiveCellIndex = Math.Min(pinVal.Length, pin.Length - 1);
                CaretResetTimestamp = Stopwatch.GetTimestamp();
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
            }
            else if (focusTarget is TextArea taFocus)
            {
                bool wasTextAreaFocused = TextAreaEditBuffer != null;
                TextAreaEditBuffer = taFocus.Value.Value ?? string.Empty;
                if (!wasTextAreaFocused)
                {
                    TextAreaCaretIndex = TextAreaEditBuffer.Length;
                }
                CaretResetTimestamp = Stopwatch.GetTimestamp();
                PositionTextAreaCaretFromMouse(taFocus, evt);
                bool shiftHeld = evt.Modifiers.HasFlag(ModifierKeys.Shift);
                if (!shiftHeld || !wasTextAreaFocused)
                {
                    TextAreaSelectionAnchor = TextAreaCaretIndex;
                }
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PinEditBuffer = null;
                PasswordEditBuffer = null;
            }
            else if (focusTarget is TagInput tagFocus)
            {
                tagFocus.IsFocused = true;
                tagFocus.LiveTags = new List<string>(tagFocus.Value.Value);
                CaretResetTimestamp = Stopwatch.GetTimestamp();

                // Check if click hit a × remove button
                var clickPt = new Point(evt.X, evt.Y);
                bool hitRemove = false;
                for (int i = 0; i < tagFocus.TagRemoveBounds.Count; i++)
                {
                    if (tagFocus.TagRemoveBounds[i].Width > 0 && tagFocus.TagRemoveBounds[i].Contains(clickPt))
                    {
                        tagFocus.RemoveTagAt(i);
                        hitRemove = true;
                        break;
                    }
                }

                if (!hitRemove)
                {
                    tagFocus.CaretIndex = tagFocus.InputBuffer.Length;
                }

                textInputBuffer = null;
                ActiveEditBuffer = null;
                PinEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
            }
            else if (focusTarget is MentionInput mentionFocus)
            {
                // Clear previous MentionInput's IsFocused if switching
                if (FocusManager.FocusedElement is MentionInput prevMention
                    && !ReferenceEquals(prevMention, mentionFocus))
                {
                    prevMention.IsFocused = false;
                    prevMention.ClosePopup();
                }

                mentionFocus.IsFocused = true;
                MentionEditBuffer = mentionFocus.Value.Value ?? string.Empty;
                ActiveEditBuffer = MentionEditBuffer;
                MentionInputCaretIndex = MentionEditBuffer.Length;
                MentionInputSelectionAnchor = MentionInputCaretIndex;
                CaretResetTimestamp = Stopwatch.GetTimestamp();

                textInputBuffer = null;
                PinEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
            }
            else
            {
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PinEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
            }
        }
        else if (!keepFocus)
        {
            if (FocusManager.FocusedElement is TagInput prevTag2)
            {
                prevTag2.IsFocused = false;
            }
            if (FocusManager.FocusedElement is MentionInput prevMention2)
            {
                prevMention2.IsFocused = false;
                prevMention2.ClosePopup();
            }
            FocusManager.ClearFocus();
            textInputBuffer = null;
            ActiveEditBuffer = null;
            MentionEditBuffer = null;
            PinEditBuffer = null;
            PasswordEditBuffer = null;
            TextAreaEditBuffer = null;
        }

        // Fire PointerDown
        InvokePointerGesture(hitNode, g => g.PointerDown, evt);

        // Detect drag-and-drop start — check if the pressed node (or an ancestor) is draggable
        if (evt.Button == NativeMouseButton.Left && rootNode != null)
        {
            var draggable = HitTester.FindDraggableAt(rootNode, evt.X, evt.Y);
            if (draggable != null && draggable.LayoutData.DragData is { IsDraggable: true } dragData)
            {
                dragDropPending = true;
                dragDropSourceNode = draggable;
                dragDropPayload = dragData.Payload;
                dragDropStartX = evt.X;
                dragDropStartY = evt.Y;
            }
        }

        // Right-click → the node's context-menu handler; ContextMenu.Show(items) opens at the pointer.
        if (evt.Button == NativeMouseButton.Right && hitNode != null)
        {
            InvokeContextMenuHandler(hitNode, MenuPlacement.AtPoint(new Point(evt.X, evt.Y)));
        }
    }

    // Computes which revealed swipe-action button (if any) contains the point,
    // using the current swipe offset. Returns (leading, actionIndex) or null.
    private static (bool leading, int index)? HitSwipeButton(IListViewNode lv, float x, float y)
    {
        int row = lv.SwipeRowIndex;
        if (row < 0)
        {
            return null;
        }

        var b = lv.ReorderBounds;
        float ih = lv.GetItemHeight();
        float rowTop = b.Y + (row * ih - lv.OffsetY);
        if (y < rowTop || y > rowTop + ih)
        {
            return null;
        }

        float bw = lv.SwipeButtonWidth;
        float off = lv.SwipeOffsetX;

        if (off < 0f)
        {
            int n = lv.TrailingActionCount(row);
            for (int k = 0; k < n; k++)
            {
                float bx = b.X + b.Width + (k * bw) + off;
                if (x >= bx && x < bx + bw)
                {
                    return (false, k);
                }
            }
        }
        else if (off > 0f)
        {
            int n = lv.LeadingActionCount(row);
            for (int k = 0; k < n; k++)
            {
                float bx = b.X + (k * bw) - (n * bw) + off;
                if (x >= bx && x < bx + bw)
                {
                    return (true, k);
                }
            }
        }

        return null;
    }

    private void CloseSwipe()
    {
        if (swipeLv != null)
        {
            swipeLv.SwipeRowIndex = -1;
            swipeLv.SwipeOffsetX = 0f;
        }

        swipeLv = null;
        swipeRow = -1;
        swipeActive = false;
        swipeRawDx = 0f;
    }

    private void HandleMouseUp(Node? hitNode, NativeMouseEvent evt)
    {
        // A tag dragged within a reorderable tag input lands where it was dropped.
        if (pressedNode is TagInput { DragIndex: >= 0 } tagUp)
        {
            if (tagUp.Dragging && tagUp.DropIndex >= 0)
            {
                tagUp.MoveTag(tagUp.DragIndex, tagUp.DropIndex);
            }
            tagUp.DragIndex = -1;
            tagUp.Dragging = false;
            tagUp.DropIndex = -1;
            RequestRepaint?.Invoke();
        }

        // A calendar press ends: the drag it became, or a click on the event or day.
        if (pressedNode is Calendar { Drag: not null } calUp)
        {
            ReleaseCalendar(calUp);
        }

        // The end of a drag across a picker's colour surface makes that colour a recent one.
        if (pressedNode is ColorPicker { DragPart: not ColorPickerPart.None } pickerUp)
        {
            pickerUp.DragPart = ColorPickerPart.None;
            pickerUp.NoteRecent();
            RequestRepaint?.Invoke();
        }

        if (TryEndLabelSelection())
        {
            return;
        }

        // A revealed swipe-action button was pressed on mouse-down — invoke on release.
        if (swipeButtonPending)
        {
            if (swipeLv != null && swipeLv.SwipeRowIndex >= 0)
            {
                if (swipeButtonPendingLeading)
                {
                    swipeLv.InvokeLeadingAction(swipeRow, swipeButtonPendingIndex);
                }
                else
                {
                    swipeLv.InvokeTrailingAction(swipeRow, swipeButtonPendingIndex);
                }
            }

            swipeButtonPending = false;
            swipeButtonPendingIndex = -1;
            CloseSwipe();
            isMouseDown = false;
            RequestRepaint?.Invoke();
            return;
        }

        // Finish an active swipe drag: full-swipe invoke, snap open, or snap closed.
        if (swipeActive && swipeLv != null)
        {
            float off = swipeLv.SwipeOffsetX;
            float bw = swipeLv.SwipeButtonWidth;
            int trailN = swipeLv.TrailingActionCount(swipeRow);
            int leadN = swipeLv.LeadingActionCount(swipeRow);
            float rowW = swipeLv.ReorderBounds.Width;

            if (off < 0f && trailN > 0 && swipeLv.TrailingIsFullSwipe(swipeRow)
                && -swipeRawDx >= rowW * 0.5f)
            {
                swipeLv.InvokeTrailingAction(swipeRow, 0);
                CloseSwipe();
            }
            else if (off > 0f && leadN > 0 && swipeLv.LeadingIsFullSwipe(swipeRow)
                && swipeRawDx >= rowW * 0.5f)
            {
                swipeLv.InvokeLeadingAction(swipeRow, 0);
                CloseSwipe();
            }
            else if (off < 0f && trailN > 0 && -off >= trailN * bw * 0.5f)
            {
                swipeLv.SwipeOffsetX = -trailN * bw; // snap trailing open
                swipeActive = false;                 // keep swipeLv referenced (open)
            }
            else if (off > 0f && leadN > 0 && off >= leadN * bw * 0.5f)
            {
                swipeLv.SwipeOffsetX = leadN * bw;   // snap leading open
                swipeActive = false;
            }
            else
            {
                CloseSwipe(); // not far enough — snap closed
            }

            isMouseDown = false;
            RequestRepaint?.Invoke();
            return;
        }

        // Finish a ListView drag-to-reorder (if it actually activated).
        if (listReorderLv != null)
        {
            if (listReorderActive)
            {
                listReorderLv.ApplyReorder(listReorderLv.ReorderFromIndex, listReorderLv.ReorderToIndex);
            }

            listReorderLv.ReorderFromIndex = -1;
            listReorderLv.ReorderToIndex = -1;
            listReorderLv = null;
            listReorderActive = false;
            listReorderStartRow = -1;
            RequestRepaint?.Invoke();
        }

        // Drop a swipe that was armed but never engaged (a plain click). A snapped-open
        // swipe (SwipeRowIndex >= 0) is kept so the next click can dismiss/invoke it.
        if (swipeLv != null && !swipeActive && swipeLv.SwipeRowIndex < 0)
        {
            swipeLv = null;
            swipeRow = -1;
            swipeRawDx = 0f;
        }

        // Finish column resize drag
        if (columnResizeTdn != null)
        {
            columnResizeTdn.ResizingColumnIndex = -1;
            columnResizeTdn = null;
            RequestRepaint?.Invoke();

            // Re-decide the cursor now rather than on the next move: after the drag the pointer
            // is usually still on the border (keep the resize cursor) but may have been released
            // well past it, in which case it should go back to the arrow immediately.
            UpdateHoverCursor(hitNode, evt.X, evt.Y);
        }

        // Finish column reorder drag — commit if threshold was exceeded, else treat as sort click
        if (columnReorderTdn != null)
        {
            if (reorderDragActive)
            {
                int from = columnReorderTdn.ReorderDragIndex;
                int to = columnReorderTdn.ReorderDropIndex;
                if (from >= 0 && to >= 0 && from != to)
                {
                    columnReorderTdn.ReorderColumn(from, to);
                }
                columnReorderTdn.ReorderDragIndex = -1;
                columnReorderTdn.ReorderDropIndex = -1;
            }
            else
            {
                // Drag threshold not exceeded — treat as header click (sort)
                var sortTdn = columnReorderTdn;
                int col = reorderPendingCol;
                if (col >= 0 && sortTdn.IsSortable && sortTdn.IsColumnSortable(col))
                {
                    sortTdn.ApplySort(col);
                }
            }
            columnReorderTdn = null;
            reorderPendingCol = -1;
            reorderDragActive = false;
            RequestRepaint?.Invoke();
        }

        var previousPressed = pressedNode;
        isMouseDown = false;
        pressedNode = null;

        // Clear pending drag-and-drop if threshold was never reached
        if (dragDropPending && !dragDropActive)
        {
            dragDropPending = false;
            dragDropSourceNode = null;
            dragDropPayload = null;
        }

        // Clear pressed state on the previously pressed node
        if (previousPressed != null)
        {
            previousPressed.IsPressed = false;
            if (previousPressed is ToolBar tbPrev)
            {
                tbPrev.PressedItemIndex = -1;
            }
            if (previousPressed is INumberInput niPrev)
            {
                niPrev.PressedStepperButton = -1;
            }
            ReleaseRowAction(previousPressed);
            if (previousPressed is TabBar tabBarPrev)
            {
                ReleaseTabBar(tabBarPrev);
            }
            RequestRepaint?.Invoke();
        }

        if (hitNode == null)
        {
            return;
        }

        if (TryMiddleClickTabBar(hitNode, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        // Fire PointerUp
        InvokePointerGesture(hitNode, g => g.PointerUp, evt);

        // A left click is a tap when it lands on the node it started on. ReferenceEquals covers the
        // normal (reconciled) tree. For virtualized ListView/TreeView rows, the built content is
        // rebuilt every frame, so a press then release over the SAME on-screen button resolves to two
        // different instances — treat a zero-movement release over an interactive node as a tap too,
        // so per-row buttons (e.g. a row's "remove" X) actually fire.
        bool sameNode = ReferenceEquals(hitNode, previousPressed);
        bool tapWithoutMove = previousPressed != null
            && IsInteractiveNode(hitNode)
            && MathF.Abs(evt.X - mouseDownX) < 4f
            && MathF.Abs(evt.Y - mouseDownY) < 4f;
        if (hitNode is not null && (sameNode || tapWithoutMove) && evt.Button == NativeMouseButton.Left)
        {
            InvokeTap(hitNode);

            // Mark ScrollView layers dirty when interactive controls are tapped.
            // Controls with animations (hover, press, value changes) will trigger
            // direct painting via PaintScrollView's hasActiveAnimations check.
            // For non-animated state changes, we mark dirty here so the layer
            // gets recaptured with the updated state.
            if (rootNode != null && IsInteractiveNode(hitNode))
            {
                MarkScrollViewLayersDirty(rootNode, hitNode);
            }
        }
    }

    /// <summary>
    /// Forgets the pressed button state after something outside the dispatcher consumed the
    /// release — a window move (<c>AppWindow.BeginDrag</c>) runs its own loop and eats the mouse-up.
    /// </summary>
    internal void CancelPointerPress()
    {
        isMouseDown = false;
        pressedNode = null;
        dragDropPending = false;
        dragDropSourceNode = null;
        dragDropPayload = null;
        RequestRepaint?.Invoke();
    }

    private void HandleMouseLeave()
    {
        UpdateTreeViewHover(null);
        if (hoveredTabBar is { } leftTabBar)
        {
            ClearTabBarHover(leftTabBar);
            hoveredTabBar = null;
        }

        if (hoveredNode != null)
        {
            hoveredNode.IsHovered = false;
            InvokePointerLeave(hoveredNode);
            hoveredNode = null;
            RequestRepaint?.Invoke();
        }
    }

    // ── Scroll events ─────────────────────────────────────────────────

    /// <summary>
    /// What the most recent wheel event scrolled (or <see cref="ScrollOutcome.Nothing"/>), for the
    /// DevTools <c>scroll</c> verb: lists, grids and tables own their offsets, so the page's
    /// ScrollView offset says nothing about them.
    /// </summary>
    internal ScrollOutcome LastScroll { get; private set; } = ScrollOutcome.Nothing;

    /// <summary>
    /// Handles a scroll event from the platform layer.
    /// </summary>
    internal void HandleScrollEvent(NativeScrollEvent evt)
    {
        ForgetLiveBounds();
        LastScroll = ScrollOutcome.Nothing;
        if (rootNode == null)
        {
            return;
        }

        if (HandleMenuScroll(evt))
        {
            LastScroll = new ScrollOutcome(ScrollTargetKind.Menu, null, 0f, 0f, false);
            return;
        }

        if (RouteOverlayScroll(evt))
        {
            return;
        }

        DispatchScroll(evt);
    }

    /// <summary>Wheel dispatch against <see cref="rootNode"/> (the page or an overlay's tree).</summary>
    private void DispatchScroll(NativeScrollEvent evt)
    {
        if (rootNode is null)
        {
            return;
        }

        // Scroll within an open Select dropdown
        if (openSelect != null)
        {
            var dropdownBounds = openSelect.DropdownBounds;
            var scrollPoint = new Point(evt.X, evt.Y);
            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(scrollPoint))
            {
                float itemHeight = openSelect.DropdownItemHeight;
                if (itemHeight > 0)
                {
                    int visibleCount = Math.Max(1, (int)(dropdownBounds.Height / itemHeight));
                    int maxScrollOffset = Math.Max(0, openSelect.OptionCount - visibleCount);

                    // DeltaY is typically positive for scroll-up, negative for scroll-down
                    int scrollDelta = evt.DeltaY > 0 ? -1 : evt.DeltaY < 0 ? 1 : 0;
                    int newOffset = Math.Clamp(openSelect.ScrollOffset + scrollDelta, 0, maxScrollOffset);

                    bool moved = newOffset != openSelect.ScrollOffset;
                    if (moved)
                    {
                        openSelect.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }

                    LastScroll = new ScrollOutcome(ScrollTargetKind.Dropdown, openSelect as Node, newOffset, maxScrollOffset, moved);
                }

                return;
            }
        }

        // Scroll within an open MultiSelect dropdown
        if (openMultiSelect != null)
        {
            var dropdownBounds = openMultiSelect.DropdownBounds;
            var scrollPoint = new Point(evt.X, evt.Y);
            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(scrollPoint))
            {
                float itemHeight = openMultiSelect.DropdownItemHeight;
                if (itemHeight > 0)
                {
                    int visibleCount = Math.Max(1, (int)(dropdownBounds.Height / itemHeight));
                    int maxScrollOffset = Math.Max(0, openMultiSelect.OptionCount - visibleCount);
                    int scrollDelta = evt.DeltaY > 0 ? -1 : evt.DeltaY < 0 ? 1 : 0;
                    int newOffset = Math.Clamp(openMultiSelect.ScrollOffset + scrollDelta, 0, maxScrollOffset);

                    bool moved = newOffset != openMultiSelect.ScrollOffset;
                    if (moved)
                    {
                        openMultiSelect.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }

                    LastScroll = new ScrollOutcome(ScrollTargetKind.Dropdown, openMultiSelect as Node, newOffset, maxScrollOffset, moved);
                }

                return;
            }
        }

        // Scroll within an open Combobox dropdown
        if (openCombobox != null)
        {
            var dropdownBounds = openCombobox.DropdownBounds;
            var scrollPoint = new Point(evt.X, evt.Y);
            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(scrollPoint))
            {
                float itemHeight = openCombobox.DropdownItemHeight;
                if (itemHeight > 0)
                {
                    int visibleCount = Math.Max(1, (int)(dropdownBounds.Height / itemHeight));
                    int maxScrollOffset = Math.Max(0, openCombobox.FilteredOptionCount - visibleCount);
                    int scrollDelta = evt.DeltaY > 0 ? -1 : evt.DeltaY < 0 ? 1 : 0;
                    int newOffset = Math.Clamp(openCombobox.ScrollOffset + scrollDelta, 0, maxScrollOffset);

                    bool moved = newOffset != openCombobox.ScrollOffset;
                    if (moved)
                    {
                        openCombobox.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }

                    LastScroll = new ScrollOutcome(ScrollTargetKind.Dropdown, openCombobox as Node, newOffset, maxScrollOffset, moved);
                }

                return;
            }
        }

        // Scroll within an open DatePicker calendar — navigate by view mode
        if (openDatePicker != null)
        {
            var calBounds = openDatePicker.CalendarBounds;
            var scrollPoint = new Point(evt.X, evt.Y);
            if (calBounds.Width > 0 && calBounds.Contains(scrollPoint))
            {
                int delta = evt.DeltaY > 0 ? -1 : evt.DeltaY < 0 ? 1 : 0;
                if (delta != 0)
                {
                    switch (openDatePicker.ViewMode)
                    {
                        case CalendarViewMode.Days:
                            openDatePicker.NavigateMonth(delta);
                            break;
                        case CalendarViewMode.Months:
                            openDatePicker.DisplayedYear += delta;
                            break;
                        case CalendarViewMode.Years:
                            openDatePicker.NavigateYearGrid(delta * 12);
                            break;
                    }

                    RequestRepaint?.Invoke();
                }

                LastScroll = new ScrollOutcome(ScrollTargetKind.Calendar, openDatePicker, 0f, 0f, delta != 0);
                return;
            }
        }

        // Scroll within an open DataGrid date popup
        if (openGridOverlay != null && openGridOverlay.IsDatePopupOpen)
        {
            var dp = openGridOverlay.DatePopupPicker;
            if (dp != null)
            {
                var calBounds = dp.CalendarBounds;
                var scrollPoint = new Point(evt.X, evt.Y);
                if (calBounds.Width > 0 && calBounds.Contains(scrollPoint))
                {
                    int delta = evt.DeltaY > 0 ? -1 : evt.DeltaY < 0 ? 1 : 0;
                    if (delta != 0)
                    {
                        switch (dp.ViewMode)
                        {
                            case CalendarViewMode.Days:
                                dp.NavigateMonth(delta);
                                break;
                            case CalendarViewMode.Months:
                                dp.DisplayedYear += delta;
                                break;
                            case CalendarViewMode.Years:
                                dp.NavigateYearGrid(delta * 12);
                                break;
                        }

                        RequestRepaint?.Invoke();
                    }

                    LastScroll = new ScrollOutcome(ScrollTargetKind.Calendar, openGridOverlay as Node, 0f, 0f, delta != 0);
                    return;
                }
            }
        }

        // TextArea scroll— if a TextArea is active, has scrollable content, and mouse is over it
        if (TextAreaEditBuffer != null
            && TextAreaAbsoluteBounds.Width > 0
            && TextAreaMaxScrollY > 0)
        {
            var taBounds = TextAreaAbsoluteBounds;
            if (taBounds.Contains(new Point(evt.X, evt.Y)))
            {
                const float pixelsPerNotch = 48f;
                float delta = -evt.DeltaY * pixelsPerNotch;
                float newOffset = Math.Clamp(TextAreaScrollOffsetY + delta, 0f, TextAreaMaxScrollY);
                bool moved = Math.Abs(newOffset - TextAreaScrollOffsetY) > 0.001f;
                if (moved)
                {
                    TextAreaScrollOffsetY = newOffset;
                    CaretResetTimestamp = Stopwatch.GetTimestamp();
                    RequestRepaint?.Invoke();
                }

                LastScroll = new ScrollOutcome(ScrollTargetKind.TextArea, FocusManager.FocusedElement, newOffset, TextAreaMaxScrollY, moved);
                return;
            }
        }

        // A zoomable image zooms about the pointer.
        if (HitTester.HitTest(rootNode, evt.X, evt.Y) is Image { ZoomPanEnabled: true } zoomImage && evt.DeltaY != 0)
        {
            zoomImage.ZoomAbout(zoomImage.Zoom * (evt.DeltaY > 0 ? 1.2f : 1f / 1.2f), evt.X, evt.Y);
            RequestRepaint?.Invoke();
            return;
        }

        // An open time picker's wheels turn.
        if (ScrollTimePickerPopup(evt))
        {
            return;
        }

        // A calendar scrolls its hours (or agenda).
        if (HitTester.HitTest(rootNode, evt.X, evt.Y) is Calendar wheelCalendar && ScrollCalendar(wheelCalendar, evt))
        {
            return;
        }

        // An emoji picker scrolls its grid.
        if (HitTester.HitTest(rootNode, evt.X, evt.Y) is EmojiPicker { IsDisabled: false } emojiPicker)
        {
            var pickerBounds = Live(emojiPicker, emojiPicker.AbsoluteBounds);
            float max = emojiPicker.MaxScroll(pickerBounds.Height);
            float next = Math.Clamp(emojiPicker.ScrollOffset - evt.DeltaY * 48f, 0f, max);
            if (Math.Abs(next - emojiPicker.ScrollOffset) > 0.001f)
            {
                emojiPicker.ScrollOffset = next;
                RequestRepaint?.Invoke();
            }
            return;
        }

        // A toolbar in OverflowMode.Scroll scrolls its items along its axis.
        if (HitTester.HitTest(rootNode, evt.X, evt.Y) is ToolBar { OverflowSetting: OverflowMode.Scroll } scrollBar)
        {
            var barBounds = Live(scrollBar, scrollBar.AbsoluteBounds);
            float view = scrollBar.IsVertical ? barBounds.Height : barBounds.Width;
            float max = Math.Max(0f, scrollBar.NaturalLength - view);
            if (max > 0f)
            {
                // Either wheel axis moves it, so an ordinary wheel scrolls a horizontal toolbar too.
                float wheel = Math.Abs(evt.DeltaX) > Math.Abs(evt.DeltaY) ? evt.DeltaX : -evt.DeltaY;
                float next = Math.Clamp(scrollBar.ScrollOffset + wheel * 48f, 0f, max);
                if (Math.Abs(next - scrollBar.ScrollOffset) > 0.001f)
                {
                    scrollBar.ScrollOffset = next;
                    RequestRepaint?.Invoke();
                }
                return;
            }
        }

        // DataGrid / DataTable scroll — handle wheel within the grid's data area
        var hitForGrid = HitTester.HitTest(rootNode, evt.X, evt.Y);
        if (hitForGrid is ITabularDataNode tdnScroll && tdnScroll.MaxScrollOffsetY > 0)
        {
            var gdBounds = Live(tdnScroll, tdnScroll.AbsoluteBounds);
            if (gdBounds.Width > 0 && gdBounds.Contains(new Point(evt.X, evt.Y)))
            {
                const float pixelsPerNotch = 48f;
                float delta = -evt.DeltaY * pixelsPerNotch;
                float oldOffset = tdnScroll.ScrollOffsetY;
                tdnScroll.ScrollOffsetY = oldOffset + delta;
                bool moved = Math.Abs(tdnScroll.ScrollOffsetY - oldOffset) > 0.001f;
                if (moved)
                {
                    // The rows moved under a pointer that did not: what it hovers changed.
                    RefreshTabularHover(tdnScroll, evt.X, evt.Y);
                    RequestRepaint?.Invoke();
                }

                LastScroll = new ScrollOutcome(ScrollTargetKind.Table, hitForGrid, tdnScroll.ScrollOffsetY, tdnScroll.MaxScrollOffsetY, moved);
                return;
            }
        }

        // A tab bar whose tabs overflow scrolls its strip under the wheel.
        if (hitForGrid is TabBar tabBarScroll && TryScrollTabBar(tabBarScroll, evt))
        {
            return;
        }

        // Virtualized ListView — a list that owns its own scroll offset (builds only
        // the visible slice). Takes priority over an enclosing ScrollView.
        var virtualList = HitTester.FindScrollableListViewAt(rootNode, evt.X, evt.Y);
        if (virtualList != null)
        {
            const float pixelsPerNotch = 48f;
            float delta = -evt.DeltaY * pixelsPerNotch;
            float newOffset = Math.Clamp(virtualList.OffsetY + delta, 0f, virtualList.MaxY);
            bool moved = Math.Abs(newOffset - virtualList.OffsetY) > 0.001f;
            if (moved)
            {
                virtualList.OffsetY = newOffset;
                RequestRepaint?.Invoke();
            }

            LastScroll = new ScrollOutcome(ScrollTargetKind.ListView, virtualList as Node, virtualList.OffsetY, virtualList.MaxY, moved);
            return;
        }

        // ScrollView handling: offered to the ScrollViews under the pointer, innermost first
        // (see ScrollWheelAlongChain).
        var scrollChain = HitTester.FindScrollViewChainAt(rootNode, evt.X, evt.Y);
        if (scrollChain.Count > 0 && ScrollWheelAlongChain(scrollChain, evt) is { } scrolled)
        {
            // Keep globals in sync for DevTools backward compatibility
            ScrollViewOffsetY = scrolled.View.OffsetY;
            ScrollViewMaxY = scrolled.View.MaxY;
            if (scrolled.Moved)
            {
                RequestRepaint?.Invoke();
            }

            LastScroll = new ScrollOutcome(ScrollTargetKind.ScrollView, scrolled.View, scrolled.View.OffsetY, scrolled.View.MaxY, scrolled.Moved);
            return;
        }

        var hitNode = HitTester.HitTest(rootNode, evt.X, evt.Y);
        if (hitNode == null)
        {
            return;
        }

        var gestDelta = new Point(evt.DeltaX, evt.DeltaY);
        if (hitNode.LayoutData.GestureData?.Scroll is not null)
        {
            LastScroll = new ScrollOutcome(ScrollTargetKind.Gesture, hitNode, 0f, 0f, false);
        }

        InvokeGesture(hitNode, g => g.Scroll, gestDelta);
    }

    // ── Keyboard events ───────────────────────────────────────────────

    /// <summary>
    /// Handles a keyboard event from the platform layer.
    /// </summary>
    // A key-down consumed by a binding or accelerator swallows the character message that follows
    // it, so Shift+1 bound to an action does not also type '!' into a focused text field.
    private bool suppressNextCharacter;

    internal void HandleKeyEvent(NativeKeyEvent evt)
    {
        ForgetLiveBounds();
        bool isCharacter = evt.Character is not null && evt.Key == Key.None;
        if (!isCharacter)
        {
            Keyboard.Observe(evt.Modifiers);
        }

        systemKeyHandled = false;
        if (evt.Type != NativeKeyEventType.KeyDown)
        {
            current = this;
            if (HandleMenuBarKeyUp(evt))
            {
                return;
            }

            RouteKeyUp(evt);
            return;
        }

        if (isCharacter)
        {
            if (suppressNextCharacter)
            {
                suppressNextCharacter = false;

                // The character of an Alt+letter the menu bar used: no system beep either.
                systemKeyHandled = evt.Modifiers.HasFlag(ModifierKeys.Alt);
                return;
            }
        }
        else
        {
            suppressNextCharacter = false;
        }

#if DEBUG
        // DevTools keyboard shortcuts (F12, Ctrl+Shift+I, Ctrl+Shift+L, etc.)
        if (DevTools.CascadeDevTools.HandleKeyDown(evt.Key, evt.Modifiers))
        {
            return;
        }
#endif

        current = this;
        if (!isCharacter && evt.Key != Key.None)
        {
            LastInputWasKeyboard = true;
        }

        // An open menu is modal for the keyboard: it takes every key-down until it closes.
        if (HandleMenuKey(evt))
        {
            return;
        }

        // Then the overlay that owns the keyboard (a modal one, or the one holding focus).
        if (RouteOverlayKey(evt))
        {
            altTapArmed = false;
            return;
        }

        // The menu bar: Alt, F10, access keys, and its keyboard mode.
        if (HandleMenuBarKey(evt))
        {
            return;
        }

        DispatchKey(evt);
    }

    /// <summary>
    /// Key-down dispatch against <see cref="rootNode"/> — the page, or an overlay's tree while
    /// <see cref="RouteOverlayKey"/> has swapped it in.
    /// </summary>
    private void DispatchKey(NativeKeyEvent evt)
    {
        if (TryCopyLabelSelection(evt))
        {
            return;
        }

        // CommandPalette intercepts all input when open
        if (CommandPalette.IsOpen && CommandPalette.Instance != null)
        {
            HandleCommandPaletteKeyboard(evt);
            return;
        }

        // An open suggestion list (autocomplete) takes Up/Down/Enter/Tab/Escape before the field (and before Escape clears focus).
        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is ISuggestionHost { Suggestions.IsOpen: true } suggestionHost
            && HandleSuggestionKey(suggestionHost, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }
        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is Calendar focusedCalendar && evt.Modifiers == ModifierKeys.None
            && HandleCalendarKey(focusedCalendar, evt))
        {
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, focusedCalendar);
            }
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is TimePicker { IsDisabled: false } focusedTime
            && HandleTimePickerKey(focusedTime, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is NavItem { Owner: { } navOwner } focusedNav
            && evt.Modifiers == ModifierKeys.None && HandleNavItemKey(navOwner, focusedNav, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && HandleDateFamilyKey(evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        // A focused date picker opens from the keyboard, on its date (or today).
        if (evt.Type == NativeKeyEventType.KeyDown && openDatePicker is null && FocusManager.FocusedElement is DatePicker { IsCalendarOpen: false } focusedDate
            && evt.Key is Key.Enter or Key.Space or Key.Down)
        {
            focusedDate.OpenCalendar();
            focusedDate.MoveKeyboardTo(focusedDate.Value.Value ?? DateOnly.FromDateTime(DateTime.Today));
            openDatePicker = focusedDate;
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is RangeSlider { IsDisabled: false } focusedRange
            && HandleRangeSliderKey(focusedRange, evt))
        {
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, focusedRange);
            }
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is IRadioButton and Node focusedRadio
            && evt.Modifiers == ModifierKeys.None && HandleRadioKey(focusedRadio, evt))
        {
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, focusedRadio);
            }
            RequestRepaint?.Invoke();
            return;
        }

        if (activeEyeDropper is { } dropping && evt.Key == Key.Escape)
        {
            FinishEyeDropper(dropping, take: false);
            return;
        }
        if (evt.Type == NativeKeyEventType.KeyDown && FocusManager.FocusedElement is ColorPicker { IsDisabled: false } focusedPicker
            && HandleColorPickerKey(focusedPicker, evt))
        {
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, focusedPicker);
            }
            RequestRepaint?.Invoke();
            return;
        }

        // Select dropdown keyboard navigation (intercepts before normal key handling)
        if (openSelect != null)
        {
            HandleSelectKeyboard(evt);
            return;
        }

        // Combobox keyboard navigation and text input
        if (openCombobox != null)
        {
            HandleComboboxKeyboard(evt);
            return;
        }

        // An open MultiSelect: arrows move, Space/Enter tick the highlighted row, typing searches.
        if (openMultiSelect != null && evt.Type == NativeKeyEventType.KeyDown && HandleMultiSelectKeyboard(openMultiSelect, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        // DatePicker calendar keyboard navigation
        if (openDatePicker != null)
        {
            HandleCalendarKeyboard(evt);
            return;
        }

        // DataGrid/DataTable keyboard navigation — must run before generic
        // Escape/Tab handlers so overlays (select dropdown, date popup) and
        // cell editing can intercept keys first.
        var focusedNode = FocusManager.FocusedElement;
        if (focusedNode is ITabularDataNode tdnFocus)
        {
            if (HandleTabularDataKeyboard(tdnFocus, evt))
            {
                // A key the grid used (Space selecting a row, Enter starting an edit) must not
                // also type its character into the cell.
                if (evt.Key != Key.None)
                {
                    suppressNextCharacter = true;
                }

                return;
            }
        }

        // A focused HotkeyPicker records the combination (before Tab traversal, so Ctrl+Tab etc.
        // can be recorded).
        if (focusedNode is HotkeyPicker picker && picker.HandleKey(evt.Key, evt.Modifiers))
        {
            suppressNextCharacter = true;
            RequestRepaint?.Invoke();
            return;
        }

        // Tab shortcuts (Ctrl+Tab, Ctrl+W, Ctrl+1…9) for the focused or nearest TabBar — before
        // Tab-key traversal, so Ctrl+Tab switches tabs rather than moving focus.
        if (TryHandleTabShortcut(evt))
        {
            suppressNextCharacter = true;
            RequestRepaint?.Invoke();
            return;
        }

        // Tab key → focus traversal
        if (evt.Key == Key.Tab)
        {
            bool backward = evt.Modifiers.HasFlag(ModifierKeys.Shift);
            var previousFocus = FocusManager.FocusedElement;

            // Derive the tab order from the live rendered tree in document order
            // rather than the focusable-registration list. Registration only
            // happens when a control is clicked or explicitly marked focusable, so
            // a freshly loaded form had no tab order until a field was clicked.
            // Walking the tree makes every enabled interactive control a tab stop
            // in visual order, out of the box, and reflects the current disabled
            // state without any registration bookkeeping.
            var newFocus = FindNextTabStop(previousFocus, backward);
            if (newFocus != null && !ReferenceEquals(newFocus, previousFocus))
            {

                FocusManager.RequestFocus(newFocus);
                // RequestFocus clears the keyboard-focus flag (it assumes a mouse
                // origin); this traversal is keyboard-driven, so the focus ring
                // should show.
                FocusManager.LastFocusWasKeyboard = true;

                // The focused control paints ActiveEditBuffer (not its own Bindable)
                // and commits it on the next keystroke, so without re-seeding,
                // tabbing into a field would show — and then save — the *previous*
                // field's text (the buffer is a single shared static).
                SeedEditBuffersForFocus(previousFocus, newFocus);
            }

            RequestRepaint?.Invoke();
            return;
        }

        // Escape → clear focus (but if MentionInput popup is open, just close it)
        if (evt.Key == Key.Escape)
        {
            if (openNotificationBell != null)
            {
                openNotificationBell.Close();
                openNotificationBell = null;
                RequestRepaint?.Invoke();
                return;
            }

            if (focusedNode is MentionInput mi && mi.IsPopupOpen)
            {
                mi.ClosePopup();
                RequestRepaint?.Invoke();
                return;
            }

            if (focusedNode is PropertyGrid && PropertyGridEditingRow >= 0)
            {
                CancelPropertyGridEdit();
                RequestRepaint?.Invoke();
                return;
            }

            // An app's own Escape binding (close a panel, hide a launcher) wins over the default.
            if (DispatchKeyBinding(evt))
            {
                RequestRepaint?.Invoke();
                return;
            }

            // Inside a dialog, sheet or popover: Escape dismisses it (or does nothing when it
            // is not dismissable) instead of clearing focus out of it.
            if (HandleOverlayEscape())
            {
                RequestRepaint?.Invoke();
                return;
            }

            FocusManager.ClearFocus();
            textInputBuffer = null;
            ActiveEditBuffer = null;
            MentionEditBuffer = null;
            PinEditBuffer = null;
            TextAreaEditBuffer = null;
            RequestRepaint?.Invoke();
            return;
        }

        // The context-menu key / Shift+F10: an app binding wins, then the focused control's menu.
        if (IsContextMenuKey(evt))
        {
            if (DispatchKeyBinding(evt) || OpenContextMenuForFocus())
            {
                suppressNextCharacter = true;
                RequestRepaint?.Invoke();
                return;
            }
        }


        // TextInput character input — route typed characters, backspace, delete
        if (focusedNode is TextInput textInput && !textInput.IsDisabled && !textInput.IsReadOnly)
        {
            if (HandleTextInputKey(textInput, evt))
            {
                return;
            }
        }

        // NumberInput typing — digits/sign/decimal editing with a commit-on-Enter/blur buffer.
        // Unlike TextInput (which commits live and re-renders via its binding), NumberInput edits are
        // buffered, so nothing else triggers a paint — request one here after every handled key.
        if (focusedNode is INumberInput numberInput && !numberInput.IsDisabled && !numberInput.IsReadOnly)
        {
            if (HandleNumberInputKey(numberInput, evt))
            {
                RequestRepaint?.Invoke();
                return;
            }
        }

        if (evt.Type == NativeKeyEventType.KeyDown && HandleFocusedChoiceKey(focusedNode, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && focusedNode is IToggleGroup { IsControlDisabled: false } focusedGroup
            && evt.Modifiers == ModifierKeys.None && HandleToggleGroupKey(focusedGroup, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        if (evt.Type == NativeKeyEventType.KeyDown && focusedNode is Slider focusedSlider && !focusedSlider.IsDisabled && !focusedSlider.IsReadOnly
            && evt.Modifiers == ModifierKeys.None && HandleSliderKey(focusedSlider, evt))
        {
            RequestRepaint?.Invoke();
            return;
        }

        // PasswordInput character input — same as TextInput but with separate buffer
        if (focusedNode is PasswordInput pwdInput && !pwdInput.IsDisabled && !pwdInput.IsReadOnly)
        {
            if (HandlePasswordInputKey(pwdInput, evt))
            {
                return;
            }
        }

        // TextArea character input — multi-line, Enter inserts newline
        if (focusedNode is TextArea taInput && !taInput.IsDisabled && !taInput.IsReadOnly)
        {
            if (HandleTextAreaKey(taInput, evt))
            {
                return;
            }
        }

        // PinInput character input — route typed characters, backspace, arrow keys
        if (focusedNode is PinInput pinInput && !pinInput.IsDisabled && !pinInput.IsReadOnly)
        {
            if (HandlePinInputKey(pinInput, evt))
            {
                return;
            }
        }

        // TagInput character input — type to build tag, Enter/Comma to commit, Backspace to remove
        if (focusedNode is TagInput tagInput && !tagInput.IsDisabled && !tagInput.IsReadOnly)
        {
            if (HandleTagInputKey(tagInput, evt))
            {
                return;
            }
        }

        // MentionInput character input — typing, trigger detection, popup navigation
        if (focusedNode is MentionInput mentionInput && !mentionInput.IsDisabled && !mentionInput.IsReadOnly)
        {
            if (HandleMentionInputKey(mentionInput, evt))
            {
                return;
            }
        }

        // PropertyGrid inline editing — keyboard input for text/number fields
        if (focusedNode is PropertyGrid pgFocused && PropertyGridEditingRow >= 0)
        {
            if (HandlePropertyGridKey(pgFocused, evt))
            {
                return;
            }
        }

        // A focused tab bar: arrows, Home/End, Enter/Space, Delete.
        if (focusedNode is TabBar focusedTabBar && HandleTabBarKey(focusedTabBar, evt))
        {
            suppressNextCharacter = true;
            RequestRepaint?.Invoke();
            return;
        }

        // Enter/Space on focused button → invoke click
        if (evt.Key is Key.Enter or Key.Space)
        {
            if (focusedNode is Button btn && !btn.IsDisabled && !btn.IsLoading)
            {
                btn.OnClick();
                return;
            }

            if (focusedNode is LinkButton lb && !lb.IsDisabled)
            {
                lb.OnClick();
                return;
            }

            if (focusedNode is IconButton ib && !ib.IsDisabled && !ib.IsLoading)
            {
                ib.OnClick();
                return;
            }
        }

        // Space flips a focused checkbox or toggle, as a click does.
        if (evt.Key == Key.Space && focusedNode is Checkbox or Toggle)
        {
            if (TryToggle(focusedNode))
            {
                suppressNextCharacter = true;
                RequestRepaint?.Invoke();
            }
            return;
        }

        // App menu accelerators (MenuBar item shortcuts, e.g. Ctrl+Z / Ctrl+S) —
        // fire even while a control is focused, as long as that control did not
        // consume the key above (text controls return early when they do).
        if (DispatchMenuBarShortcut(evt))
        {
            suppressNextCharacter = true;
            // A menu action (e.g. Undo) may have changed the value bound to the
            // focused text control. That control renders its own edit buffer while
            // focused, so refresh the buffer from the (possibly new) bound value or
            // the change would not appear until focus leaves.
            ResyncFocusedEditBuffer();
            RequestRepaint?.Invoke();
            return;
        }

        // Arrow-key navigation in a focused selectable list.
        if (focusedNode is IListViewNode focusedList && focusedList.IsSelectable && HandleListViewKeyboard(focusedList, evt))
        {
            suppressNextCharacter = true;
            RequestRepaint?.Invoke();
            return;
        }

        // Dispatch to KeyHandler bindings on the focused element's ancestor chain
        if (DispatchKeyBinding(evt))
        {
            suppressNextCharacter = true;
            return;
        }

        // A key nothing used scrolls the ScrollView around the focus (or under the pointer).
        if (HandleScrollViewKey(evt))
        {
            suppressNextCharacter = true;
        }
    }

    /// <summary>
    /// Drops the focused text control's edit buffer after an external change (e.g. an
    /// undo/redo from a menu accelerator) so it re-reads its bound value on the next
    /// paint. We can't read the value here: the focused node's <c>Bindable</c> holds a
    /// snapshot from the last render, not the field the action just changed — the
    /// re-render the action scheduled rebuilds the node with the new value, and a null
    /// buffer makes the control show it (and re-init on the next keystroke). The
    /// painter clamps the caret against the new text, so no stale index survives.
    /// </summary>
    private static void ResyncFocusedEditBuffer()
    {
        switch (FocusManager.FocusedElement)
        {
            case TextArea:
                TextAreaEditBuffer = null;
                break;
            case TextInput:
                ActiveEditBuffer = null;
                break;
        }
    }

    /// <summary>
    /// Walks the tree for <see cref="MenuBar"/> nodes and invokes the first enabled
    /// menu item (including nested submenu items) whose shortcut matches the event.
    /// Menu accelerators are global — they work without opening the menu.
    /// </summary>
    private bool DispatchMenuBarShortcut(NativeKeyEvent evt)
    {
        return rootNode is not null && TryMenuBarShortcut(rootNode, evt);
    }

    private static bool TryMenuBarShortcut(Node node, NativeKeyEvent evt)
    {
        if (node is MenuBar mb)
        {
            foreach (var menu in mb.Menus)
            {
                if (InvokeMatchingMenuItem(menu.Items, evt))
                {
                    return true;
                }
            }
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (TryMenuBarShortcut(child, evt))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InvokeMatchingMenuItem(IReadOnlyList<MenuItem> items, NativeKeyEvent evt)
    {
        foreach (var item in items)
        {
            if (item.Shortcut is { } hotkey
                && item.Enabled
                && hotkey.Key == evt.Key
                && hotkey.Modifiers == evt.Modifiers)
            {
                if (item.OnClick is not null)
                {
                    item.OnClick();
                    return true;
                }
                if (item.ToggleValue.OnChange is not null)
                {
                    item.ToggleValue.OnChange(item.Kind == MenuItemKind.Radio || !item.ToggleValue.Value);
                    return true;
                }
            }

            if (item.Items is not null && InvokeMatchingMenuItem(item.Items, evt))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the next (or previous, when <paramref name="backward"/> is true) keyboard
    /// tab stop relative to <paramref name="current"/>, in document order over the live
    /// rendered tree. Wraps around at the ends. Returns null when the tree has no tab
    /// stops.
    /// </summary>
    /// <remarks>
    /// While <paramref name="current"/> is inside a <see cref="FocusExtensions.FocusTrap{T}"/>
    /// subtree, the order is that subtree's tab stops only (the innermost trap when traps nest),
    /// so Tab and Shift+Tab wrap inside it. A trap does not pull focus in: from outside it, Tab
    /// walks the whole tree and may enter the trap, after which focus stays inside.
    /// </remarks>
    private Node? FindNextTabStop(Node? current, bool backward)
    {
        if (rootNode is null)
        {
            return null;
        }

        Node scope = rootNode;
        if (current is not null && TryFindFocusTrap(rootNode, current, null, out var trap) && trap is not null)
        {
            scope = trap;
        }

        var stops = new List<Node>();
        CollectTabStops(scope, stops);
        if (stops.Count == 0)
        {
            return null;
        }

        int currentIndex = -1;
        for (int i = 0; i < stops.Count; i++)
        {
            if (ReferenceEquals(stops[i], current))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            // Focus is not on a known tab stop (or nothing focused) — enter the order
            // at the first stop tabbing forward, the last tabbing backward.
            return backward ? stops[^1] : stops[0];
        }

        int nextIndex = backward
            ? (currentIndex - 1 + stops.Count) % stops.Count
            : (currentIndex + 1) % stops.Count;
        return stops[nextIndex];
    }

    /// <summary>
    /// Finds <paramref name="target"/> under <paramref name="node"/>. Returns true when found, with
    /// <paramref name="trap"/> set to the innermost <c>.FocusTrap()</c> node on the path to it
    /// (the target itself included), or null when no node on the path traps focus.
    /// </summary>
    private static bool TryFindFocusTrap(Node node, Node target, Node? enclosingTrap, out Node? trap)
    {
        if (node.LayoutData.FocusData is { FocusTrap: true })
        {
            enclosingTrap = node;
        }

        if (ReferenceEquals(node, target))
        {
            trap = enclosingTrap;
            return true;
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (TryFindFocusTrap(child, target, enclosingTrap, out trap))
            {
                return true;
            }
        }

        trap = null;
        return false;
    }

    /// <summary>
    /// Walks the tree in document (paint) order, appending every enabled keyboard tab
    /// stop to <paramref name="result"/>.
    /// </summary>
    private static void CollectTabStops(Node node, List<Node> result) => CollectTabStops(node, result, null);

    // A radio group is one tab stop: its selected option (or its first); the arrows move within it.
    private static void CollectTabStops(Node node, List<Node> result, HashSet<Node>? notStops)
    {
        if (node is IRadioGroup group)
        {
            var radios = RadiosIn(group.Content);
            var chosen = radios.FirstOrDefault(r => ((IRadioButton)r).IsSelected) ?? radios.FirstOrDefault();
            notStops = [.. (notStops ?? []), .. radios.Where(r => !ReferenceEquals(r, chosen))];
        }

        if (IsTabStop(node) && notStops?.Contains(node) != true)
        {
            result.Add(node);
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            CollectTabStops(child, result, notStops);
        }
    }

    /// <summary>The radio buttons inside a group's content, in order.</summary>
    private static List<Node> RadiosIn(Node content)
    {
        var radios = new List<Node>();
        void Walk(Node n)
        {
            if (n is IRadioButton)
            {
                radios.Add(n);
                return;
            }
            foreach (var child in NodeDiffer.GetChildren(n))
            {
                Walk(child);
            }
        }
        Walk(content);
        return radios;
    }

    /// <summary>The radio group a radio button is in (searched from the root: nodes have no parent link).</summary>
    private IRadioGroup? GroupOf(Node radio)
    {
        IRadioGroup? found = null;
        void Walk(Node n)
        {
            if (found is not null)
            {
                return;
            }
            if (n is IRadioGroup group && RadiosIn(group.Content).Contains(radio))
            {
                found = group;
                return;
            }
            foreach (var child in NodeDiffer.GetChildren(n))
            {
                Walk(child);
            }
        }
        if (rootNode is not null)
        {
            Walk(rootNode);
        }
        return found;
    }

    /// <summary>
    /// Keys on a focused radio button: the arrows select the next (Down/Right) or previous
    /// (Up/Left) option of its group, wrapping, and focus moves with them; Space selects it.
    /// </summary>
    private bool HandleRadioKey(Node radio, NativeKeyEvent evt)
    {
        if (evt.Key == Key.Space || evt.Character == ' ')
        {
            ((IRadioButton)radio).Select();
            return true;
        }
        int step = evt.Key switch
        {
            Key.Down or Key.Right => 1,
            Key.Up or Key.Left => -1,
            _ => 0,
        };
        if (step == 0 || GroupOf(radio) is not { IsGroupDisabled: false } group)
        {
            return false;
        }
        var radios = RadiosIn(group.Content);
        int at = radios.IndexOf(radio);
        if (at < 0 || radios.Count == 0)
        {
            return false;
        }
        var next = radios[(at + step + radios.Count) % radios.Count];
        ((IRadioButton)next).Select();
        FocusManager.RequestFocus(next);
        FocusManager.LastFocusWasKeyboard = true;
        return true;
    }

    /// <summary>
    /// True when a node should receive keyboard focus during Tab traversal: an enabled
    /// interactive control, or a node explicitly opted out of / into the tab order via
    /// <see cref="FocusExtensions"/>. Disabled controls and controls marked
    /// <see cref="TabIndex.Skip"/> are excluded.
    /// </summary>
    private static bool IsTabStop(Node node)
    {
        var focusData = node.LayoutData.FocusData;
        if (focusData is not null && !focusData.IsFocusable)
        {
            // Explicit TabIndex.Skip — never a tab stop.
            return false;
        }

        if (IsControlDisabled(node))
        {
            return false;
        }

        // Known focusable control types (the same list FindFocusableNode uses).
        if (IsFocusableControl(node))
        {
            return true;
        }

        // A node explicitly made focusable (.Focusable()/.TabIndex()/.AutoFocus()).
        return focusData is not null && focusData.IsFocusable
            && focusData.RegistrationOrder > 0;
    }

    /// <summary>
    /// True when an interactive control is disabled and should be skipped in tab order.
    /// Covers the focusable control types; other node types are treated as enabled.
    /// </summary>
    private static bool IsControlDisabled(Node node)
    {
        return node switch
        {
            Button b => b.IsDisabled,
            LinkButton lb => lb.IsDisabled,
            IconButton ib => ib.IsDisabled,
            TextInput ti => ti.IsDisabled,
            TextArea ta => ta.IsDisabled,
            PasswordInput pw => pw.IsDisabled,
            PinInput pin => pin.IsDisabled,
            MentionInput mi => mi.IsDisabled,
            TagInput tag => tag.IsDisabled,
            ColorPicker cp => cp.IsDisabled,
            Checkbox cb => cb.IsDisabled,
            Toggle tog => tog.IsDisabled,
            Slider sl => sl.IsDisabled,
            HotkeyPicker hp => hp.IsDisabled,
            TabBar tabBar => tabBar.IsDisabled,
            ISelectNode select => select.IsNodeDisabled,
            IMultiSelectNode multi => multi.IsNodeDisabled,
            IComboboxNode combo => combo.IsNodeDisabled,
            IToggleGroup group => group.IsControlDisabled,
            ISegmentedControl segmented => segmented.IsControlDisabled,
            Rating rating => rating.IsDisabled,
            _ => false
        };
    }

    /// <summary>
    /// Re-seeds the text-editing buffers when keyboard focus moves to a new control
    /// (Tab / Shift+Tab traversal). Mirrors the buffer initialization the mouse-click
    /// focus path performs, but places the caret at the end of the value rather than
    /// under the pointer. Clears the transient focus state of a TagInput/MentionInput
    /// the focus is leaving, exactly as a click to a different control would.
    /// </summary>
    /// <summary>
    /// Focus moved (any cause: click, Tab, or an app calling FocusManager.RequestFocus): point the
    /// shared edit buffers at the new control so it never shows or commits the previous one's text.
    /// </summary>
    internal static void NotifyFocusMoved(Node? previousFocus, Node? newFocus)
    {
        // Focus entering a tab bar (by Tab, a click or the app) lands on its selected tab, not
        // wherever a manual-activation cursor was left last time.
        if (newFocus is TabBar enteredTabBar && !ReferenceEquals(previousFocus, newFocus))
        {
            enteredTabBar.State.FocusPosition = -1;
        }

        current?.SeedEditBuffersForFocus(previousFocus, newFocus);
    }

    private void SeedEditBuffersForFocus(Node? previousFocus, Node? newFocus)
    {
        // Clear the control we are leaving so its popup/caret state does not linger.
        if (previousFocus is TagInput leavingTag && !ReferenceEquals(leavingTag, newFocus))
        {
            leavingTag.IsFocused = false;
        }

        if (previousFocus is MentionInput leavingMention && !ReferenceEquals(leavingMention, newFocus))
        {
            leavingMention.IsFocused = false;
            leavingMention.ClosePopup();
        }

        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // A debounced change in the field being left is delivered now.
        FlushPendingChange(now: true);

        switch (newFocus)
        {
            case TextInput ti:
                textInputBuffer = ti.Value.Value ?? string.Empty;
                ActiveEditBuffer = textInputBuffer;
                TextInputCaretIndex = textInputBuffer.Length;
                TextInputSelectionAnchor = TextInputCaretIndex;
                TextInputScrollOffsetX = 0f;
                PasswordEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
                MentionEditBuffer = null;
                break;

            case PasswordInput pwd:
                PasswordEditBuffer = pwd.Value.Value ?? string.Empty;
                PasswordRevealed = false;
                PasswordScrollOffsetX = 0f;
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
                MentionEditBuffer = null;
                break;

            case PinInput pin:
                string pinVal = pin.Value.Value ?? string.Empty;
                PinEditBuffer = pinVal;
                PinActiveCellIndex = Math.Min(pinVal.Length, pin.Length - 1);
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PasswordEditBuffer = null;
                TextAreaEditBuffer = null;
                MentionEditBuffer = null;
                break;

            case TextArea ta:
                TextAreaEditBuffer = ta.Value.Value ?? string.Empty;
                TextAreaCaretIndex = TextAreaEditBuffer.Length;
                TextAreaSelectionAnchor = TextAreaCaretIndex;
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PasswordEditBuffer = null;
                PinEditBuffer = null;
                MentionEditBuffer = null;
                break;

            case TagInput tag:
                tag.IsFocused = true;
                tag.LiveTags = new List<string>(tag.Value.Value);
                tag.CaretIndex = tag.InputBuffer.Length;
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PasswordEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
                MentionEditBuffer = null;
                break;

            case MentionInput mention:
                mention.IsFocused = true;
                MentionEditBuffer = mention.Value.Value ?? string.Empty;
                ActiveEditBuffer = MentionEditBuffer;
                MentionInputCaretIndex = MentionEditBuffer.Length;
                MentionInputSelectionAnchor = MentionInputCaretIndex;
                textInputBuffer = null;
                PasswordEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
                break;

            default:
                textInputBuffer = null;
                ActiveEditBuffer = null;
                PasswordEditBuffer = null;
                PinEditBuffer = null;
                TextAreaEditBuffer = null;
                MentionEditBuffer = null;
                break;
        }
    }

    /// <summary>
    /// Handles keyboard input for a focused TextInput control.
    /// Supports caret-aware insertion, deletion, navigation, and text selection.
    /// </summary>
    private bool HandleTextInputKey(TextInput textInput, NativeKeyEvent evt)
    {
        // Use the editing buffer (not the stale Bindable value)
        textInputBuffer ??= textInput.Value.Value ?? string.Empty;
        ActiveEditBuffer = textInputBuffer;
        TextInputCaretIndex = Math.Clamp(TextInputCaretIndex, 0, textInputBuffer.Length);
        TextInputSelectionAnchor = Math.Clamp(TextInputSelectionAnchor, 0, textInputBuffer.Length);

        // Reset caret blink so it's always visible right after a keystroke
        CaretResetTimestamp = Stopwatch.GetTimestamp();

        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);

        // A masked field edits its raw characters (see HandleMaskedTextKey).
        if ((textInput.MaskPatternValue is not null || textInput.CustomMaskValue is not null)
            && HandleMaskedTextKey(textInput, evt, ctrl, shift))
        {
            return true;
        }

        // Ctrl+A — select all
        if (ctrl && evt.Key == Key.A)
        {
            TextInputSelectionAnchor = 0;
            TextInputCaretIndex = textInputBuffer.Length;
            return true;
        }

        // A password-type field's text is a secret: copy and cut never put it on the clipboard.
        if (ctrl && evt.Key is Key.C or Key.X && textInput.InputType == InputType.Password)
        {
            return true;
        }

        // Ctrl+C — copy selection
        if (ctrl && evt.Key == Key.C)
        {
            CopyTextInputSelection();
            return true;
        }

        // Ctrl+X — cut selection
        if (ctrl && evt.Key == Key.X)
        {
            if (TextInputSelectionAnchor != TextInputCaretIndex)
            {
                CopyTextInputSelection();
                DeleteTextInputSelection(textInput);
            }
            return true;
        }

        // Ctrl+V / Shift+Insert — paste
        if ((ctrl && evt.Key == Key.V) || (shift && evt.Key == Key.Insert))
        {
            PasteIntoTextInput(textInput);
            return true;
        }

        // Arrow keys — navigate within the buffer
        if (evt.Key == Key.Left)
        {
            if (!shift && TextInputSelectionAnchor != TextInputCaretIndex)
            {
                TextInputCaretIndex = Math.Min(TextInputSelectionAnchor, TextInputCaretIndex);
                TextInputSelectionAnchor = TextInputCaretIndex;
            }
            else if (TextInputCaretIndex > 0)
            {
                TextInputCaretIndex--;
                if (!shift)
                {
                    TextInputSelectionAnchor = TextInputCaretIndex;
                }
            }
            return true;
        }

        if (evt.Key == Key.Right)
        {
            if (!shift && TextInputSelectionAnchor != TextInputCaretIndex)
            {
                TextInputCaretIndex = Math.Max(TextInputSelectionAnchor, TextInputCaretIndex);
                TextInputSelectionAnchor = TextInputCaretIndex;
            }
            else if (TextInputCaretIndex < textInputBuffer.Length)
            {
                TextInputCaretIndex++;
                if (!shift)
                {
                    TextInputSelectionAnchor = TextInputCaretIndex;
                }
            }
            return true;
        }

        if (evt.Key == Key.Home)
        {
            TextInputCaretIndex = 0;
            if (!shift)
            {
                TextInputSelectionAnchor = TextInputCaretIndex;
            }
            return true;
        }

        if (evt.Key == Key.End)
        {
            TextInputCaretIndex = textInputBuffer.Length;
            if (!shift)
            {
                TextInputSelectionAnchor = TextInputCaretIndex;
            }
            return true;
        }

        // Character input (from WM_CHAR) — insert at caret, replace selection
        if (evt.Character is char ch && ch >= ' ')
        {
            if (TextInputSelectionAnchor != TextInputCaretIndex)
            {
                DeleteTextInputSelection(textInput);
            }

            if (textInput.MaxLengthValue.HasValue && textInputBuffer.Length >= textInput.MaxLengthValue.Value)
            {
                return true;
            }

            textInputBuffer = textInputBuffer.Insert(TextInputCaretIndex, ch.ToString());
            TextInputCaretIndex++;
            TextInputSelectionAnchor = TextInputCaretIndex;
            ActiveEditBuffer = textInputBuffer;
            textInput.Value.OnChange(textInputBuffer);
            RaiseOnChange(textInput.OnChangeHandler, textInput.DebounceDelay, textInputBuffer);
            return true;
        }

        // Backspace — delete selection or character before caret
        if (evt.Character == '\b')
        {
            if (TextInputSelectionAnchor != TextInputCaretIndex)
            {
                DeleteTextInputSelection(textInput);
            }
            else if (TextInputCaretIndex > 0)
            {
                textInputBuffer = textInputBuffer.Remove(TextInputCaretIndex - 1, 1);
                TextInputCaretIndex--;
                TextInputSelectionAnchor = TextInputCaretIndex;
                ActiveEditBuffer = textInputBuffer;
                textInput.Value.OnChange(textInputBuffer);
                RaiseOnChange(textInput.OnChangeHandler, textInput.DebounceDelay, textInputBuffer);
            }
            return true;
        }

        // Delete key — delete selection or character at caret
        if (evt.Key == Key.Delete)
        {
            if (TextInputSelectionAnchor != TextInputCaretIndex)
            {
                DeleteTextInputSelection(textInput);
            }
            else if (TextInputCaretIndex < textInputBuffer.Length)
            {
                textInputBuffer = textInputBuffer.Remove(TextInputCaretIndex, 1);
                ActiveEditBuffer = textInputBuffer;
                textInput.Value.OnChange(textInputBuffer);
                RaiseOnChange(textInput.OnChangeHandler, textInput.DebounceDelay, textInputBuffer);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles keyboard input for a focused PasswordInput: the same single-line editing as a
    /// TextInput (caret, Shift-selection, Home/End, Backspace/Delete at the caret, paste replacing
    /// the selection) except that the text never leaves the field: copy and cut do nothing, and
    /// Ctrl+Left/Right jump to the ends rather than revealing where the words break.
    /// </summary>
    private static bool HandlePasswordInputKey(PasswordInput pwd, NativeKeyEvent evt)
    {
        PasswordEditBuffer ??= pwd.Value.Value ?? string.Empty;
        string buffer = PasswordEditBuffer;
        PasswordCaretIndex = Math.Clamp(PasswordCaretIndex, 0, buffer.Length);
        PasswordSelectionAnchor = Math.Clamp(PasswordSelectionAnchor, 0, buffer.Length);
        CaretResetTimestamp = Stopwatch.GetTimestamp();
        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);
        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        bool hasSelection = PasswordSelectionAnchor != PasswordCaretIndex;

        if (ctrl && evt.Key == Key.A)
        {
            PasswordSelectionAnchor = 0;
            PasswordCaretIndex = buffer.Length;
            return true;
        }

        if ((ctrl && evt.Key == Key.V) || (shift && evt.Key == Key.Insert))
        {
            var content = Clipboard.GetContentAsync().GetAwaiter().GetResult();
            string? pasted = content.HasText ? content.GetTextAsync().GetAwaiter().GetResult() : null;
            if (!string.IsNullOrEmpty(pasted))
            {
                // One line: a key copied from a web page often brings a line break with it.
                ReplacePasswordSelection(pwd, pasted.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal));
            }
            return true;
        }

        // The text is a secret: copy and cut are swallowed rather than passed on.
        if ((ctrl && evt.Key is Key.C or Key.X or Key.Insert) || (shift && evt.Key == Key.Delete))
        {
            return true;
        }

        if (evt.Key is Key.Left or Key.Right or Key.Home or Key.End)
        {
            int target = evt.Key switch
            {
                Key.Home => 0,
                Key.End => buffer.Length,
                Key.Left when ctrl => 0,
                Key.Right when ctrl => buffer.Length,
                // Without Shift, an arrow collapses a selection to the side it points at.
                Key.Left when hasSelection && !shift => Math.Min(PasswordSelectionAnchor, PasswordCaretIndex),
                Key.Right when hasSelection && !shift => Math.Max(PasswordSelectionAnchor, PasswordCaretIndex),
                Key.Left => Math.Max(0, PasswordCaretIndex - 1),
                _ => Math.Min(buffer.Length, PasswordCaretIndex + 1),
            };
            PasswordCaretIndex = target;
            if (!shift)
            {
                PasswordSelectionAnchor = target;
            }
            return true;
        }

        if (evt.Character is { } typed && typed >= ' ' && typed != 127 && !ctrl)
        {
            ReplacePasswordSelection(pwd, typed.ToString());
            return true;
        }

        // Backspace / Delete: the selection, else one character (Ctrl: everything on that side).
        // Plain Backspace arrives as the '\b' character; Ctrl+Backspace only as the key (Windows
        // drops its character), so each is acted on once.
        if (evt.Character == '\b' || (ctrl && evt.Key == Key.Backspace) || evt.Key == Key.Delete)
        {
            bool forward = evt.Key == Key.Delete;
            if (!hasSelection)
            {
                int caret = PasswordCaretIndex;
                PasswordSelectionAnchor = forward
                    ? (ctrl ? buffer.Length : Math.Min(buffer.Length, caret + 1))
                    : (ctrl ? 0 : Math.Max(0, caret - 1));
            }
            ReplacePasswordSelection(pwd, string.Empty);
            return true;
        }

        return false;
    }

    // Replaces the selection (or inserts at the caret) with text, kept within MaxLength.
    private static void ReplacePasswordSelection(PasswordInput pwd, string text)
    {
        string buffer = PasswordEditBuffer ?? string.Empty;
        int start = Math.Min(PasswordSelectionAnchor, PasswordCaretIndex);
        int end = Math.Max(PasswordSelectionAnchor, PasswordCaretIndex);
        string kept = buffer[..start] + buffer[end..];
        if (pwd.MaxLengthValue is { } max && kept.Length + text.Length > max)
        {
            text = text[..Math.Max(0, max - kept.Length)];
        }
        if (text.Length == 0 && start == end)
        {
            return;
        }
        SetPasswordText(pwd, kept.Insert(start, text), start + text.Length);
    }

    private static void SetPasswordText(PasswordInput pwd, string text, int caret)
    {
        passwordEditBuffer = text;
        PasswordCaretIndex = caret;
        PasswordSelectionAnchor = caret;
        pwd.Value.OnChange(text);
    }

    /// <summary>
    /// The caret index for a click at window x on a password field: measured against the text as the
    /// painter draws it (masked or revealed, same font, padding and scroll).
    /// </summary>
    internal int PasswordCaretIndexAt(PasswordInput pwd, string buffer, float x)
    {
        if (buffer.Length == 0 || Live(pwd, pwd.AbsoluteBounds).Width <= 0)
        {
            return 0;
        }
        var theme = ThemeSwitcher.Current;
        float scrollX = ReferenceEquals(FocusManager.FocusedElement, pwd) ? PasswordScrollOffsetX : 0f;
        float relX = x - Live(pwd, pwd.AbsoluteBounds).X - theme.TextInput.PaddingH + scrollX;
        if (relX <= 0)
        {
            return 0;
        }
        string? fontPath = LayoutSolver.DefaultFontPath;
        if (string.IsNullOrEmpty(fontPath))
        {
            return buffer.Length;
        }
        string drawn = PasswordRevealed ? buffer : new string('●', buffer.Length);
        var layout = TextLayoutEngine.Layout(drawn, new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = theme.Typography.Scale.Body.Size,
            MaxWidth = float.PositiveInfinity,
            MaxLines = 1,
            Overflow = TextOverflow.Clip,
        });
        if (layout.Lines.Count == 0)
        {
            return buffer.Length;
        }
        var line = layout.Lines[0];
        var hit = layout.HitTest(relX, line.Y + line.Height / 2f);
        return Math.Clamp(hit.Offset + (hit.IsTrailingEdge ? 1 : 0), 0, buffer.Length);
    }
    /// <summary>
    /// Handles keyboard input for a focused TextArea.
    /// Supports caret-aware insertion, deletion, navigation, and text selection.
    /// </summary>
    private static bool HandleTextAreaKey(TextArea ta, NativeKeyEvent evt)
    {
        TextAreaEditBuffer ??= ta.Value.Value ?? string.Empty;
        TextAreaCaretIndex = Math.Clamp(TextAreaCaretIndex, 0, TextAreaEditBuffer.Length);
        TextAreaSelectionAnchor = Math.Clamp(TextAreaSelectionAnchor, 0, TextAreaEditBuffer.Length);

        CaretResetTimestamp = Stopwatch.GetTimestamp();

        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);

        // Ctrl+A — select all
        if (ctrl && evt.Key == Key.A)
        {
            TextAreaSelectionAnchor = 0;
            TextAreaCaretIndex = TextAreaEditBuffer.Length;
            return true;
        }

        // Ctrl+C — copy selection
        if (ctrl && evt.Key == Key.C)
        {
            CopyTextAreaSelection();
            return true;
        }

        // Ctrl+X — cut selection
        if (ctrl && evt.Key == Key.X)
        {
            if (TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                CopyTextAreaSelection();
                DeleteTextAreaSelection(ta);
            }
            return true;
        }

        // Ctrl+V — paste at caret (replacing selection if any)
        if (ctrl && evt.Key == Key.V)
        {
            PasteIntoTextArea(ta);
            return true;
        }

        // Enter inserts a newline at caret. Windows delivers Enter as WM_CHAR '\r'
        // (and a separate WM_KEYDOWN Key.Enter we ignore to avoid a double-fire);
        // '\n' arrives from synthetic input (paste of a bare LF, automation) — treat
        // both as a newline.
        if (evt.Character is '\r' or '\n')
        {
            if (TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                DeleteTextAreaSelection(ta);
            }
            TextAreaEditBuffer = TextAreaEditBuffer.Insert(TextAreaCaretIndex, "\n");
            TextAreaCaretIndex++;
            TextAreaSelectionAnchor = TextAreaCaretIndex;
            ta.Value.OnChange(TextAreaEditBuffer);
            RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
            return true;
        }

        // Arrow keys — navigate within the buffer
        if (evt.Key == Key.Left)
        {
            if (!shift && TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                TextAreaCaretIndex = Math.Min(TextAreaSelectionAnchor, TextAreaCaretIndex);
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            else if (TextAreaCaretIndex > 0)
            {
                TextAreaCaretIndex--;
                if (!shift)
                {
                    TextAreaSelectionAnchor = TextAreaCaretIndex;
                }
            }
            return true;
        }

        if (evt.Key == Key.Right)
        {
            if (!shift && TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                TextAreaCaretIndex = Math.Max(TextAreaSelectionAnchor, TextAreaCaretIndex);
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            else if (TextAreaCaretIndex < TextAreaEditBuffer.Length)
            {
                TextAreaCaretIndex++;
                if (!shift)
                {
                    TextAreaSelectionAnchor = TextAreaCaretIndex;
                }
            }
            return true;
        }

        if (evt.Key == Key.Up)
        {
            MoveTextAreaCaretVertically(-1);
            if (!shift)
            {
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            return true;
        }

        if (evt.Key == Key.Down)
        {
            MoveTextAreaCaretVertically(1);
            if (!shift)
            {
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            return true;
        }

        if (evt.Key == Key.Home)
        {
            if (ctrl)
            {
                // Ctrl+Home → document start (Ctrl+Shift+Home extends the selection).
                TextAreaCaretIndex = 0;
            }
            else
            {
                // Move to the start of the current *visual* line (soft-wrap aware).
                var layout = GetTextAreaLayout(TextAreaEditBuffer);
                if (layout is not null && layout.Lines.Count > 0)
                {
                    int li = TextAreaVisualLineIndex(layout, TextAreaEditBuffer, TextAreaCaretIndex);
                    if (li < layout.Lines.Count)
                    {
                        TextAreaCaretIndex = layout.Lines[li].TextStart;
                    }
                    // A phantom trailing row's start IS the buffer end — leave the caret.
                }
                else
                {
                    int lineStart = TextAreaCaretIndex > 0
                        ? TextAreaEditBuffer.LastIndexOf('\n', TextAreaCaretIndex - 1)
                        : -1;
                    TextAreaCaretIndex = lineStart < 0 ? 0 : lineStart + 1;
                }
            }
            if (!shift)
            {
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            return true;
        }

        if (evt.Key == Key.End)
        {
            if (ctrl)
            {
                // Ctrl+End → document end (Ctrl+Shift+End extends the selection).
                TextAreaCaretIndex = TextAreaEditBuffer.Length;
            }
            else
            {
                // Move to the end of the current *visual* line (soft-wrap aware).
                var layout = GetTextAreaLayout(TextAreaEditBuffer);
                if (layout is not null && layout.Lines.Count > 0)
                {
                    int li = TextAreaVisualLineIndex(layout, TextAreaEditBuffer, TextAreaCaretIndex);
                    if (li < layout.Lines.Count)
                    {
                        var line = layout.Lines[li];
                        int end = line.TextStart + line.TextLength;
                        // A hard-newline line folds its trailing '\n' into TextLength; End
                        // must land before the '\n', not at the start of the next row.
                        if (end > line.TextStart && end <= TextAreaEditBuffer.Length
                            && TextAreaEditBuffer[end - 1] == '\n')
                        {
                            end--;
                        }
                        TextAreaCaretIndex = end;
                    }
                    // Phantom trailing row: caret is already at the buffer end.
                }
                else
                {
                    int lineEnd = TextAreaEditBuffer.IndexOf('\n', TextAreaCaretIndex);
                    TextAreaCaretIndex = lineEnd < 0 ? TextAreaEditBuffer.Length : lineEnd;
                }
            }
            if (!shift)
            {
                TextAreaSelectionAnchor = TextAreaCaretIndex;
            }
            return true;
        }

        // Printable characters — insert at caret (replace selection if any)
        if (evt.Character is char ch && ch >= ' ')
        {
            if (TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                DeleteTextAreaSelection(ta);
            }

            if (ta.MaxLengthValue.HasValue && TextAreaEditBuffer.Length >= ta.MaxLengthValue.Value)
            {
                return true;
            }

            TextAreaEditBuffer = TextAreaEditBuffer.Insert(TextAreaCaretIndex, ch.ToString());
            TextAreaCaretIndex++;
            TextAreaSelectionAnchor = TextAreaCaretIndex;
            ta.Value.OnChange(TextAreaEditBuffer);
            RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
            return true;
        }

        // Backspace — delete selection or character before caret
        if (evt.Character == '\b')
        {
            if (TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                DeleteTextAreaSelection(ta);
            }
            else if (TextAreaCaretIndex > 0)
            {
                TextAreaEditBuffer = TextAreaEditBuffer.Remove(TextAreaCaretIndex - 1, 1);
                TextAreaCaretIndex--;
                TextAreaSelectionAnchor = TextAreaCaretIndex;
                ta.Value.OnChange(TextAreaEditBuffer);
                RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
            }
            return true;
        }

        // Delete key — delete selection or character at caret
        if (evt.Key == Key.Delete)
        {
            if (TextAreaSelectionAnchor != TextAreaCaretIndex)
            {
                DeleteTextAreaSelection(ta);
            }
            else if (TextAreaCaretIndex < TextAreaEditBuffer.Length)
            {
                TextAreaEditBuffer = TextAreaEditBuffer.Remove(TextAreaCaretIndex, 1);
                ta.Value.OnChange(TextAreaEditBuffer);
                RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the caret's *visual* line index in the wrapped TextArea layout,
    /// matching how the painter positions the caret. A caret immediately after a
    /// '\n' belongs at the START of the next row (downstream affinity), but the
    /// fast-path layout folds a line's trailing '\n' into its <c>TextLength</c>, so
    /// that line's [TextStart, TextStart + TextLength] range overlaps the next line's
    /// start and <see cref="TextLayoutResult.GetLineIndexForOffset"/> returns the
    /// PREVIOUS line — which is why Up/Down/Home/End did nothing (or jumped) on a
    /// blank line or at the start of a line. Returns <c>layout.Lines.Count</c> for a
    /// caret on the phantom trailing row after a final '\n' (the engine emits no line
    /// for it, matching the painter's fresh-row handling).
    /// </summary>
    internal static int TextAreaVisualLineIndex(TextLayoutResult layout, string buf, int caret)
    {
        if (caret > 0 && caret <= buf.Length && buf[caret - 1] == '\n')
        {
            for (int i = 0; i < layout.Lines.Count; i++)
            {
                if (layout.Lines[i].TextStart == caret)
                {
                    return i;
                }
            }
            return layout.Lines.Count; // phantom trailing row below the last real line
        }
        return layout.GetLineIndexForOffset(caret);
    }

    /// <summary>
    /// Moves the TextArea caret up or down by the specified number of lines.
    /// </summary>
    private static void MoveTextAreaCaretVertically(int direction)
    {
        string buf = TextAreaEditBuffer ?? "";
        int caret = Math.Clamp(TextAreaCaretIndex, 0, buf.Length);

        // Move across *visual* (soft-wrapped) lines so Up/Down track what the user
        // sees, not the underlying paragraphs. Reuse the painter's wrapped layout.
        var layout = GetTextAreaLayout(buf);
        if (layout is null || layout.Lines.Count == 0)
        {
            return;
        }

        int lineCount = layout.Lines.Count;
        int currentLine = TextAreaVisualLineIndex(layout, buf, caret);

        // Preserve the caret's horizontal position. At a line start (right after a
        // '\n') the caret sits at the line origin, but GetCaretInfo would report the
        // PREVIOUS line's right edge (its range includes the '\n') — so use the line
        // origin directly. On the phantom trailing row the origin is column 0.
        float caretX;
        bool atLineStart = caret > 0 && buf[caret - 1] == '\n';
        if (currentLine >= lineCount)
        {
            caretX = 0f;
        }
        else if (atLineStart)
        {
            caretX = layout.Lines[currentLine].X;
        }
        else
        {
            caretX = layout.GetCaretInfo(caret).X;
        }

        int targetLine = currentLine + direction;
        if (targetLine < 0)
        {
            TextAreaCaretIndex = 0;
            return;
        }
        if (targetLine >= lineCount)
        {
            TextAreaCaretIndex = buf.Length;
            return;
        }

        // Hit test the preserved X against the vertical middle of the destination
        // line. A trailing '\n' has no visual glyph, so a hit past the line's text
        // lands the caret before the newline (the end of the visible line), never
        // after it.
        var destLine = layout.Lines[targetLine];
        float destY = destLine.Y + destLine.Height / 2f;
        var hit = layout.HitTest(caretX, destY);
        int offset = hit.Offset + (hit.IsTrailingEdge ? 1 : 0);
        TextAreaCaretIndex = Math.Clamp(offset, 0, buf.Length);
    }

    /// <summary>
    /// Computes caret position from mouse click coordinates on a TextArea.
    /// Uses the TextArea's AbsoluteBounds and the painter's line height.
    /// </summary>
    private void PositionTextAreaCaretFromMouse(TextArea ta, NativeMouseEvent evt)
    {
        var bounds = Live(ta, ta.AbsoluteBounds);
        if (bounds.Width <= 0)
        {
            return;
        }

        string buf = TextAreaEditBuffer ?? "";
        if (buf.Length == 0)
        {
            TextAreaCaretIndex = 0;
            return;
        }

        var layout = GetTextAreaLayout(buf);
        if (layout is null || layout.Lines.Count == 0)
        {
            TextAreaCaretIndex = Math.Clamp(buf.Length, 0, buf.Length);
            return;
        }

        // Convert the click into layout-space coordinates (matching how the painter
        // positions each wrapped line: contentLeft = bounds.X + paddingH,
        // contentTop = bounds.Y + paddingV, minus the scroll offset).
        float relX = evt.X - bounds.X - TextAreaPaddingH;
        float relY = evt.Y - bounds.Y - TextAreaPaddingV + TextAreaScrollOffsetY;

        var hit = layout.HitTest(relX, relY);
        // HitTest reports the cluster the point falls on plus which edge; a trailing
        // hit places the caret after that character.
        int offset = hit.Offset + (hit.IsTrailingEdge ? 1 : 0);
        TextAreaCaretIndex = Math.Clamp(offset, 0, buf.Length);
    }

    /// <summary>
    /// Places the caret where a click lands on a TextInput: the click is measured against the text
    /// laid out exactly as the painter draws it (same font, size, content origin and scroll).
    /// </summary>
    private void PositionTextInputCaretFromMouse(TextInput ti, NativeMouseEvent evt)
    {
        if (Live(ti, ti.AbsoluteBounds).Width <= 0)
        {
            return;
        }
        TextInputCaretIndex = CaretIndexAt(ti, ActiveEditBuffer ?? ti.Value.Value ?? "", evt.X);
    }

    /// <summary>The caret index for a click at window x <paramref name="x"/> on <paramref name="ti"/> showing <paramref name="buf"/>.</summary>
    internal int CaretIndexAt(TextInput ti, string buf, float x)
    {
        var bounds = Live(ti, ti.AbsoluteBounds);
        if (buf.Length == 0)
        {
            return 0;
        }

        var theme = ThemeSwitcher.Current;
        float scrollX = ReferenceEquals(FocusManager.FocusedElement, ti) ? TextInputScrollOffsetX : 0f;
        float relX = x - bounds.X - TextInputMetrics.ContentLeft(ti, theme) + scrollX;
        if (relX <= 0)
        {
            return 0;
        }

        // Password fields paint one mask glyph per character; measure what is drawn.
        string drawn = ti.InputType == InputType.Password ? new string('●', buf.Length) : buf;
        string? fontPath = LayoutSolver.DefaultFontPath;
        if (string.IsNullOrEmpty(fontPath))
        {
            return buf.Length;
        }
        var layout = TextLayoutEngine.Layout(drawn, new TextLayoutOptions
        {
            FontPath = fontPath,
            FontSize = TextInputMetrics.FontSize(theme),
            MaxWidth = float.PositiveInfinity,
            MaxLines = 1,
            Overflow = TextOverflow.Clip,
        });
        if (layout.Lines.Count == 0)
        {
            return buf.Length;
        }

        var line = layout.Lines[0];
        var hit = layout.HitTest(relX, line.Y + line.Height / 2f);
        return Math.Clamp(hit.Offset + (hit.IsTrailingEdge ? 1 : 0), 0, buf.Length);
    }

    // ── Clipboard and selection helpers ────────────────────────────────

    /// <summary>Copies the TextArea selection to the system clipboard.</summary>
    private static void CopyTextAreaSelection()
    {
        if (TextAreaEditBuffer == null || TextAreaSelectionAnchor == TextAreaCaretIndex)
        {
            return;
        }
        int start = Math.Min(TextAreaSelectionAnchor, TextAreaCaretIndex);
        int end = Math.Max(TextAreaSelectionAnchor, TextAreaCaretIndex);
        string selected = TextAreaEditBuffer[start..end];
        _ = Clipboard.WriteTextAsync(selected);
    }

    /// <summary>Deletes the TextArea selection and updates the bindable.</summary>
    private static void DeleteTextAreaSelection(TextArea ta)
    {
        if (TextAreaEditBuffer == null || TextAreaSelectionAnchor == TextAreaCaretIndex)
        {
            return;
        }
        int start = Math.Min(TextAreaSelectionAnchor, TextAreaCaretIndex);
        int end = Math.Max(TextAreaSelectionAnchor, TextAreaCaretIndex);
        TextAreaEditBuffer = TextAreaEditBuffer.Remove(start, end - start);
        TextAreaCaretIndex = start;
        TextAreaSelectionAnchor = start;
        ta.Value.OnChange(TextAreaEditBuffer);
        RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
    }

    /// <summary>Pastes clipboard text into the TextArea at the caret, replacing any selection.</summary>
    private static void PasteIntoTextArea(TextArea ta)
    {
        // All platform clipboard APIs return synchronous Task.FromResult tasks
        var content = Clipboard.GetContentAsync().GetAwaiter().GetResult();
        if (!content.HasText)
        {
            return;
        }
        string? text = content.GetTextAsync().GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (TextAreaSelectionAnchor != TextAreaCaretIndex)
        {
            DeleteTextAreaSelection(ta);
        }

        TextAreaEditBuffer ??= "";
        TextAreaEditBuffer = TextAreaEditBuffer.Insert(TextAreaCaretIndex, text);
        TextAreaCaretIndex += text.Length;
        TextAreaSelectionAnchor = TextAreaCaretIndex;
        ta.Value.OnChange(TextAreaEditBuffer);
        RaiseOnChange(ta.OnChangeHandler, ta.DebounceDelay, TextAreaEditBuffer);
    }

    /// <summary>Copies the TextInput selection to the system clipboard.</summary>
    private void CopyTextInputSelection()
    {
        if (textInputBuffer == null || TextInputSelectionAnchor == TextInputCaretIndex)
        {
            return;
        }
        int start = Math.Min(TextInputSelectionAnchor, TextInputCaretIndex);
        int end = Math.Max(TextInputSelectionAnchor, TextInputCaretIndex);
        string selected = textInputBuffer[start..end];
        _ = Clipboard.WriteTextAsync(selected);
    }

    /// <summary>Deletes the TextInput selection and updates the bindable.</summary>
    private void DeleteTextInputSelection(TextInput ti)
    {
        if (textInputBuffer == null || TextInputSelectionAnchor == TextInputCaretIndex)
        {
            return;
        }
        int start = Math.Min(TextInputSelectionAnchor, TextInputCaretIndex);
        int end = Math.Max(TextInputSelectionAnchor, TextInputCaretIndex);
        textInputBuffer = textInputBuffer.Remove(start, end - start);
        TextInputCaretIndex = start;
        TextInputSelectionAnchor = start;
        ActiveEditBuffer = textInputBuffer;
        ti.Value.OnChange(textInputBuffer);
        RaiseOnChange(ti.OnChangeHandler, ti.DebounceDelay, textInputBuffer);
    }

    /// <summary>Pastes clipboard text into the TextInput at the caret, replacing any selection.</summary>
    /// <summary>
    /// Typing into a masked field (<c>.Mask</c>): only characters the next slot accepts go in, the
    /// mask's separators appear by themselves, Backspace removes the last character typed, and a
    /// paste is fed through character by character. The caret stays at the end. With everything
    /// selected (Ctrl+A), the next edit starts afresh. Other keys (arrows, copy) work as usual.
    /// </summary>
    private bool HandleMaskedTextKey(TextInput ti, NativeKeyEvent evt, bool ctrl, bool shift)
    {
        string buffer = textInputBuffer ?? "";
        bool allSelected = buffer.Length > 0 && Math.Min(TextInputSelectionAnchor, TextInputCaretIndex) == 0
            && Math.Max(TextInputSelectionAnchor, TextInputCaretIndex) == buffer.Length;
        var pattern = ti.MaskPatternValue;
        var custom = ti.CustomMaskValue;
        string raw = allSelected ? "" : pattern is not null ? pattern.StripLiterals(buffer) : custom!.Parse(buffer);

        bool Accepts(string r, char c) => pattern is not null ? pattern.Accepts(r, c) : custom!.IsCharAllowed(c);
        string Format(string r) => pattern is not null ? pattern.FormatPartial(r) : custom!.Format(r);

        string? next = null;
        if (evt.Character is { } typed && typed >= ' ' && typed != (char)127 && !ctrl)
        {
            next = Accepts(raw, typed) ? raw + typed : null;
            if (next is null)
            {
                return true; // not allowed here: swallowed
            }
        }
        else if (evt.Character == '\b' || evt.Key == Key.Delete)
        {
            next = allSelected ? "" : raw.Length > 0 ? raw[..^1] : raw;
        }
        else if ((ctrl && evt.Key == Key.V) || (shift && evt.Key == Key.Insert))
        {
            var content = Clipboard.GetContentAsync().GetAwaiter().GetResult();
            string pasted = content.HasText ? content.GetTextAsync().GetAwaiter().GetResult() ?? "" : "";
            next = raw;
            foreach (char c in pasted)
            {
                if (Accepts(next, c))
                {
                    next += c;
                }
            }
        }
        else if (ctrl && evt.Key == Key.X)
        {
            return false; // cut works on the shown text as usual
        }

        if (next is null)
        {
            return false;
        }

        string formatted = Format(next);
        if (ti.MaxLengthValue is { } max && formatted.Length > max)
        {
            return true;
        }
        textInputBuffer = formatted;
        ActiveEditBuffer = formatted;
        TextInputCaretIndex = formatted.Length;
        TextInputSelectionAnchor = formatted.Length;
        if (formatted != buffer)
        {
            ti.Value.OnChange(formatted);
            RaiseOnChange(ti.OnChangeHandler, ti.DebounceDelay, formatted);
        }
        return true;
    }

    private void PasteIntoTextInput(TextInput ti)
    {
        var content = Clipboard.GetContentAsync().GetAwaiter().GetResult();
        if (!content.HasText)
        {
            return;
        }
        string? text = content.GetTextAsync().GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Strip newlines for single-line input
        text = text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');

        if (TextInputSelectionAnchor != TextInputCaretIndex)
        {
            DeleteTextInputSelection(ti);
        }

        textInputBuffer ??= "";
        // MaxLength holds for a paste as for typing.
        if (ti.MaxLengthValue is { } max)
        {
            text = text[..Math.Clamp(max - textInputBuffer.Length, 0, text.Length)];
            if (text.Length == 0)
            {
                return;
            }
        }
        textInputBuffer = textInputBuffer.Insert(TextInputCaretIndex, text);
        TextInputCaretIndex += text.Length;
        TextInputSelectionAnchor = TextInputCaretIndex;
        ActiveEditBuffer = textInputBuffer;
        ti.Value.OnChange(textInputBuffer);
        RaiseOnChange(ti.OnChangeHandler, ti.DebounceDelay, textInputBuffer);
    }

    /// <summary>
    /// Walks from the focused node upward looking for a KeyHandler with a matching binding.
    /// </summary>
    // Up/Down/Home/End/PageUp/PageDown move the selection (scrolling it into view); Enter activates.
    private static bool HandleListViewKeyboard(IListViewNode list, NativeKeyEvent evt)
    {
        if (evt.Modifiers != ModifierKeys.None || list.ItemCount == 0)
        {
            return false;
        }

        int current = list.SelectedIndex;
        if (evt.Key == Key.Enter)
        {
            if (current < 0)
            {
                return false;
            }
            list.ActivateIndex(current);
            return true;
        }

        int page = Math.Max(1, (int)(list.ViewportHeight / Math.Max(1f, list.RowExtent(Math.Max(current, 0))?.Height ?? list.GetItemHeight())) - 1);
        int last = list.ItemCount - 1;
        int target = evt.Key switch
        {
            Key.Down => current < 0 ? 0 : Math.Min(last, current + 1),
            Key.Up => current < 0 ? 0 : Math.Max(0, current - 1),
            Key.Home => 0,
            Key.End => last,
            Key.PageDown => Math.Min(last, Math.Max(current, 0) + page),
            Key.PageUp => Math.Max(0, current - page),
            _ => -1,
        };
        if (target < 0)
        {
            return false;
        }

        if (target != current)
        {
            list.SelectIndex(target);
        }
        if (list.RowExtent(target) is { } extent && list.ViewportHeight > 0f)
        {
            list.OffsetY = Math.Clamp(LayoutSolver.ScrollToReveal(list.OffsetY, extent.Top, extent.Height, list.ViewportHeight), 0f, list.MaxY);
        }
        return true;
    }

    private bool DispatchKeyBinding(NativeKeyEvent evt)
    {
        if (evt.Key == Key.None)
        {
            return false; // a bare modifier or a character message
        }

        var keyEvent = new KeyEvent(evt.Key, evt.Modifiers, evt.IsRepeat);
        foreach (var handler in KeyHandlersInScope())
        {
            if (handler.KeyDown?.Invoke(keyEvent) == true)
            {
                return true;
            }

            foreach (var binding in handler.Bindings)
            {
                if (MatchesBinding(binding, evt))
                {
                    binding.Handler();
                    return true;
                }
            }
        }
        return false;
    }

    private void DispatchKeyUp(NativeKeyEvent evt)
    {
        if (FocusManager.FocusedElement is HotkeyPicker { IsRecording: true })
        {
            RequestRepaint?.Invoke(); // a released modifier changes the live prompt
        }

        var keyEvent = new KeyEvent(evt.Key, evt.Modifiers, IsRepeat: false);
        foreach (var handler in KeyHandlersInScope())
        {
            handler.KeyUp?.Invoke(keyEvent);
        }
    }

    /// <summary>
    /// The KeyHandlers a key reaches: with focus, those on the path from the root to the focused
    /// node, innermost first (the closest scope wins); with no focus — or focus outside the
    /// reconciled tree, e.g. in a popup — every handler in document order. The walk uses the
    /// reconciler's child list, so handlers inside components, scroll views, split views, etc.
    /// are found (it used to stop at a handful of container types).
    /// </summary>
    private List<KeyHandler> KeyHandlersInScope()
    {
        var handlers = new List<KeyHandler>();
        if (rootNode is null)
        {
            return handlers;
        }

        var path = new List<Node>();
        if (FocusManager.FocusedElement is { } focused && FindPath(rootNode, focused, path))
        {
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] is KeyHandler handler)
                {
                    handlers.Add(handler);
                }
            }
        }
        else
        {
            CollectKeyHandlers(rootNode, handlers);
        }
        return handlers;
    }

    private static bool FindPath(Node node, Node target, List<Node> path)
    {
        path.Add(node);
        if (ReferenceEquals(node, target))
        {
            return true;
        }
        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (child is not null && FindPath(child, target, path))
            {
                return true;
            }
        }
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static void CollectKeyHandlers(Node node, List<KeyHandler> handlers)
    {
        if (node is KeyHandler handler)
        {
            handlers.Add(handler);
        }
        foreach (var child in NodeDiffer.GetChildren(node))
        {
            if (child is not null)
            {
                CollectKeyHandlers(child, handlers);
            }
        }
    }

    private static bool MatchesBinding(KeyBinding binding, NativeKeyEvent evt)
    {
        if (!binding.When || (evt.IsRepeat && !binding.AllowRepeat))
        {
            return false;
        }

        return binding.Hotkey.Key == evt.Key && binding.Hotkey.Modifiers == evt.Modifiers;
    }

    // ── Gesture invocation ────────────────────────────────────────────

    /// <summary>
    /// Flips a checkbox or toggle unless it is disabled or read-only. A three-state checkbox goes
    /// to checked, or from checked to unchecked. Returns whether the value changed.
    /// </summary>
    private static bool TryToggle(Node node)
    {
        switch (node)
        {
            case Checkbox { IsDisabled: false, IsReadOnly: false, BoolValue: { } binding }:
                binding.OnChange(!binding.Value);
                return true;
            case Checkbox { IsDisabled: false, IsReadOnly: false, ThreeStateValue: { } state, OnChange: { } onChange }:
                onChange(state == CheckboxValue.Checked ? CheckboxValue.Unchecked : CheckboxValue.Checked);
                return true;
            case Toggle { IsDisabled: false, IsReadOnly: false } toggle:
                toggle.Value.OnChange(!toggle.Value.Value);
                return true;
            default:
                return false;
        }
    }

    private void InvokeTap(Node node)
    {
        // Direct control click handlers
        switch (node)
        {
            case Button btn when !btn.IsDisabled && !btn.IsLoading:
                btn.OnClick();
                return;
            case LinkButton lb when !lb.IsDisabled:
                lb.OnClick();
                return;
            case IconButton ib when !ib.IsDisabled && !ib.IsLoading:
                ib.OnClick();
                return;
            case Checkbox or Toggle:
                TryToggle(node);
                return;
            case IRadioButton rb:
                rb.Select();
                return;
            case HotkeyPicker picker when !picker.IsDisabled:
                picker.IsRecording = true;
                RequestRepaint?.Invoke();
                return;
            case Rating rating when rating.BoundValue is { } rBind && !rating.IsDisabled && !rating.IsReadOnly:
                HandleRatingClick(rating, rBind);
                return;
            case Card { ClickHandler: not null } card:
                card.ClickHandler();
                return;
            case Expander exp:
                ToggleExpander(exp);
                return;
            case ITreeView tv:
                HandleTreeViewClick(tv);
                return;
            // A selectable tag flips its binding (the only handler a Tag(label, binding) has) and tells OnToggle.
            case Tag { Selected: not null } or Tag { OnToggle: not null }:
            {
                var tag = (Tag)node;
                bool selected = !(tag.Selected?.Value ?? false);
                tag.Selected?.OnChange(selected);
                tag.OnToggle?.Invoke(selected);
                return;
            }
            case Tag { OnRemove: not null } tag:
                tag.OnRemove!();
                return;

            case ISegmentedControl sc when !sc.IsControlDisabled:
                HandleSegmentedControlClick(sc);
                return;

            case TabBar tabBar when !tabBar.IsDisabled:
                HandleTabBarClick(tabBar);
                return;

            case IToggleGroup tg when !tg.IsControlDisabled:
                HandleToggleGroupClick(tg);
                return;

            case INumberInput ni when !ni.IsDisabled:
                HandleNumberInputClick(ni);
                return;

            case Breadcrumb bc:
                HandleBreadcrumbClick(bc);
                return;

            case StepIndicator si when si.StepClickHandler != null:
                HandleStepIndicatorClick(si);
                return;

            case Banner banner when banner.OnDismiss != null:
                HandleBannerDismissClick(banner);
                return;

            case ColorPicker:
                return; // handled on the press (PressColorPicker)

            case PinInput pin when !pin.IsDisabled && !pin.IsReadOnly:
                HandlePinInputClick(pin);
                return;

            case ToolBar tb:
                HandleToolBarClick(tb);
                return;

            case PasswordInput pwdTap when pwdTap.ShowToggleButton:
            {
                // Only intercept if click is on the eye toggle icon area
                var pwdBounds = Live(pwdTap, pwdTap.AbsoluteBounds);
                if (pwdBounds.Width > 0 && lastMousePosition.X >= pwdBounds.X + pwdBounds.Width - 36f)
                {
                    PasswordRevealed = !PasswordRevealed;
                    return;
                }
                break;  // Not on toggle — fall through to generic gesture
            }

            case ISelectNode sel when !sel.IsNodeDisabled:
                sel.ToggleOpen();
                TrackOpenSelect(sel);
                return;

            case IMultiSelectNode ms when !ms.IsNodeDisabled:
                ms.ToggleOpen();
                openMultiSelect = ms.IsOpen ? ms : null;
                RequestRepaint?.Invoke();
                return;

            case IComboboxNode cb when !cb.IsNodeDisabled:
                cb.ToggleOpen();
                openCombobox = cb.IsOpen ? cb : null;
                RequestRepaint?.Invoke();
                return;

            case SplitButton sb when !sb.IsDisabled:
            {
                // Use AbsoluteBounds (set by painter) for correct viewport-coordinate math.
                // The arrow opens the shared menu anchored below the button; a click on the
                // arrow while it is open is taken by the menu (outside its panel) and closes it.
                var absBounds = Live(sb, sb.AbsoluteBounds);
                float clickLocalX = lastMousePosition.X - absBounds.X;
                if (clickLocalX >= sb.ArrowZoneX)
                {
                    OpenSplitButtonMenu(sb, highlightFirst: false);
                }
                else
                {
                    sb.OnClick();
                }
                RequestRepaint?.Invoke();
                return;
            }

            case MenuBar mb:
                HandleMenuBarClick(mb);
                return;

            case PropertyGrid pg:
            {
                HandlePropertyGridClick(pg);
                RequestRepaint?.Invoke();
                return;
            }

            case NotificationBell nb when !nb.IsDisabled:
            {
                if (nb.IsOpen)
                {
                    nb.Close();
                    openNotificationBell = null;
                }
                else
                {
                    nb.Open();
                    openNotificationBell = nb;
                }
                RequestRepaint?.Invoke();
                return;
            }

            case EmojiPicker ep when !ep.IsDisabled:
            {
                HandleEmojiPickerClick(ep);
                RequestRepaint?.Invoke();
                return;
            }

            case DatePicker dp:
                dp.ToggleCalendar();
                openDatePicker = dp.IsCalendarOpen ? dp : null;
                RequestRepaint?.Invoke();
                return;

            case DateTimePicker dtp:
                if (!dtp.IsDisabled)
                {
                    dtp.ToggleCalendar();
                    openDateTimePicker = dtp.IsCalendarOpen ? dtp : null;
                    RequestRepaint?.Invoke();
                }
                return;

            case TimePicker tp:
                if (!tp.IsDisabled)
                {
                    if (tp.PopupDisplayStyle == TimePickerPopupStyle.Input)
                    {
                        // Typed: the field takes focus and its text is ready to edit.
                        FocusManager.RequestFocus(tp);
                        tp.InputText ??= tp.Value.Value?.ToString(tp.DefaultFormat, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
                    }
                    else
                    {
                        tp.TogglePopup();
                        openTimePicker = tp.IsPopupOpen ? tp : null;
                    }
                    RequestRepaint?.Invoke();
                }
                return;

            case MonthPicker mp:
                if (!mp.IsDisabled)
                {
                    mp.TogglePopup();
                    openMonthPicker = mp.IsPopupOpen ? mp : null;
                    RequestRepaint?.Invoke();
                }
                return;

            case DateRangePicker drp:
                drp.ToggleCalendar();
                openDateRangePicker = drp.IsCalendarOpen ? drp : null;
                RequestRepaint?.Invoke();
                return;

            // Calendar is handled in HandleMouseDown (viewport→document coord conversion needed)

            case NavItem { Enabled: true } navItem:
                navItem.Owner?.Activate(navItem);
                if (rootNode is not null)
                {
                    MarkScrollViewLayersDirty(rootNode, navItem);
                }
                RequestRepaint?.Invoke();
                return;

            case MarkdownText mdText when MarkdownLinkAt(mdText, lastMousePosition.X, lastMousePosition.Y) is { } link:
                mdText.Owner?.ActivateLink(link);
                RequestRepaint?.Invoke();
                return;

            case ITabularDataNode tdn:
                HandleTabularDataClick(tdn);
                return;
        }

        // Generic gesture tap — always repaint after user gesture handlers,
        // because handlers almost certainly mutate component state.
        InvokeGesture(node, g => g.Tap);
        RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Returns true if the node type is interactive (responds to tap/click with
    /// state changes or animations). Used to decide whether to mark ScrollView
    /// layers dirty after a tap.
    /// </summary>
    private static bool IsInteractiveNode(Node node)
    {
        return node is Button or LinkButton or IconButton or Checkbox
            or IRadioButton or Toggle or Rating or Card or Expander
            or ITreeView or Tag or ISegmentedControl or IToggleGroup or TabBar
            or INumberInput or Breadcrumb or StepIndicator or Banner
            or ColorPicker or PinInput or ToolBar or PasswordInput
            or ISelectNode or IMultiSelectNode or IComboboxNode
            or SplitButton or Slider or RangeSlider or Calendar
            or Accordion or MenuBar or StatusBar
            or ProgressBar or ProgressRing or Gauge or Spinner
            or NotificationBell or CommandPalette or KeyHandler
            or ScrollView or MarkdownText or NavItem;
    }

    /// <summary>
    /// Marks ScrollView layers dirty, but only for ScrollViews that contain
    /// <paramref name="targetNode"/> in their subtree. This prevents a click
    /// in one ScrollView from forcing expensive layer recapture in unrelated
    /// ScrollViews elsewhere in the app.
    /// </summary>
    private static bool MarkScrollViewLayersDirty(Node node, Node targetNode)
    {
        bool containsTarget = ReferenceEquals(node, targetNode);

        var children = NodeDiffer.GetChildren(node);
        foreach (var child in children)
        {
            if (MarkScrollViewLayersDirty(child, targetNode))
            {
                containsTarget = true;
            }
        }

        if (node is ScrollView sv && containsTarget)
        {
            sv.IsLayerDirty = true;
        }

        return containsTarget;
    }

    // ── Rating helpers ────────────────────────────────────────────────

    private void HandleRatingClick(Rating rating, Bindable<float> bind)
    {
        // AbsoluteBounds is set by the painter in viewport coordinates
        var bounds = Live(rating, rating.AbsoluteBounds);
        float iconSize = rating.SizeValue ?? 24f;
        float gap = 4f;
        int max = rating.Max;

        float totalWidth = (iconSize * max) + (gap * (max - 1));
        float startX = bounds.X + (bounds.Width - totalWidth) / 2f;

        float relX = lastMousePosition.X - startX;
        if (relX < 0)
        {
            relX = 0;
        }

        // Map click to star index using star centers to avoid dead zones in gaps
        float newValue = 0f;
        for (int i = 0; i < max; i++)
        {
            float starLeft = i * (iconSize + gap);
            float starRight = starLeft + iconSize;
            float starMid = starLeft + iconSize / 2f;

            if (relX >= starLeft && relX <= starRight)
            {
                newValue = rating.HalfStarsEnabled && relX < starMid
                    ? i + 0.5f
                    : i + 1f;
                break;
            }
            else if (i < max - 1 && relX > starRight && relX < (i + 1) * (iconSize + gap))
            {
                // In the gap — attribute to the star on the left
                newValue = i + 1f;
                break;
            }
            else if (i == max - 1)
            {
                // Past or on the last star
                newValue = max;
                break;
            }
        }

        if (newValue <= 0f)
        {
            newValue = 1f;
        }

        bind.OnChange(newValue);
    }

    // ── Expander helpers ─────────────────────────────────────────────

    private void ToggleExpander(Expander exp)
    {
        bool newValue;
        if (exp.ExpandedBind.OnChange != null)
        {
            // Controlled: the two-way binding owns the state and its setter re-renders.
            newValue = !exp.ExpandedBind.Value;
            exp.ExpandedBind.OnChange(newValue);
        }
        else
        {
            // Uncontrolled: the expander owns its own state. Flip it and repaint —
            // the reconciler carries ExpandedState across re-renders so it sticks.
            newValue = !exp.ExpandedState;
            exp.ExpandedState = newValue;
            RequestRepaint?.Invoke();
        }

        // Notify a fluent .OnToggle(...) handler regardless of controlled/uncontrolled.
        exp.LayoutData.ExpanderData?.OnToggleHandler?.Invoke(newValue);
    }

    // ── TreeView helpers ──────────────────────────────────────────────

    private Node? lastHoveredTreeNode;

    // Updates the hovered TreeView row from the pointer position so the painter can
    // highlight it. The tree is typically inside a layer-cached ScrollView, so a hover
    // change must mark that layer dirty — a plain repaint would composite the stale
    // cached texture and the highlight would never move.
    private void UpdateTreeViewHover(Node? hitNode)
    {
        int newRow = -1;
        Node? treeNode = null;
        if (hitNode is ITreeView tv && Live(tv, tv.AbsoluteBounds).Height > 0)
        {
            treeNode = hitNode;
            const float rowHeight = 28f;
            float relY = lastMousePosition.Y - Live(tv, tv.AbsoluteBounds).Y;
            if (relY >= 0)
            {
                int idx = (int)(relY / rowHeight);
                if (idx >= 0 && idx < tv.GetFlattenedDisplay().Count)
                {
                    newRow = idx;
                }
            }
        }

        if (newRow == TreeViewInteractionState.HoveredRow)
        {
            return;
        }

        TreeViewInteractionState.HoveredRow = newRow;

        var dirtyTarget = treeNode ?? lastHoveredTreeNode;
        if (rootNode != null && dirtyTarget != null)
        {
            MarkScrollViewLayersDirty(rootNode, dirtyTarget);
        }
        lastHoveredTreeNode = treeNode;
        RequestRepaint?.Invoke();
    }

    private void HandleTreeViewClick(ITreeView tv)
    {
        var bounds = Live(tv, tv.AbsoluteBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        const float rowHeight = 28f;
        float relY = lastMousePosition.Y - bounds.Y;
        int rowIndex = (int)(relY / rowHeight);

        if (rowIndex < 0)
        {
            return;
        }

        tv.ToggleRow(rowIndex);
        tv.SelectRow(rowIndex);
        RequestRepaint?.Invoke();
    }

    // ── TabularData (DataGrid/DataTable) helpers ────────────────────

    private void HandleTabularDataClick(ITabularDataNode tdn)
    {
        var bounds = Live(tdn, tdn.AbsoluteBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float headerHeight = TabularRowGeometry.HeaderHeight(tdn);
        const float filterRowHeight = TabularRowGeometry.FilterRowHeight;
        float relX = lastMousePosition.X - bounds.X;
        float relY = lastMousePosition.Y - bounds.Y;

        // Click in header area → sort (but not if near a column border for resize)
        if (relY < headerHeight)
        {
            // Check if click is on the column chooser button
            if (tdn.IsColumnChooserEnabled)
            {
                var btnBounds = tdn.ColumnChooserButtonBounds;
                if (btnBounds.Width > 0 &&
                    lastMousePosition.X >= btnBounds.X && lastMousePosition.X <= btnBounds.Right &&
                    lastMousePosition.Y >= btnBounds.Y && lastMousePosition.Y <= btnBounds.Bottom)
                {
                    tdn.ToggleColumnChooser();
                    if (tdn.IsColumnChooserOpen)
                    {
                        openGridOverlay = tdn;
                    }
                    else
                    {
                        openGridOverlay = null;
                    }
                    RequestRepaint?.Invoke();
                    return;
                }
            }

            // Close column chooser if clicking header outside button
            if (tdn.IsColumnChooserOpen)
            {
                tdn.ToggleColumnChooser();
                openGridOverlay = null;
            }

            // Deactivate filter if active
            if (tdn.ActiveFilterCol >= 0)
            {
                tdn.ActiveFilterCol = -1;
                RequestRepaint?.Invoke();
            }
            int borderCol = HitTestColumnBorder(tdn, relX, bounds.Width);
            if (borderCol < 0 && tdn.IsSortable)
            {
                int col = HitTestTabularColumn(tdn, relX, bounds.Width);
                if (col >= 0 && tdn.IsColumnSortable(col))
                {
                    tdn.ApplySort(col);
                    RequestRepaint?.Invoke();
                }
            }
            return;
        }

        // Check if click is inside the column chooser dropdown
        if (tdn.IsColumnChooserOpen)
        {
            var ddBounds = tdn.ColumnChooserBounds;
            if (ddBounds.Width > 0 &&
                lastMousePosition.X >= ddBounds.X && lastMousePosition.X <= ddBounds.Right &&
                lastMousePosition.Y >= ddBounds.Y && lastMousePosition.Y <= ddBounds.Bottom)
            {
                // Determine which item was clicked
                float itemHeight = 28f;
                int idx = (int)((lastMousePosition.Y - ddBounds.Y) / itemHeight);
                if (idx >= 0 && idx < tdn.ColumnCount)
                {
                    tdn.ToggleColumnVisibility(idx);
                    RequestRepaint?.Invoke();
                }
                return;
            }
            // Click outside dropdown → close it
            tdn.ToggleColumnChooser();
            openGridOverlay = null;
            RequestRepaint?.Invoke();
        }

        // Click in filter row area → activate filter cell
        if (tdn.HasFilterRow)
        {
            if (relY >= headerHeight && relY < headerHeight + filterRowHeight)
            {
                int col = HitTestTabularColumn(tdn, relX, bounds.Width);
                if (col >= 0)
                {
                    // Check if click is on the clear (X) button
                    string filterText = tdn.GetColumnFilter(col);
                    if (filterText.Length > 0)
                    {
                        float colX = 0f;
                        float[] scaledW = GetScaledColumnWidths(tdn, bounds.Width);
                        for (int c = 0; c < col; c++)
                        {
                            colX += scaledW[c];
                        }
                        float colW = scaledW[col];
                        float clearX = colX + colW - 18f;
                        if (relX >= clearX)
                        {
                            tdn.SetColumnFilter(col, "");
                            tdn.ActiveFilterCol = col;
                            tdn.FilterCursorPos = 0;
                            RequestRepaint?.Invoke();
                            return;
                        }
                    }

                    tdn.ActiveFilterCol = col;
                    tdn.FilterCursorPos = tdn.GetColumnFilter(col).Length;

                    // Cancel any cell editing
                    if (tdn.IsEditing)
                    {
                        tdn.CommitEdit();
                    }
                }
                if (tdn is Node filterNode)
                {
                    FocusManager.RequestFocus(filterNode);
                }
                RequestRepaint?.Invoke();
                return;
            }
        }

        // Click below filter row → deactivate filter input
        if (tdn.ActiveFilterCol >= 0)
        {
            tdn.ActiveFilterCol = -1;
        }

        // An inline row action takes the click before the row does.
        if (TryClickRowAction(tdn, relX, relY, bounds.Height))
        {
            return;
        }

        // Rows, group headers and detail panels: the shared geometry applies the vertical scroll,
        // which the walk that used to live here did not — a click on a scrolled table selected
        // the row that would have been there at offset zero.
        var hit = TabularRowGeometry.HitTest(tdn, relY, bounds.Height);
        switch (hit.Kind)
        {
            case TabularHitKind.GroupHeader:
                tdn.ToggleGroupCollapse(hit.Index);
                RequestRepaint?.Invoke();
                return;

            case TabularHitKind.Row:
                if (tdn.HasRowDetail && relX < ExpandIndicatorWidth)
                {
                    tdn.ToggleRowDetail(hit.Index);
                    RequestRepaint?.Invoke();
                    return;
                }

                HandleTabularDataRowClick(tdn, hit.Index, relX, bounds.Width);
                return;

            default:
                // Aggregate rows and detail panels are not interactive.
                return;
        }
    }

    private void HandleTabularDataRowClick(ITabularDataNode tdn, int row, float relX, float boundsWidth)
    {
        // Data rows are shifted right by the expand indicator when row detail is enabled
        float dataRelX = tdn.HasRowDetail ? relX - ExpandIndicatorWidth : relX;
        int col = HitTestTabularColumn(tdn, dataRelX, boundsWidth);

        // Bool column click: toggle directly
        if (col >= 0 && tdn.IsBoolColumn(col) && tdn.IsColumnEditable(col))
        {
            tdn.ToggleBool(row, col);
            RequestRepaint?.Invoke();
            return;
        }

        // If clicking the same row that's already selected, consider entering edit mode
        bool wasSelected = tdn.IsRowSelected(row);
        bool ctrl = lastMouseModifiers.HasFlag(ModifierKeys.Ctrl);
        bool shift = lastMouseModifiers.HasFlag(ModifierKeys.Shift);

        if (wasSelected && !ctrl && !shift && col >= 0 &&
            tdn.EditModeValue == GridEditMode.ClickToEdit)
        {
            // If already editing this cell, don't restart
            if (!(tdn.IsEditing && tdn.EditingRow == row && tdn.EditingCol == col))
            {
                // Commit any current edit first
                if (tdn.IsEditing)
                {
                    tdn.CommitEdit();
                }

                // The clicked cell becomes the current cell (keyboard navigation continues from it).
                if (tdn is ITabularCellGrid { CellNavigationEnabled: true } clickedCells)
                {
                    if (clickedCells.RangeSelectionEnabled)
                    {
                        clickedCells.SelectCell(row, col, CellSelectKind.Replace);
                    }
                    else
                    {
                        clickedCells.SetCurrentColumn(col);
                    }
                }
                // Close any existing DataGrid overlay
                if (openGridOverlay != null && !ReferenceEquals(openGridOverlay, tdn))
                {
                    openGridOverlay.CloseOverlay();
                }
                tdn.BeginEdit(row, col);
                // Track overlay if one opened
                if (tdn.IsSelectDropdownOpen || tdn.IsDatePopupOpen)
                {
                    openGridOverlay = tdn;
                }
            }
        }
        else
        {
            // Cancel any current edit
            if (tdn.IsEditing)
            {
                tdn.CommitEdit();
            }
            // Close any DataGrid overlay
            if (openGridOverlay != null)
            {
                openGridOverlay.CloseOverlay();
                openGridOverlay = null;
            }

            if (!SelectClickedGridCell(tdn, row, col, ctrl, shift))
            {
                tdn.SelectRow(row, ctrl, shift);
            }
        }

        // A click puts keyboard focus on the row, not on one of its actions.
        if (tdn.RowActionStrip is { } clickedActions)
        {
            clickedActions.FocusedIndex = -1;
        }

        // Give focus to the tabular data node for keyboard navigation
        if (tdn is Node tdnNode)
        {
            FocusManager.RequestFocus(tdnNode);
        }
        RequestRepaint?.Invoke();
    }

    private bool HandleTabularDataKeyboard(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        // If a filter cell is active, route keys to filter handler
        if (tdn.ActiveFilterCol >= 0)
        {
            return HandleTabularDataFilterKeyboard(tdn, evt);
        }

        // If a select dropdown is open, route keys to it
        if (tdn.IsSelectDropdownOpen)
        {
            return HandleDataGridSelectDropdownKeyboard(tdn, evt);
        }

        // If a date popup is open, route keys to it
        if (tdn.IsDatePopupOpen)
        {
            return HandleDataGridDatePopupKeyboard(tdn, evt);
        }

        // If currently editing, route keys to edit handler
        if (tdn.IsEditing)
        {
            return HandleTabularDataEditKeyboard(tdn, evt);
        }

        // ── Ctrl shortcuts: Undo/Redo/Clipboard ────────────────────────
        if (evt.Modifiers.HasFlag(ModifierKeys.Ctrl))
        {
            switch (evt.Key)
            {
                case Key.Z:
                    if (evt.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // Ctrl+Shift+Z = Redo
                        if (tdn.RedoEdit())
                        {
                            RequestRepaint?.Invoke();
                        }
                    }
                    else
                    {
                        // Ctrl+Z = Undo
                        if (tdn.UndoEdit())
                        {
                            RequestRepaint?.Invoke();
                        }
                    }
                    return true;

                case Key.Y:
                    // Ctrl+Y = Redo
                    if (tdn.RedoEdit())
                    {
                        RequestRepaint?.Invoke();
                    }
                    return true;

                case Key.C:
                    // Ctrl+C = Copy
                    if (tdn.IsClipboardEnabled)
                    {
                        _ = tdn.CopyCellsAsync();
                    }
                    return true;

                case Key.V:
                    // Ctrl+V = Paste
                    if (tdn.IsClipboardEnabled)
                    {
                        _ = HandleTabularDataPasteAsync(tdn);
                    }
                    return true;

                case Key.X:
                    // Ctrl+X = Cut
                    if (tdn.IsClipboardEnabled)
                    {
                        _ = HandleTabularDataCutAsync(tdn);
                    }
                    return true;
            }
        }

        // A grid's cells come first — unless keyboard focus is on one of the row's inline actions,
        // which then keep Right/Left/Enter/Space/Escape (Right past the last cell reaches them).
        bool actionKey = tdn.RowActionStrip is { FocusedIndex: >= 0 }
            && evt.Key is Key.Right or Key.Left or Key.Enter or Key.Space or Key.Escape;
        if (!actionKey && tdn is ITabularCellGrid cells && HandleGridCellKey(tdn, cells, evt))
        {
            return true;
        }

        // The selected row's inline actions: Right/Left/Enter/Space/Escape when they apply.
        if (HandleRowActionKey(tdn, evt))
        {
            return true;
        }

        if (evt.Key == Key.None)
        {
            return false;
        }

        // Row movement follows the on-screen order (TabularNavigation): a grouped grid shows its
        // rows group by group with collapsed groups hidden, so "down" is not display row + 1.
        switch (evt.Key)
        {
            case Key.Up:
                tdn.MoveSelection(-1);
                AfterTabularNavigation(tdn);
                return true;

            case Key.Down:
                tdn.MoveSelection(1);
                AfterTabularNavigation(tdn);
                return true;

            case Key.Home:
                tdn.SelectFirst();
                AfterTabularNavigation(tdn);
                return true;

            case Key.End:
                tdn.SelectLast();
                AfterTabularNavigation(tdn);
                return true;

            case Key.PageUp:
                tdn.MoveSelection(-TabularNavigation.PageSize(tdn));
                AfterTabularNavigation(tdn);
                return true;

            case Key.PageDown:
                tdn.MoveSelection(TabularNavigation.PageSize(tdn));
                AfterTabularNavigation(tdn);
                return true;

            case Key.F2:
                // F2 starts editing the selected row, first editable column
                if (tdn.SelectedRowIndex >= 0)
                {
                    for (int c = 0; c < tdn.ColumnCount; c++)
                    {
                        if (tdn.IsColumnEditable(c) && !tdn.IsBoolColumn(c))
                        {
                            tdn.BeginEdit(tdn.SelectedRowIndex, c);
                            if (tdn.IsSelectDropdownOpen || tdn.IsDatePopupOpen)
                            {
                                openGridOverlay = tdn;
                            }
                            RequestRepaint?.Invoke();
                            break;
                        }
                    }
                }
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// After a keyboard move: bring the selected row into view, keep a focused row action inside
    /// the new row's actions, and repaint.
    /// </summary>
    private void AfterTabularNavigation(ITabularDataNode tdn)
    {
        if (tdn.SelectedRowIndex >= 0)
        {
            tdn.ScrollIntoView(tdn.SelectedRowIndex);
        }

        ClampRowActionFocus(tdn);
        RequestRepaint?.Invoke();
    }

    private async Task HandleTabularDataPasteAsync(ITabularDataNode tdn)
    {
        bool pasted = await tdn.PasteCellsAsync();
        if (pasted)
        {
            RequestRepaint?.Invoke();
        }
    }

    private async Task HandleTabularDataCutAsync(ITabularDataNode tdn)
    {
        bool cut = await tdn.CutCellsAsync();
        if (cut)
        {
            RequestRepaint?.Invoke();
        }
    }

    private bool HandleTabularDataEditKeyboard(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        // A grid with cell navigation: Enter/Up/Down/Tab commit and move to the next cell.
        if (tdn is ITabularCellGrid cells && HandleGridEditNavigationKey(tdn, cells, evt) is { } handled)
        {
            return handled;
        }

        switch (evt.Key)
        {
            case Key.Enter:
                tdn.CommitEdit();
                RequestRepaint?.Invoke();
                return true;

            case Key.Escape:
                tdn.CancelEdit();
                RequestRepaint?.Invoke();
                return true;

            case Key.Tab:
            {
                // Commit current edit and move to next editable column
                int row = tdn.EditingRow;
                int col = tdn.EditingCol;
                tdn.CommitEdit();
                // Find next editable column
                for (int c = col + 1; c < tdn.ColumnCount; c++)
                {
                    if (tdn.IsColumnEditable(c) && !tdn.IsBoolColumn(c))
                    {
                        tdn.BeginEdit(row, c);
                        RequestRepaint?.Invoke();
                        return true;
                    }
                }
                // No more editable columns, just commit
                RequestRepaint?.Invoke();
                return true;
            }

            case Key.Backspace:
            case Key.Delete:
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
                tdn.HandleEditKey(evt.Key);
                RequestRepaint?.Invoke();
                return true;

            default:
                // Handle typed characters
                if (evt.Character != null && !char.IsControl(evt.Character.Value))
                {
                    tdn.HandleEditChar(evt.Character.Value);
                    RequestRepaint?.Invoke();
                    return true;
                }
                return false;
        }
    }

    private bool HandleTabularDataFilterKeyboard(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        int col = tdn.ActiveFilterCol;
        string text = tdn.GetColumnFilter(col);
        int cursor = tdn.FilterCursorPos;

        switch (evt.Key)
        {
            case Key.Escape:
                tdn.ActiveFilterCol = -1;
                RequestRepaint?.Invoke();
                return true;

            case Key.Tab:
            {
                // Move to next filter column
                int nextCol = col + 1;
                if (nextCol >= tdn.ColumnCount)
                {
                    tdn.ActiveFilterCol = -1;
                }
                else
                {
                    tdn.ActiveFilterCol = nextCol;
                    tdn.FilterCursorPos = tdn.GetColumnFilter(nextCol).Length;
                }
                RequestRepaint?.Invoke();
                return true;
            }

            case Key.Backspace:
                if (cursor > 0 && text.Length > 0)
                {
                    tdn.SetColumnFilter(col, text.Remove(cursor - 1, 1));
                    tdn.FilterCursorPos = cursor - 1;
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Delete:
                if (cursor < text.Length)
                {
                    tdn.SetColumnFilter(col, text.Remove(cursor, 1));
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Left:
                if (cursor > 0)
                {
                    tdn.FilterCursorPos = cursor - 1;
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Right:
                if (cursor < text.Length)
                {
                    tdn.FilterCursorPos = cursor + 1;
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Home:
                tdn.FilterCursorPos = 0;
                RequestRepaint?.Invoke();
                return true;

            case Key.End:
                tdn.FilterCursorPos = text.Length;
                RequestRepaint?.Invoke();
                return true;

            case Key.Enter:
                tdn.ActiveFilterCol = -1;
                RequestRepaint?.Invoke();
                return true;

            default:
                if (evt.Character != null && !char.IsControl(evt.Character.Value))
                {
                    string newText = text.Insert(cursor, evt.Character.Value.ToString());
                    tdn.SetColumnFilter(col, newText);
                    tdn.FilterCursorPos = cursor + 1;
                    RequestRepaint?.Invoke();
                    return true;
                }
                return false;
        }
    }

    private bool HandleDataGridSelectDropdownKeyboard(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        var options = tdn.GetSelectOptions(tdn.SelectDropdownCol);
        int count = options?.Count ?? 0;

        switch (evt.Key)
        {
            case Key.Escape:
                tdn.CloseSelectDropdown();
                openGridOverlay = null;
                RequestRepaint?.Invoke();
                return true;

            case Key.Enter:
                if (tdn.SelectDropdownHoverIndex >= 0 && tdn.SelectDropdownHoverIndex < count)
                {
                    tdn.CommitSelectOption(tdn.SelectDropdownHoverIndex);
                    openGridOverlay = null;
                }
                else
                {
                    tdn.CloseSelectDropdown();
                    openGridOverlay = null;
                }
                RequestRepaint?.Invoke();
                return true;

            case Key.Up:
                if (tdn.SelectDropdownHoverIndex > 0)
                {
                    tdn.SelectDropdownHoverIndex--;
                }
                RequestRepaint?.Invoke();
                return true;

            case Key.Down:
                if (tdn.SelectDropdownHoverIndex < count - 1)
                {
                    tdn.SelectDropdownHoverIndex++;
                }
                RequestRepaint?.Invoke();
                return true;

            default:
                return false;
        }
    }

    private bool HandleDataGridDatePopupKeyboard(ITabularDataNode tdn, NativeKeyEvent evt)
    {
        var dp = tdn.DatePopupPicker;
        if (dp == null)
        {
            return false;
        }

        switch (evt.Key)
        {
            case Key.Escape:
                tdn.CloseDatePopup();
                openGridOverlay = null;
                RequestRepaint?.Invoke();
                return true;

            case Key.Left:
                dp.NavigateMonth(-1);
                RequestRepaint?.Invoke();
                return true;

            case Key.Right:
                dp.NavigateMonth(1);
                RequestRepaint?.Invoke();
                return true;

            default:
                return false;
        }
    }

    private void HandleDataGridCalendarClick(ITabularDataNode tdn, DatePicker dp, float x, float y)
    {
        // Reuse the same calendar popup click logic as standalone DatePicker
        var calBounds = dp.CalendarBounds;

        // Check prev/next arrows
        if (dp.PrevMonthBounds.Width > 0 && dp.PrevMonthBounds.Contains(new Point(x, y)))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.NavigateMonth(-1);
                    break;
                case CalendarViewMode.Months:
                    dp.DisplayedYear--;
                    break;
                case CalendarViewMode.Years:
                    dp.NavigateYearGrid(-12);
                    break;
            }
            return;
        }

        if (dp.NextMonthBounds.Width > 0 && dp.NextMonthBounds.Contains(new Point(x, y)))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.NavigateMonth(1);
                    break;
                case CalendarViewMode.Months:
                    dp.DisplayedYear++;
                    break;
                case CalendarViewMode.Years:
                    dp.NavigateYearGrid(12);
                    break;
            }
            return;
        }

        // Check header label click (switch view mode)
        if (dp.HeaderLabelBounds.Width > 0 && dp.HeaderLabelBounds.Contains(new Point(x, y)))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.ShowMonths();
                    break;
                case CalendarViewMode.Months:
                    dp.ShowYears();
                    break;
            }
            return;
        }

        // Handle clicks in the grid area based on view mode
        switch (dp.ViewMode)
        {
            case CalendarViewMode.Days:
                HandleDataGridCalendarDayClick(tdn, dp, x, y);
                break;
            case CalendarViewMode.Months:
                HandleDataGridCalendarMonthClick(dp, x, y);
                break;
            case CalendarViewMode.Years:
                HandleDataGridCalendarYearClick(dp, x, y);
                break;
        }
    }

    private void HandleDataGridCalendarDayClick(ITabularDataNode tdn, DatePicker dp, float x, float y)
    {
        if (dp.CalendarGridLeft <= 0 || dp.CalendarCellSize <= 0)
        {
            return;
        }

        float cellSize = dp.CalendarCellSize;
        float gridTop = dp.CalendarGridTop;
        float gridLeft = dp.CalendarGridLeft;

        int col = (int)((x - gridLeft) / cellSize);
        int row = (int)((y - gridTop) / cellSize);
        if (col < 0 || col >= 7 || row < 0 || row >= 6)
        {
            return;
        }

        int cellIndex = row * 7 + col;
        if (cellIndex >= dp.CalendarGridCellCount)
        {
            return;
        }

        DateOnly date = dp.CalendarGridStartDate.AddDays(cellIndex);

        // Check min/max constraints
        if (dp.Min.HasValue && date < dp.Min.Value)
        {
            return;
        }
        if (dp.Max.HasValue && date > dp.Max.Value)
        {
            return;
        }
        if (dp.DisabledDatesPredicate != null && dp.DisabledDatesPredicate(date))
        {
            return;
        }

        // Commit the date
        tdn.CommitDateValue(date);
        openGridOverlay = null;
    }

    private static void HandleDataGridCalendarMonthClick(DatePicker dp, float x, float y)
    {
        float cellW = (dp.CalendarBounds.Width - 24f) / 4f;
        float cellH = dp.CalendarCellSize;
        float gridLeft = dp.CalendarBounds.X + 12f;
        float gridTop = dp.CalendarGridTop;

        int col = (int)((x - gridLeft) / cellW);
        int row = (int)((y - gridTop) / cellH);
        if (col < 0 || col >= 4 || row < 0 || row >= 3)
        {
            return;
        }

        int month = row * 4 + col + 1;
        dp.SelectMonth(month);
    }

    private static void HandleDataGridCalendarYearClick(DatePicker dp, float x, float y)
    {
        float cellW = (dp.CalendarBounds.Width - 24f) / 4f;
        float cellH = dp.CalendarCellSize;
        float gridLeft = dp.CalendarBounds.X + 12f;
        float gridTop = dp.CalendarGridTop;

        int col = (int)((x - gridLeft) / cellW);
        int row = (int)((y - gridTop) / cellH);
        if (col < 0 || col >= 4 || row < 0 || row >= 3)
        {
            return;
        }

        int year = dp.YearGridStart + row * 4 + col;
        dp.SelectYear(year);
    }

    private static float[] GetScaledColumnWidths(ITabularDataNode tdn, float tableWidth)
    {
        return TabularRowGeometry.ScaledColumnWidths(tdn, tableWidth);
    }

    private static int HitTestTabularColumn(ITabularDataNode tdn, float relX, float availableWidth)
    {
        float[] colWidths = GetScaledColumnWidths(tdn, availableWidth);
        float colX = 0;
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            if (relX >= colX && relX < colX + colWidths[c])
            {
                return c;
            }
            colX += colWidths[c];
        }
        return -1;
    }

    /// <summary>
    /// Returns the column index whose RIGHT border is near the given X position,
    /// or -1 if not near any border. Used for column resize hit detection.
    /// </summary>
    /// <summary>
    /// Picks the pointer cursor for the current position and applies it if it changed.
    /// Kinds: 0 arrow, 1 east-west resize, 2 north-south resize, 3 text I-beam (see the window's
    /// <c>SetCursorOverride</c>). A column resize in progress keeps the resize cursor even when
    /// the pointer outruns the border, so it does not flicker mid-drag; otherwise a resizable
    /// column border in a table header, or a SplitView divider, asks for it. Anything else — and
    /// crucially, the same table away from a border — goes back to the arrow.
    /// </summary>
    private void UpdateHoverCursor(Node? hitNode, float x, float y)
    {
        int desiredCursor = 0;

        if (activeEyeDropper is not null)
        {
            desiredCursor = 5;
        }
        else if (columnResizeTdn != null)
        {
            desiredCursor = 1;
        }
        else if (hitNode is Calendar { OnDragResize: not null } cursorCalendar && CalendarCursor(cursorCalendar, x, y) is > 0 and var calendarCursor)
        {
            desiredCursor = calendarCursor;
        }
        else if (hitNode is ITabularDataNode tdn && IsOverResizableColumnBorder(tdn, x, y))
        {
            desiredCursor = 1;
        }
        else if (hitNode is SplitView sv)
        {
            desiredCursor = sv.Orientation == SplitOrientation.Horizontal ? 1 : 2;
        }
        else if (hitNode is MarkdownText { HasLinks: true } linkText && MarkdownLinkAt(linkText, x, y) is not null)
        {
            desiredCursor = 4;
        }
        else if (labelSelectionActive || hitNode is TextInput { IsDisabled: false } or TextArea { IsDisabled: false }
            || (rootNode is not null && FindSelectableLabelAt(x, y) is not null))
        {
            desiredCursor = 3;
        }

        if (desiredCursor != currentCursorKind)
        {
            currentCursorKind = desiredCursor;
            RequestCursorChange?.Invoke(desiredCursor);
        }
    }

    /// <summary>The Markdown link under a window point, from the text node's laid-out position.</summary>
    private string? MarkdownLinkAt(MarkdownText text, float x, float y)
    {
        if (rootNode is null || !HitTester.TryGetAbsoluteBounds(rootNode, text, out var bounds))
        {
            return null;
        }
        var padding = text.LayoutData.Padding;
        return text.LinkAt(new Point(x - bounds.X - padding.Left, y - bounds.Y - padding.Top));
    }

    /// <summary>
    /// The same test the press path uses to start a column resize: in the header band, within
    /// the border tolerance of a column's right edge, and that column allows resizing. Kept
    /// identical on purpose — the cursor must promise exactly what a press will do.
    /// </summary>
    private bool IsOverResizableColumnBorder(ITabularDataNode tdn, float x, float y)
    {
        var bounds = Live(tdn, tdn.AbsoluteBounds);
        if (bounds.Width <= 0f)
        {
            return false;
        }

        float relY = y - bounds.Y;
        float headerH = tdn.GetRowHeight() + 4f;
        if (relY < 0f || relY >= headerH)
        {
            return false;
        }

        int borderCol = HitTestColumnBorder(tdn, x - bounds.X, bounds.Width);
        return borderCol >= 0 && tdn.IsColumnResizable(borderCol);
    }

    private static int HitTestColumnBorder(ITabularDataNode tdn, float relX, float availableWidth)
    {
        const float borderTolerance = 5f;
        float[] colWidths = GetScaledColumnWidths(tdn, availableWidth);
        float colX = 0;
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            colX += colWidths[c];
            // Check if mouse is within tolerance of the right edge of this column
            if (MathF.Abs(relX - colX) <= borderTolerance)
            {
                return c;
            }
        }
        return -1;
    }

    private int HitTestTabularRow(ITabularDataNode tdn, float mouseX, float mouseY)
    {
        var bounds = Live(tdn, tdn.AbsoluteBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return -1;
        }

        var hit = TabularRowGeometry.HitTest(tdn, mouseY - bounds.Y, bounds.Height);
        return hit.Kind == TabularHitKind.Row ? hit.Index : -1;
    }

    // ── SegmentedControl helpers ─────────────────────────────────────

    private void HandleSegmentedControlClick(ISegmentedControl sc)
    {
        var bounds = Live(sc, sc.AbsoluteBounds);
        int count = sc.SegmentCount;
        if (count == 0)
        {
            return;
        }

        int clickedIndex = HitTestVariableWidthSegments(
            bounds,
            count,
            i => sc.GetSegmentLabel(i),
            SegmentLayout.SegmentedPaddingH,
            lastMousePosition.X);
        sc.SelectIndex(clickedIndex);
    }

    // ── NumberInput helpers ──────────────────────────────────────────

    /// <summary>Commits a pending typing buffer to the control's value (if any) and clears it.</summary>
    private static void CommitNumberInputEdit(INumberInput ni)
    {
        if (NumberEditBuffer is { } buf)
        {
            if (buf.Trim().Length > 0)
            {
                ni.TryCommitText(buf); // invalid text leaves the value unchanged
            }
            NumberEditBuffer = null;
        }
    }

    /// <summary>
    /// Keyboard editing for a focused NumberInput: type digits (plus sign/decimal where the numeric
    /// type allows), Backspace/Delete, caret navigation, Up/Down to step, Enter to commit, Escape to
    /// revert. The typed value is buffered and only written back on Enter or when focus leaves.
    /// Returns true when the key was consumed.
    /// </summary>
    private bool HandleNumberInputKey(INumberInput ni, NativeKeyEvent evt)
    {
        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);

        // Commit / revert / step — valid whether or not a buffer exists yet.
        if (evt.Key == Key.Enter)
        {
            CommitNumberInputEdit(ni);
            return true;
        }
        if (evt.Key == Key.Escape)
        {
            NumberEditBuffer = null; // discard edits, fall back to the bound value
            RequestRepaint?.Invoke();
            return true;
        }
        if (evt.Key == Key.Up)
        {
            NumberEditBuffer = null;
            ni.Increment();
            return true;
        }
        if (evt.Key == Key.Down)
        {
            NumberEditBuffer = null;
            ni.Decrement();
            return true;
        }

        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // Printable character: begin a FRESH edit from empty so the first digit replaces the
        // displayed value (the common "click and type a new number" flow). Subsequent characters
        // append/insert normally.
        if (evt.Character is char ch && ch >= ' ')
        {
            if (NumberEditBuffer is null)
            {
                NumberEditBuffer = string.Empty;
                NumberInputCaretIndex = 0;
            }
            NumberInputCaretIndex = Math.Clamp(NumberInputCaretIndex, 0, NumberEditBuffer.Length);
            if (IsValidNumberChar(ni, ch, NumberEditBuffer, NumberInputCaretIndex))
            {
                NumberEditBuffer = NumberEditBuffer.Insert(NumberInputCaretIndex, ch.ToString());
                NumberInputCaretIndex++;
            }
            return true; // swallow non-numeric printable keys too
        }

        // Navigation / deletion edit the EXISTING value — seed the buffer from it if needed.
        if (NumberEditBuffer is null)
        {
            NumberEditBuffer = ni.EditableText;
            NumberInputCaretIndex = NumberEditBuffer.Length;
        }
        NumberInputCaretIndex = Math.Clamp(NumberInputCaretIndex, 0, NumberEditBuffer.Length);

        if (ctrl && evt.Key == Key.A)
        {
            NumberInputCaretIndex = NumberEditBuffer.Length;
            return true;
        }
        if (evt.Key == Key.Left)
        {
            if (NumberInputCaretIndex > 0) { NumberInputCaretIndex--; }
            return true;
        }
        if (evt.Key == Key.Right)
        {
            if (NumberInputCaretIndex < NumberEditBuffer.Length) { NumberInputCaretIndex++; }
            return true;
        }
        if (evt.Key == Key.Home)
        {
            NumberInputCaretIndex = 0;
            return true;
        }
        if (evt.Key == Key.End)
        {
            NumberInputCaretIndex = NumberEditBuffer.Length;
            return true;
        }
        if (evt.Key == Key.Backspace)
        {
            if (NumberInputCaretIndex > 0)
            {
                NumberEditBuffer = NumberEditBuffer.Remove(NumberInputCaretIndex - 1, 1);
                NumberInputCaretIndex--;
            }
            return true;
        }
        if (evt.Key == Key.Delete)
        {
            if (NumberInputCaretIndex < NumberEditBuffer.Length)
            {
                NumberEditBuffer = NumberEditBuffer.Remove(NumberInputCaretIndex, 1);
            }
            return true;
        }

        return false;
    }

    private static bool IsValidNumberChar(INumberInput ni, char ch, string buffer, int caret)
    {
        if (ch is >= '0' and <= '9')
        {
            return true;
        }
        if (ch == '-' && ni.AllowsSign && caret == 0 && !buffer.Contains('-', StringComparison.Ordinal))
        {
            return true;
        }
        if (ch == '.' && ni.AllowsDecimal && !buffer.Contains('.', StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private void HandleNumberInputClick(INumberInput ni)
    {
        // A stepper/scroll interaction cancels any in-progress typing (its value wins).
        NumberEditBuffer = null;

        var bounds = Live(ni, ni.AbsoluteBounds);
        float relX = lastMousePosition.X - bounds.X;
        float relY = lastMousePosition.Y - bounds.Y;
        float buttonWidth = 28f;

        if (ni.StepperPos == StepperPosition.Split)
        {
            if (relX < buttonWidth)
            {
                ni.Decrement();
            }
            else if (relX > bounds.Width - buttonWidth)
            {
                ni.Increment();
            }
        }
        else if (ni.StepperPos == StepperPosition.Right)
        {
            if (relX > bounds.Width - buttonWidth)
            {
                float halfH = bounds.Height / 2f;
                if (relY < halfH)
                {
                    ni.Increment();
                }
                else
                {
                    ni.Decrement();
                }
            }
        }
    }

    /// <summary>
    /// Returns which stepper button the mouse is over: 0 = decrement, 1 = increment, -1 = none.
    /// </summary>
    private int ComputeNumberInputHoveredButton(INumberInput ni, float mouseX, float mouseY)
    {
        var bounds = Live(ni, ni.AbsoluteBounds);
        float relX = mouseX - bounds.X;
        float relY = mouseY - bounds.Y;
        float buttonWidth = 28f;

        if (ni.StepperPos == StepperPosition.Split)
        {
            if (relX < buttonWidth)
            {
                return 0; // decrement (left)
            }

            if (relX > bounds.Width - buttonWidth)
            {
                return 1; // increment (right)
            }
        }
        else if (ni.StepperPos == StepperPosition.Right)
        {
            if (relX > bounds.Width - buttonWidth)
            {
                float halfH = bounds.Height / 2f;
                return relY < halfH ? 1 : 0; // top = increment, bottom = decrement
            }
        }

        return -1;
    }

    // ── Breadcrumb helpers ──────────────────────────────────────────

    private void HandleBreadcrumbClick(Breadcrumb bc)
    {
        var absBounds = Live(bc, bc.AbsoluteBounds);
        if (absBounds.Width <= 0)
        {
            return;
        }

        // The same arrangement the painter drew.
        var arranged = bc.Arrange(LayoutSolver.BodyFontSize, LayoutSolver.DefaultFontPath, absBounds.Width);
        float clickX = lastMousePosition.X - absBounds.X;
        foreach (var item in arranged.Items)
        {
            if (clickX >= item.X && clickX < item.X + item.Width)
            {
                if (item.Segment >= 0 && item.Segment < bc.Segments.Count - 1 && bc.Segments[item.Segment].OnClick is { } onClick)
                {
                    onClick();
                }
                return;
            }
        }
    }
    // ── ToggleGroup helpers ───────────────────────────────────────────

    private void HandleToggleGroupClick(IToggleGroup tg)
    {
        var bounds = Live(tg, tg.AbsoluteBounds);
        int count = tg.OptionCount;
        if (count == 0)
        {
            return;
        }

        int clickedIndex = count - 1;
        if (tg.IsVertical)
        {
            clickedIndex = Math.Clamp((int)MathF.Floor((lastMousePosition.Y - bounds.Y - 2f) / IToggleGroup.VerticalRowHeight), 0, count - 1);
        }
        else
        {
            // The buttons as layout and paint size them (rich labels by their laid-out width).
            float[] widths = SegmentLayout.ToggleWidths(tg, bounds.Width);
            float relX = lastMousePosition.X - bounds.X;
            float right = 0f;
            for (int i = 0; i < count; i++)
            {
                right += widths[i];
                if (relX < right)
                {
                    clickedIndex = i;
                    break;
                }
            }
        }
        tg.SelectIndex(clickedIndex);
    }

    /// <summary>
    /// Keys for a focused, closed dropdown, segmented control or rating: Space, Enter, Down or
    /// Alt+Down opens a Select / MultiSelect / Combobox (a typed character opens a Combobox with it
    /// as the search); arrows move a segmented control's selection; arrows, Home and End set a rating.
    /// </summary>
    private bool HandleFocusedChoiceKey(Node? focused, NativeKeyEvent evt)
    {
        bool opens = evt.Key is Key.Space or Key.Enter or Key.Down
            && (evt.Modifiers == ModifierKeys.None || (evt.Key == Key.Down && evt.Modifiers == ModifierKeys.Alt));
        switch (focused)
        {
            case ISelectNode { IsNodeDisabled: false, IsOpen: false } select when opens:
                select.ToggleOpen();
                TrackOpenSelect(select);
                return true;

            case IMultiSelectNode { IsNodeDisabled: false, IsOpen: false } multi when opens:
                multi.ToggleOpen();
                openMultiSelect = multi.IsOpen ? multi : null;
                return true;

            case IComboboxNode { IsNodeDisabled: false, IsOpen: false } combo:
                if (opens)
                {
                    combo.ToggleOpen();
                }
                else if (evt.Character is { } typed && typed >= ' ' && evt.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
                {
                    combo.ToggleOpen();
                    if (combo.IsOpen)
                    {
                        combo.SearchText = typed.ToString();
                    }
                }
                else
                {
                    return false;
                }
                openCombobox = combo.IsOpen ? combo : null;
                return true;

            case ISegmentedControl { IsControlDisabled: false } segmented when evt.Modifiers == ModifierKeys.None:
            {
                int delta = evt.Key switch { Key.Left or Key.Up => -1, Key.Right or Key.Down => 1, _ => 0 };
                if (delta == 0 || segmented.SegmentCount == 0)
                {
                    return false;
                }
                int next = Math.Clamp(segmented.SelectedIndex < 0 ? 0 : segmented.SelectedIndex + delta, 0, segmented.SegmentCount - 1);
                if (next != segmented.SelectedIndex)
                {
                    segmented.SelectIndex(next);
                }
                return true;
            }

            case Rating { IsDisabled: false, IsReadOnly: false, BoundValue: { } bind } rating when evt.Modifiers == ModifierKeys.None:
            {
                float step = rating.HalfStarsEnabled ? 0.5f : 1f;
                float? target = evt.Key switch
                {
                    Key.Right or Key.Up => bind.Value + step,
                    Key.Left or Key.Down => bind.Value - step,
                    Key.Home => 0f,
                    Key.End => rating.Max,
                    _ => null,
                };
                if (target is not { } value)
                {
                    return false;
                }
                value = Math.Clamp(MathF.Round(value / step) * step, 0f, rating.Max);
                if (value != bind.Value)
                {
                    bind.OnChange(value);
                }
                return true;
            }

            default:
                return false;
        }
    }

    // ── Debounced OnChange (.Debounce) ──────────────────────────────────

    private static Action<string>? pendingChangeHandler;
    private static string pendingChangeValue = "";
    private static long pendingChangeDue;

    /// <summary>
    /// Raises a text field's OnChange: at once, or with <c>.Debounce(delay)</c> once typing has
    /// paused that long (only the latest text is reported). A pending change is delivered early when
    /// focus moves on, so it is never lost.
    /// </summary>
    internal static void RaiseOnChange(Action<string>? handler, TimeSpan? delay, string value)
    {
        if (handler is null)
        {
            return;
        }
        if (delay is not { } wait || wait <= TimeSpan.Zero)
        {
            handler(value);
            return;
        }
        pendingChangeHandler = handler;
        pendingChangeValue = value;
        pendingChangeDue = Stopwatch.GetTimestamp() + (long)(wait.TotalSeconds * Stopwatch.Frequency);
        ControlStateAnimator.SignalActiveTransition();
    }

    /// <summary>Delivers a debounced OnChange whose delay has passed (or any, with <paramref name="now"/>). Called each frame.</summary>
    internal static void FlushPendingChange(bool now = false)
    {
        if (pendingChangeHandler is not { } handler)
        {
            return;
        }
        if (!now && Stopwatch.GetTimestamp() < pendingChangeDue)
        {
            // Keep frames coming until it is due.
            ControlStateAnimator.SignalActiveTransition();
            return;
        }
        pendingChangeHandler = null;
        handler(pendingChangeValue);
    }

    /// <summary>
    /// Keys for an open suggestion list: Up/Down move the highlight, Enter or Tab accepts the highlighted
    /// suggestion (with none highlighted they go to the field as usual), Escape puts the list away.
    /// </summary>
    private bool HandleSuggestionKey(ISuggestionHost host, NativeKeyEvent evt)
    {
        var list = host.Suggestions;
        switch (evt.Key)
        {
            case Key.Down:
                list.Move(1);
                return true;
            case Key.Up:
                list.Move(-1);
                return true;
            case Key.Enter or Key.Tab when list.Highlight >= 0 && list.Highlight < list.Items.Count && evt.Modifiers == ModifierKeys.None:
                AcceptSuggestion(host, list.Items[list.Highlight]);
                return true;
            case Key.Escape:
                list.Dismiss();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Puts a suggestion in its field: a TextInput's text becomes it (caret at the end, OnChange
    /// raised); a TagInput adds it as a tag.
    /// </summary>
    private void AcceptSuggestion(ISuggestionHost host, SuggestionItem item)
    {
        switch (host)
        {
            case TextInput input:
                textInputBuffer = item.Text;
                ActiveEditBuffer = item.Text;
                TextInputCaretIndex = item.Text.Length;
                TextInputSelectionAnchor = TextInputCaretIndex;
                input.Value.OnChange(item.Text);
                RaiseOnChange(input.OnChangeHandler, input.DebounceDelay, item.Text);
                break;
            case TagInput tags:
                tags.AddTag(item.Text);
                break;
        }
        host.Suggestions.Dismiss();
    }

    /// <summary>
    /// Scrolls a wheel step along a chain of nested ScrollViews (outermost first): the innermost that
    /// can still move in that direction takes it, unless one says otherwise (.NestedScroll):
    /// <see cref="NestedScrollMode.ParentFirst"/> lets its parent move first, and
    /// <see cref="NestedScrollMode.SelfOnly"/> keeps the wheel even at its edge. Shift turns the wheel
    /// sideways, as does a view that only scrolls across. Returns the view that took it, or null.
    /// </summary>
    private static (ScrollView View, bool Moved)? ScrollWheelAlongChain(List<ScrollView> chain, NativeScrollEvent evt)
    {
        (float X, float Y) DeltaFor(ScrollView sv)
        {
            float step = sv.Physics.MouseWheelStepPx;
            float dx = -evt.DeltaX * step, dy = -evt.DeltaY * step;
            if (evt.Modifiers.HasFlag(ModifierKeys.Shift) || (!sv.ScrollsVertically && dx == 0f))
            {
                (dx, dy) = (dx == 0f ? dy : dx, 0f);
            }
            return (sv.ScrollsHorizontally ? dx : 0f, sv.ScrollsVertically ? dy : 0f);
        }

        static bool CanMove(ScrollView sv, (float X, float Y) d) =>
            (d.Y < 0f && sv.OffsetY > 0f) || (d.Y > 0f && sv.OffsetY < sv.MaxY)
            || (d.X < 0f && sv.OffsetX > 0f) || (d.X > 0f && sv.OffsetX < sv.MaxX);

        static (ScrollView, bool) Move(ScrollView sv, (float X, float Y) d) =>
            (sv, sv.Control.MoveTo(sv.OffsetX + d.X, sv.OffsetY + d.Y));

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var sv = chain[i];
            var parent = i > 0 ? chain[i - 1] : null;
            if (sv.ParentScrollsFirst && parent is not null && CanMove(parent, DeltaFor(parent)))
            {
                return Move(parent, DeltaFor(parent));
            }
            if (CanMove(sv, DeltaFor(sv)))
            {
                return Move(sv, DeltaFor(sv));
            }
            if (sv.KeepsWheelAtEdge)
            {
                return (sv, sv.Overscroll(DeltaFor(sv).X, DeltaFor(sv).Y));
            }
        }

        // Nothing could move: the innermost shows the overscroll (stretch, glow, pull to refresh).
        var innermost = chain[^1];
        return innermost.Overscroll(DeltaFor(innermost).X, DeltaFor(innermost).Y) ? (innermost, true) : null;
    }

    /// <summary>
    /// Keyboard scrolling (.KeyboardScrolling, on by default) for keys no control used: arrows move
    /// 40 px, Page Up/Down (and Space / Shift+Space) a viewport, Home/End to the ends. The ScrollView
    /// is the nearest one around the focused control, else the one under the pointer.
    /// </summary>
    private bool HandleScrollViewKey(NativeKeyEvent evt)
    {
        if (rootNode is null || evt.Type != NativeKeyEventType.KeyDown || evt.Modifiers.HasFlag(ModifierKeys.Ctrl)
            || evt.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return false;
        }

        ScrollView? sv = null;
        if (FocusManager.FocusedElement is { } focused && TryGetAbsoluteBounds(focused, out var focusBounds))
        {
            sv = HitTester.FindScrollViewAt(rootNode, focusBounds.X + 1f, focusBounds.Y + 1f);
        }
        sv ??= HitTester.FindScrollViewAt(rootNode, lastMousePosition.X, lastMousePosition.Y);
        if (sv is null || !sv.IsKeyboardScrollingEnabled)
        {
            return false;
        }

        var viewport = sv.Control.ViewportSize;
        bool across = !sv.ScrollsVertically;
        float page = (across ? viewport.Width : viewport.Height) * 0.9f;
        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        (float X, float Y)? target = evt.Key switch
        {
            Key.Down when sv.ScrollsVertically => (sv.OffsetX, sv.OffsetY + 40f),
            Key.Up when sv.ScrollsVertically => (sv.OffsetX, sv.OffsetY - 40f),
            Key.Right when sv.ScrollsHorizontally => (sv.OffsetX + 40f, sv.OffsetY),
            Key.Left when sv.ScrollsHorizontally => (sv.OffsetX - 40f, sv.OffsetY),
            Key.PageDown => across ? (sv.OffsetX + page, sv.OffsetY) : (sv.OffsetX, sv.OffsetY + page),
            Key.PageUp => across ? (sv.OffsetX - page, sv.OffsetY) : (sv.OffsetX, sv.OffsetY - page),
            Key.Space when evt.Character is null or ' ' => across
                ? (sv.OffsetX + (shift ? -page : page), sv.OffsetY)
                : (sv.OffsetX, sv.OffsetY + (shift ? -page : page)),
            Key.Home => across ? (0f, sv.OffsetY) : (sv.OffsetX, 0f),
            Key.End => across ? (sv.MaxX, sv.OffsetY) : (sv.OffsetX, sv.MaxY),
            _ => null,
        };
        if (target is not { } to)
        {
            return false;
        }
        _ = sv.Control.ScrollToAsync(to.X, to.Y);
        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>Arrow keys move a focused toggle group's selection to the previous or next option.</summary>
    private static bool HandleToggleGroupKey(IToggleGroup tg, NativeKeyEvent evt)
    {
        int count = tg.OptionCount;
        int delta = evt.Key switch
        {
            Key.Left or Key.Up => -1,
            Key.Right or Key.Down => 1,
            _ => 0,
        };
        if (delta == 0 || count == 0)
        {
            return false;
        }
        int next = Math.Clamp(tg.SelectedIndex < 0 ? 0 : tg.SelectedIndex + delta, 0, count - 1);
        if (next != tg.SelectedIndex)
        {
            tg.SelectIndex(next);
        }
        return true;
    }

    /// <summary>
    /// Hit-tests a SegmentedControl or ToggleGroup: the segments are sized by
    /// <see cref="SegmentLayout"/>, exactly as the layout and the painter size them.
    /// </summary>
    internal static int HitTestVariableWidthSegments(
        Rect bounds,
        int count,
        Func<int, string> getLabel,
        float paddingH,
        float absoluteX)
    {
        if (count == 0)
        {
            return 0;
        }

        float[] widths = new float[count];
        for (int i = 0; i < count; i++)
        {
            widths[i] = SegmentLayout.NaturalWidth(getLabel(i), paddingH);
        }

        SegmentLayout.ScaleToFit(widths, bounds.Width);

        float relX = absoluteX - bounds.X;
        float xAcc = 0f;
        for (int i = 0; i < count; i++)
        {
            float nextX = xAcc + widths[i];
            if (relX < nextX)
            {
                return i;
            }
            xAcc = nextX;
        }
        return count - 1;
    }

    // ── StepIndicator helpers ────────────────────────────────────────

    private void HandleStepIndicatorClick(StepIndicator si)
    {
        var bounds = Live(si, si.AbsoluteBounds);
        int stepCount = si.Steps.Count;
        if (stepCount == 0)
        {
            return;
        }

        // The same arrangement the painter drew, for whichever style it is.
        int step = StepIndicator.StepAt(si.StyleSetting, stepCount, bounds.Width, bounds.Height,
            lastMousePosition.X - bounds.X, lastMousePosition.Y - bounds.Y);
        if (step >= 0 && (si.ClickablePredicate == null || si.ClickablePredicate(step)))
        {
            si.StepClickHandler?.Invoke(step);
        }
    }

    private void HandleBannerDismissClick(Banner banner)
    {
        // Only dismiss if click is in the dismiss hit rect
        var dismissRect = banner.DismissHitRect;
        if (dismissRect.Width > 0 && dismissRect.Height > 0)
        {
            float mx = lastMousePosition.X;
            float my = lastMousePosition.Y;
            if (mx >= dismissRect.X && mx <= dismissRect.X + dismissRect.Width &&
                my >= dismissRect.Y && my <= dismissRect.Y + dismissRect.Height)
            {
                banner.OnDismiss?.Invoke();
                return;
            }
        }

        // Fallback: any click on the banner triggers dismiss
        banner.OnDismiss?.Invoke();
    }

    // ── DatePicker calendar helpers ──────────────────────────────────

    private void HandleCalendarPopupClick(DatePicker dp, float x, float y)
    {
        var clickPoint = new Point(x, y);

        // Check header label click (switches view mode)
        if (dp.HeaderLabelBounds.Width > 0 && dp.HeaderLabelBounds.Contains(clickPoint))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.ShowMonths();
                    break;
                case CalendarViewMode.Months:
                    dp.ShowYears();
                    break;
                // Years header is not clickable (already at top level)
            }

            RequestRepaint?.Invoke();
            return;
        }

        // Check prev/next arrows (behavior depends on view mode)
        if (dp.PrevMonthBounds.Width > 0 && dp.PrevMonthBounds.Contains(clickPoint))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.NavigateMonth(-1);
                    break;
                case CalendarViewMode.Months:
                    dp.DisplayedYear--;
                    break;
                case CalendarViewMode.Years:
                    dp.NavigateYearGrid(-12);
                    break;
            }

            RequestRepaint?.Invoke();
            return;
        }

        if (dp.NextMonthBounds.Width > 0 && dp.NextMonthBounds.Contains(clickPoint))
        {
            switch (dp.ViewMode)
            {
                case CalendarViewMode.Days:
                    dp.NavigateMonth(1);
                    break;
                case CalendarViewMode.Months:
                    dp.DisplayedYear++;
                    break;
                case CalendarViewMode.Years:
                    dp.NavigateYearGrid(12);
                    break;
            }

            RequestRepaint?.Invoke();
            return;
        }

        // Dispatch grid click based on view mode
        switch (dp.ViewMode)
        {
            case CalendarViewMode.Days:
                HandleCalendarDayClick(dp, x, y);
                break;
            case CalendarViewMode.Months:
                HandleCalendarMonthClick(dp, x, y);
                break;
            case CalendarViewMode.Years:
                HandleCalendarYearClick(dp, x, y);
                break;
        }
    }

    private void HandleCalendarDayClick(DatePicker dp, float x, float y)
    {
        float cellSize = dp.CalendarCellSize;
        if (cellSize <= 0 || y < dp.CalendarGridTop)
        {
            return;
        }

        int col = (int)((x - dp.CalendarGridLeft) / cellSize);
        int row = (int)((y - dp.CalendarGridTop) / cellSize);
        if (col < 0 || col >= 7 || row < 0 || row >= 6)
        {
            return;
        }

        int cellIndex = row * 7 + col;
        var date = dp.CalendarGridStartDate.AddDays(cellIndex);

        // Only allow selection of dates in the current month
        if (date.Month != dp.DisplayedMonth || date.Year != dp.DisplayedYear)
        {
            return;
        }

        // Check min/max constraints
        if (dp.Min.HasValue && date < dp.Min.Value)
        {
            return;
        }

        if (dp.Max.HasValue && date > dp.Max.Value)
        {
            return;
        }

        // Check disabled dates
        if (dp.DisabledDatesPredicate?.Invoke(date) == true)
        {
            return;
        }

        dp.SelectDate(date);
        openDatePicker = null;
    }

    private void HandleCalendarMonthClick(DatePicker dp, float x, float y)
    {
        if (y < dp.CalendarGridTop)
        {
            return;
        }

        // 4 cols × 3 rows grid
        float cellW = (dp.CalendarBounds.Width - 24f) / 4f; // contentW / 4
        float cellH = (dp.CalendarCellSize * 6) / 3f;       // (cellSize * rows) / 3
        int col = (int)((x - dp.CalendarGridLeft) / cellW);
        int row = (int)((y - dp.CalendarGridTop) / cellH);

        if (col < 0 || col >= 4 || row < 0 || row >= 3)
        {
            return;
        }

        int monthIndex = row * 4 + col;
        if (monthIndex >= 0 && monthIndex < 12)
        {
            dp.SelectMonth(monthIndex + 1);
            RequestRepaint?.Invoke();
        }
    }

    private void HandleCalendarYearClick(DatePicker dp, float x, float y)
    {
        if (y < dp.CalendarGridTop)
        {
            return;
        }

        // 4 cols × 3 rows grid
        float cellW = (dp.CalendarBounds.Width - 24f) / 4f;
        float cellH = (dp.CalendarCellSize * 6) / 3f;
        int col = (int)((x - dp.CalendarGridLeft) / cellW);
        int row = (int)((y - dp.CalendarGridTop) / cellH);

        if (col < 0 || col >= 4 || row < 0 || row >= 3)
        {
            return;
        }

        int yearIndex = row * 4 + col;
        if (yearIndex >= 0 && yearIndex < 12)
        {
            dp.SelectYear(dp.YearGridStart + yearIndex);
            RequestRepaint?.Invoke();
        }
    }

    private void UpdateCalendarHover(DatePicker dp, float x, float y)
    {
        var calBounds = dp.CalendarBounds;
        if (calBounds.Width <= 0)
        {
            return;
        }

        if (y < dp.CalendarGridTop)
        {
            if (dp.HighlightedDay != -1)
            {
                dp.HighlightedDay = -1;
                RequestRepaint?.Invoke();
            }

            return;
        }

        int cellIndex = -1;

        switch (dp.ViewMode)
        {
            case CalendarViewMode.Days:
            {
                float cellSize = dp.CalendarCellSize;
                if (cellSize <= 0)
                {
                    break;
                }

                int col = (int)((x - dp.CalendarGridLeft) / cellSize);
                int row = (int)((y - dp.CalendarGridTop) / cellSize);
                if (col >= 0 && col < 7 && row >= 0 && row < 6)
                {
                    cellIndex = row * 7 + col;
                }

                break;
            }
            case CalendarViewMode.Months:
            case CalendarViewMode.Years:
            {
                float cellW = (calBounds.Width - 24f) / 4f;
                float cellH = (dp.CalendarCellSize * 6) / 3f;
                int col = (int)((x - dp.CalendarGridLeft) / cellW);
                int row = (int)((y - dp.CalendarGridTop) / cellH);
                if (col >= 0 && col < 4 && row >= 0 && row < 3)
                {
                    cellIndex = row * 4 + col;
                }

                break;
            }
        }

        if (cellIndex != dp.HighlightedDay)
        {
            dp.HighlightedDay = cellIndex;
            RequestRepaint?.Invoke();
        }
    }

    private void HandleCalendarKeyboard(NativeKeyEvent evt)
    {
        if (openDatePicker == null)
        {
            return;
        }

        var dp = openDatePicker;

        switch (evt.Key)
        {
            case Key.Escape:
                // Escape goes back one level, or closes if already at Days
                if (dp.ViewMode == CalendarViewMode.Years)
                {
                    dp.ViewMode = CalendarViewMode.Months;
                    dp.HighlightedDay = -1;
                }
                else if (dp.ViewMode == CalendarViewMode.Months)
                {
                    dp.ViewMode = CalendarViewMode.Days;
                    dp.HighlightedDay = -1;
                }
                else
                {
                    dp.CloseCalendar();
                    openDatePicker = null;
                }

                RequestRepaint?.Invoke();
                break;

            // Days: the keyboard moves a day at a time (a week up or down), pages by month (Shift:
            // year), Home/End to the week's ends, and Enter or Space chooses.
            case Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Space
                when dp.ViewMode == CalendarViewMode.Days:
            {
                var from = dp.KeyboardDate == default ? dp.Value.Value ?? DateOnly.FromDateTime(DateTime.Today) : dp.KeyboardDate;
                bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
                if (evt.Key == Key.Space)
                {
                    if (!dp.IsDateDisabled(from))
                    {
                        dp.SelectDate(from);
                        openDatePicker = null;
                    }
                    RequestRepaint?.Invoke();
                    break;
                }
                var to = evt.Key switch
                {
                    Key.Left => from.AddDays(-1),
                    Key.Right => from.AddDays(1),
                    Key.Up => from.AddDays(-7),
                    Key.Down => from.AddDays(7),
                    Key.PageUp => shift ? from.AddYears(-1) : from.AddMonths(-1),
                    Key.PageDown => shift ? from.AddYears(1) : from.AddMonths(1),
                    Key.Home => CalendarLayout.WeekStart(from),
                    _ => CalendarLayout.WeekStart(from).AddDays(6),
                };
                dp.MoveKeyboardTo(to);
                RequestRepaint?.Invoke();
                break;
            }

            // Months and years: the arrows move round the 4 × 3 grid.
            case Key.Up or Key.Down when dp.ViewMode != CalendarViewMode.Days:
                dp.HighlightedDay = Math.Clamp((dp.HighlightedDay < 0 ? 0 : dp.HighlightedDay) + (evt.Key == Key.Down ? 4 : -4), 0, 11);
                RequestRepaint?.Invoke();
                break;

            case Key.Left when dp.ViewMode != CalendarViewMode.Days && dp.HighlightedDay > 0:
                dp.HighlightedDay--;
                RequestRepaint?.Invoke();
                break;

            case Key.Right when dp.ViewMode != CalendarViewMode.Days && dp.HighlightedDay is >= 0 and < 11:
                dp.HighlightedDay++;
                RequestRepaint?.Invoke();
                break;

            case Key.Left:
                switch (dp.ViewMode)
                {
                    case CalendarViewMode.Days:
                        dp.NavigateMonth(-1);
                        break;
                    case CalendarViewMode.Months:
                        dp.DisplayedYear--;
                        break;
                    case CalendarViewMode.Years:
                        dp.NavigateYearGrid(-12);
                        break;
                }

                RequestRepaint?.Invoke();
                break;

            case Key.Right:
                switch (dp.ViewMode)
                {
                    case CalendarViewMode.Days:
                        dp.NavigateMonth(1);
                        break;
                    case CalendarViewMode.Months:
                        dp.DisplayedYear++;
                        break;
                    case CalendarViewMode.Years:
                        dp.NavigateYearGrid(12);
                        break;
                }

                RequestRepaint?.Invoke();
                break;

            case Key.Enter:
            {
                if (dp.HighlightedDay < 0)
                {
                    break;
                }

                switch (dp.ViewMode)
                {
                    case CalendarViewMode.Days:
                    {
                        if (dp.HighlightedDay < dp.CalendarGridCellCount)
                        {
                            // The keyboard's day if it has one (the painter's grid can lag a month change).
                            var date = dp.KeyboardDate != default && dp.KeyboardDate.Month == dp.DisplayedMonth
                                ? dp.KeyboardDate : dp.CalendarGridStartDate.AddDays(dp.HighlightedDay);
                            if (date.Month == dp.DisplayedMonth && date.Year == dp.DisplayedYear)
                            {
                                bool disabled = (dp.Min.HasValue && date < dp.Min.Value)
                                    || (dp.Max.HasValue && date > dp.Max.Value)
                                    || (dp.DisabledDatesPredicate?.Invoke(date) == true);

                                if (!disabled)
                                {
                                    dp.SelectDate(date);
                                    openDatePicker = null;
                                    RequestRepaint?.Invoke();
                                }
                            }
                        }

                        break;
                    }
                    case CalendarViewMode.Months:
                    {
                        if (dp.HighlightedDay < 12)
                        {
                            dp.SelectMonth(dp.HighlightedDay + 1);
                            RequestRepaint?.Invoke();
                        }

                        break;
                    }
                    case CalendarViewMode.Years:
                    {
                        if (dp.HighlightedDay < 12)
                        {
                            dp.SelectYear(dp.YearGridStart + dp.HighlightedDay);
                            RequestRepaint?.Invoke();
                        }

                        break;
                    }
                }

                break;
            }
        }
    }

    /// <summary>
    /// Keys for the range, date-and-time and month pickers (the date picker has its own): focused
    /// and closed, Enter, Space or Down opens on the current value; open, Escape closes (the
    /// date-and-time picker without keeping the change). In a day grid the arrows walk the days (a
    /// week up or down), Page Up/Down the month (Shift: the year), Enter or Space chooses (the range
    /// picker's start, then its end); the month picker's arrows go round its 4 × 3 grid.
    /// </summary>
    private bool HandleDateFamilyKey(NativeKeyEvent evt)
    {
        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        DateOnly Walk(DateOnly from) => evt.Key switch
        {
            Key.Left => from.AddDays(-1),
            Key.Right => from.AddDays(1),
            Key.Up => from.AddDays(-7),
            Key.Down => from.AddDays(7),
            Key.PageUp => shift ? from.AddYears(-1) : from.AddMonths(-1),
            Key.PageDown => shift ? from.AddYears(1) : from.AddMonths(1),
            Key.Home => CalendarLayout.WeekStart(from),
            Key.End => CalendarLayout.WeekStart(from).AddDays(6),
            _ => from,
        };
        bool walking = evt.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End;
        bool choosing = evt.Key is Key.Enter or Key.Space;
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (openDateRangePicker is { } range)
        {
            if (evt.Key == Key.Escape)
            {
                range.CloseCalendar();
                openDateRangePicker = null;
                return true;
            }
            var at = range.KeyboardDate == default ? range.StartBind.Value ?? today : range.KeyboardDate;
            if (walking)
            {
                range.MoveKeyboardTo(Walk(at));
                return true;
            }
            if (choosing)
            {
                range.SelectDay(at);
                if (!range.IsCalendarOpen)
                {
                    openDateRangePicker = null;
                }
                return true;
            }
            return false;
        }

        if (openDateTimePicker is { } dateTime)
        {
            if (evt.Key == Key.Escape)
            {
                dateTime.CancelCalendar();
                openDateTimePicker = null;
                return true;
            }
            var at = dateTime.SelectedDate ?? (dateTime.Value.Value is { } v ? DateOnly.FromDateTime(v) : today);
            if (walking)
            {
                dateTime.MoveKeyboardTo(Walk(at));
                return true;
            }
            if (choosing)
            {
                dateTime.SelectDate(at);
                dateTime.CloseCalendar();
                openDateTimePicker = null;
                return true;
            }
            return false;
        }

        if (openMonthPicker is { } month)
        {
            int at = month.HighlightedMonth < 0 ? (month.Value.Value is { } ym && ym.Year == month.DisplayedYear ? ym.Month - 1 : 0) : month.HighlightedMonth;
            switch (evt.Key)
            {
                case Key.Escape:
                    month.ClosePopup();
                    openMonthPicker = null;
                    return true;
                case Key.Left:
                    month.HighlightedMonth = Math.Max(0, at - 1);
                    return true;
                case Key.Right:
                    month.HighlightedMonth = Math.Min(11, at + 1);
                    return true;
                case Key.Up:
                    month.HighlightedMonth = Math.Max(0, at - 4);
                    return true;
                case Key.Down:
                    month.HighlightedMonth = Math.Min(11, at + 4);
                    return true;
                case Key.PageUp or Key.PageDown:
                    month.NavigateYear(evt.Key == Key.PageUp ? -1 : 1);
                    month.HighlightedMonth = at;
                    return true;
                case Key.Enter or Key.Space:
                    month.SelectMonth(at + 1);
                    if (!month.IsPopupOpen)
                    {
                        openMonthPicker = null;
                    }
                    return true;
            }
            return false;
        }

        // Closed and focused: open from the keyboard.
        if (evt.Key is not (Key.Enter or Key.Space or Key.Down))
        {
            return false;
        }
        switch (FocusManager.FocusedElement)
        {
            case DateRangePicker closedRange when !closedRange.IsCalendarOpen:
                closedRange.OpenCalendar();
                closedRange.MoveKeyboardTo(closedRange.StartBind.Value ?? today);
                openDateRangePicker = closedRange;
                return true;
            case DateTimePicker closedDateTime when !closedDateTime.IsCalendarOpen:
                closedDateTime.OpenCalendar();
                closedDateTime.MoveKeyboardTo(closedDateTime.Value.Value is { } current ? DateOnly.FromDateTime(current) : today);
                openDateTimePicker = closedDateTime;
                return true;
            case MonthPicker closedMonth when !closedMonth.IsPopupOpen:
                closedMonth.OpenPopup();
                closedMonth.HighlightedMonth = closedMonth.Value.Value is { } chosen && chosen.Year == closedMonth.DisplayedYear ? chosen.Month - 1 : 0;
                openMonthPicker = closedMonth;
                return true;
        }
        return false;
    }

    // ── Calendar (full inline) helpers ────────────────────────────────

    // ── NavigationBar ─────────────────────────────────────────────────

    /// <summary>
    /// Keys on a focused section item: Enter or Space makes it active; the arrows along the bar
    /// (Up/Down in a sidebar or rail, Left/Right in a top or bottom bar) move to the next enabled
    /// item, wrapping, and Home/End to the first and last.
    /// </summary>
    private bool HandleNavItemKey(NavigationBar bar, NavItem item, NativeKeyEvent evt)
    {
        if (evt.Key is Key.Enter or Key.Space)
        {
            bar.Activate(item);
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, item);
            }
            return true;
        }
        bool vertical = item.BarStyle is NavBarStyle.Sidebar or NavBarStyle.Rail;
        var enabled = bar.Items.Where(i => i.Enabled).ToList();
        int at = enabled.IndexOf(item);
        if (at < 0 || enabled.Count == 0)
        {
            return false;
        }
        int next = evt.Key switch
        {
            Key.Up when vertical => (at - 1 + enabled.Count) % enabled.Count,
            Key.Down when vertical => (at + 1) % enabled.Count,
            Key.Left when !vertical => (at - 1 + enabled.Count) % enabled.Count,
            Key.Right when !vertical => (at + 1) % enabled.Count,
            Key.Home => 0,
            Key.End => enabled.Count - 1,
            _ => -1,
        };
        if (next < 0)
        {
            return false;
        }
        FocusManager.RequestFocus(enabled[next]);
        FocusManager.LastFocusWasKeyboard = true;
        return true;
    }

    // ── Calendar ──────────────────────────────────────────────────────

    // The calendar's arrangement and a window point in its coordinates, from where layout put it.
    private CalendarLayout? CalendarAt(Calendar cal, float x, float y, out Point local)
    {
        local = default;
        if (rootNode is null || !HitTester.TryGetAbsoluteBounds(rootNode, cal, out var bounds))
        {
            return null;
        }
        cal.EnsureInitialized();
        local = new Point(x - bounds.X, y - bounds.Y);
        return CalendarLayout.Arrange(cal, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// A press on a calendar: navigation and the view switch act at once; on an event, a day or a
    /// time it starts what a drag makes of it (moving, stretching, creating) or, released without
    /// moving, a click on it.
    /// </summary>
    private void PressCalendar(Calendar cal, float x, float y)
    {
        if (CalendarAt(cal, x, y, out var p) is not { } l)
        {
            return;
        }
        var (part, index, day) = l.HitTest(p);
        switch (part)
        {
            case CalendarPart.Prev:
                cal.Navigate(-1);
                return;
            case CalendarPart.Next:
                cal.Navigate(1);
                return;
            case CalendarPart.Today:
                cal.GoToToday();
                return;
            case CalendarPart.ViewTab:
                cal.ActiveView = l.ViewTabs[index].View;
                return;
            case CalendarPart.More:
                cal.ActiveView = CalendarView.Day;
                cal.Anchor = day;
                cal.KeyboardDate = day;
                return;
            case CalendarPart.Event or CalendarPart.EventEnd:
            {
                var box = l.Boxes[index];
                bool timed = cal.IsTimeGrid && box.Timed;
                var at = Calendar.TimeAt(l, p, timed);
                var kind = part == CalendarPart.EventEnd && cal.OnDragResize is not null ? Calendar.DragKind.Resize : Calendar.DragKind.Move;
                cal.Drag = new Calendar.DragState(kind, box.Event, at, at, p, false, timed);
                return;
            }
            case CalendarPart.Day or CalendarPart.AllDay or CalendarPart.Slot:
            {
                bool timed = part == CalendarPart.Slot;
                var at = Calendar.TimeAt(l, p, timed);
                cal.Drag = new Calendar.DragState(Calendar.DragKind.Create, null, at, at, p, false, timed);
                return;
            }
        }
    }

    private void DragCalendar(Calendar cal, float x, float y)
    {
        if (cal.Drag is not { } d || CalendarAt(cal, x, y, out var p) is not { } l)
        {
            return;
        }
        // Only a drag something listens for counts; without one, the press stays a click.
        bool wanted = d.Kind switch
        {
            Calendar.DragKind.Create => cal.OnDragCreate is not null,
            Calendar.DragKind.Move => cal.OnDragMove is not null,
            _ => cal.OnDragResize is not null,
        };
        if (!wanted)
        {
            return;
        }
        bool moved = d.Moved || MathF.Abs(p.X - d.PressedAt.X) > 4f || MathF.Abs(p.Y - d.PressedAt.Y) > 4f;
        cal.Drag = d with { Current = Calendar.TimeAt(l, p, d.Timed), Moved = moved };
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, cal);
        }
        RequestRepaint?.Invoke();
    }

    private void ReleaseCalendar(Calendar cal)
    {
        if (cal.Drag is not { } d)
        {
            return;
        }
        if (d.Moved)
        {
            cal.FinishDrag();
        }
        else
        {
            cal.Drag = null;
            if (d.Event is { } clicked)
            {
                cal.OnEventClick?.Invoke(clicked);
            }
            else
            {
                cal.ChooseDay(DateOnly.FromDateTime(d.Origin.DateTime));
            }
        }
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, cal);
        }
        RequestRepaint?.Invoke();
    }

    private void HoverCalendar(Calendar cal, float x, float y)
    {
        if (CalendarAt(cal, x, y, out var p) is not { } l)
        {
            return;
        }
        var day = l.View == CalendarView.Month && p.Y >= l.GridTop ? l.DayAt(p) : null;
        if (day != cal.HoveredDate)
        {
            cal.HoveredDate = day;
            RequestRepaint?.Invoke();
        }
    }

    // The resize cursor over the end of an event that can be stretched.
    private int CalendarCursor(Calendar cal, float x, float y)
    {
        if (cal.OnDragResize is null || CalendarAt(cal, x, y, out var p) is not { } l)
        {
            return 0;
        }
        if (cal.Drag is { Kind: Calendar.DragKind.Resize } resizing)
        {
            return resizing.Timed ? 2 : 1;
        }
        var (part, index, _) = l.HitTest(p);
        return part == CalendarPart.EventEnd ? (l.Boxes[index].Timed && cal.IsTimeGrid ? 2 : 1) : 0;
    }

    private bool ScrollCalendar(Calendar cal, NativeScrollEvent evt)
    {
        if (cal.ActiveView == CalendarView.Month || CalendarAt(cal, evt.X, evt.Y, out var p) is not { } l || !l.Scroller.Contains(p))
        {
            return false;
        }
        float next = Math.Clamp(l.ScrollY - evt.DeltaY * 48f, 0f, l.MaxScroll);
        if (Math.Abs(next - l.ScrollY) > 0.001f)
        {
            cal.ScrollY = next;
            if (rootNode is not null)
            {
                MarkScrollViewLayersDirty(rootNode, cal);
            }
            RequestRepaint?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// Keys on a focused calendar: arrows move the day (a week up or down), Page Up/Down the period,
    /// Home goes to today, Enter or Space chooses the day; in the day view Up/Down scroll the hours.
    /// </summary>
    private static bool HandleCalendarKey(Calendar cal, NativeKeyEvent evt)
    {
        cal.EnsureInitialized();
        var day = cal.KeyboardDate;
        switch (evt.Key)
        {
            case Key.Left:
                day = day.AddDays(-1);
                break;
            case Key.Right:
                day = day.AddDays(1);
                break;
            case Key.Up when cal.ActiveView == CalendarView.Day:
                cal.ScrollY = Math.Max(0f, (cal.ScrollY ?? CalendarLayout.HourHeight * 8f) - CalendarLayout.HourHeight);
                return true;
            case Key.Down when cal.ActiveView == CalendarView.Day:
                cal.ScrollY = Math.Min(CalendarLayout.HourHeight * 23f, (cal.ScrollY ?? CalendarLayout.HourHeight * 8f) + CalendarLayout.HourHeight);
                return true;
            case Key.Up:
                day = day.AddDays(-7);
                break;
            case Key.Down:
                day = day.AddDays(7);
                break;
            case Key.PageUp:
                cal.Navigate(-1);
                return true;
            case Key.PageDown:
                cal.Navigate(1);
                return true;
            case Key.Home:
                cal.GoToToday();
                return true;
            case Key.Enter or Key.Space:
                cal.ChooseDay(day);
                return true;
            default:
                return false;
        }

        // The shown period follows the keyboard day.
        cal.KeyboardDate = day;
        bool outside = cal.ActiveView switch
        {
            CalendarView.Month => day.Month != cal.Anchor.Month || day.Year != cal.Anchor.Year,
            CalendarView.Week => CalendarLayout.WeekStart(day) != CalendarLayout.WeekStart(cal.Anchor),
            CalendarView.Day => day != cal.Anchor,
            _ => false,
        };
        if (outside)
        {
            cal.Anchor = day;
        }
        if (cal.ActiveView != CalendarView.Month)
        {
            cal.SelectedDate = day;
        }
        return true;
    }

    // ── ColorPicker helpers ───────────────────────────────────────────

    // The picker's arrangement and the point in its coordinates, from where layout put it.
    private ColorPickerGeometry? ColorPickerAt(ColorPicker cp, float x, float y, out Point local)
    {
        local = default;
        if (rootNode is null || !HitTester.TryGetAbsoluteBounds(rootNode, cp, out var bounds))
        {
            return null;
        }
        local = new Point(x - bounds.X, y - bounds.Y);
        return ColorPickerGeometry.Arrange(cp, bounds.Width);
    }

    private void PressColorPicker(ColorPicker cp, float x, float y)
    {
        if (ColorPickerAt(cp, x, y, out var p) is not { } g)
        {
            return;
        }
        cp.Sync();
        var (part, index) = g.HitTest(p);

        // A press elsewhere in the picker applies what was typed (or drops it if it is not a colour).
        if (part != ColorPickerPart.Value && cp.EditText is not null && !cp.CommitEdit())
        {
            cp.EditText = null;
        }

        switch (part)
        {
            case ColorPickerPart.ModeTab:
                cp.ModeIndex = index;
                break;
            case ColorPickerPart.Canvas or ColorPickerPart.HueBar or ColorPickerPart.Ring or ColorPickerPart.Triangle
                or ColorPickerPart.Channel or ColorPickerPart.Opacity:
                cp.DragPart = part;
                cp.DragIndex = index;
                ApplyColorPickerPoint(cp, g, p);
                break;
            case ColorPickerPart.Format:
                cp.FormatIndex = (cp.FormatIndex + 1) % cp.Formats.Count;
                cp.EditText = null;
                break;
            case ColorPickerPart.Value:
                cp.EditText ??= cp.ValueText;
                cp.EditSelectAll = true;
                break;
            case ColorPickerPart.EyeDropper:
                BeginEyeDropper(cp);
                break;
            case ColorPickerPart.Swatch when cp.SavedSwatches?.Value is { } saved && index < saved.Count:
                cp.SetColor(saved[index]);
                cp.NoteRecent();
                break;
            case ColorPickerPart.AddSwatch:
                cp.AddSwatch();
                break;
            case ColorPickerPart.Recent when cp.RecentShown is { } recent && index < recent.Count:
                cp.SetColor(recent[index]);
                break;
        }
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, cp);
        }
        RequestRepaint?.Invoke();
    }

    private void DragColorPicker(ColorPicker cp, float x, float y)
    {
        if (cp.DragPart != ColorPickerPart.None && ColorPickerAt(cp, x, y, out var p) is { } g)
        {
            ApplyColorPickerPoint(cp, g, p);
        }
    }

    // Sets the colour from a point on the part being dragged.
    private static void ApplyColorPickerPoint(ColorPicker cp, ColorPickerGeometry g, Point p)
    {
        static float Across(Rect r, float x) => r.Width <= 0f ? 0f : Math.Clamp((x - r.X) / r.Width, 0f, 1f);
        switch (cp.DragPart)
        {
            case ColorPickerPart.Canvas:
                cp.Set(saturation: Across(g.Canvas, p.X), brightness: 1f - Math.Clamp((p.Y - g.Canvas.Y) / g.Canvas.Height, 0f, 1f));
                break;
            case ColorPickerPart.HueBar:
                cp.Set(hue: Across(g.HueBar, p.X) * 360f);
                break;
            case ColorPickerPart.Ring:
            {
                float angle = MathF.Atan2(p.X - g.WheelCenter.X, -(p.Y - g.WheelCenter.Y)) * 180f / MathF.PI;
                cp.Set(hue: angle < 0f ? angle + 360f : angle);
                break;
            }
            case ColorPickerPart.Triangle:
            {
                var (s, b) = g.TriangleAt(p);
                cp.Set(saturation: s, brightness: b);
                break;
            }
            case ColorPickerPart.Channel when cp.DragIndex < g.Channels.Count:
            {
                var (r, gg, b) = ColorMath.HsbToRgb(cp.Hue, cp.Saturation, cp.Brightness);
                float[] channels = [r, gg, b];
                channels[cp.DragIndex] = Across(g.Channels[cp.DragIndex], p.X);
                var (h, s, v) = ColorMath.RgbToHsb(channels[0], channels[1], channels[2]);
                cp.Set(hue: s > 0.001f && v > 0.001f ? h : cp.Hue, saturation: v > 0.001f ? s : cp.Saturation, brightness: v);
                break;
            }
            case ColorPickerPart.Opacity:
                cp.Set(alpha: Across(g.Opacity, p.X));
                break;
        }
    }

    // The eye dropper: the next press anywhere (the mouse is captured, so outside the window too)
    // takes the colour of the screen under the pointer; Escape gives up.
    private void BeginEyeDropper(ColorPicker cp)
    {
        cp.IsPickingFromScreen = true;
        activeEyeDropper = cp;
        RequestPointerCapture?.Invoke(true);
        currentCursorKind = 5;
        RequestCursorChange?.Invoke(5);
    }

    private void FinishEyeDropper(ColorPicker cp, bool take)
    {
        activeEyeDropper = null;
        cp.IsPickingFromScreen = false;
        RequestPointerCapture?.Invoke(false);
        currentCursorKind = 0;
        RequestCursorChange?.Invoke(0);
        if (take && ScreenColor.AtPointer() is { } color)
        {
            cp.SetColor(color);
            cp.NoteRecent();
        }
        if (rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, cp);
        }
        RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Keys on a focused picker: typing edits the value (Enter applies, Escape drops it); otherwise
    /// Left/Right move saturation, Up/Down brightness (Shift: in tens), Page Up/Down the hue, and a
    /// hex digit or # starts typing a value.
    /// </summary>
    private static bool HandleColorPickerKey(ColorPicker cp, NativeKeyEvent evt)
    {
        cp.Sync();
        if (cp.EditText is { } text)
        {
            switch (evt.Key)
            {
                case Key.Enter:
                    cp.CommitEdit();
                    return true;
                case Key.Escape:
                    cp.EditText = null;
                    return true;
                case Key.Backspace when evt.Character is null:
                    return true; // the '\b' character that follows deletes
            }
            if (evt.Character is '\b')
            {
                cp.EditText = cp.EditSelectAll || text.Length == 0 ? string.Empty : text[..^1];
                cp.EditSelectAll = false;
                return true;
            }
            if (evt.Character is { } typed && !char.IsControl(typed))
            {
                cp.EditText = (cp.EditSelectAll ? string.Empty : text) + typed;
                cp.EditSelectAll = false;
                return true;
            }
            return evt.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End;
        }

        float step = evt.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1f : 0.01f;
        float hueStep = evt.Modifiers.HasFlag(ModifierKeys.Shift) ? 30f : 10f;
        switch (evt.Key)
        {
            case Key.Left:
                cp.Set(saturation: cp.Saturation - step);
                return true;
            case Key.Right:
                cp.Set(saturation: cp.Saturation + step);
                return true;
            case Key.Up:
                cp.Set(brightness: cp.Brightness + step);
                return true;
            case Key.Down:
                cp.Set(brightness: cp.Brightness - step);
                return true;
            case Key.PageUp:
                cp.Set(hue: (cp.Hue + hueStep) % 360f);
                return true;
            case Key.PageDown:
                cp.Set(hue: (cp.Hue - hueStep + 360f) % 360f);
                return true;
        }
        if (evt.Character is { } c && (Uri.IsHexDigit(c) || c == '#') && !evt.Modifiers.HasFlag(ModifierKeys.Ctrl))
        {
            cp.EditText = c.ToString();
            cp.EditSelectAll = false;
            return true;
        }
        return false;
    }

    // ── PinInput helpers ──────────────────────────────────────────────

    private void HandlePinInputClick(PinInput pin)
    {
        var abs = Live(pin, pin.AbsoluteBounds);
        if (abs.Width <= 0)
        {
            return;
        }

        // Determine which cell was clicked based on X position
        float mx = lastMousePosition.X;
        const float cellWidth = 40f;
        const float gap = 8f;
        const float separatorExtra = 12f;

        float x = abs.X;
        string value = pin.Value.Value ?? "";

        for (int i = 0; i < pin.Length; i++)
        {
            if (pin.SeparatorPositions.Contains(i) && i > 0)
            {
                x += separatorExtra;
            }

            float cellRight = x + cellWidth;
            if (mx >= x && mx <= cellRight)
            {
                // Clicked on cell i — set active cell to min(i, value.Length) so cursor
                // sits at the clicked position or end of entered text, whichever is less
                PinActiveCellIndex = Math.Min(i, value.Length);
                CaretResetTimestamp = Stopwatch.GetTimestamp();
                RequestRepaint?.Invoke();
                return;
            }

            x += cellWidth + gap;
        }

        // Clicked past all cells — set to end of entered text
        PinActiveCellIndex = Math.Min(value.Length, pin.Length - 1);
        CaretResetTimestamp = Stopwatch.GetTimestamp();
        RequestRepaint?.Invoke();
    }

    private bool HandlePinInputKey(PinInput pin, NativeKeyEvent evt)
    {
        // Use the dispatcher-owned buffer (survives re-render), not the stale
        // Bindable.Value which is frozen at the value from the last render.
        PinEditBuffer ??= pin.Value.Value ?? "";
        string value = PinEditBuffer;
        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // Character input (from WM_CHAR)
        if (evt.Character is char ch && ch >= ' ')
        {
            if (!pin.AcceptsCharacter(ch))
            {
                return true;
            }

            // Overwrite mode: replace character at active cell or append at end
            int insertAt = Math.Min(PinActiveCellIndex, value.Length);
            string newValue;
            if (insertAt >= value.Length)
            {
                // Append at the end (cursor past all filled cells)
                if (value.Length >= pin.Length)
                {
                    return true;
                }

                newValue = value + ch;
            }
            else
            {
                // Replace the character at the active cell position
                var chars = value.ToCharArray();
                chars[insertAt] = ch;
                newValue = new string(chars);
            }

            PinEditBuffer = newValue;
            pin.Value.OnChange(newValue);
            PinActiveCellIndex = Math.Min(insertAt + 1, pin.Length - 1);
            RequestRepaint?.Invoke();

            // Auto-submit when all cells are filled
            if (newValue.Length == pin.Length)
            {
                pin.AutoSubmitHandler?.Invoke(newValue);
            }

            return true;
        }

        // Backspace: clear current cell content and move cursor back
        if (evt.Character == '\b')
        {
            if (PinActiveCellIndex < value.Length)
            {
                // Current cell has content — clear it
                string newValue = value[..PinActiveCellIndex] + value[(PinActiveCellIndex + 1)..];
                PinEditBuffer = newValue;
                pin.Value.OnChange(newValue);
            }

            // Move cursor to previous cell (or stay at 0)
            PinActiveCellIndex = Math.Max(0, PinActiveCellIndex - 1);
            RequestRepaint?.Invoke();
            return true;
        }

        // Delete key — remove character at active cell
        if (evt.Key == Key.Delete)
        {
            if (PinActiveCellIndex < value.Length)
            {
                string newValue = value[..PinActiveCellIndex] + value[(PinActiveCellIndex + 1)..];
                PinEditBuffer = newValue;
                pin.Value.OnChange(newValue);
                RequestRepaint?.Invoke();
            }

            return true;
        }

        // Arrow keys to move between cells
        if (evt.Key == Key.Left && PinActiveCellIndex > 0)
        {
            PinActiveCellIndex--;
            RequestRepaint?.Invoke();
            return true;
        }

        if (evt.Key == Key.Right && PinActiveCellIndex < Math.Min(value.Length, pin.Length - 1))
        {
            PinActiveCellIndex++;
            RequestRepaint?.Invoke();
            return true;
        }

        return false;
    }

    // ── Select dropdown helpers ───────────────────────────────────────

    // ── ToolBar helpers ──────────────────────────────────────────────

    private int ComputeToolBarItemIndex(ToolBar tb, float mouseX, float mouseY)
    {
        var bounds = Live(tb, tb.AbsoluteBounds);
        return tb.ItemAt(mouseX - bounds.X, mouseY - bounds.Y, bounds.Width, bounds.Height);
    }

    private void HandleToolBarClick(ToolBar tb)
    {
        int idx = ComputeToolBarItemIndex(tb, lastMousePosition.X, lastMousePosition.Y);
        if (idx == ToolBar.OverflowIndex)
        {
            // The "more" button: the items that didn't fit, as a menu below it (beside it when vertical).
            var bounds = Live(tb, tb.AbsoluteBounds);
            var arrangement = tb.Arrange(bounds.Width, bounds.Height);
            if (arrangement.OverflowButton is { } more)
            {
                var anchor = new Rect(bounds.X + more.X, bounds.Y + more.Y, more.Width, more.Height);
                OpenMenu(tb.OverflowMenu(arrangement.Overflowed),
                    tb.IsVertical ? MenuPlacement.AtPoint(new Point(anchor.Right, anchor.Y)) : MenuPlacement.Below(anchor),
                    highlightFirst: LastInputWasKeyboard, owner: tb);
            }
            return;
        }
        if (idx < 0 || idx >= tb.Items.Count)
        {
            return;
        }

        var item = tb.Items[idx];
        if (item.IsSeparator || !item.Enabled)
        {
            return;
        }

        if (item.ToggleValue.OnChange is not null)
        {
            item.ToggleValue.OnChange(!item.ToggleValue.Value);
        }
        else
        {
            item.OnClick?.Invoke();
        }
    }

    private static int ComputeMenuBarLabelIndex(MenuBar mb, float mouseX, float mouseY)
    {
        var point = new Point(mouseX, mouseY);
        for (int i = 0; i < mb.MenuLabelBounds.Length; i++)
        {
            if (mb.MenuLabelBounds[i].Contains(point))
            {
                return i;
            }
        }
        return -1;
    }

    private void HandlePropertyGridClick(PropertyGrid pg)
    {
        var abs = Live(pg, pg.AbsoluteBounds);
        float relX = lastMousePosition.X - abs.X;
        float relY = lastMousePosition.Y - abs.Y;
        float groupHeaderH = pg.GroupHeaderHeight;
        float rowH = pg.RowHeight;
        float labelRatio = 0.4f;

        float y = 0f;
        int flatRow = 0;
        for (int gi = 0; gi < pg.Groups.Count; gi++)
        {
            var group = pg.Groups[gi];
            if (group.Visible != null && !group.Visible())
            {
                continue;
            }

            // Group header hit test
            if (relY >= y && relY < y + groupHeaderH)
            {
                CancelPropertyGridEdit();
                pg.ToggleGroup(gi);
                return;
            }
            y += groupHeaderH;

            if (pg.CollapsedGroups.Contains(gi))
            {
                continue;
            }

            // Property row hit test
            for (int pi = 0; pi < group.Properties.Count; pi++)
            {
                if (relY >= y && relY < y + rowH)
                {
                    var prop = group.Properties[pi];

                    // Bool toggle
                    if (prop.EditorKind == PropertyEditorKind.Bool && prop.Setter is Action<bool> setter)
                    {
                        CancelPropertyGridEdit();
                        var getter = (Func<bool>)prop.Getter!;
                        setter(!getter());
                    }
                    // Editable text/numeric — click on editor column starts inline edit
                    else if (!prop.IsReadOnly && relX > abs.Width * labelRatio
                        && prop.EditorKind is PropertyEditorKind.String
                            or PropertyEditorKind.Float
                            or PropertyEditorKind.Int)
                    {
                        string currentValue = prop.EditorKind switch
                        {
                            PropertyEditorKind.String => ((Func<string>)prop.Getter!).Invoke(),
                            PropertyEditorKind.Float => ((Func<float>)prop.Getter!).Invoke()
                                .ToString(prop.FormatString ?? "F1"),
                            PropertyEditorKind.Int => ((Func<int>)prop.Getter!).Invoke().ToString(),
                            _ => ""
                        };
                        BeginPropertyGridEdit(flatRow, prop, currentValue);
                        FocusManager.RequestFocus(pg);
                    }
                    else
                    {
                        CancelPropertyGridEdit();
                    }

                    return;
                }
                y += rowH;
                flatRow++;
            }
        }

        // Clicked outside all rows — cancel edit
        CancelPropertyGridEdit();
    }

    private int ComputePropertyGridRow(PropertyGrid pg, float mouseX, float mouseY)
    {
        var abs = Live(pg, pg.AbsoluteBounds);
        float relY = mouseY - abs.Y;
        float groupHeaderH = pg.GroupHeaderHeight;
        float rowH = pg.RowHeight;

        float y = 0f;
        int flatRow = 0;

        for (int gi = 0; gi < pg.Groups.Count; gi++)
        {
            var group = pg.Groups[gi];
            if (group.Visible != null && !group.Visible())
            {
                continue;
            }

            // Skip group header
            if (relY >= y && relY < y + groupHeaderH)
            {
                return -1;
            }
            y += groupHeaderH;

            if (pg.CollapsedGroups.Contains(gi))
            {
                continue;
            }

            for (int pi = 0; pi < group.Properties.Count; pi++)
            {
                if (relY >= y && relY < y + rowH)
                {
                    return flatRow;
                }
                y += rowH;
                flatRow++;
            }
        }

        return -1;
    }

    private bool HandlePropertyGridKey(PropertyGrid pg, NativeKeyEvent evt)
    {
        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // Enter — commit edit
        if (evt.Key == Key.Enter)
        {
            CommitPropertyGridEdit();
            RequestRepaint?.Invoke();
            return true;
        }

        // Character input — insert at caret
        if (evt.Character is char ch && ch >= ' ')
        {
            PropertyGridEditBuffer = PropertyGridEditBuffer.Insert(PropertyGridEditCaret, ch.ToString());
            PropertyGridEditCaret++;
            RequestRepaint?.Invoke();
            return true;
        }

        // Backspace
        if (evt.Character == '\b')
        {
            if (PropertyGridEditCaret > 0)
            {
                PropertyGridEditBuffer = PropertyGridEditBuffer.Remove(PropertyGridEditCaret - 1, 1);
                PropertyGridEditCaret--;
                RequestRepaint?.Invoke();
            }
            return true;
        }

        // Delete
        if (evt.Key == Key.Delete)
        {
            if (PropertyGridEditCaret < PropertyGridEditBuffer.Length)
            {
                PropertyGridEditBuffer = PropertyGridEditBuffer.Remove(PropertyGridEditCaret, 1);
                RequestRepaint?.Invoke();
            }
            return true;
        }

        // Arrow keys
        if (evt.Key == Key.Left)
        {
            if (PropertyGridEditCaret > 0)
            {
                PropertyGridEditCaret--;
                RequestRepaint?.Invoke();
            }
            return true;
        }

        if (evt.Key == Key.Right)
        {
            if (PropertyGridEditCaret < PropertyGridEditBuffer.Length)
            {
                PropertyGridEditCaret++;
                RequestRepaint?.Invoke();
            }
            return true;
        }

        if (evt.Key == Key.Home)
        {
            PropertyGridEditCaret = 0;
            RequestRepaint?.Invoke();
            return true;
        }

        if (evt.Key == Key.End)
        {
            PropertyGridEditCaret = PropertyGridEditBuffer.Length;
            RequestRepaint?.Invoke();
            return true;
        }

        // Ctrl+A — select all (move caret to end)
        if (evt.Modifiers.HasFlag(ModifierKeys.Ctrl) && evt.Key == Key.A)
        {
            PropertyGridEditCaret = PropertyGridEditBuffer.Length;
            return true;
        }

        return false;
    }

    private void HandleEmojiPickerClick(EmojiPicker ep)
    {
        var abs = Live(ep, ep.AbsoluteBounds);
        int hit = ep.HitTest(lastMousePosition.X - abs.X, lastMousePosition.Y - abs.Y, abs.Width, abs.Height);
        if (hit == EmojiPicker.ToneButtonHover)
        {
            ep.NextSkinTone();
        }
        else if (hit <= -100)
        {
            ep.SelectedCategoryIndex = -(hit + 100);
            ep.ScrollOffset = 0f;
        }
        else if (hit >= 0)
        {
            ep.Choose(ep.CurrentEmoji[hit]);
        }
    }

    private int ComputeEmojiPickerHover(EmojiPicker ep, float mouseX, float mouseY)
    {
        var abs = Live(ep, ep.AbsoluteBounds);
        return ep.HitTest(mouseX - abs.X, mouseY - abs.Y, abs.Width, abs.Height);
    }

    // ── Select dropdown helpers (continued) ──────────────────────────

    private void TrackOpenSelect(ISelectNode sel)
    {
        if (sel.IsOpen)
        {
            openSelect = sel;
        }
        else
        {
            openSelect = null;
        }
    }

    private void UpdateSelectDropdownHover(float x, float y)
    {
        if (openSelect == null)
        {
            return;
        }

        var dropdownBounds = openSelect.DropdownBounds;
        if (dropdownBounds.Width <= 0 || openSelect.OptionCount == 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (dropdownBounds.Contains(point))
        {
            float itemHeight = openSelect.DropdownItemHeight;
            if (itemHeight <= 0)
            {
                itemHeight = dropdownBounds.Height / Math.Max(1, openSelect.OptionCount);
            }

            int visibleIndex = (int)((y - dropdownBounds.Y) / itemHeight);
            int index = openSelect.ScrollOffset + visibleIndex;
            if (index >= 0 && index < openSelect.OptionCount)
            {
                if (openSelect.HighlightedIndex != index)
                {
                    openSelect.HighlightedIndex = index;
                    RequestRepaint?.Invoke();
                }

                return;
            }
        }

        // Mouse is not over any option
        if (openSelect.HighlightedIndex != -1)
        {
            openSelect.HighlightedIndex = -1;
            RequestRepaint?.Invoke();
        }
    }

    private void UpdateMultiSelectDropdownHover(float x, float y)
    {
        if (openMultiSelect == null)
        {
            return;
        }

        var dropdownBounds = openMultiSelect.DropdownBounds;
        if (dropdownBounds.Width <= 0 || openMultiSelect.OptionCount == 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (dropdownBounds.Contains(point))
        {
            float itemHeight = openMultiSelect.DropdownItemHeight;
            if (itemHeight <= 0)
            {
                itemHeight = dropdownBounds.Height / Math.Max(1, openMultiSelect.OptionCount);
            }

            int visibleIndex = (int)((y - dropdownBounds.Y) / itemHeight);
            int index = openMultiSelect.ScrollOffset + visibleIndex;
            if (index >= 0 && index < openMultiSelect.OptionCount)
            {
                if (openMultiSelect.HighlightedIndex != index)
                {
                    openMultiSelect.HighlightedIndex = index;
                    RequestRepaint?.Invoke();
                }

                return;
            }
        }

        if (openMultiSelect.HighlightedIndex != -1)
        {
            openMultiSelect.HighlightedIndex = -1;
            RequestRepaint?.Invoke();
        }
    }

    private void UpdateComboboxDropdownHover(float x, float y)
    {
        if (openCombobox == null)
        {
            return;
        }

        var dropdownBounds = openCombobox.DropdownBounds;
        if (dropdownBounds.Width <= 0 || openCombobox.FilteredOptionCount == 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (dropdownBounds.Contains(point))
        {
            float itemHeight = openCombobox.DropdownItemHeight;
            if (itemHeight <= 0)
            {
                itemHeight = dropdownBounds.Height / Math.Max(1, openCombobox.FilteredOptionCount);
            }

            int visibleIndex = (int)((y - dropdownBounds.Y) / itemHeight);
            int index = openCombobox.ScrollOffset + visibleIndex;
            if (index >= 0 && index < openCombobox.FilteredOptionCount)
            {
                if (openCombobox.HighlightedIndex != index)
                {
                    openCombobox.HighlightedIndex = index;
                    RequestRepaint?.Invoke();
                }

                return;
            }
        }

        if (openCombobox.HighlightedIndex != -1)
        {
            openCombobox.HighlightedIndex = -1;
            RequestRepaint?.Invoke();
        }
    }


    private void UpdateDataGridSelectDropdownHover(ITabularDataNode tdn, float x, float y)
    {
        var ddBounds = tdn.SelectDropdownBounds;
        if (ddBounds.Width <= 0)
        {
            return;
        }

        var options = tdn.GetSelectOptions(tdn.SelectDropdownCol);
        int count = options?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (ddBounds.Contains(point))
        {
            float itemHeight = ddBounds.Height / Math.Max(1, count);
            int index = (int)((y - ddBounds.Y) / itemHeight);
            if (index >= 0 && index < count && tdn.SelectDropdownHoverIndex != index)
            {
                tdn.SelectDropdownHoverIndex = index;
                RequestRepaint?.Invoke();
            }
            return;
        }

        if (tdn.SelectDropdownHoverIndex != -1)
        {
            tdn.SelectDropdownHoverIndex = -1;
            RequestRepaint?.Invoke();
        }
    }

    private void UpdateColumnChooserHover(ITabularDataNode tdn, float x, float y)
    {
        var ddBounds = tdn.ColumnChooserBounds;
        if (ddBounds.Width <= 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (ddBounds.Contains(point))
        {
            const float itemHeight = 28f;
            int index = (int)((y - ddBounds.Y) / itemHeight);
            if (index >= 0 && index < tdn.ColumnCount && tdn.ColumnChooserHoverIndex != index)
            {
                tdn.ColumnChooserHoverIndex = index;
                RequestRepaint?.Invoke();
            }
            return;
        }

        if (tdn.ColumnChooserHoverIndex != -1)
        {
            tdn.ColumnChooserHoverIndex = -1;
            RequestRepaint?.Invoke();
        }
    }

    // ── CommandPalette keyboard ─────────────────────────────────────────

    private void HandleCommandPaletteKeyboard(NativeKeyEvent evt)
    {
        var cp = CommandPalette.Instance!;

        switch (evt.Key)
        {
            case Key.Escape:
                CommandPalette.Close();
                cp.SearchText = "";
                RequestRepaint?.Invoke();
                return;

            case Key.Enter:
                cp.ExecuteHighlighted();
                RequestRepaint?.Invoke();
                return;

            case Key.Up:
                if (cp.HighlightedIndex > 0)
                {
                    cp.HighlightedIndex--;
                }
                RequestRepaint?.Invoke();
                return;

            case Key.Down:
                if (cp.HighlightedIndex < cp.FilteredCommands.Count - 1)
                {
                    cp.HighlightedIndex++;
                }
                RequestRepaint?.Invoke();
                return;

            case Key.Backspace:
                if (cp.SearchText.Length > 0)
                {
                    cp.SearchText = cp.SearchText[..^1];
                    cp.UpdateFilter();
                }
                RequestRepaint?.Invoke();
                return;

            default:
                // Printable characters
                if (evt.Character != null && !char.IsControl(evt.Character.Value))
                {
                    cp.SearchText += evt.Character.Value;
                    cp.UpdateFilter();
                    RequestRepaint?.Invoke();
                }
                return;
        }
    }

    private void HandleSelectKeyboard(NativeKeyEvent evt)
    {
        if (openSelect == null)
        {
            return;
        }

        // A searchable list: characters build the search (Space too, once it has begun), Backspace trims it.
        if (openSelect.IsSearchable && !evt.Modifiers.HasFlag(ModifierKeys.Ctrl))
        {
            if (evt.Character is { } typed && typed > ' ' && typed != (char)127
                || evt.Character == ' ' && openSelect.SearchText.Length > 0)
            {
                openSelect.SearchText += evt.Character!.Value;
                RequestRepaint?.Invoke();
                return;
            }
            if (evt.Key == Key.Backspace && openSelect.SearchText.Length > 0)
            {
                openSelect.SearchText = openSelect.SearchText[..^1];
                RequestRepaint?.Invoke();
                return;
            }
        }

        switch (evt.Key)
        {
            case Key.Down:
            {
                int next = openSelect.HighlightedIndex + 1;
                if (next >= openSelect.OptionCount)
                {
                    next = 0;
                }

                openSelect.HighlightedIndex = next;
                EnsureHighlightedVisible();
                RequestRepaint?.Invoke();
                break;
            }

            case Key.Up:
            {
                int prev = openSelect.HighlightedIndex - 1;
                if (prev < 0)
                {
                    prev = openSelect.OptionCount - 1;
                }

                openSelect.HighlightedIndex = prev;
                EnsureHighlightedVisible();
                RequestRepaint?.Invoke();
                break;
            }

            case Key.Enter:
            case Key.Space:
            {
                int idx = openSelect.HighlightedIndex;
                if (idx >= 0 && idx < openSelect.OptionCount)
                {
                    openSelect.SelectIndex(idx);
                }
                else
                {
                    openSelect.Close();
                }

                openSelect = null;
                RequestRepaint?.Invoke();
                break;
            }

            case Key.Escape:
            {
                openSelect.Close();
                openSelect = null;
                RequestRepaint?.Invoke();
                break;
            }
        }
    }

    /// <summary>
    /// Keys for an open MultiSelect, which had none: Up/Down move the highlight, Space or Enter ticks
    /// the highlighted row (Enter closes when nothing is highlighted), Escape closes; a searchable one
    /// takes typing and Backspace. Returns whether the key was used.
    /// </summary>
    private bool HandleMultiSelectKeyboard(IMultiSelectNode ms, NativeKeyEvent evt)
    {
        if (ms.IsSearchable && !evt.Modifiers.HasFlag(ModifierKeys.Ctrl))
        {
            if (evt.Character is { } typed && typed > ' ' && typed != (char)127
                || evt.Character == ' ' && ms.SearchText.Length > 0)
            {
                ms.SearchText += evt.Character!.Value;
                return true;
            }
            if (evt.Key == Key.Backspace && ms.SearchText.Length > 0)
            {
                ms.SearchText = ms.SearchText[..^1];
                return true;
            }
        }

        int count = ms.OptionCount;
        switch (evt.Key)
        {
            case Key.Down when count > 0:
                ms.HighlightedIndex = ms.HighlightedIndex + 1 >= count ? 0 : ms.HighlightedIndex + 1;
                return true;
            case Key.Up when count > 0:
                ms.HighlightedIndex = ms.HighlightedIndex - 1 < 0 ? count - 1 : ms.HighlightedIndex - 1;
                return true;
            case Key.Space or Key.Enter when ms.HighlightedIndex >= 0 && ms.HighlightedIndex < count:
                ms.ToggleItem(ms.HighlightedIndex);
                return true;
            case Key.Enter or Key.Escape:
                ms.Close();
                openMultiSelect = null;
                return true;
            default:
                return false;
        }
    }

    private void EnsureHighlightedVisible()
    {
        if (openSelect == null)
        {
            return;
        }

        int highlighted = openSelect.HighlightedIndex;
        if (highlighted < 0)
        {
            return;
        }

        float itemHeight = openSelect.DropdownItemHeight;
        if (itemHeight <= 0)
        {
            return;
        }

        var dropdownBounds = openSelect.DropdownBounds;
        int visibleCount = Math.Max(1, (int)(dropdownBounds.Height / itemHeight));
        int scrollOffset = openSelect.ScrollOffset;

        if (highlighted < scrollOffset)
        {
            openSelect.ScrollOffset = highlighted;
        }
        else if (highlighted >= scrollOffset + visibleCount)
        {
            openSelect.ScrollOffset = highlighted - visibleCount + 1;
        }
    }

    private void HandleComboboxKeyboard(NativeKeyEvent evt)
    {
        if (openCombobox == null)
        {
            return;
        }

        switch (evt.Key)
        {
            case Key.Down:
            {
                int count = openCombobox.FilteredOptionCount;
                if (count > 0)
                {
                    int next = openCombobox.HighlightedIndex + 1;
                    if (next >= count)
                    {
                        next = 0;
                    }

                    openCombobox.HighlightedIndex = next;
                }

                RequestRepaint?.Invoke();
                break;
            }

            case Key.Up:
            {
                int count = openCombobox.FilteredOptionCount;
                if (count > 0)
                {
                    int prev = openCombobox.HighlightedIndex - 1;
                    if (prev < 0)
                    {
                        prev = count - 1;
                    }

                    openCombobox.HighlightedIndex = prev;
                }

                RequestRepaint?.Invoke();
                break;
            }

            case Key.Enter:
            {
                int idx = openCombobox.HighlightedIndex;
                if (idx >= 0 && idx < openCombobox.FilteredOptionCount)
                {
                    openCombobox.SelectFilteredIndex(idx);
                }
                else
                {
                    openCombobox.CommitText();
                }

                openCombobox = null;
                RequestRepaint?.Invoke();
                break;
            }

            case Key.Escape:
            {
                openCombobox.Close();
                openCombobox = null;
                RequestRepaint?.Invoke();
                break;
            }

            case Key.Backspace:
            {
                string text = openCombobox.SearchText;
                if (text.Length > 0)
                {
                    openCombobox.SearchText = text[..^1];
                    openCombobox.HighlightedIndex = -1;
                    openCombobox.ScrollOffset = 0;
                }

                RequestRepaint?.Invoke();
                break;
            }

            default:
            {
                // Append the typed character (the platform's, so any keyboard layout and accented or
                // non-Latin letters work; a US-only key table used to stand in for it).
                if (evt.Character is { } ch && ch >= ' ' && ch != (char)127 && !evt.Modifiers.HasFlag(ModifierKeys.Ctrl))
                {
                    openCombobox.SearchText += ch;
                    openCombobox.HighlightedIndex = -1;
                    openCombobox.ScrollOffset = 0;
                    RequestRepaint?.Invoke();
                }

                break;
            }
        }
    }

    private static void InvokePointerEnter(Node node)
    {
        InvokeGesture(node, g => g.PointerEnter);
    }

    private static void InvokePointerLeave(Node node)
    {
        InvokeGesture(node, g => g.PointerLeave);
    }

    private static void InvokeGesture(Node node, Func<GestureNodeData, Action?> selector)
    {
        var gestureData = node.LayoutData.GestureData;
        if (gestureData == null)
        {
            return;
        }

        var handler = selector(gestureData);
        handler?.Invoke();
    }

    private static void InvokeGesture<T>(Node node, Func<GestureNodeData, Action<T>?> selector, T arg)
    {
        var gestureData = node.LayoutData.GestureData;
        if (gestureData == null)
        {
            return;
        }

        var handler = selector(gestureData);
        handler?.Invoke(arg);
    }

    private static Node? FindFocusableNode(Node node)
    {
        // Check if this node itself is focusable
        var focusData = node.LayoutData.FocusData;
        if (focusData != null && focusData.IsFocusable)
        {
            return node;
        }

        // Known focusable control types
        if (IsFocusableControl(node)
            && node is not HotkeyPicker { IsDisabled: true } and not TabBar { IsDisabled: true })
        {
            return node;
        }

        return null;
    }

    /// <summary>
    /// The control types that take keyboard focus, by a click or by Tab: everything here answers
    /// the keyboard once focused. Shared by <see cref="FindFocusableNode"/> and <see cref="IsTabStop"/>
    /// so the two cannot drift apart (they had: a Select, ToggleGroup or Rating could never be focused).
    /// </summary>
    private static bool IsFocusableControl(Node node) =>
        node is Button or LinkButton or IconButton
            or TextInput or TextArea or PasswordInput or PinInput
            or MentionInput or TagInput or ColorPicker or Calendar or TimePicker or NavItem { Enabled: true } or IRadioButton or RangeSlider or DatePicker
            or DateRangePicker or DateTimePicker or MonthPicker
            or Checkbox or Toggle or Slider or HotkeyPicker or TabBar
            or ISelectNode or IMultiSelectNode or IComboboxNode
            or IToggleGroup or ISegmentedControl
            or Rating { BoundValue: not null }
        || IsNumberInput(node);

    /// <summary>
    /// Computes and applies a new slider value based on the current drag X position.
    /// Uses the delta from the drag-start X and the slider's rendered width to determine
    /// how far along the track the pointer is, then maps that to the value range.
    /// </summary>
    private void UpdateSliderFromDrag(Slider slider, float currentX, float currentY)
    {
        // Along the track: rightwards, or upwards on a vertical slider (its minimum is at the bottom).
        bool vertical = slider.OrientationValue == Orientation.Vertical;
        var theme = ThemeSwitcher.Current.Slider;
        float thumbAlong = vertical ? theme.ThumbHeight : theme.ThumbWidth;
        var bounds = slider.LayoutData.Bounds;
        float trackLength = Math.Max(1f, (vertical ? bounds.Height : bounds.Width) - thumbAlong);
        float delta = vertical ? sliderDragStartY - currentY : currentX - sliderDragStartX;

        float newValue = slider.Snap(sliderDragStartValue + delta * (slider.Max - slider.Min) / trackLength);
        if (newValue != slider.Bind.Value)
        {
            slider.Bind.OnChange(newValue);
        }
    }

    /// <summary>
    /// Arrow keys move a focused slider a step (Up/Right raise it), Page Up/Down a tenth of the
    /// range, Home/End to its ends.
    /// </summary>
    private static bool HandleSliderKey(Slider slider, NativeKeyEvent evt)
    {
        float range = slider.Max - slider.Min;
        float? target = evt.Key switch
        {
            Key.Right or Key.Up => slider.Bind.Value + slider.KeyboardStep,
            Key.Left or Key.Down => slider.Bind.Value - slider.KeyboardStep,
            Key.PageUp => slider.Bind.Value + Math.Max(slider.KeyboardStep, range / 10f),
            Key.PageDown => slider.Bind.Value - Math.Max(slider.KeyboardStep, range / 10f),
            Key.Home => slider.Min,
            Key.End => slider.Max,
            _ => null,
        };
        if (target is not { } next)
        {
            return false;
        }
        next = slider.Snap(next);
        if (next != slider.Bind.Value)
        {
            slider.Bind.OnChange(next);
        }
        return true;
    }

    /// <summary>
    /// Keys on a focused range slider: Tab goes from the low thumb to the high one (Shift+Tab back)
    /// before leaving the control; the arrows step the focused thumb, Page Up/Down by a tenth,
    /// Home and End to its limit. A thumb never crosses the other.
    /// </summary>
    private static bool HandleRangeSliderKey(RangeSlider rs, NativeKeyEvent evt)
    {
        bool shift = evt.Modifiers.HasFlag(ModifierKeys.Shift);
        if (evt.Key == Key.Tab && (evt.Modifiers & ~ModifierKeys.Shift) == ModifierKeys.None)
        {
            if (!shift && rs.FocusedThumb == 0)
            {
                rs.FocusedThumb = 1;
                return true;
            }
            if (shift && rs.FocusedThumb == 1)
            {
                rs.FocusedThumb = 0;
                return true;
            }
            return false;
        }
        if (evt.Modifiers != ModifierKeys.None || rs.IsReadOnly)
        {
            return false;
        }
        int thumb = rs.FocusedThumb;
        float value = thumb == 0 ? rs.MinBind.Value : rs.MaxBind.Value;
        float page = Math.Max(rs.KeyboardStep, (rs.Max - rs.Min) / 10f);
        float? target = evt.Key switch
        {
            Key.Right or Key.Up => value + rs.KeyboardStep,
            Key.Left or Key.Down => value - rs.KeyboardStep,
            Key.PageUp => value + page,
            Key.PageDown => value - page,
            Key.Home => rs.Min,
            Key.End => rs.Max,
            _ => null,
        };
        if (target is not { } next)
        {
            return false;
        }
        rs.MoveThumb(thumb, next);
        return true;
    }

    private void UpdateRangeSliderFromDrag(RangeSlider rs, float currentX)
    {
        float deltaX = currentX - sliderDragStartX;
        const float thumbWidthEstimate = 20f;
        float trackWidth = Math.Max(1f, rs.LayoutData.Bounds.Width - thumbWidthEstimate);
        if (trackWidth <= 0)
        {
            return;
        }

        float range = rs.Max - rs.Min;
        float valueDelta = deltaX * range / trackWidth;
        float newValue = rangeSliderDragStartValue + valueDelta;

        // Snap to step if specified
        if (rs.Step.HasValue && rs.Step.Value > 0)
        {
            float step = rs.Step.Value;
            newValue = MathF.Round(newValue / step) * step;
        }

        if (rangeSliderDragIsMax)
        {
            // Max thumb: clamp between current min and track max
            newValue = Math.Clamp(newValue, rs.MinBind.Value, rs.Max);
            rs.MaxBind.OnChange(newValue);
        }
        else
        {
            // Min thumb: clamp between track min and current max
            newValue = Math.Clamp(newValue, rs.Min, rs.MaxBind.Value);
            rs.MinBind.OnChange(newValue);
        }
    }

    // Raises a pointer gesture with Position relative to the node's window position (layout bounds are
    // parent-relative, so they are resolved through the tree — only when the node has the handler) and
    // Delta since the previous pointer event.
    private void InvokePointerGesture(Node node, Func<GestureNodeData, Action<PointerEventArgs>?> selector, NativeMouseEvent evt)
    {
        if (node.LayoutData.GestureData is not { } gestures || selector(gestures) is not { } handler)
        {
            return;
        }

        Rect origin = rootNode is not null && HitTester.TryGetAbsoluteBounds(rootNode, node, out Rect absolute)
            ? absolute
            : node.LayoutData.Bounds;
        handler(new PointerEventArgs
        {
            Position = new Point(evt.X - origin.X, evt.Y - origin.Y),
            Delta = new Point(evt.X - previousMousePosition.X, evt.Y - previousMousePosition.Y),
            Button = evt.Type == NativeMouseEventType.MouseMove ? NativeMouseButton.None : evt.Button,
        });
    }

    private static bool IsNumberInput(Node node)
    {
        var type = node.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NumberInput<>);
    }

    // ── DateRangePicker helpers ───────────────────────────────────────

    private void HandleDateRangePopupClick(DateRangePicker drp, float x, float y)
    {
        var clickPoint = new Point(x, y);

        // Check preset clicks
        if (drp.PresetBounds.Length > 0 && drp.PresetRanges is { Count: > 0 })
        {
            for (int i = 0; i < drp.PresetBounds.Length; i++)
            {
                if (drp.PresetBounds[i].Contains(clickPoint))
                {
                    drp.ApplyPreset(drp.PresetRanges[i]);
                    openDateRangePicker = null;
                    RequestRepaint?.Invoke();
                    return;
                }
            }
        }

        // Check nav arrows
        if (drp.PrevMonthBounds.Width > 0 && drp.PrevMonthBounds.Contains(clickPoint))
        {
            drp.NavigateMonth(-1);
            RequestRepaint?.Invoke();
            return;
        }

        if (drp.NextMonthBounds.Width > 0 && drp.NextMonthBounds.Contains(clickPoint))
        {
            drp.NavigateMonth(1);
            RequestRepaint?.Invoke();
            return;
        }

        // Check day click in left calendar
        float cellSize = drp.CalendarCellSize;
        if (cellSize > 0 && y >= drp.CalendarGridTop)
        {
            // Try left calendar
            if (x >= drp.LeftGridLeft && x < drp.LeftGridLeft + cellSize * 7)
            {
                int col = (int)((x - drp.LeftGridLeft) / cellSize);
                int row = (int)((y - drp.CalendarGridTop) / cellSize);
                if (col >= 0 && col < 7 && row >= 0 && row < 6)
                {
                    int cellIndex = row * 7 + col;
                    var date = drp.LeftGridStartDate.AddDays(cellIndex);
                    TrySelectDateRangeDay(drp, date, drp.DisplayedMonth, drp.DisplayedYear);
                    return;
                }
            }

            // Try right calendar
            if (x >= drp.RightGridLeft && x < drp.RightGridLeft + cellSize * 7)
            {
                int col = (int)((x - drp.RightGridLeft) / cellSize);
                int row = (int)((y - drp.CalendarGridTop) / cellSize);
                if (col >= 0 && col < 7 && row >= 0 && row < 6)
                {
                    int cellIndex = row * 7 + col;
                    var date = drp.RightGridStartDate.AddDays(cellIndex);
                    var (rightMonth, rightYear) = drp.RightMonth();
                    TrySelectDateRangeDay(drp, date, rightMonth, rightYear);
                    return;
                }
            }
        }
    }

    private void TrySelectDateRangeDay(DateRangePicker drp, DateOnly date, int month, int year)
    {
        // Only allow selection of dates in the displayed month
        if (date.Month != month || date.Year != year)
        {
            return;
        }

        // Check min/max constraints
        if (drp.Min.HasValue && date < drp.Min.Value)
        {
            return;
        }

        if (drp.Max.HasValue && date > drp.Max.Value)
        {
            return;
        }

        drp.SelectDay(date);
        if (!drp.IsCalendarOpen)
        {
            openDateRangePicker = null;
        }

        RequestRepaint?.Invoke();
    }

    private void UpdateDateRangeHover(DateRangePicker drp, float x, float y)
    {
        var calBounds = drp.CalendarBounds;
        if (calBounds.Width <= 0)
        {
            return;
        }

        int newLeftIndex = -1;
        int newRightIndex = -1;
        DateOnly? newHoverDate = null;

        float cellSize = drp.CalendarCellSize;
        if (cellSize > 0 && y >= drp.CalendarGridTop)
        {
            // Left calendar
            if (x >= drp.LeftGridLeft && x < drp.LeftGridLeft + cellSize * 7)
            {
                int col = (int)((x - drp.LeftGridLeft) / cellSize);
                int row = (int)((y - drp.CalendarGridTop) / cellSize);
                if (col >= 0 && col < 7 && row >= 0 && row < 6)
                {
                    newLeftIndex = row * 7 + col;
                    var date = drp.LeftGridStartDate.AddDays(newLeftIndex);
                    if (date.Month == drp.DisplayedMonth && date.Year == drp.DisplayedYear)
                    {
                        bool isDisabled = (drp.Min.HasValue && date < drp.Min.Value)
                            || (drp.Max.HasValue && date > drp.Max.Value);
                        if (!isDisabled)
                        {
                            newHoverDate = date;
                        }
                    }
                }
            }

            // Right calendar
            if (x >= drp.RightGridLeft && x < drp.RightGridLeft + cellSize * 7)
            {
                int col = (int)((x - drp.RightGridLeft) / cellSize);
                int row = (int)((y - drp.CalendarGridTop) / cellSize);
                if (col >= 0 && col < 7 && row >= 0 && row < 6)
                {
                    newRightIndex = row * 7 + col;
                    var date = drp.RightGridStartDate.AddDays(newRightIndex);
                    var (rightMonth, rightYear) = drp.RightMonth();
                    if (date.Month == rightMonth && date.Year == rightYear)
                    {
                        bool isDisabled = (drp.Min.HasValue && date < drp.Min.Value)
                            || (drp.Max.HasValue && date > drp.Max.Value);
                        if (!isDisabled)
                        {
                            newHoverDate = date;
                        }
                    }
                }
            }
        }

        // Check presets hover
        int newPresetIndex = -1;
        if (drp.PresetBounds.Length > 0)
        {
            var point = new Point(x, y);
            for (int i = 0; i < drp.PresetBounds.Length; i++)
            {
                if (drp.PresetBounds[i].Contains(point))
                {
                    newPresetIndex = i;
                    break;
                }
            }
        }

        bool changed = newLeftIndex != drp.HighlightedDayLeft
            || newRightIndex != drp.HighlightedDayRight
            || newHoverDate != drp.HoverDate
            || newPresetIndex != drp.HighlightedPreset;

        if (changed)
        {
            drp.HighlightedDayLeft = newLeftIndex;
            drp.HighlightedDayRight = newRightIndex;
            drp.HoverDate = newHoverDate;
            drp.HighlightedPreset = newPresetIndex;
            RequestRepaint?.Invoke();
        }
    }

    // ── DateTimePicker popup helpers ─────────────────────────────────

    private static void HandleDateTimePopupClick(DateTimePicker dtp, float x, float y)
    {
        var clickPoint = new Point(x, y);

        // Check prev/next month arrows
        if (dtp.PrevMonthBounds.Width > 0 && dtp.PrevMonthBounds.Contains(clickPoint))
        {
            dtp.NavigateMonth(-1);
            return;
        }

        if (dtp.NextMonthBounds.Width > 0 && dtp.NextMonthBounds.Contains(clickPoint))
        {
            dtp.NavigateMonth(1);
            return;
        }

        // Time spinner controls
        if (dtp.HourUpBounds.Width > 0 && dtp.HourUpBounds.Contains(clickPoint))
        {
            dtp.AdjustHour(1);
            return;
        }

        if (dtp.HourDownBounds.Width > 0 && dtp.HourDownBounds.Contains(clickPoint))
        {
            dtp.AdjustHour(-1);
            return;
        }

        if (dtp.MinuteUpBounds.Width > 0 && dtp.MinuteUpBounds.Contains(clickPoint))
        {
            dtp.AdjustMinute(1);
            return;
        }

        if (dtp.MinuteDownBounds.Width > 0 && dtp.MinuteDownBounds.Contains(clickPoint))
        {
            dtp.AdjustMinute(-1);
            return;
        }

        if (dtp.TimeFormatValue == TimeFormat.Hour12 && dtp.AmPmBounds.Width > 0 && dtp.AmPmBounds.Contains(clickPoint))
        {
            dtp.ToggleAmPm();
            return;
        }

        // Check calendar day grid click
        float cellSize = dtp.CalendarCellSize;
        if (cellSize > 0 && y >= dtp.CalendarGridTop)
        {
            int col = (int)((x - dtp.CalendarGridLeft) / cellSize);
            int row = (int)((y - dtp.CalendarGridTop) / cellSize);
            if (col >= 0 && col < 7 && row >= 0 && row < 6)
            {
                var date = dtp.CalendarGridStartDate.AddDays(row * 7 + col);
                if (date.Month == dtp.DisplayedMonth && date.Year == dtp.DisplayedYear
                    && !(dtp.MinDate.HasValue && date < dtp.MinDate.Value)
                    && !(dtp.MaxDate.HasValue && date > dtp.MaxDate.Value)
                    && !(dtp.DisabledDatesPredicate?.Invoke(date) == true))
                {
                    dtp.SelectDate(date);
                }
            }
        }
    }

    private void UpdateDateTimePickerHover(DateTimePicker dtp, float x, float y)
    {
        int newHighlighted = -1;
        float cellSize = dtp.CalendarCellSize;

        if (cellSize > 0 && y >= dtp.CalendarGridTop)
        {
            int col = (int)((x - dtp.CalendarGridLeft) / cellSize);
            int row = (int)((y - dtp.CalendarGridTop) / cellSize);

            if (col >= 0 && col < 7 && row >= 0 && row < 6)
            {
                int cellIndex = row * 7 + col;
                var date = dtp.CalendarGridStartDate.AddDays(cellIndex);

                bool isCurrentMonth = date.Month == dtp.DisplayedMonth && date.Year == dtp.DisplayedYear;
                bool isDisabled = false;
                if (dtp.MinDate.HasValue && date < dtp.MinDate.Value)
                {
                    isDisabled = true;
                }
                if (dtp.MaxDate.HasValue && date > dtp.MaxDate.Value)
                {
                    isDisabled = true;
                }
                if (dtp.DisabledDatesPredicate?.Invoke(date) == true)
                {
                    isDisabled = true;
                }

                if (isCurrentMonth && !isDisabled)
                {
                    newHighlighted = cellIndex;
                }
            }
        }

        if (newHighlighted != dtp.HighlightedDay)
        {
            dtp.HighlightedDay = newHighlighted;
            RequestRepaint?.Invoke();
        }
    }

    // ── TimePicker popup handling ─────────────────────────────────────

    /// <summary>
    /// A press in the popup: a wheel row turns its wheel to it; on the clock, the digits choose
    /// which part to set, AM/PM sets the half, and the dial sets the hour (then moves on to the
    /// minute) or the minute.
    /// </summary>
    private static void HandleTimePickerPopupClick(TimePicker tp, float x, float y)
    {
        var p = TimePickerPopup.Arrange(tp, tp.PopupBounds);
        var point = new Point(x, y);
        var (part, column, offset) = p.HitTest(point);
        switch (part)
        {
            case TimePickerPopupPart.WheelRow:
                tp.ActiveColumn = column;
                tp.Spin(column, offset);
                break;
            case TimePickerPopupPart.Header:
                tp.ChoosingMinute = column == 1;
                tp.ActiveColumn = column;
                break;
            case TimePickerPopupPart.AmPm when (column == 1) != tp.IsPm:
                tp.ToggleAmPm();
                break;
            case TimePickerPopupPart.Dial when !tp.ChoosingMinute:
                tp.SetHourFromDial((int)MathF.Round(p.TurnsAt(point) * 12f), tp.Format == TimeFormat.Hour24 && p.IsInner(point));
                tp.ChoosingMinute = true;
                tp.ActiveColumn = 1;
                break;
            case TimePickerPopupPart.Dial:
                tp.SetMinuteFromTurns(p.TurnsAt(point));
                break;
        }
    }

    // The wheel over a wheel column turns it a row at a time.
    private bool ScrollTimePickerPopup(NativeScrollEvent evt)
    {
        if (openTimePicker is not { PopupDisplayStyle: not TimePickerPopupStyle.Grid } tp || evt.DeltaY == 0f)
        {
            return false;
        }
        var p = TimePickerPopup.Arrange(tp, tp.PopupBounds);
        var (part, column, _) = p.HitTest(new Point(evt.X, evt.Y));
        if (part != TimePickerPopupPart.WheelRow)
        {
            return tp.PopupBounds.Contains(new Point(evt.X, evt.Y));
        }
        tp.ActiveColumn = column;
        tp.Spin(column, evt.DeltaY > 0 ? -1 : 1);
        RequestRepaint?.Invoke();
        return true;
    }

    /// <summary>
    /// Keys on a focused time picker. Typed (Input style): characters edit, Enter applies, Escape
    /// gives up, Up/Down step the minutes (Shift: hours). Popup styles: Enter, Space or Down opens;
    /// open, Left/Right choose the wheel (or clock part), Up/Down turn it, Enter keeps, Escape cancels.
    /// </summary>
    private bool HandleTimePickerKey(TimePicker tp, NativeKeyEvent evt)
    {
        if (tp.PopupDisplayStyle == TimePickerPopupStyle.Input)
        {
            if (tp.InputText is { } text)
            {
                switch (evt.Key)
                {
                    case Key.Enter:
                        tp.CommitInput();
                        return true;
                    case Key.Escape:
                        tp.InputText = null;
                        return true;
                    case Key.Backspace when evt.Character is null:
                        return true;
                }
                if (evt.Character is '\b')
                {
                    tp.InputText = text.Length == 0 ? text : text[..^1];
                    return true;
                }
                if (evt.Character is { } typed && !char.IsControl(typed))
                {
                    tp.InputText = text + typed;
                    return true;
                }
            }
            else if (evt.Character is { } first && (char.IsDigit(first)))
            {
                tp.InputText = first.ToString();
                return true;
            }
            if (evt.Key is Key.Up or Key.Down)
            {
                var now = tp.Value.Value ?? new TimeOnly(tp.SelectedHour, tp.SelectedMinute);
                int sign = evt.Key == Key.Up ? 1 : -1;
                var next = evt.Modifiers.HasFlag(ModifierKeys.Shift) ? now.AddHours(sign) : now.AddMinutes(sign * tp.MinuteStep);
                tp.InputText = null;
                tp.SelectedHour = next.Hour;
                tp.SelectedMinute = next.Minute;
                tp.IsPm = next.Hour >= 12;
                tp.Value.OnChange?.Invoke(next);
                return true;
            }
            return false;
        }

        if (!tp.IsPopupOpen)
        {
            if (evt.Key is Key.Enter or Key.Space or Key.Down)
            {
                tp.OpenPopup();
                openTimePicker = tp;
                return true;
            }
            return false;
        }

        bool clock = tp.PopupDisplayStyle == TimePickerPopupStyle.Grid;
        int columns = clock ? 2 : tp.Format == TimeFormat.Hour12 ? 3 : 2;
        switch (evt.Key)
        {
            case Key.Left:
                tp.ActiveColumn = Math.Max(0, tp.ActiveColumn - 1);
                tp.ChoosingMinute = clock && tp.ActiveColumn == 1;
                return true;
            case Key.Right:
                tp.ActiveColumn = Math.Min(columns - 1, tp.ActiveColumn + 1);
                tp.ChoosingMinute = clock && tp.ActiveColumn == 1;
                return true;
            case Key.Up:
                tp.Spin(tp.ActiveColumn, 1);
                return true;
            case Key.Down:
                tp.Spin(tp.ActiveColumn, -1);
                return true;
            case Key.Enter:
                tp.ClosePopup();
                openTimePicker = null;
                return true;
            case Key.Escape:
                tp.CancelPopup();
                openTimePicker = null;
                return true;
        }
        return false;
    }


    // ── MonthPicker popup handling ────────────────────────────────────

    private void HandleMonthPickerPopupClick(MonthPicker mp, float x, float y)
    {
        var clickPoint = new Point(x, y);

        // Check prev-year arrow
        if (mp.PrevYearBounds.Width > 0 && mp.PrevYearBounds.Contains(clickPoint))
        {
            mp.NavigateYear(-1);
            return;
        }

        // Check next-year arrow
        if (mp.NextYearBounds.Width > 0 && mp.NextYearBounds.Contains(clickPoint))
        {
            mp.NavigateYear(1);
            return;
        }

        // Check month grid cells
        if (y < mp.GridTop || mp.CellWidth <= 0 || mp.CellHeight <= 0)
        {
            return;
        }

        int col = (int)((x - mp.GridLeft) / mp.CellWidth);
        int row = (int)((y - mp.GridTop) / mp.CellHeight);

        if (col < 0 || col >= 4 || row < 0 || row >= 3)
        {
            return;
        }

        int monthIndex = row * 4 + col;
        if (monthIndex >= 0 && monthIndex < 12)
        {
            mp.SelectMonth(monthIndex + 1);
            openMonthPicker = null;
        }
    }

    private void UpdateMonthPickerHover(MonthPicker mp, float x, float y)
    {
        int newHighlighted = -1;

        if (mp.CellWidth > 0 && mp.CellHeight > 0 && y >= mp.GridTop)
        {
            int col = (int)((x - mp.GridLeft) / mp.CellWidth);
            int row = (int)((y - mp.GridTop) / mp.CellHeight);

            if (col >= 0 && col < 4 && row >= 0 && row < 3)
            {
                newHighlighted = row * 4 + col;
            }
        }

        if (newHighlighted != mp.HighlightedMonth)
        {
            mp.HighlightedMonth = newHighlighted;
            RequestRepaint?.Invoke();
        }
    }

    // ── TagInput key handling ─────────────────────────────────────────

    private bool HandleTagInputKey(TagInput tagInput, NativeKeyEvent evt)
    {
        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // Enter — commit current buffer as tag
        if (evt.Key == Key.Enter)
        {
            if (tagInput.Delimiter is TagDelimiter.Enter or TagDelimiter.EnterAndComma)
            {
                tagInput.AddTag(tagInput.InputBuffer);
                RequestRepaint?.Invoke();
                return true;
            }
        }

        // Tab — commit if Tab delimiter
        if (evt.Key == Key.Tab)
        {
            if (tagInput.Delimiter == TagDelimiter.Tab && !string.IsNullOrWhiteSpace(tagInput.InputBuffer))
            {
                tagInput.AddTag(tagInput.InputBuffer);
                RequestRepaint?.Invoke();
                return true;
            }
            return false;
        }

        // Comma character — commit if Comma delimiter
        if (evt.Character == ',')
        {
            if (tagInput.Delimiter is TagDelimiter.Comma or TagDelimiter.EnterAndComma)
            {
                tagInput.AddTag(tagInput.InputBuffer);
                RequestRepaint?.Invoke();
                return true;
            }
        }

        // Backspace — delete char from buffer, or remove last tag if buffer empty
        if (evt.Character == '\b') // Windows sends the Backspace key, then this character: act once
        {
            if (tagInput.InputBuffer.Length > 0 && tagInput.CaretIndex > 0)
            {
                tagInput.InputBuffer = tagInput.InputBuffer.Remove(tagInput.CaretIndex - 1, 1);
                tagInput.CaretIndex--;
            }
            else if (tagInput.InputBuffer.Length == 0 && tagInput.CurrentTags.Count > 0)
            {
                tagInput.RemoveTagAt(tagInput.CurrentTags.Count - 1);
            }
            RequestRepaint?.Invoke();
            return true;
        }

        // Delete key
        if (evt.Key == Key.Delete)
        {
            if (tagInput.CaretIndex < tagInput.InputBuffer.Length)
            {
                tagInput.InputBuffer = tagInput.InputBuffer.Remove(tagInput.CaretIndex, 1);
                RequestRepaint?.Invoke();
            }
            return true;
        }

        // Arrow keys
        if (evt.Key == Key.Left && tagInput.CaretIndex > 0)
        {
            tagInput.CaretIndex--;
            return true;
        }
        if (evt.Key == Key.Right && tagInput.CaretIndex < tagInput.InputBuffer.Length)
        {
            tagInput.CaretIndex++;
            return true;
        }
        if (evt.Key == Key.Home)
        {
            tagInput.CaretIndex = 0;
            return true;
        }
        if (evt.Key == Key.End)
        {
            tagInput.CaretIndex = tagInput.InputBuffer.Length;
            return true;
        }

        // Printable character input
        if (evt.Character is char ch && ch >= ' ')
        {
            tagInput.InputBuffer = tagInput.InputBuffer.Insert(tagInput.CaretIndex, ch.ToString());
            tagInput.CaretIndex++;
            RequestRepaint?.Invoke();
            return true;
        }

        return false;
    }

    // ── MentionInput key handling ────────────────────────────────────

    private bool HandleMentionInputKey(MentionInput mi, NativeKeyEvent evt)
    {
        MentionEditBuffer ??= mi.Value.Value ?? string.Empty;
        ActiveEditBuffer = MentionEditBuffer;
        MentionInputCaretIndex = Math.Clamp(MentionInputCaretIndex, 0, MentionEditBuffer.Length);
        MentionInputSelectionAnchor = Math.Clamp(MentionInputSelectionAnchor, 0, MentionEditBuffer.Length);
        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // When popup is open, intercept navigation keys
        if (mi.IsPopupOpen && mi.Suggestions.Count > 0)
        {
            if (evt.Key == Key.Down)
            {
                mi.HighlightedIndex = (mi.HighlightedIndex + 1) % mi.Suggestions.Count;
                RequestRepaint?.Invoke();
                return true;
            }
            if (evt.Key == Key.Up)
            {
                mi.HighlightedIndex = mi.HighlightedIndex <= 0
                    ? mi.Suggestions.Count - 1
                    : mi.HighlightedIndex - 1;
                RequestRepaint?.Invoke();
                return true;
            }
            if (evt.Key == Key.Enter && mi.HighlightedIndex >= 0)
            {
                SelectMentionSuggestion(mi, mi.HighlightedIndex);
                RequestRepaint?.Invoke();
                return true;
            }
            if (evt.Key == Key.Escape)
            {
                mi.ClosePopup();
                RequestRepaint?.Invoke();
                return true;
            }
        }

        bool ctrl = evt.Modifiers.HasFlag(ModifierKeys.Ctrl);

        // Ctrl+A — select all
        if (ctrl && evt.Key == Key.A)
        {
            MentionInputSelectionAnchor = 0;
            MentionInputCaretIndex = MentionEditBuffer.Length;
            return true;
        }

        // Backspace
        if (evt.Character == '\b') // Windows sends the Backspace key, then this character: act once
        {
            if (MentionInputSelectionAnchor != MentionInputCaretIndex)
            {
                DeleteMentionSelection(mi);
            }
            else if (MentionInputCaretIndex > 0)
            {
                MentionEditBuffer = MentionEditBuffer.Remove(MentionInputCaretIndex - 1, 1);
                MentionInputCaretIndex--;
            }
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            ActiveEditBuffer = MentionEditBuffer;
            mi.Value.OnChange(MentionEditBuffer);
            UpdateMentionPopupState(mi);
            return true;
        }

        // Delete
        if (evt.Key == Key.Delete)
        {
            if (MentionInputSelectionAnchor != MentionInputCaretIndex)
            {
                DeleteMentionSelection(mi);
            }
            else if (MentionInputCaretIndex < MentionEditBuffer.Length)
            {
                MentionEditBuffer = MentionEditBuffer.Remove(MentionInputCaretIndex, 1);
            }
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            ActiveEditBuffer = MentionEditBuffer;
            mi.Value.OnChange(MentionEditBuffer);
            UpdateMentionPopupState(mi);
            return true;
        }

        // Arrow keys
        if (evt.Key == Key.Left && MentionInputCaretIndex > 0)
        {
            MentionInputCaretIndex--;
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            return true;
        }
        if (evt.Key == Key.Right && MentionInputCaretIndex < MentionEditBuffer.Length)
        {
            MentionInputCaretIndex++;
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            return true;
        }
        if (evt.Key == Key.Home)
        {
            MentionInputCaretIndex = 0;
            MentionInputSelectionAnchor = 0;
            return true;
        }
        if (evt.Key == Key.End)
        {
            MentionInputCaretIndex = MentionEditBuffer.Length;
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            return true;
        }

        // Enter (no popup) — commit value
        if (evt.Key == Key.Enter && !mi.IsPopupOpen)
        {
            mi.Value.OnChange(MentionEditBuffer);
            return true;
        }

        // Ctrl+V / Shift+Insert — paste at the caret (one line), within MaxLength. MentionInput had no paste.
        if ((ctrl && evt.Key == Key.V) || (evt.Modifiers.HasFlag(ModifierKeys.Shift) && evt.Key == Key.Insert))
        {
            var content = Clipboard.GetContentAsync().GetAwaiter().GetResult();
            string pasted = (content.HasText ? content.GetTextAsync().GetAwaiter().GetResult() ?? "" : "")
                .Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
            if (MentionInputSelectionAnchor != MentionInputCaretIndex)
            {
                DeleteMentionSelection(mi);
            }
            if (mi.MaxLengthValue is { } room)
            {
                pasted = pasted[..Math.Clamp(room - MentionEditBuffer!.Length, 0, pasted.Length)];
            }
            if (pasted.Length > 0)
            {
                MentionEditBuffer = MentionEditBuffer!.Insert(MentionInputCaretIndex, pasted);
                MentionInputCaretIndex += pasted.Length;
                MentionInputSelectionAnchor = MentionInputCaretIndex;
                ActiveEditBuffer = MentionEditBuffer;
                mi.Value.OnChange(MentionEditBuffer);
            }
            return true;
        }

        // Printable character input
        if (evt.Character is char ch && ch >= ' ')
        {
            if (MentionInputSelectionAnchor != MentionInputCaretIndex)
            {
                DeleteMentionSelection(mi);
            }

            // MaxLength: a full field takes no more characters.
            if (mi.MaxLengthValue is { } max && MentionEditBuffer.Length >= max)
            {
                return true;
            }

            MentionEditBuffer = MentionEditBuffer.Insert(MentionInputCaretIndex, ch.ToString());
            MentionInputCaretIndex++;
            MentionInputSelectionAnchor = MentionInputCaretIndex;
            ActiveEditBuffer = MentionEditBuffer;
            mi.Value.OnChange(MentionEditBuffer);

            // Check if the typed character is a trigger
            foreach (var trigger in mi.Triggers)
            {
                if (ch == trigger.TriggerChar)
                {
                    mi.OpenPopup(trigger, MentionInputCaretIndex);
                    return true;
                }
            }

            // If popup is already open, update the query
            UpdateMentionPopupState(mi);
            return true;
        }

        return false;
    }

    private static void DeleteMentionSelection(MentionInput mi)
    {
        int start = Math.Min(MentionInputSelectionAnchor, MentionInputCaretIndex);
        int end = Math.Max(MentionInputSelectionAnchor, MentionInputCaretIndex);
        MentionEditBuffer = MentionEditBuffer!.Remove(start, end - start);
        MentionInputCaretIndex = start;
        MentionInputSelectionAnchor = start;
        ActiveEditBuffer = MentionEditBuffer;
    }

    private static void UpdateMentionPopupState(MentionInput mi)
    {
        if (!mi.IsPopupOpen || mi.ActiveTrigger == null || MentionEditBuffer == null)
        {
            return;
        }

        // Query text is everything from the trigger char to the caret
        if (mi.QueryStartIndex > MentionEditBuffer.Length || MentionInputCaretIndex < mi.QueryStartIndex)
        {
            mi.ClosePopup();
            return;
        }

        // Check if trigger char is still at the expected position
        if (mi.QueryStartIndex < 1 || MentionEditBuffer[mi.QueryStartIndex - 1] != mi.ActiveTrigger.TriggerChar)
        {
            mi.ClosePopup();
            return;
        }

        mi.QueryText = MentionEditBuffer[mi.QueryStartIndex..MentionInputCaretIndex];

        // If query contains a space, close the popup (mention complete or cancelled)
        if (mi.QueryText.Contains(' ', StringComparison.Ordinal))
        {
            mi.ClosePopup();
            return;
        }

        mi.UpdateSuggestions();
        if (mi.Suggestions.Count == 0)
        {
            mi.ClosePopup();
        }
    }

    private static void SelectMentionSuggestion(MentionInput mi, int index)
    {
        if (index < 0 || index >= mi.Suggestions.Count || MentionEditBuffer == null)
        {
            return;
        }

        string insertText = mi.Suggestions[index];
        // Replace from trigger char position to current caret with the insert text
        int triggerPos = mi.QueryStartIndex - 1; // position of the trigger char
        int replaceEnd = MentionInputCaretIndex;

        // A mention that would overrun MaxLength isn't cut in half: it isn't inserted.
        if (mi.MaxLengthValue is { } max && MentionEditBuffer.Length - (replaceEnd - triggerPos) + insertText.Length > max)
        {
            mi.ClosePopup();
            return;
        }

        MentionEditBuffer = MentionEditBuffer[..triggerPos] + insertText + MentionEditBuffer[replaceEnd..];
        MentionInputCaretIndex = triggerPos + insertText.Length;
        MentionInputSelectionAnchor = MentionInputCaretIndex;
        ActiveEditBuffer = MentionEditBuffer;

        mi.ClosePopup();
        mi.Value.OnChange(MentionEditBuffer);
    }

    private static void HandleMentionPopupClick(MentionInput mi, float x, float y)
    {
        for (int i = 0; i < mi.SuggestionItemBounds.Count; i++)
        {
            if (mi.SuggestionItemBounds[i].Contains(new Point(x, y)))
            {
                SelectMentionSuggestion(mi, i);
                return;
            }
        }
    }
}
