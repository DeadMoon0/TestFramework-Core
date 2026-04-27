using System;

namespace TestFramework.Core.Variables;

/// <summary>
/// Represents a constant variable reference whose value is known when the timeline is built.
/// </summary>
/// <typeparam name="T">The referenced value type.</typeparam>
public class ConstVariable<T> : VariableReference<T>
{
    /// <summary>
    /// Converts a constant variable to an immutable variable reference.
    /// </summary>
    public static implicit operator ImmutableVariable<ConstVariable<T>, T>(ConstVariable<T> constVariable) => new(constVariable);

    internal object? Value { get; }

    /// <summary>
    /// Gets a value indicating that constant variables do not have identifiers.
    /// </summary>
    public override bool HasIdentifier => false;

    /// <summary>
    /// Gets <see langword="null"/> because constant variables are not backed by identifiers.
    /// </summary>
    public override VariableIdentifier? Identifier => null;

    /// <summary>
    /// Gets a value indicating that constant variables always satisfy immutability requirements.
    /// </summary>
    public override bool RequireImmutability => true;

    private readonly VariableTransform[] _transforms;

    internal ConstVariable(object? value, VariableTransform[]? transforms = null)
    {
        Value = value;
        _transforms = transforms ?? [];
    }

    /// <summary>
    /// Resolves the constant value and applies any configured transforms.
    /// </summary>
    /// <param name="store">The variable store used for transform dependencies.</param>
    public override T? GetValue(VariableStore store)
    {
        if (_transforms.Length == 0) return (T?)Value;

        object? value = Value;
        foreach (var transform in _transforms)
        {
            value = transform.GetTransformed(value, store);
        }
        return (T?)value;
    }

    /// <summary>
    /// Projects the constant value to another variable reference type.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the current value to the projected value.</param>
    public override VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform) where TNew : default
    {
        return new ConstVariable<TNew>(transform((T?)Value)!);
    }

    /// <summary>
    /// Projects the constant value using additional variable-reference inputs.
    /// </summary>
    /// <typeparam name="TNew">The projected value type.</typeparam>
    /// <param name="transform">Transforms the current value and the resolved dependent values.</param>
    /// <param name="variables">Additional variable references resolved for the transform.</param>
    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default
    {
        return new ConstVariable<TNew>(Value, [.. _transforms, VariableTransform.FromTransformAndVars((o, v) => transform((T?)o, v), variables)]);
    }
}