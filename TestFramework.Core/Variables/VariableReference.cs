using System;

namespace TestFramework.Core.Variables;

/// <summary>
/// Represents a typed variable reference that can be resolved during timeline execution.
/// </summary>
/// <typeparam name="T">The referenced value type.</typeparam>
public abstract class VariableReference<T> : VariableReferenceGeneric
{
    /// <summary>
    /// Converts a raw value to a constant variable reference.
    /// </summary>
    public static implicit operator VariableReference<T>(T value) => new ConstVariable<T>(value);

    /// <summary>
    /// Resolves the value and throws when the result is <see langword="null"/>.
    /// </summary>
    /// <param name="store">The variable store used for resolution.</param>
    /// <param name="requiredReason">Additional context included in the exception message.</param>
    /// <returns>The resolved non-null value.</returns>
    public T GetRequiredValue(VariableStore store, string requiredReason = "") => GetValue(store) ?? throw new InvalidOperationException("The Value cannot be Null of Variable: " + (Identifier ?? "ConstVariable") + ". " + requiredReason);

    /// <summary>
    /// Resolves the referenced value from the provided store.
    /// </summary>
    /// <param name="store">The variable store used for resolution.</param>
    public abstract T? GetValue(VariableStore store);

    /// <summary>
    /// Projects the resolved value to another variable reference type.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value to the projected value.</param>
    public abstract VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform);

    /// <summary>
    /// Projects the resolved value using additional variable-reference inputs.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value and the resolved dependent values.</param>
    /// <param name="variables">Additional variable references resolved for the transform.</param>
    public abstract VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables);

    /// <summary>
    /// Resolves the referenced value without a static compile-time type.
    /// </summary>
    public override object? GetValueGeneric(VariableStore store) => GetValue(store);

    /// <summary>
    /// Projects the resolved value without a static compile-time type.
    /// </summary>
    public override VariableReferenceGeneric Transform(Func<object?, object?> transform) => Transform(transform);

    /// <summary>
    /// Projects the resolved value with additional dependency variables without a static compile-time type.
    /// </summary>
    public override VariableReferenceGeneric Transform(Func<object?, object?[], object?> transform, params VariableReferenceGeneric[] variables) => Transform(transform, variables);
}

/// <summary>
/// Represents the non-generic base contract for all variable references.
/// </summary>
public abstract class VariableReferenceGeneric
{
    /// <summary>
    /// Gets a value indicating whether the variable reference requires an immutable binding.
    /// </summary>
    public abstract bool RequireImmutability { get; }

    /// <summary>
    /// Gets a value indicating whether the reference is backed by a named identifier.
    /// </summary>
    public abstract bool HasIdentifier { get; }

    /// <summary>
    /// Gets the backing variable identifier when one exists.
    /// </summary>
    public abstract VariableIdentifier? Identifier { get; }

    /// <summary>
    /// Resolves the referenced value without a static value type.
    /// </summary>
    /// <param name="store">The variable store used for resolution.</param>
    public abstract object? GetValueGeneric(VariableStore store);

    /// <summary>
    /// Projects the resolved value without a static value type.
    /// </summary>
    /// <param name="transform">Transforms the resolved value to the projected value.</param>
    public abstract VariableReferenceGeneric Transform(Func<object?, object?> transform);

    /// <summary>
    /// Projects the resolved value using additional variable-reference inputs without a static value type.
    /// </summary>
    /// <param name="transform">Transforms the resolved value and the resolved dependent values.</param>
    /// <param name="variables">Additional variable references resolved for the transform.</param>
    public abstract VariableReferenceGeneric Transform(Func<object?, object?[], object?> transform, params VariableReferenceGeneric[] variables);
}