using System;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Whether a value was written by an author or produced by something this run started.
/// </summary>
public enum ValueOrigin
{
    /// <summary>An author wrote it: a configuration entry, relayed unchanged.</summary>
    Declared = 0,

    /// <summary>Something this run provisioned produced it.</summary>
    Produced = 1,
}

/// <summary>
/// One value of one resource, with everything a failure message needs to explain it.
/// </summary>
/// <param name="ResourceKind">The kind that owns it, for example <c>web.sql</c>.</param>
/// <param name="Identifier">Which one, for example <c>orders-db</c>.</param>
/// <param name="Key">Which value, and for whose viewpoint.</param>
/// <param name="Value">The value itself.</param>
/// <param name="Origin">Whether it was declared or produced.</param>
/// <param name="Source">Who supplied it - an environment's name, or the configuration section.</param>
public sealed record ResolvedValue(
    string ResourceKind,
    string Identifier,
    ValueKey Key,
    string Value,
    ValueOrigin Origin,
    string Source)
{
    /// <summary>
    /// Reads as <c>web.sql/orders-db ConnectionString (Network) from DockerWebEnvironment</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => $"{this.ResourceKind}/{this.Identifier} {this.Key} from {this.Source}";
}
