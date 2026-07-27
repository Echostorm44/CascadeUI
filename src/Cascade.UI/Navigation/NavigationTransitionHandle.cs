namespace Cascade.UI;

/// <summary>
/// A lightweight handle returned by navigation operations that start a
/// transition (<see cref="Navigation.Push{T}"/>, <see cref="Navigation.Push(Component)"/>,
/// <see cref="Navigation.Pop"/>). It lets the caller override the transition used
/// for that single operation, replacing the navigator's configured default.
/// </summary>
/// <remarks>
/// <para>
/// The override must be applied synchronously, immediately after the operation —
/// the transition is committed on the next frame, so chaining <see cref="Transition"/>
/// on the same line is the intended (and only supported) usage:
/// </para>
/// <code>
/// Navigation.Push&lt;SettingsPage&gt;().Transition(PageTransition.SlideUp);
/// Navigation.Pop().Transition(PageTransition.None);
/// </code>
/// <para>
/// When no override is applied, the operation uses the navigator's default
/// transition, so the handle can always be safely ignored.
/// </para>
/// </remarks>
public readonly struct NavigationTransitionHandle
{
    private readonly Navigator? navigator;

    internal NavigationTransitionHandle(Navigator? navigator)
    {
        this.navigator = navigator;
    }

    /// <summary>
    /// Overrides the transition for the operation that produced this handle.
    /// Has no effect if the operation is no longer pending (i.e. a frame has
    /// already rendered) or if there was no navigator to act on.
    /// </summary>
    /// <param name="transition">The transition to use for this single operation.</param>
    public void Transition(PageTransition transition)
    {
        navigator?.SetPendingTransition(transition);
    }
}
