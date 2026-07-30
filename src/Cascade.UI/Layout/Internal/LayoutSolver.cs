namespace Cascade.UI;

/// <summary>
/// Core layout algorithm. Takes a node tree and root constraints, recursively
/// measures and positions all nodes in a single depth-first pass.
/// Constraints flow down, sizes flow up.
/// </summary>
internal static class LayoutSolver
{
    /// <summary>
    /// The default font path used for text measurement during layout.
    /// Set by the FrameOrchestrator before each layout pass. When null,
    /// text controls use character-count estimation instead of real font metrics.
    /// </summary>
    internal static string? DefaultFontPath { get; set; }

    /// <summary>
    /// Semibold font path for measuring button labels and other bold text.
    /// Falls back to <see cref="DefaultFontPath"/> when null.
    /// </summary>
    internal static string? SemiBoldFontPath { get; set; }

    /// <summary>
    /// Per-side horizontal padding for buttons, from the current theme's ButtonTheme.
    /// Default 12 matches FluentTheme; AppleTheme uses 16.
    /// </summary>
    internal static float ButtonPaddingH { get; set; } = 12f;

    /// <summary>
    /// Minimum button height from the current theme's ButtonTheme.
    /// </summary>
    internal static float ButtonMinHeight { get; set; } = 32f;

    /// <summary>
    /// Whether the current theme uses semibold text for buttons.
    /// </summary>
    internal static bool ButtonUseSemiBold { get; set; } = true;

    /// <summary>
    /// Body text font size from the current theme's typography scale.
    /// Used for checkbox labels, radio button labels, toggle labels, etc.
    /// Default 14 matches FluentTheme; AppleTheme uses 17.
    /// </summary>
    internal static float BodyFontSize { get; set; } = 14f;

    /// <summary>
    /// Card content padding from the current theme's CardTheme.Padding.
    /// Default 16 matches typical Md spacing.
    /// </summary>
    internal static float CardPadding { get; set; } = 16f;

    /// <summary>
    /// Font size for button labels from the current theme's ButtonTheme.TextStyle.
    /// Default 14 matches FluentTheme Body1Strong; AppleTheme uses 17 (Body).
    /// </summary>
    internal static float ButtonFontSize { get; set; } = 14f;

    /// <summary>Heading 1 font size from the current theme's typography scale.</summary>
    internal static float H1FontSize { get; set; } = 28f;

    /// <summary>Heading 2 font size from the current theme's typography scale.</summary>
    internal static float H2FontSize { get; set; } = 22f;

    /// <summary>Heading 3 font size from the current theme's typography scale.</summary>
    internal static float H3FontSize { get; set; } = 17f;

    // Average character width ratio relative to font size for estimation fallback.
    // 0.55 is a reasonable average for proportional Latin fonts.
    private const float AverageCharWidthRatio = 0.55f;

    // Default line height multiplier when not specified by the TextStyle.
    private const float DefaultLineHeightMultiplier = 1.4f;
    /// <summary>
    /// Measures a node within the given constraints and positions its children.
    /// Returns the node's own size (not including margin).
    /// The caller is responsible for setting this node's Bounds position.
    /// </summary>
    internal static Size Measure(Node node, LayoutConstraints constraints)
    {
        var data = node.LayoutData;

        if (node.IsLayoutEmpty)
        {
            data.MeasuredSize = Size.Zero;
            data.Bounds = default;
            return Size.Zero;
        }

        // Apply explicit sizing modifiers to constraints
        constraints = ApplySizingModifiers(constraints, data);

        // Apply aspect ratio constraint
        constraints = ApplyAspectRatio(constraints, data);

        // Compute inner constraints (after subtracting padding)
        var padding = data.Padding;
        var innerConstraints = ShrinkConstraints(constraints, padding);

        // Measure content based on node type
        var contentSize = MeasureContent(node, innerConstraints);

        // Add padding back to get node size
        var desiredSize = new Size(
            contentSize.Width + padding.Horizontal,
            contentSize.Height + padding.Vertical);

        // Constrain to the modified constraints
        var finalSize = constraints.Constrain(desiredSize);

        data.MeasuredSize = finalSize;

        // Offset the content-level baseline by the top padding so it's
        // relative to the node's top edge (needed for baseline alignment).
        if (!float.IsNaN(data.FirstBaseline))
        {
            data.FirstBaseline += padding.Top;
        }

        return finalSize;
    }

    /// <summary>
    /// Measures a child node accounting for its margin, returning the total
    /// allocation size (node size + margin). Also stores the measured size
    /// on the child's LayoutData.
    /// </summary>
    internal static Size MeasureChild(Node child, LayoutConstraints constraints)
    {
        var margin = child.LayoutData.Margin;
        var innerConstraints = ShrinkConstraints(constraints, margin);
        var childSize = Measure(child, innerConstraints);
        return new Size(
            childSize.Width + margin.Horizontal,
            childSize.Height + margin.Vertical);
    }

    /// <summary>
    /// Positions a child at the given coordinates, accounting for its margin.
    /// Sets the child's Bounds on its LayoutData.
    /// </summary>
    internal static void PositionChild(Node child, float x, float y)
    {
        var margin = child.LayoutData.Margin;
        child.LayoutData.Bounds = new Rect(
            x + margin.Left,
            y + margin.Top,
            child.LayoutData.MeasuredSize.Width,
            child.LayoutData.MeasuredSize.Height);
    }

    /// <summary>
    /// Performs full layout on a root node: measure + position at origin.
    /// </summary>
    internal static void PerformLayout(Node root, LayoutConstraints constraints)
    {
        InputDispatcher.SplitViewLayoutCounter = 0;
        var size = Measure(root, constraints);
        root.LayoutData.Bounds = new Rect(0, 0, size.Width, size.Height);
    }

    private static Size MeasureContent(Node node, LayoutConstraints constraints)
    {
        return node switch
        {
            Row row => FlexLayout.Measure(
                row.Children, GetEffectiveSpacing(row.Spacing, node),
                row.MainAxisAlignment, row.CrossAxisAlignment,
                Orientation.Horizontal, constraints),

            Column col => FlexLayout.Measure(
                col.Children, GetEffectiveSpacing(col.Spacing, node),
                col.MainAxisAlignment, col.CrossAxisAlignment,
                Orientation.Vertical, constraints),

            Stack stack => StackLayout.Measure(stack.Children, constraints),

            Grid grid => GridLayout.Measure(
                grid.Children, grid.Columns,
                GetEffectiveSpacing(grid.Spacing, node), constraints),

            Center center => CenterLayout(center.Child, constraints),

            Spacer spacer => SpacerLayout(spacer, constraints),

            Label lbl => MeasureLabel(lbl, constraints),

            Button btn => MeasureButton(btn, constraints),

            Checkbox cb => MeasureCheckbox(cb, constraints),

            Slider slider => MeasureSlider(slider, constraints),

            ISelectNode select => MeasureSelect(select, constraints),

            IMultiSelectNode ms => MeasureMultiSelect(ms, constraints),

            IComboboxNode cb => MeasureCombobox(cb, constraints),

            SplitButton sb => MeasureSplitButton(sb, constraints),

            IRadioGroup rg => RadioGroupLayout(rg, constraints),

            IRadioButton => MeasureRadioButton(node, constraints),

            Toggle toggle => MeasureToggle(toggle, constraints),

            TextInput textInput => MeasureTextInput(textInput, constraints),

            ScrollView scrollView => ScrollViewLayout(scrollView, constraints),

            ProgressBar pb => MeasureProgressBar(pb, constraints),

            LinkButton lb => MeasureLinkButton(lb, constraints),

            Badge badge => MeasureBadge(badge, constraints),

            Rating rating => MeasureRating(rating, constraints),

            Spinner spinner => MeasureSpinner(spinner, constraints),

            Separator sep => MeasureSeparator(sep, constraints),

            Card card => MeasureCard(card, constraints),

            IconButton ib => MeasureIconButton(ib, constraints),

            IconView iv => MeasureIconView(iv, constraints),

            Accordion acc => MeasureAccordion(acc, constraints),

            Expander exp => MeasureExpander(exp, constraints),

            Tag tag => MeasureTag(tag, constraints),

            Avatar av => MeasureAvatar(av, constraints),

            ProgressRing pr => MeasureProgressRing(pr, constraints),

            ISegmentedControl sc => MeasureSegmentedControl(sc, constraints),

            Breadcrumb bc => MeasureBreadcrumb(bc, constraints),

            INumberInput ni => MeasureNumberInput(ni, constraints),

            Gauge gauge => MeasureGauge(gauge, constraints),

            StepIndicator si => MeasureStepIndicator(si, constraints),

            FormValidator fv => MeasureFormValidator(fv, constraints),

            KeyHandler kh => MeasureSingleChildWrapper(kh.Content, constraints),

            AnimatePresence ap when ap.IsVisible => MeasureSingleChildWrapper(ap.Child, constraints),
            AnimatePresence => Size.Zero,

            IToggleGroup tg => MeasureToggleGroup(tg, constraints),

            Banner banner => MeasureBanner(banner, constraints),

            Sparkline spark => MeasureSparkline(spark, constraints),

            RangeSlider rs => MeasureRangeSlider(rs, constraints),

            DonutGauge dg => MeasureDonutGauge(dg, constraints),

            Timeline tl => MeasureTimeline(tl, constraints),

            ColorPicker cp => MeasureColorPicker(cp, constraints),

            PinInput pin => MeasurePinInput(pin, constraints),

            StatusBar sb => MeasureStatusBar(sb, constraints),

            ToolBar tb => MeasureToolBar(tb, constraints),

            MenuBar mb => MeasureMenuBar(mb, constraints),

            PropertyGrid pg => MeasurePropertyGrid(pg, constraints),
            NotificationBell => new Size(constraints.ConstrainWidth(36f), constraints.ConstrainHeight(36f)),
            EmojiPicker => MeasureEmojiPicker(constraints),
            QrCode qr => new Size(constraints.ConstrainWidth(qr.QrSize), constraints.ConstrainHeight(qr.QrSize)),
            Barcode bc => new Size(constraints.ConstrainWidth(bc.BarcodeWidth), constraints.ConstrainHeight(bc.BarcodeHeight)),

            BarChart barChart => MeasureBarChart(barChart, constraints),

            PieChart pieChart => MeasurePieChart(pieChart, constraints),

            LineChart lineChart => MeasureLineChart(lineChart, constraints),

            AreaChart areaChart => MeasureAreaChart(areaChart, constraints),

            HeatMapChart => MeasureHeatMapChart(constraints),

            TreeMapChart => MeasureTreeMapChart(constraints),

            WaterfallChart => MeasureWaterfallChart(constraints),

            ScatterPlot => MeasureScatterPlot(constraints),

            ITreeView tv => MeasureTreeView(tv, constraints),

            PasswordInput pwd => MeasurePasswordInput(pwd, constraints),

            Image img => MeasureImage(img, constraints),

            SplitView sv => MeasureSplitView(sv, constraints),

            TextArea ta => MeasureTextArea(ta, constraints),

            DatePicker dp => MeasureDatePicker(dp, constraints),

            DateTimePicker dtp => MeasureDateTimePicker(dtp, constraints),

            TimePicker tp => MeasureTimePicker(tp, constraints),

            MonthPicker mp => MeasureMonthPicker(mp, constraints),

            TagInput ti => MeasureTagInput(ti, constraints),

            MentionInput mi => MeasureMentionInput(mi, constraints),

            Markdown md => MeasureMarkdown(md, constraints),

            CommandPalette => new Size(0, 0),

            DateRangePicker drp => MeasureDateRangePicker(drp, constraints),

            Calendar cal => MeasureCalendar(cal, constraints),

            ITabularDataNode tdn => MeasureTabularData(tdn, constraints),

            IListViewNode lvn => MeasureListView(lvn, constraints),

            CanvasNode canvas => MeasureCanvas(canvas, constraints),

            // Navigation transition host — measure the incoming page at full size
            NavigationTransitionHost nth => MeasureNavigationTransition(nth, constraints),

            // Component nodes (e.g. Navigator) — measure through to their rendered tree
            Component comp when comp.RenderedTree is not null => MeasureComponentContent(comp, constraints),

            _ => LeafLayout(constraints)
        };
    }

