using Cascade.UI;

namespace MailShell;

// ── Data models ───────────────────────────────────────────────────────────────

internal sealed record Folder(string Id, string Name, Icon FolderIcon, int UnreadCount);

internal sealed record Message(
    string Id,
    string FolderId,
    string From,
    string Subject,
    string Preview,
    string Body,
    DateTimeOffset SentAt,
    bool IsRead,
    bool IsStarred
);

// ── Icons ─────────────────────────────────────────────────────────────────────

internal static class MailIcons
{
    internal static readonly Icon Inbox = new(
        ["M3 3H21V21H3z", "M3 14H8L10 17H14L16 14H21"],
        new Size(24, 24), 24f, "Inbox");

    internal static readonly Icon Send = new(
        ["M22 2L11 13", "M22 2L15 22L11 13L2 9z"],
        new Size(24, 24), 24f, "Sent");

    internal static readonly Icon FileText = new(
        ["M4 2V22H20V8L14 2z", "M14 2V8H20", "M8 13H16", "M8 17H16"],
        new Size(24, 24), 24f, "Drafts");

    internal static readonly Icon Archive = new(
        ["M3 8V21H21V8", "M1 3H23V8H1z", "M10 12H14"],
        new Size(24, 24), 24f, "Archive");

    internal static readonly Icon Trash = new(
        ["M3 6H21", "M8 6V4H16V6", "M5 6V20H19V6"],
        new Size(24, 24), 24f, "Trash");

    internal static readonly Icon Star = new(
        ["M12 2L15 8L22 9L17 14L18 22L12 18L6 22L7 14L2 9L9 8z"],
        new Size(24, 24), 24f, "Starred");

    internal static readonly Icon Mail = new(
        ["M3 5H21V19H3z", "M3 5L12 13L21 5"],
        new Size(24, 24), 24f, "Mail");

    internal static readonly Icon MailOpen = new(
        ["M3 9V19H21V9L12 3z", "M3 9L12 15L21 9"],
        new Size(24, 24), 24f, "Open Mail");

    internal static readonly Icon Reply = new(
        ["M9 17L4 12L9 7", "M20 18V16C20 13.8 18.2 12 16 12H4"],
        new Size(24, 24), 24f, "Reply");

    internal static readonly Icon Forward = new(
        ["M15 17L20 12L15 7", "M4 18V16C4 13.8 5.8 12 8 12H20"],
        new Size(24, 24), 24f, "Forward");

    internal static readonly Icon Search = new(
        ["M11 3L11 3 19 19", "M21 21L16 16"],
        new Size(24, 24), 24f, "Search");
}

// ── Mock data ─────────────────────────────────────────────────────────────────

internal static class MockData
{
    internal static IReadOnlyList<Folder> GetFolders() =>
    [
        new("inbox",   "Inbox",   MailIcons.Inbox,    5),
        new("sent",    "Sent",    MailIcons.Send,     0),
        new("drafts",  "Drafts",  MailIcons.FileText, 2),
        new("archive", "Archive", MailIcons.Archive,  0),
        new("trash",   "Trash",   MailIcons.Trash,    0),
        new("starred", "Starred", MailIcons.Star,     0),
    ];

