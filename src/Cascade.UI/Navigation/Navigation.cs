using System.Diagnostics.CodeAnalysis;

namespace Cascade.UI;

/// <summary>
/// Static convenience methods for navigation that resolve against the nearest
/// <see cref="INavigator"/> ancestor in the component tree. These methods are
/// the primary navigation API — components call them without knowing which
/// stack they are on.
/// </summary>
/// <remarks>
/// <para>
/// For navigation outside the component tree (deep links, app startup),
/// use <c>App.Navigator</c> to access the root navigator directly.
/// </para>
/// </remarks>
public static class Navigation
{
    [ThreadStatic]
    private static INavigator? currentNavigator;

    // Fallback for event handlers running on non-render threads (e.g. MCP simulation).
    // Set alongside the ThreadStatic value whenever a Navigator renders.
    private static volatile INavigator? sharedNavigator;

    /// <summary>
    /// The shared route resolver instance used for deep link resolution.
    /// </summary>
    internal static RouteResolver SharedRouteResolver { get; } = new();

    /// <summary>
    /// The ambient navigator set during <see cref="Navigator.Render"/>.
    /// Thread-static to support concurrent rendering on different threads.
    /// </summary>
    internal static INavigator? CurrentNavigator
    {
        get { return currentNavigator; }
        set { currentNavigator = value; sharedNavigator = value; }
    }

    /// <summary>
    /// Pushes a new page onto the nearest navigator stack.
    /// </summary>
    /// <typeparam name="T">The page component type. Constructor parameters are verified at compile time.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    /// <returns>
    /// A handle for overriding this push's transition, e.g.
    /// <c>Navigation.Push&lt;SettingsPage&gt;().Transition(PageTransition.SlideUp)</c>.
    /// Safe to ignore to use the navigator's default transition.
    /// </returns>
    public static NavigationTransitionHandle Push<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        return GetNavigator().Push<T>(args);
    }

    /// <summary>
    /// Pushes an existing page component instance onto the nearest navigator stack.
    /// </summary>
    /// <param name="page">The page component instance to push.</param>
    /// <returns>A handle for overriding this push's transition. Safe to ignore.</returns>
    public static NavigationTransitionHandle Push(Component page)
    {
        return GetNavigator().Push(page);
    }

    /// <summary>
    /// Replaces the current top of the nearest navigator stack. The current
    /// page is removed from history.
    /// </summary>
    /// <typeparam name="T">The replacement page component type.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    public static void Replace<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        GetNavigator().Replace<T>(args);
    }

    /// <summary>
    /// Pops the current page from the nearest navigator stack.
    /// Does nothing if the stack has only one entry.
    /// </summary>
    /// <returns>A handle for overriding this pop's transition. Safe to ignore.</returns>
    public static NavigationTransitionHandle Pop()
    {
        return GetNavigator().Pop();
    }

    /// <summary>
    /// Pops pages until a page of type <typeparamref name="T"/> is at the
    /// top of the nearest navigator stack. Does nothing if that type is not
    /// in the stack.
    /// </summary>
    public static void PopTo<T>() where T : Component
    {
        GetNavigator().PopTo<T>();
    }

    /// <summary>
    /// Clears the nearest navigator stack and starts fresh with a new root.
    /// </summary>
    /// <typeparam name="T">The new root page component type.</typeparam>
    /// <param name="args">Constructor arguments for the page.</param>
    public static void Reset<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(params object[] args) where T : Component
    {
        GetNavigator().Reset<T>(args);
    }

    /// <summary>
    /// Pushes a page and awaits a result. The target page calls
    /// <see cref="ReturnResult{TResult}"/> to pop itself and deliver the value.
    /// </summary>
    /// <typeparam name="TPage">The page component type.</typeparam>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="ct">Cancellation token — typically the component's lifetime token.</param>
    /// <returns>
    /// The result value, or null if the page was dismissed by a back gesture.
    /// </returns>
    public static Task<TResult?> PushForResultAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, TResult>(CancellationToken ct)
        where TPage : Component
    {
        return GetNavigator().PushForResultAsync<TPage, TResult>(ct);
    }

    /// <summary>
    /// Pops the current page and delivers a result to the awaiting caller.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="result">The result value, or null for cancellation.</param>
    public static void ReturnResult<TResult>(TResult? result)
    {
        GetNavigator().ReturnResult(result);
    }

    /// <summary>
    /// Navigates to a route path by resolving it against [Route] attributes.
    /// Used for deep link handling and URL-based navigation.
    /// </summary>
    /// <param name="path">The URL-style path to navigate to.</param>
    public static void Navigate(string path)
    {
        GetNavigator().Navigate(path);
    }

    private static INavigator GetNavigator()
    {
        return currentNavigator
            ?? sharedNavigator
            ?? throw new InvalidOperationException(
                "No navigator is active. Navigation methods must be called " +
                "from within a Component's Render() cycle or from a handler " +
                "running inside a Navigator's scope.");
    }
}
