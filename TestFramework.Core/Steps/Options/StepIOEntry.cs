using System;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes a single declared input or output dependency of a step.
/// </summary>
/// <param name="Key">The variable or artifact identifier string.</param>
/// <param name="Kind">Whether this entry is a Variable or an Artifact.</param>
/// <param name="Required">When true, a missing producer causes a validation error. When false, absence is allowed.</param>
/// <param name="DeclaredType">Optional CLR type for this entry. When both the input and the matching producer
/// declare a type, the validator checks that the producer type is assignable to the input type.</param>
public record StepIOEntry(string Key, StepIOKind Kind, bool Required = true, Type? DeclaredType = null);
