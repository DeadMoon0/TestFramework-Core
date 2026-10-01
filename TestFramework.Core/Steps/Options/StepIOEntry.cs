using System;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes a single declared input or output dependency of a step.
/// </summary>
/// <remarks>
/// Read-only outside Core: a step declares its entries through <see cref="StepIOContract"/>, whose methods take
/// the identifier types and so decide the kind themselves. Building entries by hand let a key and a kind
/// disagree and spelled "required" as a bare <c>true</c>, and a positional constructor would have broken every
/// package the day an entry gained a member.
/// </remarks>
public sealed record StepIOEntry
{
    internal StepIOEntry(string key, StepIOKind kind, bool required, Type? declaredType)
    {
        this.Key = key;
        this.Kind = kind;
        this.Required = required;
        this.DeclaredType = declaredType;
    }

    /// <summary>
    /// The variable or artifact identifier string.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Whether this entry is a Variable or an Artifact.
    /// </summary>
    public StepIOKind Kind { get; }

    /// <summary>
    /// When true, a missing producer causes a validation error. When false, absence is allowed.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// The CLR type of this entry, or <see langword="null"/> when it does not say. When both an input and the
    /// matching producer declare a type, the validator checks that the producer type is assignable to the input type.
    /// </summary>
    public Type? DeclaredType { get; }
}
