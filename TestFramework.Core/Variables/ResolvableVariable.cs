using System;

namespace TestFramework.Core.Variables;

/// <summary>
/// Represents a variable reference that resolves its value from the run-time variable store.
/// </summary>
/// <typeparam name="T">The referenced value type.</typeparam>
public class ResolvableVariable<T> : VariableReference<T>
{
    /// <summary>
    /// Gets a value indicating that resolvable variables are backed by identifiers.
    /// </summary>
    public override bool HasIdentifier => true;

    /// <summary>
    /// Gets the backing variable identifier.
    /// </summary>
    public override VariableIdentifier? Identifier { get; }

    /// <summary>
    /// Gets a value indicating that resolvable variables do not require immutable binding by default.
    /// </summary>
    public override bool RequireImmutability { get; } = false;

    private readonly VariableTransform[] _transforms;

    internal ResolvableVariable(VariableIdentifier identifier, VariableTransform[] transforms)
    {
        Identifier = identifier;
        _transforms = transforms;
    }

    /// <summary>
    /// Resolves the variable from the run-time store and applies any configured transforms.
    /// </summary>
    /// <param name="store">The variable store used to resolve the value.</param>
    public override T? GetValue(VariableStore store)
    {
        if (_transforms.Length == 0) return store.GetVariable<T>(Identifier ?? throw new System.ArgumentNullException(nameof(Identifier)));

        object? value = store.GetVariable(Identifier ?? throw new System.ArgumentNullException(nameof(Identifier)));
        foreach (var transform in _transforms)
        {
            value = transform.GetTransformed(value, store);
        }
        return (T?)value;
    }

    /// <summary>
    /// Projects the resolved variable value to another variable reference type.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value to the projected value.</param>
    public override VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform) where TNew : default
    {
        return new ResolvableVariable<TNew>(Identifier!, [.. _transforms, VariableTransform.FromTransform((o) => transform((T?)o))]);
    }

    /// <summary>
    /// Projects the resolved variable value using additional variable-reference inputs.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the resolved value and the resolved dependent values.</param>
    /// <param name="variables">Additional variable references resolved for the transform.</param>
    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default
    {
        return new ResolvableVariable<TNew>(Identifier!, [.. _transforms, VariableTransform.FromTransformAndVars((o, v) => transform((T?)o, v), variables)]);
    }
}