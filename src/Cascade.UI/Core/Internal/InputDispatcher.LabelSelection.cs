namespace Cascade.UI;

/// <summary>
/// Text selection in selectable labels (<see cref="Label.Selectable"/>): a press starts a
/// selection (a double-click selects a word, a triple-click the whole text), dragging extends it,
/// Ctrl+C copies it, and a press anywhere else clears it. The selection follows its label across
/// re-renders while the text stays the same.
/// </summary>
internal sealed partial class InputDispatcher
{
    private static Label? selectedLabel;
    private static int labelSelectionAnchor;
    private static int labelSelectionCaret;

    // A press started a label selection and the button is still down.
    private bool labelSelectionActive;

    /// <summary>The selected text range of <paramref name="label"/>, or an empty range.</summary>
    internal static (int Start, int End) LabelSelectionRange(Label label)
    {
        return ReferenceEquals(label, selectedLabel)
            ? (Math.Min(labelSelectionAnchor, labelSelectionCaret), Math.Max(labelSelectionAnchor, labelSelectionCaret))
            : (0, 0);
    }

    /// <summary>The selected label's selected text, or null when nothing is selected.</summary>
    internal static string? SelectedLabelText()
    {
        if (selectedLabel is null)
        {
            return null;
        }
        (int start, int end) = LabelSelectionRange(selectedLabel);
        string text = LabelText(selectedLabel);
        end = Math.Min(end, text.Length);
        return end > start ? text[start..end] : null;
    }

    private Label? FindSelectableLabelAt(float x, float y)
        => rootNode is null ? null : HitTester.FindSelectableLabelAt(rootNode, x, y);

    /// <summary>
    /// A left press over a selectable label starts selecting in it and consumes the press; any
    /// other press clears the selection and is handled as usual.
    /// </summary>
    private bool TryBeginLabelSelection(NativeMouseEvent evt)
    {
        if (evt.Button != NativeMouseButton.Left)
        {
            return false;
        }

        var label = FindSelectableLabelAt(evt.X, evt.Y);
        if (label is null || LabelOffsetAt(label, evt.X, evt.Y) is not { } offset)
        {
            ClearLabelSelection();
            return false;
        }

        string text = LabelText(label);
        Label? previous = selectedLabel;
        selectedLabel = label;
        if (evt.ClickCount >= 3)
        {
            labelSelectionAnchor = 0;
            labelSelectionCaret = text.Length;
        }
        else if (evt.ClickCount == 2)
        {
            (labelSelectionAnchor, labelSelectionCaret) = WordAt(text, offset);
        }
        else
        {
            labelSelectionAnchor = offset;
            labelSelectionCaret = offset;
        }
        labelSelectionActive = true;
        MarkLabelSelectionChanged(previous);
        MarkLabelSelectionChanged(label);
        return true;
    }

    /// <summary>While the button is held after a press in a label, the selection follows the pointer.</summary>
    private bool TryExtendLabelSelection(NativeMouseEvent evt)
    {
        if (!labelSelectionActive || selectedLabel is null)
        {
            return false;
        }
        if (LabelOffsetAt(selectedLabel, evt.X, evt.Y) is { } offset && offset != labelSelectionCaret)
        {
            labelSelectionCaret = offset;
            MarkLabelSelectionChanged(selectedLabel);
        }
        return true;
    }

    /// <summary>Ends a selection drag; the selection stays.</summary>
    private bool TryEndLabelSelection()
    {
        if (!labelSelectionActive)
        {
            return false;
        }
        labelSelectionActive = false;
        isMouseDown = false;
        return true;
    }

    /// <summary>Ctrl+C with a label selection copies it (when no text field has focus).</summary>
    private bool TryCopyLabelSelection(NativeKeyEvent evt)
    {
        if (evt.Key != Key.C || !evt.Modifiers.HasFlag(ModifierKeys.Ctrl)
            || FocusManager.FocusedElement is TextInput or TextArea
            || SelectedLabelText() is not { } selected)
        {
            return false;
        }
        _ = Clipboard.WriteTextAsync(selected);
        return true;
    }

    private void ClearLabelSelection()
    {
        if (selectedLabel is null)
        {
            return;
        }
        var label = selectedLabel;
        selectedLabel = null;
        labelSelectionAnchor = 0;
        labelSelectionCaret = 0;
        MarkLabelSelectionChanged(label);
    }

    /// <summary>The reconciler replaced a label: the selection moves to the new one if the text is unchanged.</summary>
    private static void TransferLabelSelection(Node oldNode, Node newNode)
    {
        if (!ReferenceEquals(oldNode, selectedLabel))
        {
            return;
        }
        selectedLabel = newNode is Label { IsSelectable: true } replacement && LabelText(replacement) == LabelText((Label)oldNode)
            ? replacement
            : null;
    }

    /// <summary>The text offset nearest the window point in the label, as it was drawn.</summary>
    private int? LabelOffsetAt(Label label, float x, float y)
    {
        if (rootNode is null || !HitTester.TryGetAbsoluteBounds(rootNode, label, out var bounds))
        {
            return null;
        }
        // The painter draws a label inside its padding (NodePainter's content bounds).
        var padding = label.LayoutData.Padding;
        var drawn = NodePainter.LabelTextLayout(label,
            new Rect(0f, 0f, bounds.Width - padding.Horizontal, bounds.Height - padding.Vertical),
            ThemeSwitcher.Current.Typography.Scale.Body, LayoutSolver.DefaultFontPath);
        if (drawn is not { } text)
        {
            return null;
        }
        var hit = text.Layout.HitTest(x - bounds.X - padding.Left - text.X, y - bounds.Y - padding.Top - text.Y);
        return Math.Clamp(hit.Offset, 0, LabelText(label).Length);
    }

    /// <summary>The word (letters and digits, or a run of other non-space characters) around an offset.</summary>
    private static (int Start, int End) WordAt(string text, int offset)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }
        int at = Math.Clamp(offset, 0, text.Length - 1);
        if (at > 0 && at == text.Length - 1 && char.IsWhiteSpace(text[at]))
        {
            at--;
        }
        static int Kind(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '_' ? 0 : char.IsWhiteSpace(c) ? 1 : 2;
        int kind = Kind(text[at]);
        int start = at;
        int end = at + 1;
        while (start > 0 && Kind(text[start - 1]) == kind)
        {
            start--;
        }
        while (end < text.Length && Kind(text[end]) == kind)
        {
            end++;
        }
        return (start, end);
    }

    private static string LabelText(Label label) => label.Text ?? label.LocText.Resolve();

    // The highlight changed: repaint, and recapture any scroll layer the label sits in.
    private void MarkLabelSelectionChanged(Label? label)
    {
        if (label is not null && rootNode is not null)
        {
            MarkScrollViewLayersDirty(rootNode, label);
        }
        RequestRepaint?.Invoke();
    }
}
