using System;

namespace TestFramework.Core.Variables;

/// <summary>
/// Wraps another variable reference and marks it as immutable for downstream consumers.
/// </summary>
/// <typeparam name="TVar">The wrapped variable reference type.</typeparam>
/// <typeparam name="T">The referenced value type.</typeparam>
public class ImmutableVariable<TVar, T>(TVar var) : VariableReference<T> where TVar : VariableReference<T>
{
    /// <summary>
    /// Gets a value indicating that immutable variables require immutable binding semantics.
    /// </summary>
    public override bool RequireImmutability => true;

    /// <summary>
    /// Gets whether the wrapped variable exposes an identifier.
    /// </summary>
    public override bool HasIdentifier => var.HasIdentifier;

    /// <summary>
    /// Gets the identifier exposed by the wrapped variable.
    /// </summary>
    public override VariableIdentifier? Identifier => var.Identifier;

    /// <summary>
    /// Resolves the wrapped variable value.
    /// </summary>
    /// <param name="store">The variable store used for resolution.</param>
    public override T? GetValue(VariableStore store) => var.GetValue(store);

    /// <summary>
    /// Projects the wrapped variable to another variable reference type.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value to the projected value.</param>
    public override VariableReference<TNew> Transform<TNew>(System.Func<T?, TNew?> transform) where TNew : default => var.Transform(transform);

    /// <summary>
    /// Projects the wrapped variable using additional variable-reference inputs.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value and the resolved dependent values.</param>
    /// <param name="variables">Additional variable references resolved for the transform.</param>
    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default => var.Transform(transform, variables);
}