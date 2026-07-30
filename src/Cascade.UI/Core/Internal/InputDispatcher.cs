using System.Diagnostics;

namespace Cascade.UI;

/// <summary>
/// Routes platform input events (mouse, keyboard, scroll) to the correct nodes
/// in the visual tree. Performs hit testing, tracks hover/press state, and invokes
/// gesture callbacks and focus management.
/// </summary>
internal sealed class InputDispatcher
{
    private const float ExpandIndicatorWidth = 24f;

    private static InputDispatcher? current;

    private Node? rootNode;
    private Node? hoveredNode;
    private Node? pressedNode;
    private Point lastMousePosition;
    private bool isMouseDown;

    // Slider drag state — tracked across MouseDown/Move/Up
    private float sliderDragStartX;
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

    // Open SplitButton dropdown state
    private SplitButton? openSplitButton;

    // Open DatePicker calendar popup state
    private DatePicker? openDatePicker;

    // Open DateTimePicker popup state
    private DateTimePicker? openDateTimePicker;

    // Open TimePicker popup state
    private TimePicker? openTimePicker;

    // Open MonthPicker popup state
    private MonthPicker? openMonthPicker;

    // Open MenuBar dropdown state
    private MenuBar? openMenuBar;

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

    /// <summary>The current PasswordInput editing buffer, or null when not editing.</summary>
    internal static string? PasswordEditBuffer { get; private set; }

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

    private int currentCursorKind;

    /// <summary>
    /// Updates the root node reference. Called after each layout pass since
    /// the node tree may have changed.
    /// </summary>
    internal void SetRoot(Node? root)
    {
        rootNode = root;
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
    }

    // ── Mouse events ──────────────────────────────────────────────────

