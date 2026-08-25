using System;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// A pointer to one value of one resource, made where the resource's kind is known.
/// </summary>
/// <remarks>
/// <para>
/// The only way to name a value when reading or routing one. That is deliberate: a reference is built by
/// the package that owns the kind, through a member that exists only for a value that kind actually
/// offers, so <c>Sql("orders-db").BaseUrl</c> does not compile and never becomes a run-time miss. The
/// resource identifier comes from that package's typed identifier for the same reason - a stub's name
/// cannot be passed where a database's belongs.
/// </para>
/// <para>
/// What remains genuinely run-time is only whether this run composed that resource at all, and the graph
/// answers that before anything starts.
/// </para>
/// </remarks>
public sealed record ValueRef
{
    private ValueRef(string? resourceKind, string identifier, string valueName)
    {
        this.ResourceKind = resourceKind;
        this.Identifier = identifier;
        this.ValueName = valueName;
    }

    /// <summary>The kind that owns the value, or null when the caller is kind-agnostic by design.</summary>
    public string? ResourceKind { get; }

    /// <summary>Which resource.</summary>
    public string Identifier { get; }

    /// <summary>Which value of it.</summary>
    public string ValueName { get; }

    /// <summary>
    /// Points at a value of a resource of a known kind.
    /// </summary>
    /// <remarks>Called by the package that owns the kind, from a member named after the value.</remarks>
    /// <param name="resourceKind">The kind.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="valueName">Which value.</param>
    /// <returns>The reference.</returns>
    public static ValueRef For(string resourceKind, string identifier, string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        return new ValueRef(resourceKind, identifier, valueName);
    }

    /// <summary>
    /// Points at a value of a resource named without its kind.
    /// </summary>
    /// <remarks>
    /// For a consumer that genuinely does not care which kind answers - a browser opens whatever serves
    /// an address. A name that two kinds answer to is a stated error, never a silent pick.
    /// </remarks>
    /// <param name="identifier">Which resource.</param>
    /// <param name="valueName">Which value.</param>
    /// <returns>The reference.</returns>
    public static ValueRef AnyKind(string identifier, string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        return new ValueRef(null, identifier, valueName);
    }

    /// <summary>
    /// Reads as <c>web.sql/orders-db:ConnectionString</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => $"{this.ResourceKind ?? "*"}/{this.Identifier}:{this.ValueName}";
}
