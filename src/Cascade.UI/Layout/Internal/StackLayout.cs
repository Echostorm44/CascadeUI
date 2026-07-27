namespace Cascade.UI;

/// <summary>
/// Layout algorithm for <see cref="Stack"/>. Lays all children on top of
/// each other in z-order. Stack sizes to its largest child, and children
/// are positioned according to their <see cref="Alignment"/> modifier.
/// </summary>
internal static class StackLayout
{
    /// <summary>
    /// Measures and positions children in a stack overlay layout.
    /// Returns the content size (max width × max height of children).
    /// </summary>
    internal static Size Measure(
        IReadOnlyList<Node> children,
        LayoutConstraints constraints)
    {
        if (children.Count == 0)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        float maxChildWidth = 0;
        float maxChildHeight = 0;

        // Phase 1: Measure all children with the same constraints
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var allocSize = LayoutSolver.MeasureChild(child, constraints);
            maxChildWidth = Math.Max(maxChildWidth, allocSize.Width);
            maxChildHeight = Math.Max(maxChildHeight, allocSize.Height);
        }

        // Stack's size is the maximum of all children, clamped to constraints
        float ownWidth = constraints.ConstrainWidth(maxChildWidth);
        float ownHeight = constraints.ConstrainHeight(maxChildHeight);

        // Phase 2: Position children based on alignment
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var margin = child.LayoutData.Margin;
            var childSize = child.LayoutData.MeasuredSize;
            float totalChildW = childSize.Width + margin.Horizontal;
            float totalChildH = childSize.Height + margin.Vertical;

            var alignment = child.LayoutData.NodeAlignment ?? Alignment.TopLeft;

            // Alignment X: -1 = left, 0 = center, 1 = right
            float freeX = ownWidth - totalChildW;
            float x = freeX * ((alignment.X + 1f) / 2f);

            // Alignment Y: -1 = top, 0 = center, 1 = bottom
            float freeY = ownHeight - totalChildH;
            float y = freeY * ((alignment.Y + 1f) / 2f);

            LayoutSolver.PositionChild(child, x, y);
        }

        return new Size(ownWidth, ownHeight);
    }
}
