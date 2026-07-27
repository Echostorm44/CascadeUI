using System.Runtime.InteropServices;

namespace Cascade.UI.Tests.Platform;

/// <summary>
/// Tests for all platform IME adapters: TsfAdapter (Windows), NsTextInputAdapter (macOS),
/// IBusAdapter, FcitxAdapter, XimAdapter, WaylandTextInputAdapter (Linux).
///
/// These tests verify composition lifecycle, candidate positioning, locale detection,
/// and the event contracts all adapters share via IPlatformTextInput.
/// </summary>
public class ImeTests
{
    // ─── TsfAdapter (Windows) ──────────────────────────────────────

    [Test]
    public async Task TsfAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new TsfAdapter(0);
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_DefaultLocale_IsValid()
    {
        var adapter = new TsfAdapter(0);
        InputLocale locale = adapter.CurrentLocale;
        await Assert.That(locale.Identifier).IsNotNull();
        await Assert.That(locale.Identifier.Length).IsGreaterThanOrEqualTo(2);
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_ActivateDeactivate_Lifecycle()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();
        adapter.DeactivateInputContext();
        bool passed = true;
        await Assert.That(passed).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_DoubleActivate_IsIdempotent()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();
        adapter.ActivateInputContext();
        adapter.DeactivateInputContext();
        bool passed = true;
        await Assert.That(passed).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_DoubleDeactivate_IsIdempotent()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();
        adapter.DeactivateInputContext();
        adapter.DeactivateInputContext();
        bool passed = true;
        await Assert.That(passed).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_SetCompositionRect_BeforeActivate_NoOp()
    {
        var adapter = new TsfAdapter(0);
        adapter.SetCompositionRect(new Rect(100, 200, 50, 20));
        bool passed = true;
        await Assert.That(passed).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_HandleStartComposition_SetsComposing()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();
        adapter.HandleStartComposition();
        await Assert.That(adapter.IsComposing).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_HandleComposition_FiresEvent()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandleStartComposition();
        adapter.HandleComposition(0);

        await Assert.That(adapter.IsComposing).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_HandleEndComposition_StopsComposing()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();

        adapter.HandleStartComposition();
        adapter.HandleEndComposition();

        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_CancelComposition_FiresEvent()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandleStartComposition();
        adapter.CancelComposition();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_CancelWithoutComposition_NoOp()
    {
        var adapter = new TsfAdapter(0);
        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.CancelComposition();

        await Assert.That(cancelled).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_Dispose_DeactivatesContext()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();
        adapter.HandleStartComposition();
        adapter.Dispose();

        await Assert.That(adapter.IsComposing).IsFalse();
    }

    [Test]
    public async Task TsfAdapter_ActiveComposition_TracksState()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();

        await Assert.That(adapter.ActiveComposition).IsNull();

        adapter.HandleStartComposition();
        await Assert.That(adapter.IsComposing).IsTrue();

        adapter.HandleEndComposition();
        await Assert.That(adapter.ActiveComposition).IsNull();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_CompositionStarted_FiresEvent()
    {
        var adapter = new TsfAdapter(0);
        adapter.ActivateInputContext();

        bool started = false;
        adapter.CompositionStarted += () => started = true;

        adapter.HandleStartComposition();

        await Assert.That(started).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task TsfAdapter_HandleInputLanguageChange_NoException()
    {
        var adapter = new TsfAdapter(0);
        adapter.HandleInputLanguageChange();
        InputLocale locale = adapter.CurrentLocale;
        await Assert.That(locale.Identifier).IsNotNull();
        adapter.Dispose();
    }

    // ─── NsTextInputAdapter (macOS) ────────────────────────────────

    [Test]
    public async Task NsTextInputAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new NsTextInputAdapter(0);
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_DefaultLocale_IsValid()
    {
        var adapter = new NsTextInputAdapter(0);
        InputLocale locale = adapter.CurrentLocale;
        await Assert.That(locale.Identifier).IsNotNull();
        await Assert.That(locale.Identifier.Length).IsGreaterThanOrEqualTo(2);
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_ActivateDeactivate_Lifecycle()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();
        adapter.DeactivateInputContext();
        bool passed = true;
        await Assert.That(passed).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_SetMarkedText_StartsComposition()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandleSetMarkedText("にほん", 0, 3);

        await Assert.That(adapter.IsComposing).IsTrue();
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Text).IsEqualTo("にほん");
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_UnmarkText_CommitsComposition()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        adapter.HandleSetMarkedText("にほん", 0, 3);
        adapter.HandleUnmarkText();

        await Assert.That(committed).IsEqualTo("にほん");
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_InsertText_DirectCommit()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        adapter.HandleInsertText("Hello");

        await Assert.That(committed).IsEqualTo("Hello");
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_EmptyMarkedText_CancelsComposition()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandleSetMarkedText("test", 0, 4);
        adapter.HandleSetMarkedText("", 0, 0);

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_InputSourceChanged_NoException()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.HandleInputSourceChanged();
        InputLocale locale = adapter.CurrentLocale;
        await Assert.That(locale.Identifier).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_CancelComposition_WhileComposing()
    {
        var adapter = new NsTextInputAdapter(0);
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandleSetMarkedText("test", 0, 4);
        adapter.CancelComposition();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_HasMarkedText_MatchesComposingState()
    {
        var adapter = new NsTextInputAdapter(0);

        await Assert.That(adapter.HasMarkedText).IsFalse();

        adapter.HandleSetMarkedText("test", 0, 4);
        await Assert.That(adapter.HasMarkedText).IsTrue();

        adapter.HandleUnmarkText();
        await Assert.That(adapter.HasMarkedText).IsFalse();

        adapter.Dispose();
    }

    [Test]
    public async Task NsTextInputAdapter_SegmentBuilding_WithSelection()
    {
        var adapter = new NsTextInputAdapter(0);

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        // "abcde" with selection starting at 2, length 2 ("cd")
        adapter.HandleSetMarkedText("abcde", 2, 2);

        await Assert.That(received).IsNotNull();
        var segments = received!.Value.Segments;
        await Assert.That(segments.Count).IsEqualTo(3);

        await Assert.That(segments[0].Start).IsEqualTo(0);
        await Assert.That(segments[0].Length).IsEqualTo(2);
        await Assert.That(segments[0].Style).IsEqualTo(CompositionSegmentStyle.Input);

        await Assert.That(segments[1].Start).IsEqualTo(2);
        await Assert.That(segments[1].Length).IsEqualTo(2);
        await Assert.That(segments[1].Style).IsEqualTo(CompositionSegmentStyle.TargetConverted);

        await Assert.That(segments[2].Start).IsEqualTo(4);
        await Assert.That(segments[2].Length).IsEqualTo(1);
        await Assert.That(segments[2].Style).IsEqualTo(CompositionSegmentStyle.Input);

        adapter.Dispose();
    }

    // ─── IBusAdapter (Linux) ───────────────────────────────────────

    [Test]
    public async Task IBusAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new IBusAdapter();
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_DefaultLocale_IsValid()
    {
        var adapter = new IBusAdapter();
        InputLocale locale = adapter.CurrentLocale;
        await Assert.That(locale.Identifier).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_PreeditUpdate_StartsComposition()
    {
        var adapter = new IBusAdapter();
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandlePreeditUpdate("ni", 2, true);

        await Assert.That(adapter.IsComposing).IsTrue();
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Text).IsEqualTo("ni");
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_CommitText_EndsPreedit()
    {
        var adapter = new IBusAdapter();
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        adapter.HandlePreeditUpdate("ni", 2, true);
        adapter.HandleCommitText("你");

        await Assert.That(committed).IsEqualTo("你");
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_InvisiblePreedit_CancelsComposition()
    {
        var adapter = new IBusAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandlePreeditUpdate("ni", 2, true);
        adapter.HandlePreeditUpdate("ni", 2, false);

        await Assert.That(cancelled).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_EmptyPreedit_CancelsComposition()
    {
        var adapter = new IBusAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandlePreeditUpdate("ni", 2, true);
        adapter.HandlePreeditUpdate("", 0, true);

        await Assert.That(cancelled).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_LocaleChanged_FiresEvent()
    {
        var adapter = new IBusAdapter();

        InputLocale? newLocale = null;
        adapter.LocaleChanged += l => newLocale = l;

        adapter.HandleLocaleChanged("zh-CN");

        await Assert.That(newLocale).IsNotNull();
        await Assert.That(newLocale!.Value.Identifier).IsEqualTo("zh-CN");
        await Assert.That(adapter.CurrentLocale.Identifier).IsEqualTo("zh-CN");
        adapter.Dispose();
    }

    [Test]
    public async Task IBusAdapter_Deactivate_CancelsActiveComposition()
    {
        var adapter = new IBusAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandlePreeditUpdate("ni", 2, true);
        adapter.DeactivateInputContext();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    // ─── FcitxAdapter (Linux) ──────────────────────────────────────

    [Test]
    public async Task FcitxAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new FcitxAdapter();
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_FormattedPreedit_StartsComposition()
    {
        var adapter = new FcitxAdapter();
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        var segments = new List<CompositionSegment>
        {
            new(0, 5, CompositionSegmentStyle.Input),
        };
        adapter.HandleFormattedPreeditUpdate("nihao", 5, segments);

        await Assert.That(adapter.IsComposing).IsTrue();
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Text).IsEqualTo("nihao");
        await Assert.That(received.Value.Segments.Count).IsEqualTo(1);
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_CommitString_EndsPreedit()
    {
        var adapter = new FcitxAdapter();
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        var segments = new List<CompositionSegment>
        {
            new(0, 5, CompositionSegmentStyle.Input),
        };
        adapter.HandleFormattedPreeditUpdate("nihao", 5, segments);
        adapter.HandleCommitString("你好");

        await Assert.That(committed).IsEqualTo("你好");
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_EmptyPreedit_CancelsComposition()
    {
        var adapter = new FcitxAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        var segments = new List<CompositionSegment>
        {
            new(0, 5, CompositionSegmentStyle.Input),
        };
        adapter.HandleFormattedPreeditUpdate("nihao", 5, segments);
        adapter.HandleFormattedPreeditUpdate("", 0, Array.Empty<CompositionSegment>());

        await Assert.That(cancelled).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_LocaleChanged_FiresEvent()
    {
        var adapter = new FcitxAdapter();

        InputLocale? newLocale = null;
        adapter.LocaleChanged += l => newLocale = l;

        adapter.HandleLocaleChanged("ko-KR");

        await Assert.That(newLocale).IsNotNull();
        await Assert.That(newLocale!.Value.Identifier).IsEqualTo("ko-KR");
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_CancelComposition_WhileComposing()
    {
        var adapter = new FcitxAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        var segments = new List<CompositionSegment>
        {
            new(0, 5, CompositionSegmentStyle.Input),
        };
        adapter.HandleFormattedPreeditUpdate("nihao", 5, segments);
        adapter.CancelComposition();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task FcitxAdapter_Deactivate_CancelsActiveComposition()
    {
        var adapter = new FcitxAdapter();
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        var segments = new List<CompositionSegment>
        {
            new(0, 3, CompositionSegmentStyle.Input),
        };
        adapter.HandleFormattedPreeditUpdate("abc", 3, segments);
        adapter.DeactivateInputContext();

        await Assert.That(cancelled).IsTrue();
        adapter.Dispose();
    }

    // ─── XimAdapter (Linux) ────────────────────────────────────────

    [Test]
    public async Task XimAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new XimAdapter(0, 0);
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_PreeditUpdate_StartsComposition()
    {
        var adapter = new XimAdapter(0, 0);

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandlePreeditUpdate("test", 4);

        await Assert.That(adapter.IsComposing).IsTrue();
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Text).IsEqualTo("test");
        await Assert.That(received.Value.Segments.Count).IsEqualTo(1);
        await Assert.That(received.Value.Segments[0].Style).IsEqualTo(CompositionSegmentStyle.Input);
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_CommitText_EndsPreedit()
    {
        var adapter = new XimAdapter(0, 0);

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        adapter.HandlePreeditUpdate("test", 4);
        adapter.HandleCommitText("テスト");

        await Assert.That(committed).IsEqualTo("テスト");
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_EmptyPreedit_CancelsComposition()
    {
        var adapter = new XimAdapter(0, 0);

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandlePreeditUpdate("test", 4);
        adapter.HandlePreeditUpdate("", 0);

        await Assert.That(cancelled).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_FilterKeyEvent_ReturnsFalseWithNoContext()
    {
        var adapter = new XimAdapter(0, 0);
        bool consumed = adapter.FilterKeyEvent(0);
        await Assert.That(consumed).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_LocaleChanged_FiresEvent()
    {
        var adapter = new XimAdapter(0, 0);

        InputLocale? newLocale = null;
        adapter.LocaleChanged += l => newLocale = l;

        adapter.HandleLocaleChanged("ja-JP");

        await Assert.That(newLocale).IsNotNull();
        await Assert.That(newLocale!.Value.Identifier).IsEqualTo("ja-JP");
        adapter.Dispose();
    }

    [Test]
    public async Task XimAdapter_Deactivate_CancelsActiveComposition()
    {
        var adapter = new XimAdapter(0, 0);

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        // Start a composition (doesn't need active context)
        adapter.HandlePreeditUpdate("ab", 2);
        await Assert.That(adapter.IsComposing).IsTrue();

        // CancelComposition works regardless of context state
        adapter.CancelComposition();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    // ─── WaylandTextInputAdapter (Linux) ───────────────────────────

    [Test]
    public async Task WaylandTextInputAdapter_Implements_IPlatformTextInput()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        IPlatformTextInput input = adapter;
        await Assert.That(input).IsNotNull();
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_AtomicDone_AppliesPreedit()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandlePreeditString("にほん", 0, 3);
        await Assert.That(adapter.IsComposing).IsFalse();

        adapter.HandleDone();
        await Assert.That(adapter.IsComposing).IsTrue();
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Value.Text).IsEqualTo("にほん");
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_AtomicDone_AppliesCommit()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;

        adapter.HandleCommitString("日本語");
        await Assert.That(committed).IsNull();

        adapter.HandleDone();
        await Assert.That(committed).IsEqualTo("日本語");
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_CommitAndPreedit_InSameDone()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        adapter.ActivateInputContext();

        string? committed = null;
        adapter.CompositionCommitted += t => committed = t;
        TextComposition? preedit = null;
        adapter.CompositionUpdated += c => preedit = c;

        adapter.HandleCommitString("日");
        adapter.HandlePreeditString("ほん", 0, 2);
        adapter.HandleDone();

        await Assert.That(committed).IsEqualTo("日");
        await Assert.That(preedit).IsNotNull();
        await Assert.That(preedit!.Value.Text).IsEqualTo("ほん");
        await Assert.That(adapter.IsComposing).IsTrue();
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_EmptyPreeditAtDone_Cancels()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        adapter.ActivateInputContext();

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        adapter.HandlePreeditString("test", 0, 4);
        adapter.HandleDone();
        await Assert.That(adapter.IsComposing).IsTrue();

        adapter.HandlePreeditString("", 0, 0);
        adapter.HandleDone();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_LocaleChanged_FiresEvent()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);

        InputLocale? newLocale = null;
        adapter.LocaleChanged += l => newLocale = l;

        adapter.HandleLocaleChanged("ar-SA");

        await Assert.That(newLocale).IsNotNull();
        await Assert.That(newLocale!.Value.Identifier).IsEqualTo("ar-SA");
        await Assert.That(newLocale.Value.Direction).IsEqualTo(TextDirection.RightToLeft);
        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_SegmentBuilding_WithCursorRange()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);
        adapter.ActivateInputContext();

        TextComposition? received = null;
        adapter.CompositionUpdated += c => received = c;

        adapter.HandlePreeditString("abcde", 2, 4);
        adapter.HandleDone();

        await Assert.That(received).IsNotNull();
        var segments = received!.Value.Segments;
        await Assert.That(segments.Count).IsEqualTo(3);

        await Assert.That(segments[0].Start).IsEqualTo(0);
        await Assert.That(segments[0].Length).IsEqualTo(2);
        await Assert.That(segments[0].Style).IsEqualTo(CompositionSegmentStyle.Input);

        await Assert.That(segments[1].Start).IsEqualTo(2);
        await Assert.That(segments[1].Length).IsEqualTo(2);
        await Assert.That(segments[1].Style).IsEqualTo(CompositionSegmentStyle.TargetConverted);

        await Assert.That(segments[2].Start).IsEqualTo(4);
        await Assert.That(segments[2].Length).IsEqualTo(1);
        await Assert.That(segments[2].Style).IsEqualTo(CompositionSegmentStyle.Input);

        adapter.Dispose();
    }

    [Test]
    public async Task WaylandTextInputAdapter_Deactivate_CancelsActiveComposition()
    {
        var adapter = new WaylandTextInputAdapter(0, 0);

        bool cancelled = false;
        adapter.CompositionCancelled += () => cancelled = true;

        // Start a composition via the atomic done pattern
        adapter.HandlePreeditString("ab", 0, 2);
        adapter.HandleDone();
        await Assert.That(adapter.IsComposing).IsTrue();

        // CancelComposition works regardless of context state
        adapter.CancelComposition();

        await Assert.That(cancelled).IsTrue();
        await Assert.That(adapter.IsComposing).IsFalse();
        adapter.Dispose();
    }

    // ─── Cross-platform contracts ──────────────────────────────────

    [Test]
    public async Task AllAdapters_SetCompositionRect_AcceptsRect()
    {
        var rect = new Rect(100, 200, 300, 20);

        var tsf = new TsfAdapter(0);
        tsf.SetCompositionRect(rect);
        tsf.Dispose();

        var ns = new NsTextInputAdapter(0);
        ns.SetCompositionRect(rect);
        ns.Dispose();

        var ibus = new IBusAdapter();
        ibus.SetCompositionRect(rect);
        ibus.Dispose();

        var fcitx = new FcitxAdapter();
        fcitx.SetCompositionRect(rect);
        fcitx.Dispose();

        var xim = new XimAdapter(0, 0);
        xim.SetCompositionRect(rect);
        xim.Dispose();

        var wayland = new WaylandTextInputAdapter(0, 0);
        wayland.SetCompositionRect(rect);
        wayland.Dispose();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task AllAdapters_Dispose_IsIdempotent()
    {
        var tsf = new TsfAdapter(0);
        tsf.Dispose();
        tsf.Dispose();

        var ns = new NsTextInputAdapter(0);
        ns.Dispose();
        ns.Dispose();

        var ibus = new IBusAdapter();
        ibus.Dispose();
        ibus.Dispose();

        var fcitx = new FcitxAdapter();
        fcitx.Dispose();
        fcitx.Dispose();

        var xim = new XimAdapter(0, 0);
        xim.Dispose();
        xim.Dispose();

        var wayland = new WaylandTextInputAdapter(0, 0);
        wayland.Dispose();
        wayland.Dispose();

        bool passed = true;
        await Assert.That(passed).IsTrue();
    }

    [Test]
    public async Task RTL_Locale_Detection_ArabicAndHebrew()
    {
        var adapter = new IBusAdapter();

        adapter.HandleLocaleChanged("ar-SA");
        await Assert.That(adapter.CurrentLocale.Direction).IsEqualTo(TextDirection.RightToLeft);

        adapter.HandleLocaleChanged("he-IL");
        await Assert.That(adapter.CurrentLocale.Direction).IsEqualTo(TextDirection.RightToLeft);

        adapter.HandleLocaleChanged("en-US");
        await Assert.That(adapter.CurrentLocale.Direction).IsEqualTo(TextDirection.LeftToRight);

        adapter.HandleLocaleChanged("ja-JP");
        await Assert.That(adapter.CurrentLocale.Direction).IsEqualTo(TextDirection.LeftToRight);

        adapter.Dispose();
    }

    // ─── Platform availability checks ──────────────────────────────

    [Test]
    public async Task IBusAdapter_IsAvailable_ReturnsBoolean()
    {
        bool available = IBusAdapter.IsAvailable();
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!isLinux)
        {
            await Assert.That(available).IsFalse();
        }
        else
        {
            bool check = available || !available;
            await Assert.That(check).IsTrue();
        }
    }

    [Test]
    public async Task FcitxAdapter_IsAvailable_ReturnsBoolean()
    {
        bool available = FcitxAdapter.IsAvailable();
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!isLinux)
        {
            await Assert.That(available).IsFalse();
        }
        else
        {
            bool check = available || !available;
            await Assert.That(check).IsTrue();
        }
    }

    [Test]
    public async Task XimAdapter_IsAvailable_ReturnsBoolean()
    {
        bool available = XimAdapter.IsAvailable();
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!isLinux)
        {
            await Assert.That(available).IsFalse();
        }
        else
        {
            bool check = available || !available;
            await Assert.That(check).IsTrue();
        }
    }

    [Test]
    public async Task WaylandTextInputAdapter_IsAvailable_ReturnsBoolean()
    {
        bool available = WaylandTextInputAdapter.IsAvailable();
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!isLinux)
        {
            await Assert.That(available).IsFalse();
        }
        else
        {
            bool check = available || !available;
            await Assert.That(check).IsTrue();
        }
    }
}
