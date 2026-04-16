using System;

namespace TestFramework.Core.Variables;

public class ImmutableVariable<TVar, T>(TVar var) : VariableReference<T> where TVar : VariableReference<T>
{
    public override bool RequireImmutability => true;

    public override bool HasIdentifier => var.HasIdentifier;

    public override VariableIdentifier? Identifier => var.Identifier;

    public override T? GetValue(VariableStore store) => var.GetValue(store);

    public override VariableReference<TNew> Transform<TNew>(System.Func<T?, TNew?> transform) where TNew : default => var.Transform(transform);
    public override VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables) where TNew : default => var.Transform(transform, variables);
}