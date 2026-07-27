namespace Cascade.UI;

/// <summary>
/// Theme tokens for the <see cref="NotificationBell"/> control: icon sizing,
/// badge appearance, dropdown panel, and item layout.
/// </summary>
public class NotificationBellTheme
{
    /// <summary>Size of the bell icon in logical pixels.</summary>
    public required float IconSize { get; init; }

    /// <summary>Size of the unread count badge in logical pixels.</summary>
    public required float BadgeSize { get; init; }

    /// <summary>Background color of the unread count badge.</summary>
    public required ColorValue BadgeColor { get; init; }

    /// <summary>Text color of the unread count badge.</summary>
    public required ColorValue BadgeTextColor { get; init; }

    /// <summary>Width of the notification dropdown panel.</summary>
    public required float DropdownWidth { get; init; }

    /// <summary>Maximum height of the notification dropdown panel.</summary>
    public required float DropdownMaxHeight { get; init; }

    /// <summary>Background brush of the notification dropdown panel.</summary>
    public required Brush DropdownBackground { get; init; }

    /// <summary>Corner radius of the notification dropdown panel.</summary>
    public required float DropdownRadius { get; init; }

    /// <summary>Shadow specification for the notification dropdown panel.</summary>
    public required ShadowSpec DropdownShadow { get; init; }

    /// <summary>Height of each notification item row.</summary>
    public required float ItemHeight { get; init; }

    /// <summary>Background brush shown when hovering over a notification item.</summary>
    public required Brush ItemHoverBackground { get; init; }

    /// <summary>Background brush for unread notification items.</summary>
    public required Brush UnreadBackground { get; init; }

    /// <summary>Animation applied when the bell rings on new notifications.</summary>
    public required Transition RingAnimation { get; init; }

    /// <summary>Default transition for interactive state changes.</summary>
    public required Transition Transition { get; init; }

    /// <summary>Creates a default NotificationBellTheme derived from global theme tokens.</summary>
    public static NotificationBellTheme Default(CascadeTheme t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return new NotificationBellTheme
        {
            IconSize = 20,
            BadgeSize = 16,
            BadgeColor = t.Colors.Danger,
            BadgeTextColor = t.Colors.TextOnPrimary,
            DropdownWidth = 360,
            DropdownMaxHeight = 480,
            DropdownBackground = Brush.Solid(t.Colors.Surface),
            DropdownRadius = t.Radius.Lg,
            DropdownShadow = t.Shadows.Lg,
            ItemHeight = 72,
            ItemHoverBackground = Brush.Solid(t.Colors.SurfaceAlt),
            UnreadBackground = Brush.Solid(t.Colors.SurfaceAlt),
            RingAnimation = t.Motion.Emphasis,
            Transition = t.Motion.Subtle,
        };
    }
}
