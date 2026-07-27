namespace Cascade.UI;

/// <summary>
/// A two-way binding token holding both the current value and a callback
/// to update it. Produced by the source-generated <c>Bind(field)</c> call
/// in a component.
/// </summary>
/// <typeparam name="T">The type of the bound value.</typeparam>
/// <remarks>
/// <para>
/// If the only thing an onChange handler does is set the bound field,
/// <c>Bind(field)</c> is the correct idiom. Explicit onChange lambdas
/// are used only when additional logic is needed beyond the field assignment.
/// </para>
/// <para>
/// The source generator rewrites <c>Bind(field)</c> into a
/// <see cref="Bindable{T}"/> with the appropriate getter/setter pair,
/// ensuring reactivity tracking is maintained.
/// </para>
/// </remarks>
/// <param name="Value">The current value of the bound field.</param>
/// <param name="OnChange">
/// Callback invoked when the control wants to update the value.
/// The generator wires this to the reactive field's setter.
/// </param>
public readonly record struct Bindable<T>(T Value, Action<T> OnChange)
{
    /// <summary>
    /// Implicit conversion to the underlying value, allowing a
    /// <see cref="Bindable{T}"/> to be used where a <typeparamref name="T"/>
    /// is expected in read-only contexts.
    /// </summary>
    /// <remarks>
    /// CA2225 is suppressed because the alternate method pattern conflicts with
    /// CA1000 (no static members on generic types). The implicit operator is the
    /// intended API surface — <see cref="Value"/> serves as the explicit alternative.
    /// </remarks>
#pragma warning disable CA2225 // Operator overloads have named alternates — Value property serves this role
    public static implicit operator T(Bindable<T> bindable)
#pragma warning restore CA2225
    {
        return bindable.Value;
    }
}
