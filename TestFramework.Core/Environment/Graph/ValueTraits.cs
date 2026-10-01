using System;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// What is true of a value a resource kind offers, beyond its name.
/// </summary>
[Flags]
public enum ValueTraits
{
    /// <summary>Every instance has it, and it may be shown.</summary>
    None = 0,

    /// <summary>An instance may legitimately not have it.</summary>
    Optional = 1,

    /// <summary>
    /// It must never be printed. A connection string is the usual case: it is a coordinate and a credential in
    /// one indivisible string, so it travels as a value and is redacted everywhere values are listed.
    /// </summary>
    Secret = 2,
}
