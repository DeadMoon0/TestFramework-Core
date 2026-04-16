using System;

namespace TestFramework.Core.Variables;

public class ConstVariable<T> : VariableReference<T>
{
    public static implicit operator ImmutableVariable<ConstVariable<T>, T>(ConstVariable<T> constVariable) => new(constVariable);

    internal object? Value { get; }

    public override bool HasIdentifier => false;
    public override VariableIdentifier? Identifier => null;

    public override bool RequireImmutability => true;

    private readonly VariableTransform[] _transforms;

    internal ConstVariable(object? value, VariableTransform[]? transforms = null)
    {
        Value = value;
        _transforms = transforms ?? [];
    }

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

    public override VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform) where TNew : default
    {
        return new ConstVariable<TNew>(transform((T?)Value)!);
    }

    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default
    {
        return new ConstVariable<TNew>(Value, [.. _transforms, VariableTransform.FromTransformAndVars((o, v) => transform((T?)o, v), variables)]);
    }
}