using System;

namespace TestFramework.Core.Variables;

public class ResolvableVariable<T> : VariableReference<T>
{
    public override bool HasIdentifier => true;
    public override VariableIdentifier? Identifier { get; }

    public override bool RequireImmutability { get; } = false;

    private readonly VariableTransform[] _transforms;

    internal ResolvableVariable(VariableIdentifier identifier, VariableTransform[] transforms)
    {
        Identifier = identifier;
        _transforms = transforms;
    }

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

    public override VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform) where TNew : default
    {
        return new ResolvableVariable<TNew>(Identifier!, [.. _transforms, VariableTransform.FromTransform((o) => transform((T?)o))]);
    }

    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default
    {
        return new ResolvableVariable<TNew>(Identifier!, [.. _transforms, VariableTransform.FromTransformAndVars((o, v) => transform((T?)o, v), variables)]);
    }
}