#pragma warning disable CA2000 // harnesses are disposed by using

using Cascade.UI.Tests.Rendering;

namespace Cascade.UI.Tests;

/// <summary>
/// NavigationBar was never laid out, painted or wired to input: both modes drew nothing. Now
/// the title bar and the sidebar, rail, bottom and top styles are built, items switch sections
/// by click and keyboard, and the sidebar collapses.
/// </summary>
[NotInParallel(["ThemeSwitcher", "FocusManager", nameof(LayoutSolver.DefaultFontPath), "Toast"])]
public class NavigationBarTests
{
    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in NodeDiffer.GetChildren(node))
        {
            foreach (var n in Walk(child))
            {
                yield return n;
            }
        }
    }

    /// <summary>A page with a section bar bound to its state.</summary>
    private sealed class Page(NavBarStyle style, bool collapsible = false) : Component
    {
        internal string Section = "inbox";
        internal bool Collapsed;
        internal NavigationBar? Bar;

        protected override Node Render()
        {
            var bar = new NavigationBar(new Bindable<string>(Section, s => { Section = s; Invalidate(); }), style,
                new NavItem("inbox", new IconView(Icons.Inbox), "Inbox", badge: 3),
                new NavItem("sent", new IconView(Icons.Send), "Sent"),
                new NavItem("spam", new IconView(Icons.Ban), "Spam", enabled: false),
                new NavItem("drafts", new IconView(Icons.File), "Drafts"));
            Bar = collapsible ? bar.Collapsible(true, new Bindable<bool>(Collapsed, c => { Collapsed = c; Invalidate(); }), collapseWidth: 60, expandWidth: 220).CollapseButton() : bar;
            return style is NavBarStyle.Sidebar or NavBarStyle.Rail ? Bar.Height(400) : Bar.Width(400);
        }

    }

    private static (RenderHarness App, Page Page) Open(NavBarStyle style, bool collapsible = false)
    {
        FocusManager.ClearFocus();
        var page = new Page(style, collapsible);
        var app = new RenderHarness(() => page, width: 600, height: 500);
        app.Frame();
        app.Frame();
        return (app, page);
    }

    private static Rect At(Page page, string id)
    {
        var item = page.Bar!.Items.Single(i => i.Id == id);
        HitTester.TryGetAbsoluteBounds(page.Bar!, item, out var r);
        return r;
    }

    [Test]
    public async Task Sections_AreLaidOut_InEveryStyle()
    {
        foreach (var style in Enum.GetValues<NavBarStyle>())
        {
            var (app, page) = Open(style);
            using (app)
            {
                var bar = page.Bar!;
                await Assert.That(bar.LayoutData.Bounds.Width).IsGreaterThan(0f).Because(style.ToString());
                var inbox = At(page, "inbox");
                var sent = At(page, "sent");
                await Assert.That(inbox.Width > 0f && inbox.Height > 0f).IsTrue().Because(style.ToString());
                bool vertical = style is NavBarStyle.Sidebar or NavBarStyle.Rail;
                await Assert.That(vertical ? sent.Y > inbox.Y : sent.X > inbox.X).IsTrue().Because(style.ToString());
                await Assert.That(bar.Items[0].IsActive).IsTrue();
                // The badge is shown.
                await Assert.That(Walk(bar.Items[0].Content).OfType<Label>().Any(l => l.Text == "3")).IsTrue().Because(style.ToString());
                if (style == NavBarStyle.Rail)
                {
                    await Assert.That(Walk(bar.Items[0].Content).OfType<Label>().Any(l => l.Text == "Inbox")).IsFalse(); // icons only
                }
            }
        }
    }

    [Test]
    public async Task ClickingAnItem_SwitchesTheSection_ButNotADisabledOne()
    {
        var (app, page) = Open(NavBarStyle.Sidebar);
        using (app)
        {
            var sent = At(page, "sent");
            app.Click(sent.X + 20f, sent.Y + sent.Height / 2f);
            await Assert.That(page.Section).IsEqualTo("sent");
            await Assert.That(page.Bar!.Items.Single(i => i.Id == "sent").IsActive).IsTrue();

            var spam = At(page, "spam");
            app.Click(spam.X + 20f, spam.Y + spam.Height / 2f);
            await Assert.That(page.Section).IsEqualTo("sent");
        }
    }

    [Test]
    public async Task Keys_MoveAlongTheBar_SkippingDisabled_AndEnterChooses()
    {
        var (app, page) = Open(NavBarStyle.Bottom);
        using (app)
        {
            FocusManager.RequestFocus(page.Bar!.Items[1]); // sent
            app.Press(Key.Right); // skips spam
            await Assert.That(((NavItem)FocusManager.FocusedElement!).Id).IsEqualTo("drafts");
            app.Press(Key.Right); // wraps
            await Assert.That(((NavItem)FocusManager.FocusedElement!).Id).IsEqualTo("inbox");
            app.Press(Key.Left);
            app.Press(Key.Enter);
            await Assert.That(page.Section).IsEqualTo("drafts");
        }
    }

    [Test]
    public async Task TheSidebar_CollapsesToIcons_WithItsButton()
    {
        var (app, page) = Open(NavBarStyle.Sidebar, collapsible: true);
        using (app)
        {
            await Assert.That(page.Bar!.LayoutData.Bounds.Width).IsEqualTo(220f);
            var toggle = Walk(page.Bar!.Tree).OfType<IconButton>().Single();
            HitTester.TryGetAbsoluteBounds(page.Bar!, toggle, out var r);
            app.Click(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            app.Frame();
            await Assert.That(page.Collapsed).IsTrue();
            await Assert.That(page.Bar!.LayoutData.Bounds.Width).IsEqualTo(60f);
            await Assert.That(Walk(page.Bar!.Items[1].Content).OfType<Label>().Any(l => l.Text == "Sent")).IsFalse();
        }
    }

    [Test]
    public async Task TheTitleBar_ShowsItsTitle_BackAndSearch()
    {
        FocusManager.ClearFocus();
        string? searched = null;
        var reference = new NavigationBarRef();
        var bar = new NavigationBar("Orders", subtitle: "12 open", showBack: true,
            trailing: new IconButton(Icons.Share, () => { }).AccessibleLabel("Share"),
            search: new SearchBarOptions { Placeholder = "Find", OnChanged = s => searched = s }).SearchRef(reference);
        using var app = new RenderHarness(() => bar.Width(500), width: 600, height: 300);
        app.Frame();
        var labels = Walk(bar.Tree).OfType<Label>().Select(l => l.Text ?? string.Empty).ToList();
        await Assert.That(labels).Contains("Orders");
        await Assert.That(labels).Contains("12 open");
        await Assert.That(Walk(bar.Tree).OfType<Button>().Any(b => b.Label.Resolve() == "Back")).IsTrue();
        await Assert.That(bar.LayoutData.Bounds.Height).IsGreaterThan(30f);

        reference.ExpandSearch();
        app.Frame();
        var field = Walk(bar.Tree).OfType<TextInput>().Single();
        field.Value.OnChange("wid");
        await Assert.That(searched).IsEqualTo("wid");
        bar.SetSearchExpanded(false);
        await Assert.That(reference.IsSearchExpanded).IsFalse();
        await Assert.That(searched).IsEqualTo(string.Empty);
    }
}
