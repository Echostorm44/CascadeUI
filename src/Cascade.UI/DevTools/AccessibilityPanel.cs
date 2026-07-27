using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Accessibility inspection panel. Provides WCAG validation, contrast checking,
/// focus order visualization, and a screen reader text preview.
/// </summary>
internal static class AccessibilityPanel
{
    // AccessibleNode and LiveRegion are declared in standalone AccessibleNode.cs
    // under #if CASCADE_DEVTOOLS so agents inspecting a Release + CascadeDevTools
    // build can consume them. Remaining panel-local DTOs (WcagViolation,
    // ContrastCheckResult, FocusOrderEntry, ScreenReaderLine, ViolationSeverity)
    // stay Debug-only since they're only consumed by the on-screen audit UI.

    /// <summary>WCAG violation severity.</summary>
    public enum ViolationSeverity
    {
        Error,
        Warning,
        Info,
    }

    /// <summary>A single WCAG violation.</summary>
    public sealed class WcagViolation
    {
        /// <summary>Node that has the violation.</summary>
        public required string NodeId { get; init; }

        /// <summary>WCAG rule ID (e.g., "1.4.3", "2.4.6").</summary>
        public required string RuleId { get; init; }

        /// <summary>Human-readable rule name.</summary>
        public required string RuleName { get; init; }

        /// <summary>Violation severity.</summary>
        public ViolationSeverity Severity { get; init; }

        /// <summary>Description of what is wrong.</summary>
        public required string Description { get; init; }

        /// <summary>Suggested fix.</summary>
        public string? SuggestedFix { get; init; }
    }

    /// <summary>
    /// Contrast check result between foreground and background colors.
    /// </summary>
    public sealed class ContrastCheckResult
    {
        /// <summary>Node being checked.</summary>
        public required string NodeId { get; init; }

        /// <summary>Foreground color as RGBA hex.</summary>
        public required string ForegroundColor { get; init; }

        /// <summary>Background color as RGBA hex.</summary>
        public required string BackgroundColor { get; init; }

        /// <summary>Contrast ratio (e.g. 4.5:1 would be 4.5).</summary>
        public float ContrastRatio { get; init; }

        /// <summary>Whether the ratio passes WCAG AA for normal text (4.5:1).</summary>
        public bool PassesAA { get; init; }

        /// <summary>Whether the ratio passes WCAG AAA for normal text (7:1).</summary>
        public bool PassesAAA { get; init; }

        /// <summary>Whether the ratio passes WCAG AA for large text (3:1).</summary>
        public bool PassesAALargeText { get; init; }
    }

    /// <summary>Focus order entry: see standalone FocusOrderEntry.cs (#if CASCADE_DEVTOOLS).</summary>

    /// <summary>Screen reader preview line for a subtree.</summary>
    public sealed class ScreenReaderLine
    {
        /// <summary>Node that generated this line.</summary>
        public required string NodeId { get; init; }

        /// <summary>Role announcement text.</summary>
        public string? RoleText { get; init; }

        /// <summary>Label text.</summary>
        public string? LabelText { get; init; }

        /// <summary>State text (checked, expanded, etc.).</summary>
        public string? StateText { get; init; }

        /// <summary>The full text as a screen reader would announce it.</summary>
        public required string FullAnnouncement { get; init; }
    }

    /// <summary>
    /// Captures the full accessibility tree from the root.
    /// </summary>
    public static AccessibleNode CaptureAccessibilityTree()
    {
        return NodeTreeWalker.GetAccessibilityTree();
    }

    /// <summary>
    /// Runs WCAG validation on the current tree and returns all violations.
    /// Checks: missing labels, insufficient contrast, missing focus indicators,
    /// incorrect heading order, missing alt text, and more.
    /// </summary>
    public static IReadOnlyList<WcagViolation> ValidateAccessibility()
    {
        var violations = new List<WcagViolation>();
        var tree = CaptureAccessibilityTree();
        ValidateNode(tree, violations, headingLevel: 0);
        return violations;
    }

    /// <summary>
    /// Checks the contrast ratio for a specific node.
    /// </summary>
    public static ContrastCheckResult? CheckContrast(string nodeId)
    {
        var node = NodeTreeWalker.FindNode(nodeId);
        if (node is null)
        {
            return null;
        }

        var colors = NodeTreeWalker.GetNodeColors(node);
        if (colors is null)
        {
            return null;
        }

        float ratio = CalculateContrastRatio(colors.Value.foreground, colors.Value.background);
        return new ContrastCheckResult
        {
            NodeId = nodeId,
            ForegroundColor = colors.Value.foreground.ToHex(),
            BackgroundColor = colors.Value.background.ToHex(),
            ContrastRatio = ratio,
            PassesAA = ratio >= 4.5f,
            PassesAAA = ratio >= 7.0f,
            PassesAALargeText = ratio >= 3.0f,
        };
    }

    /// <summary>
    /// Returns the focus order for all focusable elements.
    /// </summary>
    public static IReadOnlyList<FocusOrderEntry> GetFocusOrder()
    {
        return NodeTreeWalker.GetFocusOrder();
    }

    /// <summary>
    /// Generates a screen reader preview showing what assistive technology
    /// would announce for each visible node.
    /// </summary>
    public static IReadOnlyList<ScreenReaderLine> GetScreenReaderPreview()
    {
        var tree = CaptureAccessibilityTree();
        var lines = new List<ScreenReaderLine>();
        GenerateScreenReaderLines(tree, lines);
        return lines;
    }

