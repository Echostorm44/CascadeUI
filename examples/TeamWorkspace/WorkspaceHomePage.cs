using Cascade.UI;

namespace TeamWorkspace;

// ── Data models ───────────────────────────────────────────────────────────────

internal sealed record ActivityEvent(
    string Id,
    string ActorName,
    string Action,
    string TargetName,
    DateTime When,
    string Category
);

internal sealed record TeamMember(
    string Id,
    string Name,
    string Presence
);

// ── Mock data ─────────────────────────────────────────────────────────────────

internal static class MockData
{
    internal static IReadOnlyList<ActivityEvent> GetActivity() =>
    [
        new("a1", "Sarah Chen",     "completed",   "Q4 Revenue Report",     DateTime.Now.AddMinutes(-12), "task"),
        new("a2", "Marcus Johnson", "commented on", "API Design Proposal",   DateTime.Now.AddMinutes(-25), "comment"),
        new("a3", "Aisha Patel",    "created",      "Sprint 14 Retro Notes", DateTime.Now.AddMinutes(-45), "doc"),
        new("a4", "David Kim",      "assigned",     "Fix Login Flow",        DateTime.Now.AddHours(-1),    "task"),
        new("a5", "Elena Rodriguez","joined",       "the team",              DateTime.Now.AddHours(-2),    "member"),
        new("a6", "James Wilson",   "completed",    "Database Migration",    DateTime.Now.AddHours(-3),    "task"),
        new("a7", "Sarah Chen",     "created",      "Product Roadmap 2025",  DateTime.Now.AddHours(-4),    "doc"),
        new("a8", "Marcus Johnson", "commented on", "UX Research Findings",  DateTime.Now.AddHours(-5),    "comment"),
        new("a9", "Aisha Patel",    "completed",    "Unit Test Coverage",    DateTime.Now.AddHours(-6),    "task"),
        new("a10","David Kim",      "created",      "Architecture Decision Record", DateTime.Now.AddHours(-8), "doc"),
    ];

    internal static IReadOnlyList<TeamMember> GetTeam() =>
    [
        new("t1", "Sarah Chen",      "online"),
        new("t2", "Marcus Johnson",  "online"),
        new("t3", "Aisha Patel",     "away"),
        new("t4", "David Kim",       "online"),
        new("t5", "Elena Rodriguez", "away"),
        new("t6", "James Wilson",    "offline"),
        new("t7", "Priya Sharma",    "online"),
        new("t8", "Alex Thompson",   "offline"),
    ];

    internal static IReadOnlyList<AppNotification> GetNotifications() =>
    [
        new()
        {
            Id = "n1",
            Title = "Sarah Chen completed Q4 Revenue Report",
            Body = "The task has been marked as done",
            Timestamp = DateTimeOffset.Now.AddMinutes(-12),
            IsRead = false
        },
        new()
        {
            Id = "n2",
            Title = "Marcus Johnson commented on API Design",
            Body = "Looks good, just a few suggestions...",
            Timestamp = DateTimeOffset.Now.AddMinutes(-25),
            IsRead = false
        },
        new()
        {
            Id = "n3",
            Title = "Sprint 14 Planning starts in 30 minutes",
            Body = "Conference Room B or join via Teams",
            Timestamp = DateTimeOffset.Now.AddMinutes(-30),
            IsRead = true
        },
        new()
        {
            Id = "n4",
            Title = "Elena Rodriguez joined the team",
            Body = "Welcome Elena to the engineering team!",
            Timestamp = DateTimeOffset.Now.AddHours(-2),
            IsRead = true
        },
    ];
}

// ── Workspace Home Page ───────────────────────────────────────────────────────

internal sealed class WorkspaceHomePage : Component
{
    private IReadOnlyList<ActivityEvent> activity = MockData.GetActivity();
    private IReadOnlyList<TeamMember> team = MockData.GetTeam();
    private IReadOnlyList<AppNotification> notifications = MockData.GetNotifications();
    private readonly DateTimeOffset? lastSynced = DateTimeOffset.Now;
    private readonly bool connected = true;

    private string activeFilter = "all";

    private bool showRatingDialog;
    private string completedTaskName = "";
    private float taskRating;

    protected override Task OnMounted()
    {
        return Task.CompletedTask;
    }

