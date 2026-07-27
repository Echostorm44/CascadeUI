using System;
using System.Collections.Generic;

namespace Cascade.UI.DevTools;

#if DEBUG

/// <summary>
/// Component tree inspector panel. Displays the component hierarchy
/// with bidirectional selection — pick a node in the app to select it
/// in the tree, or select a tree node to highlight it in the app.
/// </summary>
internal static class InspectorPanel
{
    // NodeSnapshot, NodeDetail, AccessibilityInfo, SignalInfo, and ComputedInfo
    // are declared in standalone files under #if CASCADE_DEVTOOLS so agents
    // inspecting a Release + CascadeDevTools build can consume them. The panel
    // class itself (and its on-screen rendering) remains Debug-only.

    /// <summary>
    /// Captures a snapshot of the full component tree from the given root.
    /// </summary>
    /// <param name="maxDepth">Maximum depth to traverse. Default 10.</param>
    /// <returns>Root node snapshot with children populated up to maxDepth.</returns>
    public static NodeSnapshot CaptureTree(int maxDepth = 10)
    {
        return CaptureTreeFromRoot(null, maxDepth);
    }

    /// <summary>
    /// Captures the tree starting from a specific node ID.
    /// </summary>
    public static NodeSnapshot CaptureTreeFromRoot(string? rootId, int maxDepth)
    {
        // The framework's reconciler maintains the live node tree.
        // This method walks that tree and produces immutable snapshots.
        // The actual tree walking is performed by the ComponentHost which
        // has access to the mounted component graph.
        var root = NodeTreeWalker.FindNode(rootId);
        if (root is null)
        {
            return new NodeSnapshot
            {
                Id = "root",
                TypeName = "Empty",
            };
        }

        return NodeTreeWalker.Snapshot(root, maxDepth, currentDepth: 0);
    }

    /// <summary>
    /// Gets detailed information for a specific node.
    /// </summary>
    public static NodeDetail? GetNodeDetail(string nodeId)
    {
        var node = NodeTreeWalker.FindNode(nodeId);
        if (node is null)
        {
            return null;
        }

        return NodeTreeWalker.DetailSnapshot(node);
    }

    /// <summary>
    /// Attempts to open the source file for the given node in the default editor.
    /// Respects the EDITOR environment variable, falls back to OS-registered editor.
    /// </summary>
    public static void OpenSourceLocation(string nodeId)
    {
        var detail = GetNodeDetail(nodeId);
        if (detail?.SourceLocation is null)
        {
            return;
        }

        var parts = detail.SourceLocation.Split(':');
        if (parts.Length < 2)
        {
            return;
        }

        var file = parts[0];
        var line = parts[1];

        var editor = Environment.GetEnvironmentVariable("EDITOR");
        if (!string.IsNullOrEmpty(editor))
        {
            System.Diagnostics.Process.Start(editor, $"+{line} \"{file}\"");
        }
        else
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = file,
                UseShellExecute = true,
            });
        }
    }
}

#endif
