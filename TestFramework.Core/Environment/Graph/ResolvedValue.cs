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
public sealed record ResolvedValue
{
    internal ResolvedValue(
        string resourceKind,
        string identifier,
        ValueKey key,
        string value,
        ValueOrigin origin,
        string source)
    {
        this.ResourceKind = resourceKind;
        this.Identifier = identifier;
        this.Key = key;
        this.Value = value;
        this.Origin = origin;
        this.Source = source;
    }

    /// <summary>
    /// The kind that owns it, for example <c>web.sql</c>.
    /// </summary>
    public string ResourceKind { get; internal init; }

    /// <summary>
    /// Which one, for example <c>orders-db</c>.
    /// </summary>
    public string Identifier { get; internal init; }

    /// <summary>
    /// Which value, and for whose viewpoint.
    /// </summary>
    public ValueKey Key { get; internal init; }

    /// <summary>
    /// The value itself.
    /// </summary>
    public string Value { get; internal init; }

    /// <summary>
    /// Whether it was declared or produced.
    /// </summary>
    public ValueOrigin Origin { get; internal init; }

    /// <summary>
    /// Who supplied it - an environment's name, or the configuration section.
    /// </summary>
    public string Source { get; internal init; }

    /// <summary>
    /// Whether <see cref="Value"/> has been redacted because its kind declared it secret.
    /// </summary>
    /// <remarks>
    /// Set only on the way out of a snapshot. A caller reading one value deliberately - through
    /// <c>Require</c> or <c>TryGet</c> - gets the real thing, because asking for a connection string is
    /// asking for the connection string. What is redacted is the *listing*: everything that ends up in a
    /// failure message, a log line or the frozen run.
    /// </remarks>
    public bool IsSecret { get; init; }

    /// <summary>
    /// Reads as <c>web.sql/orders-db ConnectionString (Network) from DockerWebEnvironment</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => $"{this.ResourceKind}/{this.Identifier} {this.Key} from {this.Source}";
}