    protected override Node Render() =>
        new Column(spacing: 0, children:
        [
            NavBar(),
            new Separator(),
            new SplitView(
                first: ActivityPanel(),
                second: TeamSidebar()
            ).Grow(1),
            BottomStatusBar(),
            showRatingDialog ? RatingOverlay() : Node.Empty
        ]);

    // ── Navigation bar ────────────────────────────────────────────────

    private Node NavBar() =>
        new Row(spacing: 8, children:
        [
            new Label("Team Workspace")
                .FontSize(18)
                .Bold()
                .Grow(1),
            new SplitButton(
                label: "New Task",
                onClick: () => { CreateTask(); },
                items:
                [
                    ContextMenuItem.Action("New Document", onClick: () => { }),
                    ContextMenuItem.Action("New Event", onClick: () => { })
                ]
            ),
            new NotificationBell(
                notifications: new Bindable<IReadOnlyList<AppNotification>>(
                    notifications,
                    v => { notifications = v; Invalidate(); }),
                onRead: n => { MarkRead(n.Id); },
                onReadAll: () => { MarkAllRead(); },
                onClear: n => { ClearNotification(n.Id); }
            )
        ]).Padding(12);

    // ── Activity panel (left) ─────────────────────────────────────────

    private Node ActivityPanel() =>
        new Column(spacing: 0, children:
        [
            new Row(spacing: 8, children:
            [
                new Label("Activity").FontSize(14).Bold(),
                new Spacer(),
                new SegmentedControl<string>(
                    new Bindable<string>(activeFilter, v => { activeFilter = v; Invalidate(); }),
                    [
                        new SegmentOption<string>("all", "All"),
                        new SegmentOption<string>("task", "Tasks"),
                        new SegmentOption<string>("doc", "Docs"),
                        new SegmentOption<string>("comment", "Comments"),
                        new SegmentOption<string>("member", "Members"),
                    ]),
            ]).Padding(horizontal: 16, vertical: 10),
            new Separator(),
            new ScrollView(
                new Timeline(GetFilteredEvents())
                    .Layout(TimelineLayout.Compact)
                    .Padding(16)
            ).Grow(1)
        ]);

    private IReadOnlyList<TimelineEvent> GetFilteredEvents()
    {
        var filtered = activeFilter == "all"
            ? activity
            : activity.Where(e => e.Category == activeFilter).ToList();

        return filtered.Select(e => new TimelineEvent(
            timestamp: e.When,
            title: $"{e.ActorName} {e.Action} {e.TargetName}",
            iconColor: CategoryColor(e.Category)
        )).ToList();
    }

    private static ColorValue CategoryColor(string category) => category switch
    {
        "task"    => ThemeSwitcher.Current.Palette.Green,
        "doc"     => ThemeSwitcher.Current.Palette.Blue,
        "comment" => ThemeSwitcher.Current.Palette.Yellow,
        "member"  => ThemeSwitcher.Current.Palette.Purple,
        _         => ThemeSwitcher.ActiveColors.TextMuted
    };

    // ── Team sidebar (right) ──────────────────────────────────────────

    private Node TeamSidebar() =>
        new Column(spacing: 0, children:
        [
            new Row(spacing: 0, children:
            [
                new Label("Team Members")
                    .FontSize(14)
                    .Bold()
                    .Grow(1),
                new Label($"{team.Count} members")
                    .FontSize(12)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
            ]).Padding(horizontal: 16, vertical: 12),
            new Separator(),
            new ScrollView(
                new Column(spacing: 0, children:
                    team
                        .OrderBy(m => m.Presence == "offline" ? 2 : m.Presence == "away" ? 1 : 0)
                        .Select(MemberRow)
                        .ToArray()
                )
            ).Grow(1)
        ]).Background(ThemeSwitcher.ActiveColors.SurfaceAlt);

