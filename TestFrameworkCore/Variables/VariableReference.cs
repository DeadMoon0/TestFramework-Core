using System;

namespace TestFrameworkCore.Variables;

public abstract class VariableReference<T> : VariableReferenceGeneric
{
    public static implicit operator VariableReference<T>(T value) => new ConstVariable<T>(value);

    public T GetRequiredValue(VariableStore store, string requiredReason = "") => GetValue(store) ?? throw new InvalidOperationException("The Value cannot be Null of Variable: " + (Identifier ?? "ConstVariable") + ". " + requiredReason);

    public abstract T? GetValue(VariableStore store);
    public abstract VariableReference<TNew> Transform<TNew>(Func<T?, TNew?> transform);
    public abstract VariableReference<TNew> Transform<TNew>(Func<T?, object?[], TNew?> transform, params VariableReferenceGeneric[] variables);

    public override object? GetValueGeneric(VariableStore store) => GetValue(store);
    public override VariableReferenceGeneric Transform(Func<object?, object?> transform) => Transform(transform);
    public override VariableReferenceGeneric Transform(Func<object?, object?[], object?> transform, params VariableReferenceGeneric[] variables) => Transform(transform, variables);
}

public abstract class VariableReferenceGeneric
{
    public abstract bool RequireImmutability { get; }

    public abstract bool HasIdentifier { get; }
    public abstract VariableIdentifier? Identifier { get; }

    public abstract object? GetValueGeneric(VariableStore store);
    public abstract VariableReferenceGeneric Transform(Func<object?, object?> transform);
    public abstract VariableReferenceGeneric Transform(Func<object?, object?[], object?> transform, params VariableReferenceGeneric[] variables);
}