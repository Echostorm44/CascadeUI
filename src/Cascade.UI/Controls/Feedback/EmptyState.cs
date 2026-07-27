namespace Cascade.UI;

/// <summary>
/// A display component for empty or zero-data states. Shows an icon,
/// title, description, and optional action to guide the user when there
/// is no content to display.
/// </summary>
/// <remarks>
/// <code>
/// new EmptyState(
///     title:       "No projects yet",
///     description: "Create your first project to get started.",
///     icon:        Icons.Folder,
///     action:      Button("Create Project", onClick: CreateProject)
/// )
/// </code>
/// </remarks>
public class EmptyState : Component
{
    private readonly string title;
    private readonly string? description;
    private readonly Node icon;
    private readonly Node action;
    private readonly Node renderedTree;

    /// <summary>
    /// Creates an empty state display.
    /// </summary>
    /// <param name="title">Main title text (required).</param>
    /// <param name="description">Optional descriptive text below the title.</param>
    /// <param name="icon">Optional icon displayed above the title.</param>
    /// <param name="action">Optional action node (e.g., a button) below the description.</param>
    public EmptyState(
        string title,
        string? description = null,
        Node? icon = null,
        Node? action = null)
    {
        this.title = title;
        this.description = description;
        this.icon = icon ?? Node.Empty;
        this.action = action ?? Node.Empty;

        var children = new List<Node>();
        if (this.icon != Node.Empty)
        {
            children.Add(this.icon);
        }
        children.Add(new Label(title));
        if (description != null)
        {
            children.Add(new Label(description));
        }
        if (this.action != Node.Empty)
        {
            children.Add(this.action);
        }
        renderedTree = new Column(
            spacing: 12,
            crossAxisAlignment: CrossAxisAlignment.Center,
            mainAxisAlignment: MainAxisAlignment.Center,
            children: children.ToArray()
        );
    }

    /// <inheritdoc/>
    protected override Node Render()
    {
        return renderedTree;
    }
}