    private static float GetEffectiveSpacing(float constructorSpacing, Node node)
    {
        return node.LayoutData.SpacingOverride ?? constructorSpacing;
    }

    private static Size CenterLayout(Node child, LayoutConstraints constraints)
    {
        // Pass loose constraints to child
        var looseConstraints = LayoutConstraints.Loose(
            new Size(constraints.MaxWidth, constraints.MaxHeight));

        var childAllocSize = MeasureChild(child, looseConstraints);

        // Center fills available space (or child size if unbounded)
        float ownWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? childAllocSize.Width
            : constraints.MaxWidth;
        float ownHeight = float.IsPositiveInfinity(constraints.MaxHeight)
            ? childAllocSize.Height
            : constraints.MaxHeight;

        // Position child at center
        float childX = (ownWidth - childAllocSize.Width) / 2;
        float childY = (ownHeight - childAllocSize.Height) / 2;
        PositionChild(child, childX, childY);

        return new Size(
            constraints.ConstrainWidth(ownWidth),
            constraints.ConstrainHeight(ownHeight));
    }

    private static Size RadioGroupLayout(IRadioGroup rg, LayoutConstraints constraints)
    {
        // RadioGroup is a pass-through container — measure and position Content at (0,0)
        var childAllocSize = MeasureChild(rg.Content, constraints);
        PositionChild(rg.Content, 0, 0);
        return new Size(
            constraints.ConstrainWidth(childAllocSize.Width),
            constraints.ConstrainHeight(childAllocSize.Height));
    }

    private static Size SpacerLayout(Spacer spacer, LayoutConstraints constraints)
    {
        // Spacer size depends on context (flex distribution handles it).
        // Return minimum size along both axes.
        float minMain = spacer.MinSize;
        return new Size(
            constraints.ConstrainWidth(minMain),
            constraints.ConstrainHeight(0));
    }

    private static Size LeafLayout(LayoutConstraints constraints)
    {
        // Unknown leaf nodes take their minimum constrained size.
        // Explicit sizing comes from modifiers (Width/Height) already applied
        // to the constraints before this point.
        return new Size(constraints.MinWidth, constraints.MinHeight);
    }

    /// <summary>
    /// Measures a Component node by delegating to its rendered tree.
    /// Components are transparent layout wrappers — they take the size of
    /// their rendered content and position that content at (0,0).
    /// </summary>
    private static Size MeasureComponentContent(Component comp, LayoutConstraints constraints)
    {
        var size = MeasureChild(comp.RenderedTree!, constraints);
        PositionChild(comp.RenderedTree!, 0, 0);
        return size;
    }

    /// <summary>
    /// Measures a NavigationTransitionHost. Only the incoming page needs
    /// measurement — the outgoing tree is already laid out from its last
    /// render and is painted at its existing positions with a transform.
    /// </summary>
    private static Size MeasureNavigationTransition(NavigationTransitionHost nth, LayoutConstraints constraints)
    {
        // Measure the incoming page at full available size
        if (nth.IncomingPage is not null && !nth.IncomingPage.IsLayoutEmpty)
        {
            MeasureChild(nth.IncomingPage, constraints);
            PositionChild(nth.IncomingPage, 0, 0);
        }

        // The transition host fills all available space
        float w = float.IsPositiveInfinity(constraints.MaxWidth) ? 0 : constraints.MaxWidth;
        float h = float.IsPositiveInfinity(constraints.MaxHeight) ? 0 : constraints.MaxHeight;
        return new Size(w, h);
    }

    /// <summary>
    /// Measures a PageHost. Lays out the content and (when the bar is visible)
    /// a navigation bar above it. For now, we measure just the content.
    /// </summary>
    private static Size MeasurePageHost(PageHost page, LayoutConstraints constraints)
    {
        // For NavigationBarStyle.Hidden or Transparent, content fills the full area.
        // For Default, reserve space for the nav bar at the top.
        float barHeight = 0;
        if (page.BarStyle == NavigationBarStyle.Default)
        {
            barHeight = page.LargeTitle ? 52 : 44;
        }

        if (page.Content is not null && !page.Content.IsLayoutEmpty)
        {
            var contentConstraints = new LayoutConstraints(
                0, constraints.MaxWidth,
                0, Math.Max(0, constraints.MaxHeight - barHeight));
            MeasureChild(page.Content, contentConstraints);
            PositionChild(page.Content, 0, barHeight);
        }

        float w = float.IsPositiveInfinity(constraints.MaxWidth) ? 0 : constraints.MaxWidth;
        float h = float.IsPositiveInfinity(constraints.MaxHeight) ? 0 : constraints.MaxHeight;
        return new Size(w, h);
    }

    private static Size MeasureCanvas(CanvasNode canvas, LayoutConstraints constraints)
    {
        // Size.Fill (infinity) means "fill all available space."
        // Otherwise use the explicitly requested size, clamped to constraints.
        float w = float.IsPositiveInfinity(canvas.RequestedSize.Width)
            ? constraints.MaxWidth
            : canvas.RequestedSize.Width;
        float h = float.IsPositiveInfinity(canvas.RequestedSize.Height)
            ? constraints.MaxHeight
            : canvas.RequestedSize.Height;

        // If max constraints are also infinite (unconstrained parent), fall back to 0.
        if (float.IsPositiveInfinity(w))
        {
            w = 0;
        }
        if (float.IsPositiveInfinity(h))
        {
            h = 0;
        }

        return new Size(
            constraints.ConstrainWidth(w),
            constraints.ConstrainHeight(h));
    }

    /// <summary>
    /// Measures a Label by computing its text dimensions. Uses the TextLayoutEngine
    /// for real font metrics when a font is available, otherwise falls back to
    /// character-count estimation.
    /// </summary>
    private static Size MeasureLabel(Label lbl, LayoutConstraints constraints)
    {
        string text = lbl.Text ?? lbl.LocText.Resolve();
        if (string.IsNullOrEmpty(text))
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        float fontSize = lbl.TextStyleOverride?.Size ?? BodyFontSize;
        float lineHeight = lbl.TextStyleOverride?.LineHeight ?? (fontSize * DefaultLineHeightMultiplier);

        Size textSize;

        if (DefaultFontPath != null)
        {
            // Use weight-specific font path when the label has bold/semibold styling
            string fontPath = DefaultFontPath;
            var weight = lbl.TextStyleOverride?.Weight ?? FontWeight.Regular;
            if (weight is not (FontWeight.Regular or FontWeight.None)
                && SemiBoldFontPath != null)
            {
                fontPath = SemiBoldFontPath;
            }

            // Real text measurement with HarfBuzz shaping
            var options = new TextLayoutOptions
            {
                FontPath = fontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : constraints.MaxWidth,
                MaxLines = lbl.MaxLineCount ?? 0,
            };
            var result = TextLayoutEngine.Layout(text, options);
            textSize = result.BoundingBox;

            // Store the first line's baseline for CrossAxisAlignment.Baseline
            if (result.Lines.Count > 0)
            {
                lbl.LayoutData.FirstBaseline = result.Lines[0].Baseline;
            }
        }
        else
        {
            // Estimation fallback: average character width × count
            float estimatedWidth = text.Length * fontSize * AverageCharWidthRatio;
            float maxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                ? estimatedWidth
                : Math.Min(estimatedWidth, constraints.MaxWidth);

            // Estimate line count for wrapping
            int lineCount = maxWidth > 0 && estimatedWidth > maxWidth
                ? (int)Math.Ceiling(estimatedWidth / maxWidth)
                : 1;
            float totalHeight = lineCount * lineHeight;

            textSize = new Size(
                lineCount > 1 ? maxWidth : estimatedWidth,
                totalHeight);
        }

        // Add 2px horizontal padding to account for glyph overhang (negative
        // left bearings / right overhangs) that the advance-based bounding box
        // does not include. Prevents clipped edges on glyphs like J, f, etc.
        const float GlyphOverhangPadding = 2f;
        return new Size(
            constraints.ConstrainWidth(textSize.Width + GlyphOverhangPadding),
            constraints.ConstrainHeight(textSize.Height));
    }

