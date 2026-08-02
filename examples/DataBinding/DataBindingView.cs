using Cascade.UI;

namespace DataBinding;

/// <summary>
/// GOLDEN EXAMPLE — two-way data binding with <c>Bind()</c>.
///
/// <para>
/// <c>Bind(field, v =&gt; field = v)</c> makes a two-way binding for a control's value:
/// the control displays <c>field</c>, and when the user changes it the setter runs and the
/// component re-renders. You read the field directly; the framework wires the write + refresh.
/// </para>
///
/// <para>
/// PERFORMANCE — it is safe to bind to a high-frequency source (e.g. a slider you drag).
/// Every change marks this component dirty, but the render scheduler DEDUPES dirty components,
/// so many changes within a frame collapse to a SINGLE re-render per frame. Render cost is
/// bounded by frame rate, not by how fast the bound value changes — binding a fast-moving
/// value will not redline the CPU/GPU. (Each <c>Bind()</c> call allocates ~160 bytes of
/// short-lived gen-0 garbage when the component re-renders — the two setter closures — which
/// scales with the number of bindings, not with the change rate.)
/// </para>
///
/// <para>
/// NOTE: <c>Bind()</c> works on an ordinary component — no <c>partial</c> modifier and no
/// generated plumbing are involved. <c>Bind(value, setter)</c> is a plain framework method
/// that pairs the current value with a setter that runs and then calls <c>Invalidate()</c>.
/// </para>
/// </summary>
internal sealed class DataBindingView : Component
{
    // Bound state — plain fields. Read them directly; write them through Bind().
    private string name = "";
    private float volume = 0.5f;
    private bool subscribed;

    protected override Node Render() =>
        new Center(
            new Column(
                spacing: 20,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label("Two-way binding").FontSize(24),

                    // Text ⇄ field
                    new TextInput(Bind(name, v => name = v))
                        .Placeholder("Your name"),

                    // Slider ⇄ field — a HIGH-FREQUENCY source. Dragging fires many changes
                    // per second; they all coalesce to one re-render per frame.
                    new Label($"Volume: {volume:P0}"),
                    new Slider(Bind(volume, v => volume = v), min: 0f, max: 1f),

                    // Checkbox ⇄ field
                    new Checkbox(Bind(subscribed, v => subscribed = v), "Subscribe to updates"),

                    // Live summary — proves the re-render: updates as you type / drag / toggle.
                    new Label(BuildSummary()).FontSize(16),
                ]
            )
        ).Padding(40);

    private string BuildSummary()
    {
        string who = name.Length == 0 ? "there" : name;
        string tail = subscribed ? " — subscribed ✓" : string.Empty;
        return $"Hi {who}, volume {volume:P0}{tail}";
    }
}