    internal static IReadOnlyList<Message> GetMessages() =>
    [
        new("m1", "inbox", "Alice Martinez", "Q4 Budget Review",
            "Hi team, I've attached the Q4 budget review for your consideration...",
            "Hi team,\n\nI've attached the Q4 budget review for your consideration. Please take a look at the projected expenses for the engineering department — we're slightly over budget on infrastructure costs.\n\nKey highlights:\n- Cloud hosting up 12% from Q3\n- Developer tooling costs stable\n- Hiring pipeline requires additional $50k allocation\n\nLet me know your thoughts by Friday.\n\nBest,\nAlice",
            DateTimeOffset.Now.AddMinutes(-8), false, false),

        new("m2", "inbox", "Bob Chen", "Sprint Planning Notes",
            "Here are the notes from today's sprint planning session...",
            "Hey everyone,\n\nHere are the notes from today's sprint planning session.\n\nCommitted stories:\n1. User authentication refactor (8 pts)\n2. Dashboard performance optimization (5 pts)\n3. Mobile push notification support (13 pts)\n4. API rate limiting implementation (3 pts)\n\nCarried over:\n- Search indexing improvements (deferred to next sprint)\n\nTotal committed: 29 points\nVelocity average: 31 points\n\nLet me know if I missed anything.\n\nBob",
            DateTimeOffset.Now.AddMinutes(-32), false, true),

        new("m3", "inbox", "Carol Davis", "Design System Update",
            "The new component library is ready for review. I've updated the...",
            "Hi all,\n\nThe new component library v2.4 is ready for review. I've updated the Figma files and the Storybook documentation.\n\nChanges in this version:\n- New date picker component\n- Improved accessibility on all form inputs\n- Dark mode tokens finalized\n- Icon set expanded with 24 new icons\n\nPlease review and leave feedback in the Figma comments. Targeting merge by end of week.\n\nThanks,\nCarol",
            DateTimeOffset.Now.AddHours(-2), false, false),

        new("m4", "inbox", "David Kim", "Server Migration Update",
            "Good news — the database migration completed successfully overnight...",
            "Team,\n\nGood news — the database migration completed successfully overnight. All services are running on the new cluster.\n\nPerformance improvements observed:\n- Query latency down 40%\n- Connection pool utilization improved\n- Failover tested and working\n\nI'll monitor for the next 48 hours and then we can decommission the old servers.\n\nDavid",
            DateTimeOffset.Now.AddHours(-5), true, false),

        new("m5", "inbox", "Elena Rodriguez", "Welcome Aboard!",
            "Welcome to the team! I'm thrilled to have you join us...",
            "Welcome to the team!\n\nI'm thrilled to have you join our engineering group. Your first week agenda:\n\nMonday: Orientation and laptop setup\nTuesday: Meet your team, codebase walkthrough\nWednesday: Development environment setup\nThursday: First ticket assignment\nFriday: Team lunch and retrospective\n\nDon't hesitate to reach out if you need anything. My calendar is always open.\n\nBest,\nElena Rodriguez\nEngineering Manager",
            DateTimeOffset.Now.AddDays(-1), true, false),

        new("m6", "inbox", "Frank Morrison", "Code Review: Auth Refactor",
            "I've left some comments on the auth refactor PR. The overall approach...",
            "Hey,\n\nI've left some comments on the auth refactor PR (#847). The overall approach looks solid, but I have concerns about a few things:\n\n1. The token refresh logic might race under concurrent requests\n2. We should add integration tests for the SSO flow\n3. The error messages could be more specific for debugging\n\nNice work on the session management cleanup though — much cleaner than before.\n\nFrank",
            DateTimeOffset.Now.AddDays(-1).AddHours(-3), true, false),

        new("m7", "sent", "Me", "Re: Q4 Budget Review",
            "Thanks Alice, I'll review the numbers and get back to you...",
            "Thanks Alice, I'll review the numbers and get back to you by Thursday.\n\nOne question — does the infrastructure increase include the staging environment costs?",
            DateTimeOffset.Now.AddMinutes(-5), true, false),

        new("m8", "sent", "Me", "Meeting Request: Architecture Review",
            "Hi team, I'd like to schedule an architecture review...",
            "Hi team,\n\nI'd like to schedule an architecture review for the new microservices migration plan. Please check your calendars for next Tuesday at 2pm.\n\nAgenda:\n- Current state overview\n- Proposed architecture\n- Migration timeline\n- Risk assessment",
            DateTimeOffset.Now.AddHours(-1), true, false),

        new("m9", "drafts", "Me", "Project Proposal: ML Pipeline",
            "Draft — outlining the machine learning pipeline for...",
            "Draft\n\nProject Proposal: ML Pipeline\n\nObjective: Build an automated ML pipeline for customer churn prediction.\n\nPhase 1: Data collection and preprocessing\nPhase 2: Feature engineering\nPhase 3: Model training and validation\nPhase 4: Deployment and monitoring",
            DateTimeOffset.Now.AddDays(-2), false, false),

        new("m10", "drafts", "Me", "Team Offsite Planning",
            "Ideas for Q1 team offsite...",
            "Ideas for Q1 team offsite:\n\n- Location options: Lake house, mountain retreat\n- Activities: Hackathon, team building, strategy sessions\n- Duration: 3 days\n- Budget: TBD",
            DateTimeOffset.Now.AddDays(-3), false, false),

        new("m11", "inbox", "Grace Liu", "Performance Report",
            "Attached is the monthly performance report for our core services...",
            "Hi team,\n\nAttached is the monthly performance report for our core services.\n\nHighlights:\n- API response time p99: 142ms (target: <200ms) ✓\n- Uptime: 99.97% (target: 99.9%) ✓\n- Error rate: 0.02% (down from 0.05%)\n- Active users: 48,230 (+8% MoM)\n\nAll metrics are within acceptable ranges. The latency improvements from the caching layer are clearly visible.\n\nGrace",
            DateTimeOffset.Now.AddDays(-2), true, false),

        new("m12", "inbox", "Henry Park", "Security Audit Results",
            "The Q3 security audit is complete. No critical vulnerabilities found...",
            "Team,\n\nThe Q3 security audit is complete.\n\nResults:\n- Critical: 0\n- High: 1 (dependency update needed — patched)\n- Medium: 3 (all tracked in backlog)\n- Low: 7 (informational)\n\nOverall security posture: Strong. Full report available in Confluence.\n\nHenry",
            DateTimeOffset.Now.AddDays(-3), true, false),
    ];
}