    private static Node MemberRow(TeamMember member)
    {
        var (dotColor, statusLabel) = member.Presence switch
        {
            "online" => (ThemeSwitcher.Current.Palette.Green, "Online"),
            "away"   => (ThemeSwitcher.Current.Palette.Yellow, "Away"),
            _        => (ThemeSwitcher.ActiveColors.TextMuted, "Offline")
        };

        return new Row(spacing: 10, children:
        [
            new Avatar(member.Name).Size(36),
            new Column(spacing: 2, children:
            [
                new Label(member.Name).FontSize(13).Bold(),
                new Row(spacing: 6, children:
                [
                    new Row(children: [])
                        .Background(dotColor)
                        .Width(8)
                        .Height(8)
                        .CornerRadius(4),
                    new Label(statusLabel)
                        .FontSize(11)
                        .Color(ThemeSwitcher.ActiveColors.TextMuted),
                ]),
            ]).Grow(1),
        ]).Padding(horizontal: 16, vertical: 8);
    }

    // ── Status bar ────────────────────────────────────────────────────

    private Node BottomStatusBar()
    {
        string syncText = lastSynced.HasValue
            ? $"Last synced: {FormatAge(lastSynced.Value.LocalDateTime)}"
            : "Not synced";

        return new StatusBar(
            left: new Row(spacing: 12, children:
            [
                new Row(spacing: 6, children:
                [
                    new Row(children: [])
                        .Background(connected ? ThemeSwitcher.Current.Palette.Green : ThemeSwitcher.ActiveColors.Danger)
                        .Width(8)
                        .Height(8)
                        .CornerRadius(4),
                    new Label(connected ? "Connected" : "Offline")
                        .FontSize(11),
                ]),
                new Label(syncText).FontSize(11)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
            ]),
            center: new Label("Cascade Engineering").FontSize(11)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            right: new Label($"{team.Count} team members").FontSize(11)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        );
    }

    // ── Rating overlay ────────────────────────────────────────────────

    private Node RatingOverlay() =>
        new Center(
            new Column(spacing: 16, children:
            [
                new Label("How did it go?").FontSize(18).Bold(),
                new Label($"You completed: {completedTaskName}")
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted),
                new Rating(
                    value: new Bindable<float>(taskRating, v => { taskRating = v; Invalidate(); }),
                    max: 5
                ),
                new Row(spacing: 8, children:
                [
                    new Button("Skip", onClick: () =>
                    {
                        showRatingDialog = false;
                        Invalidate();
                    }),
                    new Button("Submit", onClick: () =>
                    {
                        showRatingDialog = false;
                        Invalidate();
                    }).Variant("primary"),
                ]),
            ])
            .Padding(32)
            .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
            .CornerRadius(12)
            .Width(400)
        ).Background(ThemeSwitcher.ActiveColors.Background.Opacity(0.6f));

    // ── Notification helpers ──────────────────────────────────────────

    private void MarkRead(string id)
    {
        notifications = notifications
            .Select(n => n.Id == id ? n with { IsRead = true } : n)
            .ToList();
        Invalidate();
    }

    private void MarkAllRead()
    {
        notifications = notifications
            .Select(n => n with { IsRead = true })
            .ToList();
        Invalidate();
    }

    private void ClearNotification(string id)
    {
        notifications = notifications
            .Where(n => n.Id != id)
            .ToList();
        Invalidate();
    }

    // ── Task creation ─────────────────────────────────────────────────

    private void CreateTask()
    {
        completedTaskName = "New Task";
        taskRating = 0;
        showRatingDialog = true;
        Invalidate();
    }

    // ── Time formatting ───────────────────────────────────────────────

    private static string FormatAge(DateTime t)
    {
        var diff = DateTime.Now - t;
        if (diff.TotalMinutes < 1)
        {
            return "just now";
        }
        if (diff.TotalHours < 1)
        {
            return $"{(int)diff.TotalMinutes}m ago";
        }
        if (diff.TotalDays < 1)
        {
            return $"{(int)diff.TotalHours}h ago";
        }
        return $"{(int)diff.TotalDays}d ago";
    }
}

// ── Icons ─────────────────────────────────────────────────────────────────────

internal static class WorkspaceIcons
{
    internal const string TeamIcon =
        "M12 2 L15 5 L15 9 L12 12 L9 9 L9 5 Z " +
        "M4 8 L6 10 L6 13 L4 15 L2 13 L2 10 Z " +
        "M20 8 L22 10 L22 13 L20 15 L18 13 L18 10 Z " +
        "M1 22 L1 19 L4 16 L8 18 L8 22 Z " +
        "M16 22 L16 18 L20 16 L23 19 L23 22 Z " +
        "M8 22 L8 17 L12 14 L16 17 L16 22 Z";
}
