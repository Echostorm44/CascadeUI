using Cascade.UI.Diagnostics;

namespace Cascade.UI.Tests.Diagnostics;

/// <summary>
/// A component that throws keeps its last good tree; the error must still be findable (it used to
/// go only to Debug.WriteLine, so a window silently stopped updating).
/// </summary>
[NotInParallel(nameof(ComponentErrorLog))]
public class ComponentErrorLogTests
{
    [Before(Test)]
    public void SetUp()
    {
        ComponentErrorLog.ResetForTests();
    }

    [Test]
    public async Task RenderThrowingOnReRender_IsRecorded_AndLastTreeKept()
    {
        using var component = new Flaky();
        var host = new ComponentHost(component, new RenderScheduler(), treeDepth: 0);
        host.Mount();

        component.Fail = true;
        host.ReRender();

        ComponentErrorLog.Entry[] errors = ComponentErrorLog.Snapshot();
        await Assert.That(ComponentErrorLog.TotalCount).IsEqualTo(1);
        await Assert.That(errors[0].Component).IsEqualTo(nameof(Flaky));
        await Assert.That(errors[0].HandledByBoundary).IsFalse();
        await Assert.That(errors[0].Error.Message).IsEqualTo("render failed");
        await Assert.That(component.RenderedTree).IsTypeOf<Label>();
    }

    [Test]
    public async Task Log_KeepsOnlyTheMostRecent()
    {
        for (int i = 0; i < ComponentErrorLog.Capacity + 5; i++)
        {
            ComponentErrorLog.Report("C", new InvalidOperationException(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), handledByBoundary: false);
        }

        ComponentErrorLog.Entry[] errors = ComponentErrorLog.Snapshot();
        await Assert.That(errors.Length).IsEqualTo(ComponentErrorLog.Capacity);
        await Assert.That(errors[^1].Error.Message).IsEqualTo((ComponentErrorLog.Capacity + 4).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(ComponentErrorLog.TotalCount).IsEqualTo(ComponentErrorLog.Capacity + 5);
    }

    private sealed class Flaky : Component
    {
        public bool Fail { get; set; }

        protected override Node Render()
        {
            if (Fail)
            {
                throw new InvalidOperationException("render failed");
            }
            return new Label("ok");
        }
    }
}