// ── Mail Shell Page ───────────────────────────────────────────────────────────

internal sealed partial class MailShellPage : Component
{
    readonly IReadOnlyList<Folder> folders = MockData.GetFolders();
    readonly IReadOnlyList<Message> allMessages = MockData.GetMessages();
    string activeFolder = "inbox";
    string? activeMessageId;
    IReadOnlySet<string> selectedIds = new HashSet<string>();

    IReadOnlyList<Message> CurrentMessages =>
        allMessages.Where(m => m.FolderId == activeFolder).ToList();

    Message? ActiveMsg => activeMessageId is not null
        ? allMessages.FirstOrDefault(m => m.Id == activeMessageId)
        : null;

    protected override Node Render() =>
        new Column(children:
        [
            NavBar(),
            new SplitView(
                first: FolderSidebar(),
                second: new SplitView(
                    first: MessageList(),
                    second: MessageDetail()
                ).FirstSize(340)
            ).FirstSize(220).Grow(1)
        ]);

    // ── Nav bar ───────────────────────────────────────────────────────────────

    Node NavBar() =>
        new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(MailIcons.Mail, size: 20).Color(ThemeSwitcher.ActiveColors.Primary),
            new Label("Mail").Bold().FontSize(18).Grow(1),
            new Label($"{folders.Where(f => f.UnreadCount > 0).Sum(f => f.UnreadCount)} unread")
                .FontSize(12).Color(ThemeSwitcher.ActiveColors.TextMuted)
        ]).Padding(horizontal: 16, vertical: 12)
          .Background(ThemeSwitcher.ActiveColors.Surface)
          .BorderBottom(ThemeSwitcher.ActiveColors.Border);

    // ── Folder sidebar ────────────────────────────────────────────────────────

    Node FolderSidebar()
    {
        return new ScrollView(
            new Column(children: folders.Select(FolderRow).ToArray())
                .Padding(0, 8)
        ).Background(ThemeSwitcher.ActiveColors.SurfaceAlt);
    }

    Node FolderRow(Folder folder)
    {
        bool isActive = folder.Id == activeFolder;
        var colors = ThemeSwitcher.ActiveColors;
        var iconColor = isActive ? colors.Primary : colors.TextMuted;
        // A neutral raised fill for the active row — reads as a selected surface
        // rather than a translucent blue wash, with the accent carried by the
        // icon and the semibold label.
        var bgColor = isActive ? colors.Text.Opacity(0.09f) : ColorValue.Transparent;

        return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            new IconView(folder.FolderIcon, size: 18).Color(iconColor),
            MakeLabel(folder.Name, isActive)
                .Color(colors.Text)
                .Grow(1),
            folder.UnreadCount > 0
                ? new Label(folder.UnreadCount.ToString())
                    .FontSize(11)
                    .Color(colors.TextOnPrimary)
                    .Padding(horizontal: 6, vertical: 1)
                    .Background(isActive ? colors.Primary : colors.TextMuted)
                    .CornerRadius(8)
                : Node.Empty
        ]).Padding(horizontal: 12, vertical: 9)
          .CornerRadius(6)
          .Background(bgColor)
          .OnTap(() =>
          {
              activeFolder = folder.Id;
              activeMessageId = null;
              selectedIds = new HashSet<string>();
              Invalidate();
          });
    }

    // ── Message list ──────────────────────────────────────────────────────────

    Node MessageList()
    {
        var messages = CurrentMessages;

        if (messages.Count == 0)
        {
            return new EmptyState(
                "No messages",
                description: "This folder is empty"
            );
        }

        return new ScrollView(
            new Column(
                crossAxisAlignment: CrossAxisAlignment.Stretch,
                children: messages.Select(MessageRow).ToArray()
            )
        ).Background(ThemeSwitcher.ActiveColors.Surface);
    }

    Node MessageRow(Message msg)
    {
        bool isOpen = msg.Id == activeMessageId;
        var colors = ThemeSwitcher.ActiveColors;
        // Neutral raised surface for the open message (a clear step up from the
        // recessed black list) instead of a translucent blue wash.
        var bgColor = isOpen
            ? colors.SurfaceAlt
            : ColorValue.Transparent;

        return new Row(spacing: 12, crossAxisAlignment: CrossAxisAlignment.Center, children:
        [
            // Unread dot
            new Row(children: []).Width(8).Height(8)
                .CornerRadius(4)
                .Background(msg.IsRead ? ColorValue.Transparent : colors.Primary),

            new Column(spacing: 3, children:
            [
                new Row(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children:
                [
                    MakeLabel(msg.From, !msg.IsRead)
                        .MaxLines(1)
                        .Grow(1),
                    new Label(FormatDate(msg.SentAt))
                        .FontSize(11)
                        .MaxLines(1)
                        .Color(colors.TextMuted)
                ]),
                MakeLabel(msg.Subject, !msg.IsRead)
                    .FontSize(13)
                    .MaxLines(1),
                new Label(msg.Preview)
                    .FontSize(12)
                    .Color(colors.TextMuted)
                    .MaxLines(1)
            ]).Grow(1),

            msg.IsStarred
                ? new IconView(MailIcons.Star, size: 14).Color(ThemeSwitcher.Current.Palette.Yellow)
                : Node.Empty
        ]).Padding(horizontal: 16, vertical: 10)
          .Background(bgColor)
          .BorderBottom(ThemeSwitcher.ActiveColors.Border)
          .OnTap(() =>
          {
              activeMessageId = msg.Id;
              selectedIds = new HashSet<string>();
              Invalidate();
          });
    }

    // ── Message detail ────────────────────────────────────────────────────────

    Node MessageDetail()
    {
        if (ActiveMsg is not { } msg)
        {
            return new EmptyState(
                "No message selected",
                description: "Select a message to read it"
            );
        }

        return new Column(children:
        [
            // Toolbar
            new Row(spacing: 4, crossAxisAlignment: CrossAxisAlignment.Center, children:
            [
                new IconButton(MailIcons.Reply, () => { }),
                new IconButton(MailIcons.Forward, () => { }),
                new IconButton(MailIcons.Archive, () => { }),
                new IconButton(MailIcons.Trash, () => { }),
                new Spacer(),
                new IconButton(MailIcons.Star, () =>
                {
                    // Toggle star (visual only in this example)
                })
            ]).Padding(horizontal: 16, vertical: 8)
              .BorderBottom(ThemeSwitcher.ActiveColors.Border),

            // Message content
            new ScrollView(
                new Column(spacing: 16, children:
                [
                    // Header
                    new Column(spacing: 6, children:
                    [
                        new Label(msg.Subject).Bold().FontSize(20),
                        new Row(spacing: 10, crossAxisAlignment: CrossAxisAlignment.Center, children:
                        [
                            new Avatar(msg.From).Size(36),
                            new Column(spacing: 2, children:
                            [
                                new Label(msg.From).Bold().MaxLines(1),
                                new Label(msg.SentAt.ToString("MMMM d, yyyy \\a\\t h:mm tt"))
                                    .FontSize(12)
                                    .MaxLines(1)
                                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                            ])
                        ])
                    ]),

                    // Separator
                    new Separator(),

                    // Body
                    new Label(msg.Body)
                        .FontSize(14)
                        .Color(ThemeSwitcher.ActiveColors.Text)
                ]).Padding(24)
            ).Grow(1)
        ]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatDate(DateTimeOffset sent)
    {
        var diff = DateTimeOffset.Now.Date - sent.Date;
        if (diff.TotalDays < 1) { return sent.ToString("h:mm tt"); }
        if (diff.TotalDays < 7) { return sent.ToString("ddd"); }
        return sent.ToString("MMM d");
    }

    static Label MakeLabel(string text, bool bold)
    {
        var label = new Label(text);
        return bold ? label.Bold() : label;
    }
}
