using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// Interface for a scoped navigation stack. Available on any component via
/// the <c>Navigator</c> property. Provides typed push/pop/replace operations
/// and stack state inspection.
/// </summary>
public interface INavigator
{
    // ── Stack state ───────────────────────────────────────────────────

    /// <summary>The number of pages currently on the navigation stack.</summary>
    int StackDepth { get; }

    /// <summary>True when the stack has more than one entry and can go back.</summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Returns true if a page of type <typeparamref name="T"/> is anywhere
    /// in the current stack.
    /// </summary>
    bool Contains<T>() where T : Component;

    // ── Navigation ────────────────────────────────────────────────────

    /// <summary>Pushes a new page onto the stack.</summary>
    /// <typeparam name="T">The page component type.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    /// <returns>
    /// A handle for overriding this push's transition via
    /// <see cref="NavigationTransitionHandle.Transition"/>. Safe to ignore.
    /// </returns>
    NavigationTransitionHandle Push<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component;

    /// <summary>Pushes an existing page component instance onto the stack.</summary>
    /// <param name="page">The page component instance to push.</param>
    /// <returns>
    /// A handle for overriding this push's transition via
    /// <see cref="NavigationTransitionHandle.Transition"/>. Safe to ignore.
    /// </returns>
    NavigationTransitionHandle Push(Component page);

    /// <summary>
    /// Replaces the current top of stack. The current page is removed
    /// from history.
    /// </summary>
    /// <typeparam name="T">The replacement page component type.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    void Replace<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component;

    /// <summary>
    /// Pops the current page. Does nothing if the stack has only one entry.
    /// </summary>
    /// <returns>
    /// A handle for overriding this pop's transition via
    /// <see cref="NavigationTransitionHandle.Transition"/>. Safe to ignore.
    /// </returns>
    NavigationTransitionHandle Pop();

    /// <summary>
    /// Pops pages until a page of type <typeparamref name="T"/> is at the
    /// top of the stack. Does nothing if that type is not in the stack.
    /// </summary>
    void PopTo<T>() where T : Component;

    /// <summary>
    /// Clears the entire stack and starts fresh with a new root page.
    /// </summary>
    /// <typeparam name="T">The new root page component type.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    void Reset<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component;

    /// <summary>
    /// Pushes a page and awaits a result. The target page calls
    /// <see cref="ReturnResult{TResult}"/> to pop itself and deliver the value.
    /// Returns null if the page is popped by the system back gesture.
    /// </summary>
    /// <typeparam name="TPage">The page component type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="ct">Cancellation token — typically the component's lifetime token.</param>
    Task<TResult?> PushForResultAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, TResult>(CancellationToken ct)
        where TPage : Component;

    /// <summary>
    /// Pops the current page and delivers a result to the awaiting caller
    /// of <see cref="PushForResultAsync{TPage,TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result value, or null for cancellation.</param>
    void ReturnResult<TResult>(TResult? result);

    /// <summary>
    /// Navigates to a route path, resolving against [Route] attributes.
    /// Used for deep link handling and URL-based navigation.
    /// </summary>
    /// <param name="path">The URL-style path to navigate to.</param>
    void Navigate(string path);

    // ── Events ────────────────────────────────────────────────────────

    /// <summary>Raised after a page is pushed onto the stack.</summary>
    event Action<Component> PagePushed;

    /// <summary>Raised after a page is popped from the stack.</summary>
    event Action<Component> PagePopped;

    /// <summary>Raised when a page becomes the top of the stack again after a pop.</summary>
    event Action<Component> PageResumed;
}