    /// <summary>
    /// Handles a mouse event from the platform layer.
    /// </summary>
    internal void HandleMouseEvent(NativeMouseEvent evt)
    {
        if (rootNode == null)
        {
            return;
        }

        // Scrollbar drag — intercept all mouse events while dragging
        if (scrollbarDragging && scrollbarDragTarget != null)
        {
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
                        var targetBounds = dragData.AbsoluteBounds;
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
            if (targetSv != null && targetSv.ScrollbarTrackBounds.Width > 0
                && targetSv.ScrollbarTrackBounds.Contains(new Point(evt.X, evt.Y)))
            {
                scrollbarDragging = true;
                scrollbarDragStartY = evt.Y;
                scrollbarDragStartOffset = targetSv.OffsetY;
                scrollbarDragTarget = targetSv;
                return;
            }
        }

        lastMousePosition = new Point(evt.X, evt.Y);
        CurrentMousePosition = lastMousePosition;
        var hitNode = HitTester.HitTest(rootNode, evt.X, evt.Y);

        switch (evt.Type)
        {
            case NativeMouseEventType.MouseMove:
                HandleMouseMove(hitNode, evt);
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

    private void HandleMouseMove(Node? hitNode, NativeMouseEvent evt)
    {
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

        // Update dropdown hover highlighting if a SplitButton is open
        if (openSplitButton != null)
        {
            UpdateSplitButtonDropdownHover(evt.X, evt.Y);
        }

        // Update MenuBar dropdown/label hover highlighting
        if (openMenuBar != null)
        {
            UpdateMenuBarHover(evt.X, evt.Y);
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

            // Update cursor for SplitView divider hover
            int desiredCursor = 0;
            if (hitNode is SplitView svHover)
            {
                desiredCursor = svHover.Orientation == SplitOrientation.Horizontal ? 1 : 2;
            }
            if (desiredCursor != currentCursorKind)
            {
                currentCursorKind = desiredCursor;
                RequestCursorChange?.Invoke(desiredCursor);
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
            int newIdx = ComputeToolBarItemIndex(tbHover, evt.X);
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
        else if (hoveredNode is not MenuBar && openMenuBar == null)
        {
            // Clear hover on menu bars we're no longer over
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

        // Update copy button hover on Markdown
        if (hitNode is Markdown mdHover)
        {
            var mdAbs = mdHover.AbsoluteBounds;
            var localPt = new Point(evt.X - mdAbs.X, evt.Y - mdAbs.Y);
            int newIdx = -1;
            for (int i = 0; i < mdHover.CodeBlockCopyButtons.Count; i++)
            {
                if (mdHover.CodeBlockCopyButtons[i].Bounds.Contains(localPt))
                {
                    newIdx = i;
                    break;
                }
            }
            if (newIdx != mdHover.HoveredCopyButtonIndex)
            {
                mdHover.HoveredCopyButtonIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }
        else if (hoveredNode is Markdown mdOld)
        {
            if (mdOld.HoveredCopyButtonIndex != -1)
            {
                mdOld.HoveredCopyButtonIndex = -1;
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
            if (openNotificationBell.AbsoluteBounds.Contains(new Point(evt.X, evt.Y)))
            {
                newIdx = -2;
            }

            if (newIdx != openNotificationBell.HoveredIndex)
            {
                openNotificationBell.HoveredIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }
        else if (hitNode is NotificationBell nbHover)
        {
            int newIdx = nbHover.AbsoluteBounds.Contains(new Point(evt.X, evt.Y)) ? -2 : -1;
            if (newIdx != nbHover.HoveredIndex)
            {
                nbHover.HoveredIndex = newIdx;
                RequestRepaint?.Invoke();
            }
        }

        // Update day cell hover on inline Calendar
        if (hitNode is Calendar calHover)
        {
            UpdateCalendarInlineHover(calHover,
                evt.X - calHover.AbsoluteBounds.X,
                evt.Y - calHover.AbsoluteBounds.Y);
        }

        // Update hovered cell on EmojiPicker
        if (hitNode is EmojiPicker epHover)
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
            var tdnBounds = tdnHover.AbsoluteBounds;
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
            var tdnBounds = tdnHeaderHover.AbsoluteBounds;
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
            var args = CreatePointerArgs(hitNode, evt);
            InvokeGesture(hitNode, g => g.PointerMove, args);
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
                    var tdnBoundsInit = columnReorderTdn.AbsoluteBounds;
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
                var tdnBounds = columnReorderTdn.AbsoluteBounds;
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
            UpdateSliderFromDrag(slider, evt.X);
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
        else if (isMouseDown && pressedNode is ColorPicker cpDrag && !cpDrag.IsDisabled)
        {
            UpdateColorPickerFromDrag(cpDrag, evt.X, evt.Y);
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
            var delta = new Point(evt.X - lastMousePosition.X, evt.Y - lastMousePosition.Y);
            InvokeGesture(pressedNode, g => g.Pan, delta);
        }
    }

    private void HandleMouseDown(Node? hitNode, NativeMouseEvent evt)
    {
        isMouseDown = true;
        lastMouseModifiers = evt.Modifiers;

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

        // If a MenuBar dropdown is open, check if the click is within it or on a label
        if (openMenuBar != null && evt.Button == NativeMouseButton.Left)
        {
            var dropBounds = openMenuBar.DropdownBounds;
            var click = new Point(evt.X, evt.Y);

            // Click inside dropdown — find and invoke the menu item
            if (dropBounds.Width > 0 && dropBounds.Contains(click))
            {
                var menu = openMenuBar.Menus[openMenuBar.OpenMenuIndex];
                float itemY = dropBounds.Y + 4f; // top padding
                float separatorHeight = 9f;
                float headerHeight = 24f;

                for (int i = 0; i < menu.Items.Count; i++)
                {
                    var item = menu.Items[i];
                    float currentH;
                    if (item.Label == null && item.CustomContent == Node.Empty)
                    {
                        currentH = separatorHeight;
                    }
                    else if (!item.Enabled && item.OnClick == null && item.ToggleValue.OnChange is null && item.Items == null)
                    {
                        currentH = headerHeight;
                    }
                    else
                    {
                        currentH = openMenuBar.MenuItemHeight;
                    }

                    if (evt.Y >= itemY && evt.Y < itemY + currentH)
                    {
                        if (item.Enabled)
                        {
                            if (item.OnClick != null)
                            {
                                openMenuBar.Close();
                                openMenuBar = null;
                                RequestRepaint?.Invoke();
                                item.OnClick();
                                return;
                            }
                            if (item.ToggleValue.OnChange is not null)
                            {
                                item.ToggleValue.OnChange(!item.ToggleValue.Value);
                                openMenuBar.Close();
                                openMenuBar = null;
                                RequestRepaint?.Invoke();
                                return;
                            }
                        }
                        break;
                    }

                    itemY += currentH;
                }

                return;
            }

            // Click on a different menu label — switch to it
            for (int i = 0; i < openMenuBar.MenuLabelBounds.Length; i++)
            {
                if (openMenuBar.MenuLabelBounds[i].Contains(click))
                {
                    if (i == openMenuBar.OpenMenuIndex)
                    {
                        // Same label — toggle closed
                        openMenuBar.Close();
                        openMenuBar = null;
                    }
                    else
                    {
                        openMenuBar.OpenMenu(i);
                    }
                    RequestRepaint?.Invoke();
                    return;
                }
            }

            // Click outside — close
            openMenuBar.Close();
            openMenuBar = null;
            RequestRepaint?.Invoke();
        }

        // If a NotificationBell dropdown is open, check if the click is within it
        if (openNotificationBell != null && evt.Button == NativeMouseButton.Left)
        {
            var dropBounds = openNotificationBell.DropdownBounds;
            var bellBounds = openNotificationBell.AbsoluteBounds;
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

        // If a SplitButton dropdown is open, check if the click is within it
        if (openSplitButton != null && evt.Button == NativeMouseButton.Left)
        {
            var dropdownBounds = openSplitButton.DropdownBounds;
            var clickPoint = new Point(evt.X, evt.Y);

            if (dropdownBounds.Width > 0 && dropdownBounds.Contains(clickPoint))
            {
                // Click inside dropdown — find and invoke the menu item.
                // The 6px offset matches the painter's menuPadV top inset
                // (PaintSplitButtonDropdown) so hit rows line up with the drawn rows.
                float itemY = dropdownBounds.Y + 6f;
                float separatorHeight = 9f;

                for (int i = 0; i < openSplitButton.Items.Count; i++)
                {
                    var menuItem = openSplitButton.Items[i];
                    float currentItemHeight = menuItem.Label == null
                        ? separatorHeight
                        : openSplitButton.MenuItemHeight;

                    if (evt.Y >= itemY && evt.Y < itemY + currentItemHeight)
                    {
                        if (menuItem.Label != null && !menuItem.Disabled && menuItem.OnClick != null)
                        {
                            openSplitButton.Close();
                            openSplitButton = null;
                            RequestRepaint?.Invoke();
                            menuItem.OnClick();
                        }
                        break;
                    }

                    itemY += currentItemHeight;
                }

                return;
            }

            // Click is on the trigger itself — let InvokeTap handle toggle
            if (hitNode is SplitButton sb && ReferenceEquals(sb, openSplitButton))
            {
                // Fall through to normal flow
            }
            else
            {
                // Click is outside both trigger and dropdown — close it
                openSplitButton.Close();
                openSplitButton = null;
                RequestRepaint?.Invoke();
            }
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
            var tdnBounds = tdnResize.AbsoluteBounds;
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
                    tdnResize.ResizeStartWidth = tdnResize.GetColumnWidth(borderCol, tdnBounds.Width);
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
            float localX = evt.X - calDown.AbsoluteBounds.X;
            float localY = evt.Y - calDown.AbsoluteBounds.Y;
            HandleCalendarClick(calDown, localX, localY);
            RequestRepaint?.Invoke();
            return;
        }

        pressedNode = hitNode;

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
                tbPress.PressedItemIndex = ComputeToolBarItemIndex(tbPress, evt.X);
            }
            if (hitNode is INumberInput niPress && !niPress.IsDisabled)
            {
                niPress.PressedStepperButton = ComputeNumberInputHoveredButton(niPress, evt.X, evt.Y);
            }
            RequestRepaint?.Invoke();
        }

        // Record slider drag start state
        if (hitNode is Slider sl)
        {
            sliderDragStartX = evt.X;
            sliderDragStartValue = sl.Bind.Value;
        }

        // Record range slider drag start state — pick nearest thumb
        if (hitNode is RangeSlider rs && !rs.IsDisabled && !rs.IsReadOnly)
        {
            sliderDragStartX = evt.X;
            float trackRange = rs.Max - rs.Min;
            float thumbW = 20f;
            // Use AbsoluteBounds (set by painter) for correct window-coordinate math
            var absBounds = rs.AbsoluteBounds;
            float trackW = Math.Max(1f, absBounds.Width - thumbW);
            float relX = evt.X - absBounds.X - thumbW / 2f;
            float clickFrac = Math.Clamp(relX / trackW, 0f, 1f);
            float clickVal = rs.Min + clickFrac * trackRange;

            float distMin = MathF.Abs(clickVal - rs.MinBind.Value);
            float distMax = MathF.Abs(clickVal - rs.MaxBind.Value);
            rangeSliderDragIsMax = distMax <= distMin;
            rangeSliderDragStartValue = rangeSliderDragIsMax ? rs.MaxBind.Value : rs.MinBind.Value;
        }

        if (hitNode == null)
        {
            FocusManager.ClearFocus();
            return;
        }

        // Focus the clicked node if it's focusable
        var focusTarget = FindFocusableNode(hitNode);
        if (focusTarget != null)
        {
            // Clear TagInput focus state if moving away from a TagInput
            if (FocusManager.FocusedElement is TagInput prevTag && !ReferenceEquals(prevTag, focusTarget))
            {
                prevTag.IsFocused = false;
            }

            FocusManager.RequestFocus(focusTarget);

            // Initialize text editing buffer when focusing a TextInput
            if (focusTarget is TextInput ti)
            {
                bool wasTextInputFocused = textInputBuffer != null;
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
                if (PasswordEditBuffer == null)
                {
                    PasswordEditBuffer = pwdFocus.Value.Value ?? string.Empty;
                    PasswordRevealed = false;
                    PasswordScrollOffsetX = 0f;
                }
                CaretResetTimestamp = Stopwatch.GetTimestamp();
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
        else
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
        var args = CreatePointerArgs(hitNode, evt);
        InvokeGesture(hitNode, g => g.PointerDown, args);

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

        // Right-click → context menu
        if (evt.Button == NativeMouseButton.Right && hitNode != null)
        {
            InvokeContextMenu(hitNode);
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
            RequestRepaint?.Invoke();
        }

        if (hitNode == null)
        {
            return;
        }

        // Fire PointerUp
        var args = CreatePointerArgs(hitNode, evt);
        InvokeGesture(hitNode, g => g.PointerUp, args);

        // If released on the same node as the press, it's a tap/click
        if (hitNode is not null && ReferenceEquals(hitNode, previousPressed) && evt.Button == NativeMouseButton.Left)
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

    private void HandleMouseLeave()
    {
        UpdateTreeViewHover(null);

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
    /// Handles a scroll event from the platform layer.
    /// </summary>
    internal void HandleScrollEvent(NativeScrollEvent evt)
    {
        if (rootNode == null)
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

                    if (newOffset != openSelect.ScrollOffset)
                    {
                        openSelect.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }
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

                    if (newOffset != openMultiSelect.ScrollOffset)
                    {
                        openMultiSelect.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }
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

                    if (newOffset != openCombobox.ScrollOffset)
                    {
                        openCombobox.ScrollOffset = newOffset;
                        RequestRepaint?.Invoke();
                    }
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
                if (Math.Abs(newOffset - TextAreaScrollOffsetY) > 0.001f)
                {
                    TextAreaScrollOffsetY = newOffset;
                    CaretResetTimestamp = Stopwatch.GetTimestamp();
                    RequestRepaint?.Invoke();
                }

                return;
            }
        }

        // DataGrid / DataTable scroll — handle wheel within the grid's data area
        var hitForGrid = HitTester.HitTest(rootNode, evt.X, evt.Y);
        if (hitForGrid is ITabularDataNode tdnScroll && tdnScroll.MaxScrollOffsetY > 0)
        {
            var gdBounds = tdnScroll.AbsoluteBounds;
            if (gdBounds.Width > 0 && gdBounds.Contains(new Point(evt.X, evt.Y)))
            {
                const float pixelsPerNotch = 48f;
                float delta = -evt.DeltaY * pixelsPerNotch;
                float oldOffset = tdnScroll.ScrollOffsetY;
                tdnScroll.ScrollOffsetY = oldOffset + delta;
                if (Math.Abs(tdnScroll.ScrollOffsetY - oldOffset) > 0.001f)
                {
                    RequestRepaint?.Invoke();
                }

                return;
            }
        }

        // Virtualized ListView — a list that owns its own scroll offset (builds only
        // the visible slice). Takes priority over an enclosing ScrollView.
        var virtualList = HitTester.FindScrollableListViewAt(rootNode, evt.X, evt.Y);
        if (virtualList != null)
        {
            const float pixelsPerNotch = 48f;
            float delta = -evt.DeltaY * pixelsPerNotch;
            float newOffset = Math.Clamp(virtualList.OffsetY + delta, 0f, virtualList.MaxY);
            if (Math.Abs(newOffset - virtualList.OffsetY) > 0.001f)
            {
                virtualList.OffsetY = newOffset;
                RequestRepaint?.Invoke();
            }

            return;
        }

        // ScrollView handling — check if mouse is over a ScrollView and scroll it
        var scrollView = HitTester.FindScrollViewAt(rootNode, evt.X, evt.Y);
        if (scrollView != null && scrollView.MaxY > 0)
        {
            // Scale by delta magnitude for smooth trackpad scrolling (one notch = 1.0)
            const float pixelsPerNotch = 48f;
            float delta = -evt.DeltaY * pixelsPerNotch;
            float newOffset = Math.Clamp(scrollView.OffsetY + delta, 0f, scrollView.MaxY);
            if (Math.Abs(newOffset - scrollView.OffsetY) > 0.001f)
            {
                scrollView.OffsetY = newOffset;
                // Keep globals in sync for DevTools backward compatibility
                ScrollViewOffsetY = newOffset;
                ScrollViewMaxY = scrollView.MaxY;
                RequestRepaint?.Invoke();
            }

            return;
        }

        var hitNode = HitTester.HitTest(rootNode, evt.X, evt.Y);
        if (hitNode == null)
        {
            return;
        }

        var gestDelta = new Point(evt.DeltaX, evt.DeltaY);
        InvokeGesture(hitNode, g => g.Scroll, gestDelta);
    }

    // ── Keyboard events ───────────────────────────────────────────────

    /// <summary>
    /// Handles a keyboard event from the platform layer.
    /// </summary>
    internal void HandleKeyEvent(NativeKeyEvent evt)
    {
        if (evt.Type != NativeKeyEventType.KeyDown)
        {
            return;
        }

#if DEBUG
        // DevTools keyboard shortcuts (F12, Ctrl+Shift+I, Ctrl+Shift+L, etc.)
        if (DevTools.CascadeDevTools.HandleKeyDown(evt.Key, evt.Modifiers))
        {
            return;
        }
#endif

        // CommandPalette intercepts all input when open
        if (CommandPalette.IsOpen && CommandPalette.Instance != null)
        {
            HandleCommandPaletteKeyboard(evt);
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
                return;
            }
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
            // Close MenuBar dropdown first
            if (openMenuBar != null)
            {
                openMenuBar.Close();
                openMenuBar = null;
                RequestRepaint?.Invoke();
                return;
            }

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

            FocusManager.ClearFocus();
            textInputBuffer = null;
            ActiveEditBuffer = null;
            MentionEditBuffer = null;
            PinEditBuffer = null;
            TextAreaEditBuffer = null;
            RequestRepaint?.Invoke();
            return;
        }

        // TextInput character input — route typed characters, backspace, delete
        if (focusedNode is TextInput textInput && !textInput.IsDisabled && !textInput.IsReadOnly)
        {
            if (HandleTextInputKey(textInput, evt))
            {
                return;
            }
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

        // Enter/Space on focused button → invoke click
        if (evt.Key is Key.Enter or Key.Space)
        {
            if (focusedNode is Button btn && !btn.IsDisabled)
            {
                btn.OnClick();
                return;
            }

            if (focusedNode is LinkButton lb && !lb.IsDisabled)
            {
                lb.OnClick();
                return;
            }

            if (focusedNode is IconButton ib && !ib.IsDisabled)
            {
                ib.OnClick();
                return;
            }
        }

        // App menu accelerators (MenuBar item shortcuts, e.g. Ctrl+Z / Ctrl+S) —
        // fire even while a control is focused, as long as that control did not
        // consume the key above (text controls return early when they do).
        if (DispatchMenuBarShortcut(evt))
        {
            // A menu action (e.g. Undo) may have changed the value bound to the
            // focused text control. That control renders its own edit buffer while
            // focused, so refresh the buffer from the (possibly new) bound value or
            // the change would not appear until focus leaves.
            ResyncFocusedEditBuffer();
            RequestRepaint?.Invoke();
            return;
        }

        // Dispatch to KeyHandler bindings on the focused element's ancestor chain
        DispatchKeyBinding(evt);
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
                    item.ToggleValue.OnChange(!item.ToggleValue.Value);
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
    private Node? FindNextTabStop(Node? current, bool backward)
    {
        if (rootNode is null)
        {
            return null;
        }

        var stops = new List<Node>();
        CollectTabStops(rootNode, stops);
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
    /// Walks the tree in document (paint) order, appending every enabled keyboard tab
    /// stop to <paramref name="result"/>.
    /// </summary>
    private static void CollectTabStops(Node node, List<Node> result)
    {
        if (IsTabStop(node))
        {
            result.Add(node);
        }

        foreach (var child in NodeDiffer.GetChildren(node))
        {
            CollectTabStops(child, result);
        }
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

        // Known focusable control types (mirrors FindFocusableNode). Anything that can
        // be click-focused is also tab-focusable, so the two stay consistent.
        if (node is Button or LinkButton or IconButton
            or TextInput or TextArea or PasswordInput or PinInput
            or MentionInput or TagInput or ColorPicker
            or Checkbox or Toggle or Slider
            || IsNumberInput(node))
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

        // Ctrl+A — select all
        if (ctrl && evt.Key == Key.A)
        {
            TextInputSelectionAnchor = 0;
            TextInputCaretIndex = textInputBuffer.Length;
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

        // Ctrl+V — paste
        if (ctrl && evt.Key == Key.V)
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
            textInput.OnChangeHandler?.Invoke(textInputBuffer);
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
                textInput.OnChangeHandler?.Invoke(textInputBuffer);
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
                textInput.OnChangeHandler?.Invoke(textInputBuffer);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles keyboard input for a focused PasswordInput. Similar to TextInput
    /// but uses a separate buffer and updates PasswordEditBuffer.
    /// </summary>
    private static bool HandlePasswordInputKey(PasswordInput pwd, NativeKeyEvent evt)
    {
        if (PasswordEditBuffer == null)
        {
            PasswordEditBuffer = pwd.Value.Value ?? string.Empty;
        }

        CaretResetTimestamp = Stopwatch.GetTimestamp();

        // Printable characters
        if (evt.Character >= ' ' && evt.Character != 127)
        {
            PasswordEditBuffer += evt.Character;
            pwd.Value.OnChange(PasswordEditBuffer);
            return true;
        }

        // Backspace
        if (evt.Character == '\b')
        {
            if (PasswordEditBuffer.Length > 0)
            {
                PasswordEditBuffer = PasswordEditBuffer[..^1];
                pwd.Value.OnChange(PasswordEditBuffer);
            }
            return true;
        }

        // Delete key clears all
        if (evt.Key == Key.Delete)
        {
            if (PasswordEditBuffer.Length > 0)
            {
                PasswordEditBuffer = string.Empty;
                pwd.Value.OnChange(PasswordEditBuffer);
            }
            return true;
        }

        return false;
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
            ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
            ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
                ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
                ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
    private static void PositionTextAreaCaretFromMouse(TextArea ta, NativeMouseEvent evt)
    {
        var bounds = ta.AbsoluteBounds;
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
    /// Computes caret position from mouse click coordinates on a TextInput.
    /// Uses the TextInput's AbsoluteBounds and approximate character width.
    /// </summary>
    private static void PositionTextInputCaretFromMouse(TextInput ti, NativeMouseEvent evt)
    {
        var bounds = ti.AbsoluteBounds;
        if (bounds.Width <= 0)
        {
            return;
        }

        string buf = ActiveEditBuffer ?? ti.Value.Value ?? "";
        if (buf.Length == 0)
        {
            TextInputCaretIndex = 0;
            return;
        }

        float paddingH = 12f; // theme.TextInput.PaddingH default
        float fontSize = 17f; // Apple theme body size

        float relX = evt.X - bounds.X - paddingH;
        if (relX <= 0)
        {
            TextInputCaretIndex = 0;
            return;
        }

        float avgCharWidth = fontSize * 0.52f;
        TextInputCaretIndex = Math.Clamp((int)(relX / avgCharWidth + 0.5f), 0, buf.Length);
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
        ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
        ta.OnChangeHandler?.Invoke(TextAreaEditBuffer);
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
        ti.OnChangeHandler?.Invoke(textInputBuffer);
    }

    /// <summary>Pastes clipboard text into the TextInput at the caret, replacing any selection.</summary>
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
        textInputBuffer = textInputBuffer.Insert(TextInputCaretIndex, text);
        TextInputCaretIndex += text.Length;
        TextInputSelectionAnchor = TextInputCaretIndex;
        ActiveEditBuffer = textInputBuffer;
        ti.Value.OnChange(textInputBuffer);
        ti.OnChangeHandler?.Invoke(textInputBuffer);
    }

    /// <summary>
    /// Walks from the focused node upward looking for a KeyHandler with a matching binding.
    /// </summary>
    private void DispatchKeyBinding(NativeKeyEvent evt)
    {
        if (rootNode == null)
        {
            return;
        }

        var focused = FocusManager.FocusedElement;
        if (focused == null)
        {
            // No focused element — dispatch to root-level key handlers
            DispatchKeyBindingsInTree(rootNode, evt);
            return;
        }

        // Search for KeyHandler ancestors by walking the full tree from root
        // (since we don't have parent pointers, we search for the path)
        DispatchKeyBindingsInTree(rootNode, evt);
    }

    private static bool DispatchKeyBindingsInTree(Node node, NativeKeyEvent evt)
    {
        if (node is KeyHandler kh)
        {
            foreach (var binding in kh.Bindings)
            {
                if (MatchesBinding(binding, evt))
                {
                    binding.Handler();
                    return true;
                }
            }
        }

        // Recurse into children
        if (node is Row row)
        {
            foreach (var child in row.Children)
            {
                if (DispatchKeyBindingsInTree(child, evt))
                {
                    return true;
                }
            }
        }
        else if (node is Column col)
        {
            foreach (var child in col.Children)
            {
                if (DispatchKeyBindingsInTree(child, evt))
                {
                    return true;
                }
            }
        }
        else if (node is Stack stack)
        {
            foreach (var child in stack.Children)
            {
                if (DispatchKeyBindingsInTree(child, evt))
                {
                    return true;
                }
            }
        }
        else if (node is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (DispatchKeyBindingsInTree(child, evt))
                {
                    return true;
                }
            }
        }
        else if (node is KeyHandler kh2)
        {
            if (kh2.Content != null && DispatchKeyBindingsInTree(kh2.Content, evt))
            {
                return true;
            }
        }
        else if (node is Center center && center.Child != null)
        {
            if (DispatchKeyBindingsInTree(center.Child, evt))
            {
                return true;
            }
        }
        else if (node is Card card && card.Content != null)
        {
            if (DispatchKeyBindingsInTree(card.Content, evt))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesBinding(KeyBinding binding, NativeKeyEvent evt)
    {
        if (!binding.When)
        {
            return false;
        }

        return binding.Hotkey.Key == evt.Key && binding.Hotkey.Modifiers == evt.Modifiers;
    }

    // ── Gesture invocation ────────────────────────────────────────────

    private void InvokeTap(Node node)
    {
        // Direct control click handlers
        switch (node)
        {
            case Button btn when !btn.IsDisabled:
                btn.OnClick();
                return;
            case LinkButton lb when !lb.IsDisabled:
                lb.OnClick();
                return;
            case IconButton ib when !ib.IsDisabled:
                ib.OnClick();
                return;
            case Checkbox cb when cb.BoolValue is { } binding:
                binding.OnChange(!binding.Value);
                return;
            case IRadioButton rb:
                rb.Select();
                return;
            case Toggle toggle:
                toggle.Value.OnChange(!toggle.Value.Value);
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
            case Tag { OnToggle: not null } tag:
                bool selected = tag.Selected?.Value ?? false;
                tag.OnToggle!(!selected);
                return;
            case Tag { OnRemove: not null } tag:
                tag.OnRemove!();
                return;

            case ISegmentedControl sc when !sc.IsControlDisabled:
                HandleSegmentedControlClick(sc);
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

            case ColorPicker cp when !cp.IsDisabled:
                HandleColorPickerClick(cp);
                return;

            case PinInput pin when !pin.IsDisabled && !pin.IsReadOnly:
                HandlePinInputClick(pin);
                return;

            case ToolBar tb:
                HandleToolBarClick(tb);
                return;

            case PasswordInput pwdTap when pwdTap.ShowToggleButton:
            {
                // Only intercept if click is on the eye toggle icon area
                var pwdBounds = pwdTap.AbsoluteBounds;
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
                // Use AbsoluteBounds (set by painter) for correct viewport-coordinate math
                var absBounds = sb.AbsoluteBounds;
                float clickLocalX = lastMousePosition.X - absBounds.X;
                if (clickLocalX >= sb.ArrowZoneX)
                {
                    // Arrow zone — toggle dropdown
                    sb.ToggleOpen();
                    openSplitButton = sb.IsOpen ? sb : null;
                }
                else
                {
                    // Primary zone — close dropdown if open, invoke action
                    if (sb.IsOpen)
                    {
                        sb.Close();
                        openSplitButton = null;
                    }
                    sb.OnClick();
                }
                RequestRepaint?.Invoke();
                return;
            }

            case MenuBar mb:
            {
                // Find which menu label was clicked
                var clickPt = new Point(lastMousePosition.X, lastMousePosition.Y);
                for (int i = 0; i < mb.MenuLabelBounds.Length; i++)
                {
                    if (mb.MenuLabelBounds[i].Contains(clickPt))
                    {
                        if (mb.IsOpen && mb.OpenMenuIndex == i)
                        {
                            mb.Close();
                            openMenuBar = null;
                        }
                        else
                        {
                            mb.OpenMenu(i);
                            openMenuBar = mb;
                        }
                        RequestRepaint?.Invoke();
                        return;
                    }
                }
                return;
            }

            case PropertyGrid pg:
            {
                HandlePropertyGridClick(pg);
                RequestRepaint?.Invoke();
                return;
            }

            case NotificationBell nb:
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

            case EmojiPicker ep:
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
                    tp.TogglePopup();
                    openTimePicker = tp.IsPopupOpen ? tp : null;
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

            case Markdown md:
            {
                var mdAbs = md.AbsoluteBounds;
                var clickPt = new Point(
                    lastMousePosition.X - mdAbs.X,
                    lastMousePosition.Y - mdAbs.Y);
                foreach (var (btnBounds, codeText) in md.CodeBlockCopyButtons)
                {
                    if (btnBounds.Contains(clickPt))
                    {
                        _ = Clipboard.WriteTextAsync(codeText);
                        RequestRepaint?.Invoke();
                        return;
                    }
                }
                return;
            }

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
            or ITreeView or Tag or ISegmentedControl or IToggleGroup
            or INumberInput or Breadcrumb or StepIndicator or Banner
            or ColorPicker or PinInput or ToolBar or PasswordInput
            or ISelectNode or IMultiSelectNode or IComboboxNode
            or SplitButton or Slider or RangeSlider or Calendar
            or Accordion or MenuBar or StatusBar
            or ProgressBar or ProgressRing or Gauge or Spinner
            or NotificationBell or CommandPalette or KeyHandler
            or ScrollView or Markdown;
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
        var bounds = rating.AbsoluteBounds;
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
        if (hitNode is ITreeView tv && tv.AbsoluteBounds.Height > 0)
        {
            treeNode = hitNode;
            const float rowHeight = 28f;
            float relY = lastMousePosition.Y - tv.AbsoluteBounds.Y;
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
        var bounds = tv.AbsoluteBounds;
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
        var bounds = tdn.AbsoluteBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float rowHeight = tdn.GetRowHeight();
        float headerHeight = rowHeight + 4f;
        const float filterRowHeight = 28f;
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
        float dataAreaTop = headerHeight;
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
            dataAreaTop += filterRowHeight;
        }

        // Click below filter row → deactivate filter input
        if (tdn.ActiveFilterCol >= 0)
        {
            tdn.ActiveFilterCol = -1;
        }

        // Skip top aggregate row (not interactive)
        if (tdn.HasAggregateRow && tdn.AggregatePos == AggregatePosition.Top)
        {
            float aggH = tdn.GetAggregateRowHeight();
            if (relY >= dataAreaTop && relY < dataAreaTop + aggH)
            {
                return; // Click on aggregate row — no action
            }
            dataAreaTop += aggH;
        }

        // ── Grouped mode: hit-test group headers vs data rows ────────
        if (tdn.IsGrouped)
        {
            const float groupHeaderHeight = 32f;
            float currentY = dataAreaTop;
            for (int g = 0; g < tdn.GroupCount; g++)
            {
                // Group header
                if (relY >= currentY && relY < currentY + groupHeaderHeight)
                {
                    tdn.ToggleGroupCollapse(g);
                    RequestRepaint?.Invoke();
                    return;
                }
                currentY += groupHeaderHeight;

                if (!tdn.IsGroupCollapsed(g))
                {
                    int groupRowCount = tdn.GetGroupRowCount(g);
                    for (int rowInGroup = 0; rowInGroup < groupRowCount; rowInGroup++)
                    {
                        int row = tdn.GetGroupDataRowIndex(g, rowInGroup);

                        // Check if click is in this row
                        if (relY >= currentY && relY < currentY + rowHeight)
                        {
                            // Check expand indicator click
                            if (tdn.HasRowDetail && relX < ExpandIndicatorWidth)
                            {
                                tdn.ToggleRowDetail(row);
                                RequestRepaint?.Invoke();
                                return;
                            }
                            HandleTabularDataRowClick(tdn, row, relX, bounds.Width);
                            return;
                        }
                        currentY += rowHeight;

                        // Skip detail panel if expanded
                        if (tdn.HasRowDetail && tdn.IsRowExpanded(row))
                        {
                            float detailH = tdn.GetRowDetailHeight(row);
                            if (relY >= currentY && relY < currentY + detailH)
                            {
                                return; // Click in detail panel — no action
                            }
                            currentY += detailH;
                        }
                    }
                }
            }
            return;
        }

        // ── Flat (ungrouped) mode: click in data rows ────────────────
        if (tdn.HasRowDetail)
        {
            // Walk rows with variable heights when detail panels are open
            float currentY = dataAreaTop;
            for (int r = 0; r < tdn.RowCount; r++)
            {
                if (relY >= currentY && relY < currentY + rowHeight)
                {
                    // Check expand indicator click
                    if (relX < ExpandIndicatorWidth)
                    {
                        tdn.ToggleRowDetail(r);
                        RequestRepaint?.Invoke();
                        return;
                    }
                    HandleTabularDataRowClick(tdn, r, relX, bounds.Width);
                    return;
                }
                currentY += rowHeight;

                // Skip detail panel if expanded
                if (tdn.IsRowExpanded(r))
                {
                    float detailH = tdn.GetRowDetailHeight(r);
                    if (relY >= currentY && relY < currentY + detailH)
                    {
                        return; // Click in detail panel — no action
                    }
                    currentY += detailH;
                }
            }
        }
        else
        {
            float rowAreaY = relY - dataAreaTop;
            int flatRow = (int)(rowAreaY / rowHeight);
            if (flatRow >= 0 && flatRow < tdn.RowCount)
            {
                HandleTabularDataRowClick(tdn, flatRow, relX, bounds.Width);
            }
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
            tdn.SelectRow(row, ctrl, shift);
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

        switch (evt.Key)
        {
            case Key.Up:
                if (tdn.SelectedRowIndex > 0)
                {
                    tdn.MoveSelection(-1);
                    tdn.ScrollIntoView(tdn.SelectedRowIndex);
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Down:
                if (tdn.SelectedRowIndex < tdn.RowCount - 1)
                {
                    tdn.MoveSelection(1);
                    tdn.ScrollIntoView(tdn.SelectedRowIndex);
                    RequestRepaint?.Invoke();
                }
                return true;

            case Key.Home:
                tdn.SelectFirst();
                tdn.ScrollIntoView(tdn.SelectedRowIndex);
                RequestRepaint?.Invoke();
                return true;

            case Key.End:
                tdn.SelectLast();
                tdn.ScrollIntoView(tdn.SelectedRowIndex);
                RequestRepaint?.Invoke();
                return true;

            case Key.PageUp:
            {
                int jump = Math.Max(1, tdn.VisibleRowCount - 1);
                tdn.MoveSelection(-jump);
                tdn.ScrollIntoView(tdn.SelectedRowIndex);
                RequestRepaint?.Invoke();
                return true;
            }

            case Key.PageDown:
            {
                int jump = Math.Max(1, tdn.VisibleRowCount - 1);
                tdn.MoveSelection(jump);
                tdn.ScrollIntoView(tdn.SelectedRowIndex);
                RequestRepaint?.Invoke();
                return true;
            }

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

    /// <summary>
    /// Computes proportionally-scaled column widths matching what the renderer uses.
    /// When columns exceed available width (minus chooser reserve), they scale down to fit.
    /// Includes sort indicator reserve only on the currently-sorted column.
    /// </summary>
    private static float[] GetScaledColumnWidths(ITabularDataNode tdn, float availableWidth)
    {
        float[] widths = new float[tdn.ColumnCount];
        float total = 0f;
        const float sortIndicatorReserve = 5f + 3f; // arrowW + arrowGap
        for (int c = 0; c < tdn.ColumnCount; c++)
        {
            widths[c] = tdn.GetColumnWidth(c, availableWidth);
            if (widths[c] > 0f && tdn.IsSortable && tdn.SortColumnIndex == c)
            {
                widths[c] += sortIndicatorReserve;
            }
            total += widths[c];
        }

        const float chooserBtnSize = 24f;
        float chooserReserve = tdn.IsColumnChooserEnabled ? chooserBtnSize + 4f : 0f;
        float usable = availableWidth - chooserReserve;
        if (total > usable && total > 0f)
        {
            float scale = usable / total;
            for (int c = 0; c < tdn.ColumnCount; c++)
            {
                widths[c] = MathF.Floor(widths[c] * scale);
            }
        }

        return widths;
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

    private static int HitTestTabularRow(ITabularDataNode tdn, float mouseX, float mouseY)
    {
        var bounds = tdn.AbsoluteBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return -1;
        }

        float rowHeight = tdn.GetRowHeight();
        float headerHeight = rowHeight + 4f;
        const float filterRowHeight = 28f;
        float dataAreaTop = headerHeight + (tdn.HasFilterRow ? filterRowHeight : 0f);
        if (tdn.HasAggregateRow && tdn.AggregatePos == AggregatePosition.Top)
        {
            dataAreaTop += tdn.GetAggregateRowHeight();
        }
        float relY = mouseY - bounds.Y;

        if (relY < dataAreaTop)
        {
            return -1;
        }

        if (tdn.IsGrouped)
        {
            const float groupHeaderHeight = 32f;
            float currentY = dataAreaTop;
            for (int g = 0; g < tdn.GroupCount; g++)
            {
                currentY += groupHeaderHeight;
                if (tdn.IsGroupCollapsed(g))
                {
                    continue;
                }
                int groupRowCount = tdn.GetGroupRowCount(g);
                for (int rowInGroup = 0; rowInGroup < groupRowCount; rowInGroup++)
                {
                    int row = tdn.GetGroupDataRowIndex(g, rowInGroup);
                    if (relY >= currentY && relY < currentY + rowHeight)
                    {
                        return row;
                    }
                    currentY += rowHeight;

                    if (tdn.HasRowDetail && tdn.IsRowExpanded(row))
                    {
                        currentY += tdn.GetRowDetailHeight(row);
                    }
                }
            }
            return -1;
        }

        if (tdn.HasRowDetail)
        {
            float currentY = dataAreaTop;
            for (int r = 0; r < tdn.RowCount; r++)
            {
                if (relY >= currentY && relY < currentY + rowHeight)
                {
                    return r;
                }
                currentY += rowHeight;

                if (tdn.IsRowExpanded(r))
                {
                    currentY += tdn.GetRowDetailHeight(r);
                }
            }
            return -1;
        }

        int row2 = (int)((relY - dataAreaTop) / rowHeight);
        if (row2 >= 0 && row2 < tdn.RowCount)
        {
            return row2;
        }
        return -1;
    }

    // ── SegmentedControl helpers ─────────────────────────────────────

    private void HandleSegmentedControlClick(ISegmentedControl sc)
    {
        var bounds = sc.AbsoluteBounds;
        int count = sc.SegmentCount;
        if (count == 0)
        {
            return;
        }

        int clickedIndex = HitTestVariableWidthSegments(
            bounds,
            count,
            i => sc.GetSegmentLabel(i),
            lastMousePosition.X);
        sc.SelectIndex(clickedIndex);
    }

    // ── NumberInput helpers ──────────────────────────────────────────

    private void HandleNumberInputClick(INumberInput ni)
    {
        var bounds = ni.AbsoluteBounds;
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
    private static int ComputeNumberInputHoveredButton(INumberInput ni, float mouseX, float mouseY)
    {
        var bounds = ni.AbsoluteBounds;
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
        var absBounds = bc.AbsoluteBounds;
        if (absBounds.Width <= 0)
        {
            return;
        }

        float fontSize = 17f;
        float separatorPad = 4f;
        float itemPadH = 4f;

        float cursorX = absBounds.X;
        float clickX = lastMousePosition.X;

        for (int i = 0; i < bc.Segments.Count; i++)
        {
            var seg = bc.Segments[i];
            bool isLast = i == bc.Segments.Count - 1;
            float segTextWidth = seg.Label.Length * fontSize * 0.55f;
            float segWidth = segTextWidth + itemPadH * 2;

            if (clickX >= cursorX && clickX < cursorX + segWidth)
            {
                if (!isLast && seg.OnClick != null)
                {
                    seg.OnClick();
                }
                return;
            }

            cursorX += segWidth;

            if (!isLast)
            {
                float chevronSize = fontSize * 0.35f;
                cursorX += chevronSize + separatorPad * 3;
            }
        }
    }

    // ── ToggleGroup helpers ───────────────────────────────────────────

    private void HandleToggleGroupClick(IToggleGroup tg)
    {
        var bounds = tg.AbsoluteBounds;
        int count = tg.OptionCount;
        if (count == 0)
        {
            return;
        }

        int clickedIndex = HitTestVariableWidthSegments(
            bounds,
            count,
            i => tg.GetOptionLabel(i),
            lastMousePosition.X);
        tg.SelectIndex(clickedIndex);
    }

    /// <summary>
    /// Hit-tests a control whose segments/buttons are sized proportionally to
    /// label text length plus a uniform horizontal padding. This must match the
    /// layout/paint logic used by SegmentedControl and ToggleGroup.
    /// </summary>
    private static int HitTestVariableWidthSegments(
        Rect bounds,
        int count,
        Func<int, string> getLabel,
        float absoluteX)
    {
        if (count == 0)
        {
            return 0;
        }

        float fontSize = LayoutSolver.BodyFontSize * 0.85f;
        float paddingH = 16f;
        float ratio = 0.55f;
        float[] widths = new float[count];
        float measuredTotal = 0f;
        for (int i = 0; i < count; i++)
        {
            string label = getLabel(i);
            float textW = label.Length * fontSize * ratio;
            float w = textW + paddingH * 2f;
            widths[i] = w;
            measuredTotal += w;
        }

        float scale = measuredTotal > 0f ? bounds.Width / measuredTotal : 1f;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            for (int i = 0; i < count; i++)
            {
                widths[i] *= scale;
            }
        }

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
        var bounds = si.AbsoluteBounds;
        int stepCount = si.Steps.Count;
        if (stepCount == 0)
        {
            return;
        }

        float circleSize = 28f;
        float totalCircleWidth = stepCount * circleSize;
        float availableGap = bounds.Width - totalCircleWidth;
        float gap = stepCount > 1 ? availableGap / (stepCount - 1) : 0;

        float relX = lastMousePosition.X - bounds.X;

        for (int i = 0; i < stepCount; i++)
        {
            float cx = i * (circleSize + gap) + circleSize / 2f;
            float hitRadius = circleSize;
            if (Math.Abs(relX - cx) <= hitRadius)
            {
                if (si.ClickablePredicate == null || si.ClickablePredicate(i))
                {
                    si.StepClickHandler?.Invoke(i);
                }
                return;
            }
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
                            var date = dp.CalendarGridStartDate.AddDays(dp.HighlightedDay);
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

    // ── Calendar (full inline) helpers ────────────────────────────────

    private void HandleCalendarClick(Calendar cal, float x, float y)
    {
        var clickPoint = new Point(x, y);

        // Check navigation buttons first
        if (cal.ShowNavigation)
        {
            if (cal.PrevBounds.Width > 0 && cal.PrevBounds.Contains(clickPoint))
            {
                cal.NavigateMonth(-1);
                RequestRepaint?.Invoke();
                return;
            }

            if (cal.NextBounds.Width > 0 && cal.NextBounds.Contains(clickPoint))
            {
                cal.NavigateMonth(1);
                RequestRepaint?.Invoke();
                return;
            }

            if (cal.TodayBounds.Width > 0 && cal.TodayBounds.Contains(clickPoint))
            {
                cal.GoToToday();
                RequestRepaint?.Invoke();
                return;
            }
        }

        // Check event chip click
        for (int i = 0; i < cal.EventHitZones.Count; i++)
        {
            var (chipBounds, evt) = cal.EventHitZones[i];
            if (chipBounds.Contains(clickPoint))
            {
                cal.OnEventClick?.Invoke(evt);
                return;
            }
        }

        // Check day cell click
        if (y >= cal.GridTop && cal.CellWidth > 0 && cal.CellHeight > 0)
        {
            int col = (int)((x - cal.GridLeft) / cal.CellWidth);
            int row = (int)((y - cal.GridTop) / cal.CellHeight);
            if (col >= 0 && col < 7 && row >= 0 && row < 6)
            {
                int cellIndex = row * 7 + col;
                var date = cal.GridStartDate.AddDays(cellIndex);
                if (date.Month == cal.DisplayedMonth && date.Year == cal.DisplayedYear)
                {
                    cal.SelectedDate = date;
                    cal.OnDayClick?.Invoke(date);
                    if (cal.Date.OnChange is not null)
                    {
                        cal.Date.OnChange(date);
                    }

                    RequestRepaint?.Invoke();
                }
            }
        }
    }

    private void UpdateCalendarInlineHover(Calendar cal, float x, float y)
    {
        if (cal.CellWidth <= 0 || cal.CellHeight <= 0 || y < cal.GridTop)
        {
            if (cal.HighlightedDay != -1)
            {
                cal.HighlightedDay = -1;
                RequestRepaint?.Invoke();
            }

            return;
        }

        int col = (int)((x - cal.GridLeft) / cal.CellWidth);
        int row = (int)((y - cal.GridTop) / cal.CellHeight);
        int cellIndex = (col >= 0 && col < 7 && row >= 0 && row < 6)
            ? row * 7 + col
            : -1;

        if (cellIndex != cal.HighlightedDay)
        {
            cal.HighlightedDay = cellIndex;
            RequestRepaint?.Invoke();
        }
    }

    // ── ColorPicker helpers ───────────────────────────────────────────

    private void HandleColorPickerClick(ColorPicker cp)
    {
        UpdateColorPickerFromDrag(cp, lastMousePosition.X, lastMousePosition.Y);
        RequestRepaint?.Invoke();
    }

    private static void UpdateColorPickerFromDrag(ColorPicker cp, float mouseX, float mouseY)
    {
        var abs = cp.AbsoluteBounds;
        if (abs.Width <= 0 || abs.Height <= 0)
        {
            return;
        }

        // Layout must match PaintColorPicker: pad=8, canvasHeight=130, gap=8, hueBarH=14
        const float pad = 8f;
        float innerX = abs.X + pad;
        float innerY = abs.Y + pad;
        float innerW = abs.Width - pad * 2;
        const float canvasHeight = 130f;
        const float hueBarH = 14f;
        float hueBarY = innerY + canvasHeight + 8f;
        const float tolerance = 6f;

        // Check if click is in the SB canvas region
        if (mouseY >= innerY - tolerance && mouseY <= innerY + canvasHeight + tolerance &&
            mouseX >= innerX && mouseX <= innerX + innerW)
        {
            cp.Saturation = Math.Clamp((mouseX - innerX) / innerW, 0f, 1f);
            cp.Brightness = 1f - Math.Clamp((mouseY - innerY) / canvasHeight, 0f, 1f);
            cp.Value.OnChange(HsbToColor(cp.Hue, cp.Saturation, cp.Brightness));
            return;
        }

        // Check if click is in the hue bar region
        if (mouseY >= hueBarY - tolerance && mouseY <= hueBarY + hueBarH + tolerance &&
            mouseX >= innerX && mouseX <= innerX + innerW)
        {
            float frac = Math.Clamp((mouseX - innerX) / innerW, 0f, 1f);
            cp.Hue = frac * 360f;
            cp.Value.OnChange(HsbToColor(cp.Hue, cp.Saturation, cp.Brightness));
        }
    }

    private static ColorValue HsbToColor(float hue, float saturation, float brightness)
    {
        float c = brightness * saturation;
        float hp = hue / 60f;
        float x = c * (1f - MathF.Abs(hp % 2f - 1f));
        float m = brightness - c;

        float r, g, b;
        if (hp < 1f) { r = c; g = x; b = 0f; }
        else if (hp < 2f) { r = x; g = c; b = 0f; }
        else if (hp < 3f) { r = 0f; g = c; b = x; }
        else if (hp < 4f) { r = 0f; g = x; b = c; }
        else if (hp < 5f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return ColorValue.FromRgba(r + m, g + m, b + m, 1f);
    }

    // ── PinInput helpers ──────────────────────────────────────────────

    private void HandlePinInputClick(PinInput pin)
    {
        var abs = pin.AbsoluteBounds;
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

    private static int ComputeToolBarItemIndex(ToolBar tb, float mouseX)
    {
        var bounds = tb.AbsoluteBounds;
        float relX = mouseX - bounds.X;

        const float buttonSize = 32f;
        const float gap = 4f;
        const float separatorWidth = 12f;

        float x = 0f;
        for (int i = 0; i < tb.Items.Count; i++)
        {
            var item = tb.Items[i];
            if (i > 0)
            {
                x += gap;
            }

            if (item.IsSeparator)
            {
                x += separatorWidth;
                continue;
            }

            if (relX >= x && relX < x + buttonSize)
            {
                return i;
            }

            x += buttonSize;
        }

        return -1;
    }

    private void HandleToolBarClick(ToolBar tb)
    {
        int idx = ComputeToolBarItemIndex(tb, lastMousePosition.X);
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
        var abs = pg.AbsoluteBounds;
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

    private static int ComputePropertyGridRow(PropertyGrid pg, float mouseX, float mouseY)
    {
        var abs = pg.AbsoluteBounds;
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
        const float cellSize = 36f;
        const float spacing = 2f;
        const int columns = 8;
        const float tabHeight = 36f;
        const float pad = 8f;

        var abs = ep.AbsoluteBounds;
        float relX = lastMousePosition.X - abs.X;
        float relY = lastMousePosition.Y - abs.Y;

        // Click on category tab
        if (relY < tabHeight)
        {
            float tabW = (abs.Width - pad * 2f) / EmojiPicker.CategoryIcons.Length;
            int tabIdx = (int)((relX - pad) / tabW);
            if (tabIdx >= 0 && tabIdx < EmojiPicker.CategoryIcons.Length)
            {
                ep.SelectedCategoryIndex = tabIdx;
            }
            return;
        }

        // Click on emoji cell
        float gridY = tabHeight + 4f;
        float gridX = pad;
        int col = (int)((relX - gridX) / (cellSize + spacing));
        int row = (int)((relY - gridY) / (cellSize + spacing));

        if (col >= 0 && col < columns)
        {
            int idx = row * columns + col;
            var emojis = EmojiPicker.EmojiData[ep.SelectedCategoryIndex];
            if (idx >= 0 && idx < emojis.Length)
            {
                ep.OnSelect(emojis[idx]);
            }
        }
    }

    private static int ComputeEmojiPickerHover(EmojiPicker ep, float mouseX, float mouseY)
    {
        const float cellSize = 36f;
        const float spacing = 2f;
        const int columns = 8;
        const float tabHeight = 36f;
        const float pad = 8f;

        var abs = ep.AbsoluteBounds;
        float relX = mouseX - abs.X;
        float relY = mouseY - abs.Y;

        if (relX < 0 || relX > abs.Width || relY < 0 || relY > abs.Height)
        {
            return -1;
        }

        // Hovering over tab area
        if (relY < tabHeight)
        {
            float tabW = (abs.Width - pad * 2f) / EmojiPicker.CategoryIcons.Length;
            int tabIdx = (int)((relX - pad) / tabW);
            if (tabIdx >= 0 && tabIdx < EmojiPicker.CategoryIcons.Length)
            {
                return -(100 + tabIdx);
            }
            return -1;
        }

        // Hovering over emoji grid
        float gridY = tabHeight + 4f;
        float gridX = pad;
        int col = (int)((relX - gridX) / (cellSize + spacing));
        int row = (int)((relY - gridY) / (cellSize + spacing));

        if (col >= 0 && col < columns)
        {
            int idx = row * columns + col;
            var emojis = EmojiPicker.EmojiData[ep.SelectedCategoryIndex];
            if (idx >= 0 && idx < emojis.Length)
            {
                return idx;
            }
        }

        return -1;
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

    private void UpdateSplitButtonDropdownHover(float x, float y)
    {
        if (openSplitButton == null)
        {
            return;
        }

        var dropdownBounds = openSplitButton.DropdownBounds;
        if (dropdownBounds.Width <= 0 || openSplitButton.Items.Count == 0)
        {
            return;
        }

        var point = new Point(x, y);
        if (dropdownBounds.Contains(point))
        {
            // Walk through items to find which one the cursor is over.
            // The 6px offset matches the painter's menuPadV top inset so the
            // highlighted row matches the row the cursor is actually over.
            float itemY = dropdownBounds.Y + 6f;
            float separatorHeight = 9f;

            for (int i = 0; i < openSplitButton.Items.Count; i++)
            {
                var menuItem = openSplitButton.Items[i];
                float currentItemHeight = menuItem.Label == null
                    ? separatorHeight
                    : openSplitButton.MenuItemHeight;

                if (y >= itemY && y < itemY + currentItemHeight)
                {
                    // Don't highlight separators or disabled items
                    int newIndex = (menuItem.Label != null && !menuItem.Disabled) ? i : -1;
                    if (openSplitButton.HighlightedIndex != newIndex)
                    {
                        openSplitButton.HighlightedIndex = newIndex;
                        RequestRepaint?.Invoke();
                    }
                    return;
                }

                itemY += currentItemHeight;
            }
        }

        if (openSplitButton.HighlightedIndex != -1)
        {
            openSplitButton.HighlightedIndex = -1;
            RequestRepaint?.Invoke();
        }
    }

    private void UpdateMenuBarHover(float x, float y)
    {
        if (openMenuBar == null)
        {
            return;
        }

        var point = new Point(x, y);
        var mb = openMenuBar;

        // Check if hovering over a menu label (switch menus)
        if (mb.IsOpen)
        {
            for (int i = 0; i < mb.MenuLabelBounds.Length; i++)
            {
                if (mb.MenuLabelBounds[i].Contains(point))
                {
                    if (mb.HoveredMenuIndex != i)
                    {
                        mb.HoveredMenuIndex = i;
                        // Switch open menu when hovering a different label
                        if (i != mb.OpenMenuIndex)
                        {
                            mb.OpenMenu(i);
                        }
                        RequestRepaint?.Invoke();
                    }
                    return;
                }
            }
        }

        // Check if hovering in the dropdown
        var dropBounds = mb.DropdownBounds;
        if (mb.IsOpen && dropBounds.Width > 0 && dropBounds.Contains(point))
        {
            var menu = mb.Menus[mb.OpenMenuIndex];
            float itemY = dropBounds.Y + 4f; // top padding
            float separatorHeight = 9f;
            float headerHeight = 24f;

            for (int i = 0; i < menu.Items.Count; i++)
            {
                var item = menu.Items[i];
                float currentH;
                if (item.Label == null && item.CustomContent == Node.Empty)
                {
                    currentH = separatorHeight;
                }
                else if (!item.Enabled && item.OnClick == null && item.ToggleValue.OnChange is null && item.Items == null)
                {
                    currentH = headerHeight;
                }
                else
                {
                    currentH = mb.MenuItemHeight;
                }

                if (y >= itemY && y < itemY + currentH)
                {
                    int newIndex = item.Enabled ? i : -1;
                    if (mb.HighlightedItemIndex != newIndex)
                    {
                        mb.HighlightedItemIndex = newIndex;
                        RequestRepaint?.Invoke();
                    }
                    return;
                }

                itemY += currentH;
            }
        }

        // Clear highlights when outside
        bool changed = false;
        if (mb.HoveredMenuIndex != -1)
        {
            mb.HoveredMenuIndex = -1;
            changed = true;
        }
        if (mb.HighlightedItemIndex != -1)
        {
            mb.HighlightedItemIndex = -1;
            changed = true;
        }
        if (changed)
        {
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
                // Append typed character if it's a printable key
                char? ch = KeyToChar(evt.Key, evt.Modifiers);
                if (ch.HasValue)
                {
                    openCombobox.SearchText += ch.Value;
                    openCombobox.HighlightedIndex = -1;
                    openCombobox.ScrollOffset = 0;
                    RequestRepaint?.Invoke();
                }

                break;
            }
        }
    }

    private static char? KeyToChar(Key key, ModifierKeys modifiers)
    {
        bool shift = modifiers.HasFlag(ModifierKeys.Shift);

        // Letters A-Z
        if (key >= Key.A && key <= Key.Z)
        {
            char c = (char)('a' + (key - Key.A));
            return shift ? char.ToUpperInvariant(c) : c;
        }

        // Digits 0-9 — shifted digits produce symbols on US QWERTY
        if (key >= Key.D0 && key <= Key.D9)
        {
            if (shift)
            {
                return key switch
                {
                    Key.D0 => ')',
                    Key.D1 => '!',
                    Key.D2 => '@',
                    Key.D3 => '#',
                    Key.D4 => '$',
                    Key.D5 => '%',
                    Key.D6 => '^',
                    Key.D7 => '&',
                    Key.D8 => '*',
                    Key.D9 => '(',
                    _ => null,
                };
            }

            return (char)('0' + (key - Key.D0));
        }

        // Common punctuation — shifted variants
        return key switch
        {
            Key.Space => ' ',
            Key.Period => shift ? '>' : '.',
            Key.Comma => shift ? '<' : ',',
            Key.Minus => shift ? '_' : '-',
            Key.Semicolon => shift ? ':' : ';',
            Key.Slash => shift ? '?' : '/',
            Key.Backtick => shift ? '~' : '`',
            Key.LeftBracket => shift ? '{' : '[',
            Key.RightBracket => shift ? '}' : ']',
            Key.Backslash => shift ? '|' : '\\',
            Key.Quote => shift ? '\"' : '\'',
            Key.Equals => shift ? '+' : '=',
            _ => null,
        };
    }

    private static void InvokeContextMenu(Node node)
    {
        // Check Button-specific context menu
        if (node is Button { OnContextMenuHandler: not null } btn)
        {
            btn.OnContextMenuHandler();
            return;
        }

        InvokeGesture(node, g => g.ContextMenu);
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
        if (node is Button or TextInput or TextArea or PasswordInput or
            PinInput or MentionInput or TagInput or ColorPicker or
            Checkbox or Toggle or Slider or
            LinkButton or IconButton ||
            IsNumberInput(node))
        {
            return node;
        }

        return null;
    }

    /// <summary>
    /// Computes and applies a new slider value based on the current drag X position.
    /// Uses the delta from the drag-start X and the slider's rendered width to determine
    /// how far along the track the pointer is, then maps that to the value range.
    /// </summary>
    private void UpdateSliderFromDrag(Slider slider, float currentX)
    {
        float deltaX = currentX - sliderDragStartX;
        const float thumbWidthEstimate = 20f;
        float trackWidth = Math.Max(1f, slider.LayoutData.Bounds.Width - thumbWidthEstimate);
        if (trackWidth <= 0)
        {
            return;
        }

        float range = slider.Max - slider.Min;
        float valueDelta = deltaX * range / trackWidth;
        float newValue = sliderDragStartValue + valueDelta;
        newValue = Math.Clamp(newValue, slider.Min, slider.Max);

        // Snap to step if specified
        if (slider.Step.HasValue && slider.Step.Value > 0)
        {
            float step = slider.Step.Value;
            newValue = MathF.Round(newValue / step) * step;
            newValue = Math.Clamp(newValue, slider.Min, slider.Max);
        }

        slider.Bind.OnChange(newValue);
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

    private static PointerEventArgs CreatePointerArgs(Node node, NativeMouseEvent evt)
    {
        var bounds = node.LayoutData.Bounds;
        return new PointerEventArgs
        {
            Position = new Point(evt.X - bounds.X, evt.Y - bounds.Y),
            Delta = Point.Zero
        };
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

    private static void HandleTimePickerPopupClick(TimePicker tp, float x, float y)
    {
        var clickPoint = new Point(x, y);

        if (tp.HourUpBounds.Width > 0 && tp.HourUpBounds.Contains(clickPoint))
        {
            tp.AdjustHour(1);
            return;
        }

        if (tp.HourDownBounds.Width > 0 && tp.HourDownBounds.Contains(clickPoint))
        {
            tp.AdjustHour(-1);
            return;
        }

        if (tp.MinuteUpBounds.Width > 0 && tp.MinuteUpBounds.Contains(clickPoint))
        {
            tp.AdjustMinute(1);
            return;
        }

        if (tp.MinuteDownBounds.Width > 0 && tp.MinuteDownBounds.Contains(clickPoint))
        {
            tp.AdjustMinute(-1);
            return;
        }

        if (tp.Format == TimeFormat.Hour12 && tp.AmPmBounds.Width > 0 && tp.AmPmBounds.Contains(clickPoint))
        {
            tp.ToggleAmPm();
        }
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
        if (evt.Character == '\b' || evt.Key == Key.Backspace)
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
        if (evt.Character == '\b' || evt.Key == Key.Backspace)
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

        // Printable character input
        if (evt.Character is char ch && ch >= ' ')
        {
            if (MentionInputSelectionAnchor != MentionInputCaretIndex)
            {
                DeleteMentionSelection(mi);
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