    /// <summary>
    /// Measures a Button by computing its label text size plus standard padding.
    /// Buttons have a minimum size to ensure they're tappable.
    /// </summary>
    private static Size MeasureButton(Button btn, LayoutConstraints constraints)
    {
        string text = btn.Label.Resolve();
        float fontSize = btn.StyleOverride?.Size ?? ButtonFontSize;
        float lineHeight = btn.StyleOverride?.LineHeight ?? (fontSize * DefaultLineHeightMultiplier);

        // Use theme-aware padding (per-side × 2 for total).
        float horizontalPadding = ButtonPaddingH * 2f;
        float verticalPadding = 12f;

        float textWidth;
        float textHeight;

        // Use semibold font for measurement when the theme specifies it
        string? fontPath = (ButtonUseSemiBold ? SemiBoldFontPath : null) ?? DefaultFontPath;
        if (fontPath != null && !string.IsNullOrEmpty(text))
        {
            var options = new TextLayoutOptions
            {
                FontPath = fontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : Math.Max(0, constraints.MaxWidth - horizontalPadding),
            };
            var result = TextLayoutEngine.Layout(text, options);
            textWidth = result.BoundingBox.Width;
            textHeight = result.BoundingBox.Height;
        }
        else
        {
            textWidth = (text?.Length ?? 0) * fontSize * AverageCharWidthRatio;
            textHeight = lineHeight;
        }

        // Include icon width if present
        float iconWidth = btn.Icon != default ? fontSize + 8f : 0f;

        float desiredWidth = Math.Max(64f, textWidth + iconWidth + horizontalPadding);
        float desiredHeight = Math.Max(ButtonMinHeight, textHeight + verticalPadding);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    /// <summary>
    /// Measures a SplitButton: primary label zone + divider + arrow zone.
    /// </summary>
    private static Size MeasureSplitButton(SplitButton sb, LayoutConstraints constraints)
    {
        string text = sb.Label.Resolve();
        float fontSize = ButtonFontSize;
        float lineHeight = fontSize * DefaultLineHeightMultiplier;
        float horizontalPadding = ButtonPaddingH * 2f;
        float verticalPadding = 12f;
        float arrowZoneWidth = 36f;

        float textWidth;
        float textHeight;

        string? fontPath = (ButtonUseSemiBold ? SemiBoldFontPath : null) ?? DefaultFontPath;
        if (fontPath != null && !string.IsNullOrEmpty(text))
        {
            var options = new TextLayoutOptions
            {
                FontPath = fontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : Math.Max(0, constraints.MaxWidth - horizontalPadding - arrowZoneWidth),
            };
            var result = TextLayoutEngine.Layout(text, options);
            textWidth = result.BoundingBox.Width;
            textHeight = result.BoundingBox.Height;
        }
        else
        {
            textWidth = (text?.Length ?? 0) * fontSize * AverageCharWidthRatio;
            textHeight = lineHeight;
        }

        float iconWidth = sb.Icon != default ? fontSize + 8f : 0f;
        float primaryWidth = Math.Max(64f, textWidth + iconWidth + horizontalPadding);
        float desiredWidth = primaryWidth + arrowZoneWidth;
        float desiredHeight = Math.Max(ButtonMinHeight, textHeight + verticalPadding);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureSlider(Slider slider, LayoutConstraints constraints)
    {
        // Slider: default 200px wide (or explicit width), 32px tall
        float desiredWidth = slider.WidthValue ?? 200f;
        float desiredHeight = 32f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureCheckbox(Checkbox cb, LayoutConstraints constraints)
    {
        string labelText = cb.Label.Resolve();

        // Checkbox box size + gap + text label (mirrors MeasureRadioButton)
        const float boxSize = 18f;
        const float gap = 8f;
        float fontSize = BodyFontSize;
        float lineHeight = fontSize * DefaultLineHeightMultiplier;

        float textWidth;
        float textHeight;

        if (DefaultFontPath != null && !string.IsNullOrEmpty(labelText))
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : Math.Max(0, constraints.MaxWidth - boxSize - gap),
            };
            var result = TextLayoutEngine.Layout(labelText, options);
            textWidth = result.BoundingBox.Width;
            textHeight = result.BoundingBox.Height;
        }
        else
        {
            textWidth = (labelText?.Length ?? 0) * fontSize * AverageCharWidthRatio;
            textHeight = lineHeight;
        }

        float desiredWidth = boxSize + gap + textWidth;
        float desiredHeight = Math.Max(boxSize, textHeight);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    // Card-style radio inner padding (logical px), shared by layout and painter.
    internal const float RadioCardPadding = 14f;

    private static Size MeasureRadioButton(Node node, LayoutConstraints constraints)
    {
        var rb = (IRadioButton)node;

        // Radio circle size + gap + label
        const float circleSize = 22f;
        const float gap = 8f;

        // Rich node label (multi-line content, pricing rows, cards) — measured and
        // positioned as a laid-out child to the right of the circle.
        if (!rb.NodeLabel.IsLayoutEmpty)
        {
            return MeasureRadioButtonNodeLabel(rb, constraints, circleSize, gap);
        }

        string labelText = rb.LabelText;
        float fontSize = BodyFontSize;
        float lineHeight = fontSize * DefaultLineHeightMultiplier;

        float textWidth;
        float textHeight;

        if (DefaultFontPath != null && !string.IsNullOrEmpty(labelText))
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = float.PositiveInfinity,
            };
            var result = TextLayoutEngine.Layout(labelText, options);
            textWidth = result.BoundingBox.Width;
            textHeight = result.BoundingBox.Height;
        }
        else
        {
            textWidth = (labelText?.Length ?? 0) * fontSize * AverageCharWidthRatio;
            textHeight = lineHeight;
        }

        float desiredWidth = circleSize + gap + textWidth;
        float desiredHeight = Math.Max(circleSize, textHeight);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    /// <summary>
    /// Measures a radio button carrying a rich node label. The label is laid out
    /// as a child to the right of the circle. Card-style radios add inner padding
    /// and fill the available width like a block; default node radios hug content.
    /// </summary>
    private static Size MeasureRadioButtonNodeLabel(
        IRadioButton rb, LayoutConstraints constraints, float circleSize, float gap)
    {
        bool card = rb.Style == RadioStyle.Card;
        float padding = card ? RadioCardPadding : 0f;
        float reserved = padding * 2 + circleSize + gap;

        // A card stretches to the available width (like a form field); a default
        // node radio takes only the width its content needs.
        bool fillWidth = card && !float.IsPositiveInfinity(constraints.MaxWidth);
        float outerWidth = fillWidth ? constraints.MaxWidth : float.PositiveInfinity;

        float labelMaxWidth = float.IsPositiveInfinity(outerWidth)
            ? (float.IsPositiveInfinity(constraints.MaxWidth)
                ? float.PositiveInfinity
                : Math.Max(0f, constraints.MaxWidth - reserved))
            : Math.Max(0f, outerWidth - reserved);

        var labelConstraints = new LayoutConstraints(0f, labelMaxWidth, 0f, float.PositiveInfinity);
        var labelSize = MeasureChild(rb.NodeLabel, labelConstraints);

        float contentHeight = Math.Max(circleSize, labelSize.Height);
        float totalHeight = contentHeight + padding * 2;
        float totalWidth = float.IsPositiveInfinity(outerWidth)
            ? reserved + labelSize.Width
            : outerWidth;

        float labelX = padding + circleSize + gap;
        float labelY = padding + (contentHeight - labelSize.Height) / 2f;
        PositionChild(rb.NodeLabel, labelX, labelY);

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureToggle(Toggle toggle, LayoutConstraints constraints)
    {
        // Toggle track: 51×31 (Apple standard) or 40×20 (compact)
        const float trackWidth = 51f;
        const float trackHeight = 31f;
        const float gap = 8f;
        float fontSize = BodyFontSize;
        float lineHeight = fontSize * DefaultLineHeightMultiplier;

        string labelText = toggle.Label.Resolve();

        if (string.IsNullOrEmpty(labelText))
        {
            return new Size(
                constraints.ConstrainWidth(trackWidth),
                constraints.ConstrainHeight(trackHeight));
        }

        float textWidth;
        float textHeight;

        if (DefaultFontPath != null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : Math.Max(0, constraints.MaxWidth - trackWidth - gap),
            };
            var result = TextLayoutEngine.Layout(labelText, options);
            textWidth = result.BoundingBox.Width;
            textHeight = result.BoundingBox.Height;
        }
        else
        {
            textWidth = labelText.Length * fontSize * AverageCharWidthRatio;
            textHeight = lineHeight;
        }

        float desiredWidth = trackWidth + gap + textWidth;
        float desiredHeight = Math.Max(trackHeight, textHeight);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureTextInput(TextInput textInput, LayoutConstraints constraints)
    {
        // TextInput: reasonable default width (280px), fixed height 36px.
        // Does not expand to fill the container — that would break centered layouts.
        const float defaultWidth = 280f;
        const float height = 36f;

        float desiredWidth = Math.Min(defaultWidth, constraints.MaxWidth);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureSelect(ISelectNode select, LayoutConstraints constraints)
    {
        // Select: default 200px wide, 36px tall (matching theme Height)
        float desiredWidth = 200f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureMultiSelect(IMultiSelectNode ms, LayoutConstraints constraints)
    {
        // MultiSelect: wider default to accommodate pills, same height as Select
        float desiredWidth = 280f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureCombobox(IComboboxNode cb, LayoutConstraints constraints)
    {
        float desiredWidth = 280f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureDatePicker(DatePicker dp, LayoutConstraints constraints)
    {
        // DatePicker trigger: wide enough for the calendar popup to align nicely.
        // Calendar is 7*32 + 24 = 248px; trigger should be similar width.
        float desiredWidth = 248f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureDateTimePicker(DateTimePicker dtp, LayoutConstraints constraints)
    {
        // Wider than DatePicker to show both date and time in the trigger
        float desiredWidth = 300f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureTimePicker(TimePicker tp, LayoutConstraints constraints)
    {
        // Narrower trigger — only displays time, no date
        float desiredWidth = 180f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureMonthPicker(MonthPicker mp, LayoutConstraints constraints)
    {
        // Same width as DatePicker — displays "April 2026" etc.
        float desiredWidth = 220f;
        float desiredHeight = 36f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureTagInput(TagInput ti, LayoutConstraints constraints)
    {
        float desiredWidth = 320f;
        float width = constraints.ConstrainWidth(desiredWidth);
        float minHeight = 40f;

        // Match painter constants
        float padding = 8f;
        float tagH = 24f;
        float tagGap = 6f;
        float tagPadH = 8f;
        float removeW = 16f;
        float fontSize = 12f;
        float inputMinW = 80f;
        float avgCharW = fontSize * 0.6f; // approximate character width

        float maxX = width - padding;
        float curX = padding;
        int rows = 1;

        var tags = ti.CurrentTags;
        for (int i = 0; i < tags.Count; i++)
        {
            float textW = tags[i].Length * avgCharW;
            float pillW = tagPadH + textW + 6f + removeW + tagPadH;

            if (curX + pillW > maxX && curX > padding + 1f)
            {
                curX = padding;
                rows++;
            }
            curX += pillW + tagGap;
        }

        // Account for input area — if it doesn't fit on current row, wrap
        if (curX + inputMinW > maxX && tags.Count > 0)
        {
            rows++;
        }

        float desiredHeight = Math.Max(minHeight, padding * 2 + rows * (tagH + tagGap) - tagGap);

        return new Size(width, constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureMentionInput(MentionInput mi, LayoutConstraints constraints)
    {
        // Same dimensions as TextInput: 280px wide, 36px tall.
        const float defaultWidth = 280f;
        const float height = 36f;

        float desiredWidth = Math.Min(defaultWidth, constraints.MaxWidth);

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureMarkdown(Markdown md, LayoutConstraints constraints)
    {
        var blocks = md.GetParsedBlocks();
        if (blocks.Count == 0)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        float maxWidth = float.IsPositiveInfinity(constraints.MaxWidth) ? 600f : constraints.MaxWidth;
        float totalHeight = 0f;
        float widestBlock = 0f;
        float bodySize = BodyFontSize;
        const float blockSpacing = 8f;
        const float codeBlockPadding = 12f;
        const float listIndent = 24f;
        const float blockQuotePadding = 16f;
        const float blockQuoteBorderWidth = 3f;

        foreach (var block in blocks)
        {
            if (totalHeight > 0)
            {
                totalHeight += blockSpacing;
            }

            switch (block.Type)
            {
                case MarkdownBlockType.Heading:
                    float headingSize = block.HeadingLevel switch
                    {
                        1 => H1FontSize,
                        2 => H2FontSize,
                        3 => H3FontSize,
                        _ => bodySize + 1f,
                    };
                    float headingHeight = EstimateTextHeight(block.Text, headingSize, maxWidth);
                    totalHeight += headingHeight + 4f;
                    widestBlock = Math.Max(widestBlock, EstimateTextWidth(block.Text, headingSize, maxWidth));
                    break;

                case MarkdownBlockType.Paragraph:
                    float paraHeight = EstimateTextHeight(block.Text, bodySize, maxWidth);
                    totalHeight += paraHeight;
                    widestBlock = Math.Max(widestBlock, EstimateTextWidth(block.Text, bodySize, maxWidth));
                    break;

                case MarkdownBlockType.CodeBlock:
                    float codeSize = bodySize - 1f;
                    float codeHeight = EstimateCodeBlockHeight(block.Text, codeSize, 1.5f) + codeBlockPadding * 2;
                    if (block.Language != null)
                    {
                        codeHeight += 20f;
                    }
                    totalHeight += codeHeight;
                    widestBlock = Math.Max(widestBlock, maxWidth);
                    break;

                case MarkdownBlockType.BulletList:
                case MarkdownBlockType.OrderedList:
                    if (block.Items != null)
                    {
                        foreach (string item in block.Items)
                        {
                            float itemHeight = EstimateTextHeight(item, bodySize, maxWidth - listIndent);
                            totalHeight += itemHeight;
                        }
                    }
                    widestBlock = Math.Max(widestBlock, maxWidth);
                    break;

                case MarkdownBlockType.BlockQuote:
                    float quoteHeight = EstimateTextHeight(block.Text, bodySize, maxWidth - blockQuotePadding - blockQuoteBorderWidth);
                    totalHeight += quoteHeight + 8f;
                    widestBlock = Math.Max(widestBlock, maxWidth);
                    break;

                case MarkdownBlockType.HorizontalRule:
                    totalHeight += 17f;
                    widestBlock = Math.Max(widestBlock, maxWidth);
                    break;
            }
        }

        float resultWidth = Math.Max(widestBlock, constraints.MinWidth);
        return new Size(
            constraints.ConstrainWidth(resultWidth),
            constraints.ConstrainHeight(totalHeight));
    }

    private static float EstimateTextHeight(string text, float fontSize, float maxWidth)
    {
        float lineHeight = fontSize * DefaultLineHeightMultiplier;
        if (string.IsNullOrEmpty(text))
        {
            return lineHeight;
        }

        if (DefaultFontPath != null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = maxWidth,
            };
            var result = TextLayoutEngine.Layout(text, options);
            return result.BoundingBox.Height;
        }

        float estimatedWidth = text.Length * fontSize * AverageCharWidthRatio;
        int lineCount = maxWidth > 0 && estimatedWidth > maxWidth
            ? (int)Math.Ceiling(estimatedWidth / maxWidth) : 1;
        return lineCount * lineHeight;
    }

    private static float EstimateTextWidth(string text, float fontSize, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        float estimatedWidth = text.Length * fontSize * AverageCharWidthRatio;
        return Math.Min(estimatedWidth, maxWidth);
    }

    private static float EstimateCodeBlockHeight(string code, float fontSize, float lineHeightMultiplier = 0f)
    {
        float multiplier = lineHeightMultiplier > 0 ? lineHeightMultiplier : DefaultLineHeightMultiplier;
        if (string.IsNullOrEmpty(code))
        {
            return fontSize * multiplier;
        }

        int lineCount = 1;
        foreach (char c in code)
        {
            if (c == '\n')
            {
                lineCount++;
            }
        }

        return lineCount * fontSize * multiplier;
    }

    private static Size MeasureDateRangePicker(DateRangePicker drp, LayoutConstraints constraints)
    {
        // SingleField: one wide field. TwoFields: two side-by-side fields.
        // A TwoFields field with a caption stacks a small label over the value, so it
        // needs more height than a single-line field or the value clips out of the box.
        bool stackedLabels = drp.Layout == DateRangeLayout.TwoFields
            && (!string.IsNullOrEmpty(drp.StartLabel.Resolve())
                || !string.IsNullOrEmpty(drp.EndLabel.Resolve()));
        float desiredHeight = stackedLabels ? 54f : 36f;
        float desiredWidth = drp.Layout == DateRangeLayout.TwoFields ? 320f : 280f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureCalendar(Calendar cal, LayoutConstraints constraints)
    {
        // Full inline calendar: navigation header + day headers + 6 rows of day cells
        // with room for event chips in each cell.
        const float padding = 16f;
        const float navHeight = 40f;
        const float dayHeaderHeight = 28f;
        const int cols = 7;
        const int rows = 6;
        const float cellHeight = 80f; // tall cells for event chips

        float cellWidth = 0;
        float availWidth = constraints.MaxWidth;
        if (float.IsFinite(availWidth) && availWidth > 0)
        {
            cellWidth = (availWidth - padding * 2) / cols;
        }
        else
        {
            cellWidth = 100f;
        }

        float desiredWidth = cellWidth * cols + padding * 2;
        float desiredHeight = padding + navHeight + dayHeaderHeight + cellHeight * rows + padding;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureTabularData(ITabularDataNode tdn, LayoutConstraints constraints)
    {
        float rowHeight = tdn.GetRowHeight();
        float headerHeight = rowHeight + 4f; // header slightly taller
        const float groupHeaderHeight = 32f;
        const float filterRowHeight = 28f;

        float totalHeight = headerHeight;
        if (tdn.HasFilterRow)
        {
            totalHeight += filterRowHeight;
        }

        if (tdn.IsGrouped)
        {
            for (int g = 0; g < tdn.GroupCount; g++)
            {
                totalHeight += groupHeaderHeight;
                if (!tdn.IsGroupCollapsed(g))
                {
                    int groupRowCount = tdn.GetGroupRowCount(g);
                    totalHeight += groupRowCount * rowHeight;

                    // Add expanded row detail heights within this group
                    if (tdn.HasRowDetail)
                    {
                        for (int r = 0; r < groupRowCount; r++)
                        {
                            int dataRow = tdn.GetGroupDataRowIndex(g, r);
                            if (tdn.IsRowExpanded(dataRow))
                            {
                                totalHeight += tdn.GetRowDetailHeight(dataRow);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            totalHeight += tdn.RowCount * rowHeight;

            // Add expanded row detail heights
            if (tdn.HasRowDetail)
            {
                for (int r = 0; r < tdn.RowCount; r++)
                {
                    if (tdn.IsRowExpanded(r))
                    {
                        totalHeight += tdn.GetRowDetailHeight(r);
                    }
                }
            }
        }

        // Minimum height when all rows filtered out
        if (tdn.HasActiveFilter && tdn.RowCount == 0)
        {
            totalHeight += 60f;
        }

        // Aggregate row height
        if (tdn.HasAggregateRow)
        {
            totalHeight += tdn.GetAggregateRowHeight();
        }

        // Sum column widths or use available width
        float availWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? 600f
            : constraints.MaxWidth;
        float totalWidth = 0f;
        for (int i = 0; i < tdn.ColumnCount; i++)
        {
            totalWidth += tdn.GetColumnWidth(i, availWidth);
        }

        // Reserve space for column chooser button so it doesn't overlay columns
        if (tdn.IsColumnChooserEnabled)
        {
            totalWidth += 28f; // chooserBtnSize (24) + margin (4)
        }

        // Cap height when MaxVisibleRows is set (enables internal scroll)
        if (tdn.MaxVisibleRows is int maxRows)
        {
            float maxDataHeight = maxRows * rowHeight;
            float fixedHeight = headerHeight
                + (tdn.HasFilterRow ? filterRowHeight : 0)
                + (tdn.HasAggregateRow ? tdn.GetAggregateRowHeight() : 0);
            float cappedHeight = fixedHeight + maxDataHeight;
            totalHeight = Math.Min(totalHeight, cappedHeight);
        }

        return new Size(
            constraints.ConstrainWidth(MathF.Max(totalWidth, 200f)),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureListView(IListViewNode lvn, LayoutConstraints constraints)
    {
        bool boundedHeight = !float.IsPositiveInfinity(constraints.MaxHeight);

        // Width the built rows will occupy — the swipe-open row reads this to size
        // its sliding surface. Keep the last known width when unconstrained.
        if (!float.IsPositiveInfinity(constraints.MaxWidth))
        {
            lvn.ContentWidth = constraints.MaxWidth;
        }

        // Virtualized path: a flat list with a fixed item height and a bounded
        // viewport. The list owns its scroll offset and builds only the on-screen
        // slice, so cost is bounded by the viewport, not by the item count.
        if (lvn.CanVirtualize && boundedHeight)
        {
            float ih = lvn.GetItemHeight();
            float viewport = constraints.MaxHeight;

            lvn.ViewportHeight = viewport;
            lvn.MaxY = MathF.Max(0f, (lvn.ItemCount * ih) - viewport);
            lvn.OffsetY = Math.Clamp(lvn.OffsetY, 0f, lvn.MaxY);

            lvn.InvalidateContent();
            Node slice = lvn.GetContentNode();

            var sliceConstraints = new LayoutConstraints(
                constraints.MinWidth, constraints.MaxWidth, 0, float.PositiveInfinity);
            MeasureChild(slice, sliceConstraints);
            PositionChild(slice, 0, lvn.ContentOffsetY);

            float vw = float.IsPositiveInfinity(constraints.MaxWidth)
                ? slice.LayoutData.MeasuredSize.Width
                : constraints.MaxWidth;

            return new Size(constraints.ConstrainWidth(vw), constraints.ConstrainHeight(viewport));
        }

        // Non-virtualized: build all rows and report full content height (a wrapping
        // ScrollView / fixed Height clips it; sections & auto-height lists use this).
        lvn.ViewportHeight = 0f;
        lvn.InvalidateContent();
        Node content = lvn.GetContentNode();

        var contentConstraints = new LayoutConstraints(
            constraints.MinWidth, constraints.MaxWidth, 0, float.PositiveInfinity);
        Size contentSize = MeasureChild(content, contentConstraints);
        PositionChild(content, 0, 0);

        float width = float.IsPositiveInfinity(constraints.MaxWidth)
            ? contentSize.Width
            : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(MathF.Max(contentSize.Height, 1f)));
    }

    /// <summary>
    /// Measures a ScrollView: content is measured with unconstrained height (vertical scroll),
    /// and the ScrollView itself fills the available viewport.
    /// </summary>
    private static Size ScrollViewLayout(ScrollView sv, LayoutConstraints constraints)
    {
        // Viewport fills available space
        float vpWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? 0 : constraints.MaxWidth;
        float vpHeight = float.IsPositiveInfinity(constraints.MaxHeight)
            ? 0 : constraints.MaxHeight;

        // Measure content with same width but unconstrained height (vertical scroll)
        var contentConstraints = new LayoutConstraints(
            constraints.MinWidth, constraints.MaxWidth,
            0, float.PositiveInfinity);
        var contentSize = MeasureChild(sv.Content, contentConstraints);

        // Apply horizontal alignment when content is narrower than viewport
        float contentX = 0;
        var alignment = sv.Content.LayoutData.NodeAlignment;
        if (alignment != null && vpWidth > 0)
        {
            float contentWidth = sv.Content.LayoutData.MeasuredSize.Width
                + sv.Content.LayoutData.Margin.Horizontal;
            float freeX = Math.Max(0, vpWidth - contentWidth);
            contentX = freeX * ((alignment.Value.X + 1f) / 2f);
        }
        PositionChild(sv.Content, contentX, 0);

        // Use content width if viewport is unbounded
        if (vpWidth == 0)
        {
            vpWidth = contentSize.Width;
        }

        if (vpHeight == 0)
        {
            vpHeight = contentSize.Height;
        }

        // Store max scroll extent and clamp current offset on the ScrollView instance
        float maxScrollY = Math.Max(0, contentSize.Height - vpHeight);
        sv.MaxY = maxScrollY;
        sv.OffsetY = Math.Clamp(sv.OffsetY, 0f, maxScrollY);

        // Keep global in sync for backward compatibility (last-laid-out wins)
        InputDispatcher.ScrollViewMaxY = maxScrollY;
        InputDispatcher.ScrollViewOffsetY = sv.OffsetY;

        return new Size(
            constraints.ConstrainWidth(vpWidth),
            constraints.ConstrainHeight(vpHeight));
    }

    private static Size MeasureProgressBar(ProgressBar pb, LayoutConstraints constraints)
    {
        // ProgressBar: default 200px wide, 20px tall (bar is centered vertically)
        float desiredWidth = 200f;
        float desiredHeight = 20f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureLinkButton(LinkButton lb, LayoutConstraints constraints)
    {
        string text = lb.Label.Resolve();
        if (string.IsNullOrEmpty(text))
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        float fontSize = BodyFontSize;

        if (DefaultFontPath != null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
                    ? float.PositiveInfinity
                    : constraints.MaxWidth,
            };
            var result = TextLayoutEngine.Layout(text, options);
            return new Size(
                constraints.ConstrainWidth(result.BoundingBox.Width),
                constraints.ConstrainHeight(result.BoundingBox.Height));
        }

        float estimatedWidth = text.Length * fontSize * AverageCharWidthRatio;
        float lineHeight = fontSize * DefaultLineHeightMultiplier;
        return new Size(
            constraints.ConstrainWidth(estimatedWidth),
            constraints.ConstrainHeight(lineHeight));
    }

    private static Size MeasureBadge(Badge badge, LayoutConstraints constraints)
    {
        // Badge is a decorator — measure the child, then return the child's size.
        // The badge indicator overlaps without adding to layout size.
        var childSize = Measure(badge.Child, constraints);
        badge.Child.LayoutData.Bounds = new Rect(0, 0, childSize.Width, childSize.Height);
        return childSize;
    }

    private static Size MeasureFormValidator(FormValidator fv, LayoutConstraints constraints) =>
        MeasureSingleChildWrapper(fv.Content, constraints);

    private static Size MeasureSingleChildWrapper(Node child, LayoutConstraints constraints)
    {
        var childSize = Measure(child, constraints);
        child.LayoutData.Bounds = new Rect(0, 0, childSize.Width, childSize.Height);
        return childSize;
    }

    private static Size MeasureRating(Rating rating, LayoutConstraints constraints)
    {
        // Each star is iconSize × iconSize, with gap between them.
        float iconSize = rating.SizeValue ?? 24f;
        float gap = 4f;
        int max = rating.Max;

        float desiredWidth = (iconSize * max) + (gap * (max - 1));
        float desiredHeight = iconSize;

        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureSpinner(Spinner spinner, LayoutConstraints constraints)
    {
        float size = spinner.SpinnerSize ?? 32f;
        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureSeparator(Separator sep, LayoutConstraints constraints)
    {
        if (sep.SeparatorOrientation == Orientation.Horizontal)
        {
            // Horizontal separator: fill available width, thickness tall
            float width = float.IsPositiveInfinity(constraints.MaxWidth)
                ? 200f
                : constraints.MaxWidth;
            return new Size(
                constraints.ConstrainWidth(width),
                constraints.ConstrainHeight(sep.Thickness));
        }
        else
        {
            // Vertical separator: thickness wide, fill available height
            float height = float.IsPositiveInfinity(constraints.MaxHeight)
                ? 200f
                : constraints.MaxHeight;
            return new Size(
                constraints.ConstrainWidth(sep.Thickness),
                constraints.ConstrainHeight(height));
        }
    }

    private static Size MeasureCard(Card card, LayoutConstraints constraints)
    {
        float pad = card.PaddingOverride?.Left ?? CardPadding;
        float padH = pad * 2f;
        float padV = pad * 2f;

        float innerMaxWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? float.PositiveInfinity
            : Math.Max(0, constraints.MaxWidth - padH);
        float innerMaxHeight = float.IsPositiveInfinity(constraints.MaxHeight)
            ? float.PositiveInfinity
            : Math.Max(0, constraints.MaxHeight - padV);

        // Card content fills the card's available width — this is standard card
        // behavior in every design system (Material, Apple HIG, Bootstrap).
        // Without tight width, children like empty placeholder Columns measure to 0.
        float innerMinWidth = float.IsPositiveInfinity(innerMaxWidth) ? 0 : innerMaxWidth;
        var innerConstraints = new LayoutConstraints(innerMinWidth, innerMaxWidth, 0, innerMaxHeight);

        float y = pad;
        float contentWidth = 0f;

        // Measure and position child slots vertically: Media → Header → Content → Footer
        void MeasureSlot(Node slot, bool fullBleed)
        {
            if (slot.IsLayoutEmpty)
            {
                return;
            }

            float fullBleedMin = float.IsPositiveInfinity(constraints.MaxWidth) ? 0 : constraints.MaxWidth;
            var slotConstraints = fullBleed
                ? new LayoutConstraints(fullBleedMin, constraints.MaxWidth, 0, innerMaxHeight)
                : innerConstraints;

            var size = Measure(slot, slotConstraints);
            float slotX = fullBleed ? 0 : pad;
            slot.LayoutData.Bounds = new Rect(slotX, y, size.Width, size.Height);
            y += size.Height;
            contentWidth = Math.Max(contentWidth, fullBleed ? size.Width : size.Width + padH);
        }

        MeasureSlot(card.Media, fullBleed: true);
        MeasureSlot(card.Header, fullBleed: false);
        MeasureSlot(card.Content, fullBleed: false);
        MeasureSlot(card.Footer, fullBleed: false);

        y += pad;

        float totalWidth = Math.Max(contentWidth, padH);
        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(y));
    }

    private static Size MeasureIconButton(IconButton ib, LayoutConstraints constraints)
    {
        // An explicit Size pins the footprint (fixed tap target). Otherwise the
        // button sizes to the glyph: setting IconSize alone grows the whole
        // button with it (glyph at the default half-footprint proportion), so
        // "make the icon bigger" stays a one-number change. Falls back to 40.
        float size = ib.Size
            ?? (ib.IconSizeOverride is float glyph ? glyph * 2f : 40f);
        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureIconView(IconView iv, LayoutConstraints constraints)
    {
        float size = iv.RequestedSize > 0 ? iv.RequestedSize : 24f;
        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureAccordion(Accordion acc, LayoutConstraints constraints)
    {
        float y = 0;
        float maxWidth = 0;

        foreach (var section in acc.Sections)
        {
            var sectionSize = MeasureExpander(section, constraints);
            section.LayoutData.Bounds = new Rect(0, y, sectionSize.Width, sectionSize.Height);
            y += sectionSize.Height;
            maxWidth = Math.Max(maxWidth, sectionSize.Width);
        }

        return new Size(
            constraints.ConstrainWidth(maxWidth),
            constraints.ConstrainHeight(y));
    }

    private static Size MeasureExpander(Expander exp, LayoutConstraints constraints)
    {
        float headerHeight = 40f;
        float y = 0;

        // Measure header
        if (!exp.HeaderNode.IsLayoutEmpty)
        {
            var headerSize = Measure(exp.HeaderNode, constraints);
            headerHeight = Math.Max(headerHeight, headerSize.Height);
            exp.HeaderNode.LayoutData.Bounds = new Rect(0, 0, headerSize.Width, headerHeight);
        }
        y += headerHeight;

        // Measure content only if expanded
        bool isExpanded = exp.IsExpanded;

        if (isExpanded && !exp.Content.IsLayoutEmpty)
        {
            var contentConstraints = new LayoutConstraints(
                0, constraints.MaxWidth, 0,
                float.IsPositiveInfinity(constraints.MaxHeight)
                    ? float.PositiveInfinity
                    : Math.Max(0, constraints.MaxHeight - y));
            var contentSize = Measure(exp.Content, contentConstraints);
            exp.Content.LayoutData.Bounds = new Rect(0, y, contentSize.Width, contentSize.Height);
            y += contentSize.Height;
        }

        float width = float.IsPositiveInfinity(constraints.MaxWidth)
            ? 300f
            : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(y));
    }

    private static Size MeasureTag(Tag tag, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize * 0.85f;
        float paddingH = 16f;
        float paddingV = 5f;
        float xIconWidth = 10f;
        float xGap = 6f;

        // Generous per-char estimate — must never underestimate
        float textWidth = tag.Label.Length * fontSize * 0.75f;
        float textHeight = fontSize * DefaultLineHeightMultiplier;

        float totalWidth = paddingH + textWidth + paddingH;
        if (tag.OnRemove != null)
        {
            totalWidth = paddingH + textWidth + xGap + xIconWidth + paddingH;
        }

        float totalHeight = textHeight + paddingV * 2f;

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureAvatar(Avatar av, LayoutConstraints constraints)
    {
        float size = av.CustomSize ?? av.SizePreset switch
        {
            AvatarSize.Xs  => 20f,
            AvatarSize.Sm  => 28f,
            AvatarSize.Md  => 36f,
            AvatarSize.Lg  => 48f,
            AvatarSize.Xl  => 64f,
            AvatarSize.Xxl => 96f,
            _              => 36f
        };

        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureProgressRing(ProgressRing pr, LayoutConstraints constraints)
    {
        float size = pr.SizeOverride ?? 48f;
        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureSegmentedControl(ISegmentedControl sc, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize * 0.85f;
        float paddingH = 16f;
        float segmentHeight = BodyFontSize * DefaultLineHeightMultiplier + 12f;

        float totalWidth = 0f;
        for (int i = 0; i < sc.SegmentCount; i++)
        {
            string label = sc.GetSegmentLabel(i);
            float textWidth = label.Length * fontSize * AverageCharWidthRatio;
            totalWidth += textWidth + paddingH * 2;
        }

        // Add 2px border allowance
        totalWidth += 4f;

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(segmentHeight));
    }

    private static Size MeasureBreadcrumb(Breadcrumb bc, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize;
        float separatorWidth = fontSize * 0.8f;
        float paddingH = 4f;

        float totalWidth = 0f;
        for (int i = 0; i < bc.Segments.Count; i++)
        {
            float segTextWidth = bc.Segments[i].Label.Length * fontSize * AverageCharWidthRatio;
            totalWidth += segTextWidth + paddingH * 2;
            if (i < bc.Segments.Count - 1)
            {
                totalWidth += separatorWidth + paddingH * 2;
            }
        }

        float height = fontSize * DefaultLineHeightMultiplier + 8f;
        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureNumberInput(INumberInput ni, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize;
        float valueWidth = Math.Max(ni.DisplayValue.Length, 3) * fontSize * AverageCharWidthRatio;
        float buttonWidth = 28f;
        float paddingH = 8f;
        float height = fontSize * DefaultLineHeightMultiplier + 12f;

        float totalWidth;
        if (ni.StepperPos == StepperPosition.Split)
        {
            totalWidth = buttonWidth + paddingH + valueWidth + paddingH + buttonWidth;
        }
        else if (ni.StepperPos == StepperPosition.None)
        {
            totalWidth = paddingH + valueWidth + paddingH;
        }
        else
        {
            totalWidth = paddingH + valueWidth + paddingH + buttonWidth;
        }

        return new Size(
            constraints.ConstrainWidth(Math.Max(totalWidth, 100f)),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureGauge(Gauge gauge, LayoutConstraints constraints)
    {
        float size = 80f;
        if (gauge.GaugeDisplayStyle == GaugeStyle.Semi)
        {
            return new Size(
                constraints.ConstrainWidth(size),
                constraints.ConstrainHeight(size * 0.6f));
        }

        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureStepIndicator(StepIndicator si, LayoutConstraints constraints)
    {
        int stepCount = si.Steps.Count;
        if (stepCount == 0)
        {
            return new Size(0, 0);
        }

        float circleSize = 28f;
        float gap = 40f;
        float labelHeight = BodyFontSize * 1.4f;
        float totalWidth = stepCount * circleSize + (stepCount - 1) * gap;
        float totalHeight = circleSize + 6f + labelHeight;

        return new Size(
            constraints.ConstrainWidth(Math.Max(totalWidth, 100f)),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureToggleGroup(IToggleGroup tg, LayoutConstraints constraints)
    {
        int count = tg.OptionCount;
        if (count == 0)
        {
            return new Size(0, 0);
        }

        float buttonHeight = 36f;
        float paddingH = 24f; // ~8px more per side than the old 16 so labels aren't cramped
        float fontSize = BodyFontSize * 0.85f;
        float totalWidth = 0;

        for (int i = 0; i < count; i++)
        {
            string label = tg.GetOptionLabel(i);
            float textWidth = label.Length * fontSize * 0.55f;
            totalWidth += textWidth + paddingH * 2;
        }

        return new Size(
            constraints.ConstrainWidth(Math.Max(totalWidth, 80f)),
            constraints.ConstrainHeight(buttonHeight));
    }

    /// <summary>
    /// Measures a Banner by computing the space needed for icon badge + message + dismiss.
    /// The Banner's padding is handled by the outer Measure(); this measures inner content.
    /// Constraints are already post-padding (shrunk by the outer Measure).
    /// </summary>
    private static Size MeasureBanner(Banner banner, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize;
        float iconSize = MathF.Round(fontSize * 1.6f);
        float spacing = 10f;
        float dismissSize = banner.OnDismiss != null ? iconSize : 0f;
        float dismissSpacing = banner.OnDismiss != null ? spacing : 0f;

        // Available width for message text (after icon and dismiss)
        float nonTextWidth = iconSize + spacing + dismissSpacing + dismissSize;
        float availableTextWidth = float.IsPositiveInfinity(constraints.MaxWidth)
            ? float.PositiveInfinity
            : MathF.Max(0f, constraints.MaxWidth - nonTextWidth);

        // Measure message text with constrained width for proper line wrapping
        float messageWidth;
        float messageHeight;
        if (DefaultFontPath != null)
        {
            var options = new TextLayoutOptions
            {
                FontPath = DefaultFontPath,
                FontSize = fontSize,
                MaxWidth = availableTextWidth,
            };
            var result = TextLayoutEngine.Layout(banner.Message, options);
            messageWidth = result.BoundingBox.Width;
            messageHeight = result.BoundingBox.Height;
        }
        else
        {
            messageWidth = MathF.Min(banner.Message.Length * fontSize * 0.55f, availableTextWidth);
            messageHeight = fontSize * 1.2f;
        }

        float totalWidth = nonTextWidth + messageWidth;
        float height = MathF.Max(iconSize, messageHeight);

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureSparkline(Sparkline spark, LayoutConstraints constraints)
    {
        return new Size(
            constraints.ConstrainWidth(spark.widthValue),
            constraints.ConstrainHeight(spark.heightValue));
    }

    private static Size MeasureRangeSlider(RangeSlider rs, LayoutConstraints constraints)
    {
        float desiredWidth = 200f;
        float desiredHeight = 32f;
        return new Size(
            constraints.ConstrainWidth(desiredWidth),
            constraints.ConstrainHeight(desiredHeight));
    }

    private static Size MeasureDonutGauge(DonutGauge dg, LayoutConstraints constraints)
    {
        float size = dg.sizeValue;
        return new Size(
            constraints.ConstrainWidth(size),
            constraints.ConstrainHeight(size));
    }

    private static Size MeasureTimeline(Timeline tl, LayoutConstraints constraints)
    {
        int count = tl.Events.Count;
        if (count == 0)
        {
            return new Size(constraints.ConstrainWidth(200f), constraints.ConstrainHeight(40f));
        }

        // Each event: ~48px (title + optional body + spacing). Last event has no trailing space.
        const float eventHeight = 52f;
        const float minWidth = 260f;
        float totalHeight = count * eventHeight;
        return new Size(
            constraints.ConstrainWidth(minWidth),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureColorPicker(ColorPicker cp, LayoutConstraints constraints)
    {
        // SB canvas (130) + hue bar (14) + hex label row, with padding
        const float width = 240f;
        const float height = 200f;
        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasurePinInput(PinInput pin, LayoutConstraints constraints)
    {
        // Each cell is 40×48 with 8px gap between. Optional separators add 12px.
        const float cellWidth = 40f;
        const float cellHeight = 48f;
        const float gap = 8f;
        const float separatorExtra = 12f;

        int length = pin.Length;
        float totalWidth = length * cellWidth + (length - 1) * gap;

        // Add separator space
        foreach (int pos in pin.SeparatorPositions)
        {
            if (pos > 0 && pos < length)
            {
                totalWidth += separatorExtra;
            }
        }

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(cellHeight));
    }

    private static Size MeasureStatusBar(StatusBar sb, LayoutConstraints constraints)
    {
        float fontSize = BodyFontSize;
        float height = MathF.Round(fontSize * 1.8f);
        float width = float.IsPositiveInfinity(constraints.MaxWidth) ? 320f : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureToolBar(ToolBar tb, LayoutConstraints constraints)
    {
        const float buttonSize = 32f;
        const float gap = 4f;
        const float separatorWidth = 12f;

        float totalWidth = 0f;
        for (int i = 0; i < tb.Items.Count; i++)
        {
            if (i > 0)
            {
                totalWidth += gap;
            }

            if (tb.Items[i].IsSeparator)
            {
                totalWidth += separatorWidth;
            }
            else
            {
                totalWidth += buttonSize;
            }
        }

        return new Size(
            constraints.ConstrainWidth(totalWidth),
            constraints.ConstrainHeight(buttonSize));
    }

    private static Size MeasureMenuBar(MenuBar mb, LayoutConstraints constraints)
    {
        const float barHeight = 30f;
        const float labelPadH = 12f;
        const float fontSize = 13f;
        const float avgCharW = fontSize * 0.6f;

        float totalWidth = 0f;
        for (int i = 0; i < mb.Menus.Count; i++)
        {
            float labelW = mb.Menus[i].Label.Length * avgCharW + labelPadH * 2f;
            totalWidth += labelW;
        }

        // Menu bar should take full available width
        float width = float.IsPositiveInfinity(constraints.MaxWidth)
            ? Math.Max(totalWidth, 400f)
            : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(barHeight));
    }

    private static Size MeasurePropertyGrid(PropertyGrid pg, LayoutConstraints constraints)
    {
        const float groupHeaderH = 32f;
        const float rowH = 28f;

        float totalHeight = 0f;
        for (int gi = 0; gi < pg.Groups.Count; gi++)
        {
            var group = pg.Groups[gi];
            if (group.Visible != null && !group.Visible())
            {
                continue;
            }

            totalHeight += groupHeaderH;

            if (!pg.CollapsedGroups.Contains(gi))
            {
                totalHeight += group.Properties.Count * rowH;
            }
        }

        float width = float.IsPositiveInfinity(constraints.MaxWidth) ? 400f : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(totalHeight));
    }

    private static Size MeasureEmojiPicker(LayoutConstraints constraints)
    {
        const float cellSize = 36f;
        const float spacing = 2f;
        const int columns = 8;
        const float tabHeight = 36f;
        const float maxGridRows = 5f;

        float width = columns * cellSize + (columns - 1) * spacing + 16f;
        float height = tabHeight + maxGridRows * (cellSize + spacing) + 8f;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(height));
    }

    private static LayoutConstraints ApplySizingModifiers(
        LayoutConstraints constraints, LayoutNodeData data)
    {
        float minW = constraints.MinWidth;
        float maxW = constraints.MaxWidth;
        float minH = constraints.MinHeight;
        float maxH = constraints.MaxHeight;

        // Explicit width → tight width constraint
        if (data.ExplicitWidth.HasValue)
        {
            float w = data.ExplicitWidth.Value;
            if (float.IsPositiveInfinity(w))
            {
                // Size.Fill: use max constraint as tight
                w = maxW;
            }
            minW = w;
            maxW = w;
        }

        // Explicit height → tight height constraint
        if (data.ExplicitHeight.HasValue)
        {
            float h = data.ExplicitHeight.Value;
            if (float.IsPositiveInfinity(h))
            {
                h = maxH;
            }
            minH = h;
            maxH = h;
        }

        // Min/max overrides
        if (data.MinWidthMod.HasValue)
        {
            minW = Math.Max(minW, data.MinWidthMod.Value);
        }
        if (data.MaxWidthMod.HasValue)
        {
            maxW = Math.Min(maxW, data.MaxWidthMod.Value);
            minW = Math.Min(minW, maxW);
        }
        if (data.MinHeightMod.HasValue)
        {
            minH = Math.Max(minH, data.MinHeightMod.Value);
        }
        if (data.MaxHeightMod.HasValue)
        {
            maxH = Math.Min(maxH, data.MaxHeightMod.Value);
            minH = Math.Min(minH, maxH);
        }

        // Ensure min ≤ max
        maxW = Math.Max(minW, maxW);
        maxH = Math.Max(minH, maxH);

        return new LayoutConstraints(minW, maxW, minH, maxH);
    }

    private static LayoutConstraints ApplyAspectRatio(
        LayoutConstraints constraints, LayoutNodeData data)
    {
        if (!data.AspectRatio.HasValue)
        {
            return constraints;
        }

        float ratio = data.AspectRatio.Value;

        // If width is tightly constrained, derive height
        if (constraints.MinWidth == constraints.MaxWidth)
        {
            float derivedH = constraints.MinWidth / ratio;
            return new LayoutConstraints(
                constraints.MinWidth, constraints.MaxWidth,
                derivedH, derivedH);
        }

        // If height is tightly constrained, derive width
        if (constraints.MinHeight == constraints.MaxHeight)
        {
            float derivedW = constraints.MinHeight * ratio;
            return new LayoutConstraints(
                derivedW, derivedW,
                constraints.MinHeight, constraints.MaxHeight);
        }

        // Both flexible: try to fit within constraints using max width
        float hFromW = constraints.MaxWidth / ratio;
        if (hFromW >= constraints.MinHeight && hFromW <= constraints.MaxHeight)
        {
            return new LayoutConstraints(
                constraints.MaxWidth, constraints.MaxWidth,
                hFromW, hFromW);
        }

        // Fall back: use max height to derive width
        float wFromH = constraints.MaxHeight * ratio;
        return new LayoutConstraints(
            Math.Clamp(wFromH, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(wFromH, constraints.MinWidth, constraints.MaxWidth),
            constraints.MaxHeight, constraints.MaxHeight);
    }

    internal static LayoutConstraints ShrinkConstraints(
        LayoutConstraints constraints, EdgeInsets insets)
    {
        return new LayoutConstraints(
            Math.Max(0, constraints.MinWidth - insets.Horizontal),
            Math.Max(0, constraints.MaxWidth - insets.Horizontal),
            Math.Max(0, constraints.MinHeight - insets.Vertical),
            Math.Max(0, constraints.MaxHeight - insets.Vertical));
    }

    private static Size MeasureBarChart(BarChart chart, LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 200f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasurePieChart(PieChart chart, LayoutConstraints constraints)
    {
        const float defaultSize = 200f;
        return new Size(
            constraints.ConstrainWidth(defaultSize),
            constraints.ConstrainHeight(defaultSize));
    }

    private static Size MeasureLineChart(LineChart chart, LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 200f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureAreaChart(AreaChart chart, LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 200f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureHeatMapChart(LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 240f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureTreeMapChart(LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 240f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureWaterfallChart(LayoutConstraints constraints)
    {
        const float defaultWidth = 380f;
        const float defaultHeight = 240f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureScatterPlot(LayoutConstraints constraints)
    {
        const float defaultWidth = 340f;
        const float defaultHeight = 240f;
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(defaultHeight));
    }

    private static Size MeasureTreeView(ITreeView tree, LayoutConstraints constraints)
    {
        // The tree's rows are a real interactive node tree (indent + chevron +
        // rendered content). Rebuild it each frame so expand/selection changes (which
        // only repaint) are reflected, then measure and position it.
        tree.InvalidateContent();
        Node content = tree.GetContentNode();

        var contentConstraints = new LayoutConstraints(
            constraints.MinWidth, constraints.MaxWidth,
            0, float.PositiveInfinity);
        Size contentSize = MeasureChild(content, contentConstraints);
        PositionChild(content, 0, 0);

        float width = float.IsPositiveInfinity(constraints.MaxWidth)
            ? contentSize.Width
            : constraints.MaxWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(MathF.Max(contentSize.Height, 1f)));
    }

    private static Size MeasurePasswordInput(PasswordInput pwd, LayoutConstraints constraints)
    {
        const float defaultWidth = 280f;
        float height = 36f;
        if (pwd.UseStrengthIndicator)
        {
            height += 10f;
        }
        return new Size(
            constraints.ConstrainWidth(defaultWidth),
            constraints.ConstrainHeight(height));
    }

    private static Size MeasureImage(Image img, LayoutConstraints constraints)
    {
        // If the image has (or can resolve) a decoded source, use its intrinsic dimensions
        ImageSource? source = img.ResolveSource();
        if (source is not null)
        {
            float intrinsicW = source.Width;
            float intrinsicH = source.Height;

            // Scale to fit within constraints while preserving aspect ratio
            float scaleW = constraints.MaxWidth / intrinsicW;
            float scaleH = constraints.MaxHeight / intrinsicH;
            float scale = Math.Min(scaleW, scaleH);
            if (scale > 1f)
            {
                scale = 1f;
            }

            return new Size(
                constraints.ConstrainWidth(intrinsicW * scale),
                constraints.ConstrainHeight(intrinsicH * scale));
        }

        // No source yet — use a reasonable placeholder size
        const float defaultSize = 200f;
        return new Size(
            constraints.ConstrainWidth(defaultSize),
            constraints.ConstrainHeight(defaultSize));
    }

    private static Size MeasureSplitView(SplitView sv, LayoutConstraints constraints)
    {
        const float dividerWidth = 6f;

        // Assign layout index for cross-render drag state tracking
        sv.LayoutIndex = InputDispatcher.SplitViewLayoutCounter++;

        float totalW = constraints.MaxWidth;
        float totalH = constraints.MaxHeight;

        if (sv.Orientation == SplitOrientation.Horizontal)
        {
            // Check for drag override first, then fall back to configured size
            float? overrideWidth = InputDispatcher.GetSplitViewOverride(sv.LayoutIndex);
            float firstW;

            if (overrideWidth.HasValue)
            {
                firstW = overrideWidth.Value;
            }
            else
            {
                float firstFraction = 0.5f;
                var data = sv.LayoutData.SplitData;
                if (data?.FirstSizePixels is not null)
                {
                    firstFraction = data.FirstSizePixels.Value / totalW;
                }
                else if (data?.FirstSizeDescriptor is { Kind: SplitSizeKind.Fraction } desc)
                {
                    firstFraction = desc.Value;
                }
                firstW = (totalW - dividerWidth) * firstFraction;
            }

            // Apply min/max constraints
            var splitData = sv.LayoutData.SplitData;
            if (splitData?.FirstMinPixels is not null)
            {
                firstW = Math.Max(firstW, splitData.FirstMinPixels.Value);
            }
            if (splitData?.FirstMaxPixels is not null)
            {
                firstW = Math.Min(firstW, splitData.FirstMaxPixels.Value);
            }

            // Clamp to available space
            firstW = Math.Clamp(firstW, 0, totalW - dividerWidth);
            float secondW = totalW - dividerWidth - firstW;

            if (splitData?.SecondMinPixels is not null)
            {
                float secondMin = splitData.SecondMinPixels.Value;
                if (secondW < secondMin)
                {
                    secondW = secondMin;
                    firstW = totalW - dividerWidth - secondW;
                }
            }
            if (splitData?.SecondMaxPixels is not null)
            {
                float secondMax = splitData.SecondMaxPixels.Value;
                if (secondW > secondMax)
                {
                    secondW = secondMax;
                    firstW = totalW - dividerWidth - secondW;
                }
            }

            // Measure children with their allocated space
            var firstConstraints = new LayoutConstraints(0, firstW, 0, totalH);
            var secondConstraints = new LayoutConstraints(0, secondW, 0, totalH);

            MeasureChild(sv.First, firstConstraints);
            MeasureChild(sv.Second, secondConstraints);

            // Store pane positions for painting
            sv.First.LayoutData.Bounds = new Rect(0, 0, firstW, totalH);
            sv.Second.LayoutData.Bounds = new Rect(firstW + dividerWidth, 0, secondW, totalH);
        }
        else
        {
            // Vertical: divide height — check for drag override first
            float? overrideHeight = InputDispatcher.GetSplitViewOverride(sv.LayoutIndex);
            float firstH;

            if (overrideHeight.HasValue)
            {
                firstH = overrideHeight.Value;
            }
            else
            {
                float firstFraction = 0.5f;
                var data = sv.LayoutData.SplitData;
                if (data?.FirstSizePixels is not null)
                {
                    firstFraction = data.FirstSizePixels.Value / totalH;
                }
                else if (data?.FirstSizeDescriptor is { Kind: SplitSizeKind.Fraction } desc)
                {
                    firstFraction = desc.Value;
                }
                firstH = (totalH - dividerWidth) * firstFraction;
            }

            // Apply min/max constraints
            var splitData = sv.LayoutData.SplitData;
            if (splitData?.FirstMinPixels is not null)
            {
                firstH = Math.Max(firstH, splitData.FirstMinPixels.Value);
            }
            if (splitData?.FirstMaxPixels is not null)
            {
                firstH = Math.Min(firstH, splitData.FirstMaxPixels.Value);
            }

            firstH = Math.Clamp(firstH, 0, totalH - dividerWidth);
            float secondH = totalH - dividerWidth - firstH;

            if (splitData?.SecondMinPixels is not null)
            {
                float secondMin = splitData.SecondMinPixels.Value;
                if (secondH < secondMin)
                {
                    secondH = secondMin;
                    firstH = totalH - dividerWidth - secondH;
                }
            }
            if (splitData?.SecondMaxPixels is not null)
            {
                float secondMax = splitData.SecondMaxPixels.Value;
                if (secondH > secondMax)
                {
                    secondH = secondMax;
                    firstH = totalH - dividerWidth - secondH;
                }
            }

            var firstConstraints = new LayoutConstraints(0, totalW, 0, firstH);
            var secondConstraints = new LayoutConstraints(0, totalW, 0, secondH);

            MeasureChild(sv.First, firstConstraints);
            MeasureChild(sv.Second, secondConstraints);

            sv.First.LayoutData.Bounds = new Rect(0, 0, totalW, firstH);
            sv.Second.LayoutData.Bounds = new Rect(0, firstH + dividerWidth, totalW, secondH);
        }

        return new Size(
            constraints.ConstrainWidth(totalW),
            constraints.ConstrainHeight(totalH));
    }

    private static Size MeasureTextArea(TextArea textArea, LayoutConstraints constraints)
    {
        const float defaultWidth = 280f;
        // Approximate line height matching font metrics for typical body text (17px).
        // The painter uses ctx.MeasureText for the exact value; this keeps layout close.
        const float lineHeight = 22f;
        const float paddingV = 16f;

        int lines = textArea.FixedLines ?? textArea.MinLines;
        float height = lines * lineHeight + paddingV;

        // Grow the box to fit the floating character count below the content so the
        // last line never runs underneath it (the painter reserves a matching strip).
        if (textArea.CharacterCountStyle.HasValue)
        {
            height += 30f;
        }

        // A TextArea is a block control: it fills the width its container offers so
        // text wraps to the full pane, rather than sitting at a fixed narrow default
        // that leaves the caret running off the right edge. Only when width is
        // unbounded (a loose parent with no max) does it fall back to the default.
        float width = float.IsFinite(constraints.MaxWidth)
            ? constraints.MaxWidth
            : defaultWidth;

        return new Size(
            constraints.ConstrainWidth(width),
            constraints.ConstrainHeight(height));
    }
}