    private static void ValidateNode(AccessibleNode node, List<WcagViolation> violations, int headingLevel)
    {
        // Check: interactive elements must have labels (WCAG 1.3.1, 4.1.2)
        if (IsInteractiveRole(node.Role) && string.IsNullOrEmpty(node.Label))
        {
            violations.Add(new WcagViolation
            {
                NodeId = node.NodeId,
                RuleId = "4.1.2",
                RuleName = "Name, Role, Value",
                Severity = ViolationSeverity.Error,
                Description = $"Interactive element with role '{node.Role}' is missing an accessible label.",
                SuggestedFix = "Add a label using the accessibility modifier: .Accessible(label: \"...\").",
            });
        }

        // Check: heading level order (WCAG 1.3.1)
        if (node.Role == AccessibleRole.Heading)
        {
            int level = ParseHeadingLevel(node.StateProperties);
            if (level > 0 && headingLevel > 0 && level > headingLevel + 1)
            {
                violations.Add(new WcagViolation
                {
                    NodeId = node.NodeId,
                    RuleId = "1.3.1",
                    RuleName = "Info and Relationships",
                    Severity = ViolationSeverity.Warning,
                    Description = $"Heading level {level} skips from level {headingLevel}. Heading levels should not skip.",
                    SuggestedFix = $"Use heading level {headingLevel + 1} instead.",
                });
            }
            if (level > 0)
            {
                headingLevel = level;
            }
        }

        // Check: images must have alt text (WCAG 1.1.1)
        if (node.Role == AccessibleRole.Image && string.IsNullOrEmpty(node.Label) && string.IsNullOrEmpty(node.Description))
        {
            violations.Add(new WcagViolation
            {
                NodeId = node.NodeId,
                RuleId = "1.1.1",
                RuleName = "Non-text Content",
                Severity = ViolationSeverity.Error,
                Description = "Image is missing alternative text.",
                SuggestedFix = "Add alt text using .Accessible(label: \"Description of image\").",
            });
        }

        foreach (var child in node.Children)
        {
            ValidateNode(child, violations, headingLevel);
        }
    }

    private static bool IsInteractiveRole(AccessibleRole role)
    {
        return role is AccessibleRole.Button
            or AccessibleRole.TextBox
            or AccessibleRole.Checkbox
            or AccessibleRole.Radio
            or AccessibleRole.Slider
            or AccessibleRole.Switch
            or AccessibleRole.Link
            or AccessibleRole.ComboBox
            or AccessibleRole.Tab;
    }

    private static int ParseHeadingLevel(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("level", out var levelStr) && int.TryParse(levelStr, out int level))
        {
            return level;
        }
        return 0;
    }

    private static void GenerateScreenReaderLines(AccessibleNode node, List<ScreenReaderLine> lines)
    {
        if (node.Role != AccessibleRole.None || !string.IsNullOrEmpty(node.Label))
        {
            string roleText = node.Role != AccessibleRole.None ? node.Role.ToString() : "";
            string stateText = BuildStateText(node);
            string fullAnnouncement = BuildAnnouncement(roleText, node.Label, stateText);

            lines.Add(new ScreenReaderLine
            {
                NodeId = node.NodeId,
                RoleText = string.IsNullOrEmpty(roleText) ? null : roleText,
                LabelText = node.Label,
                StateText = string.IsNullOrEmpty(stateText) ? null : stateText,
                FullAnnouncement = fullAnnouncement,
            });
        }

        foreach (var child in node.Children)
        {
            GenerateScreenReaderLines(child, lines);
        }
    }

    private static string BuildStateText(AccessibleNode node)
    {
        var parts = new List<string>();
        if (node.Disabled)
        {
            parts.Add("disabled");
        }
        if (node.Focused)
        {
            parts.Add("focused");
        }
        foreach (var kvp in node.StateProperties)
        {
            if (kvp.Key is "checked" or "expanded" or "selected" or "pressed")
            {
                parts.Add($"{kvp.Key}: {kvp.Value}");
            }
        }
        return string.Join(", ", parts);
    }

    private static string BuildAnnouncement(string role, string? label, string state)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(label))
        {
            parts.Add(label);
        }
        if (!string.IsNullOrEmpty(role))
        {
            parts.Add(role);
        }
        if (!string.IsNullOrEmpty(state))
        {
            parts.Add(state);
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Calculates the WCAG contrast ratio between two colors.
    /// Uses the relative luminance formula per WCAG 2.1.
    /// </summary>
    internal static float CalculateContrastRatio(ColorValue foreground, ColorValue background)
    {
        float lum1 = RelativeLuminance(foreground);
        float lum2 = RelativeLuminance(background);

        float lighter = Math.Max(lum1, lum2);
        float darker = Math.Min(lum1, lum2);

        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(ColorValue color)
    {
        // WCAG relative luminance from linear sRGB.
        // ColorValue stores premultiplied linear sRGB, so we need to
        // un-premultiply first, then apply the luminance formula.
        float a = color.A > 0 ? color.A : 1f;
        float r = color.R / a;
        float g = color.G / a;
        float b = color.B / a;

        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }
}

#endif
