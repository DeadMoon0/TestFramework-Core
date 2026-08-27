using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// The one way anything in a run asks "where is this resource, and what is it called there".
/// </summary>
/// <remarks>
/// <para>
/// There is no fallback behind this. Declared configuration is in the run's graph as relayed values, so
/// a lookup is a lookup - not "ask the environment, else read the file", which is the branch every
/// package used to re-implement slightly differently.
/// </para>
/// <para>
/// <see cref="Require(ValueRef, ResourceVantage)"/> is the normal call, and it throws. A missing value is
/// a broken run, not a branch to take: when each call site decided for itself what to do about one, the
/// answers diverged and the messages got worse. <see cref="TryGet"/> exists for values that are genuinely
/// optional, and reads as such.
/// </para>
/// <para>
/// Asking takes a <see cref="ValueRef"/> rather than strings, so what a resource kind offers is settled
/// by the compiler; what this run composed is settled by the graph, before anything starts.
/// </para>
/// </remarks>
public sealed class ValueResolution
{
    /// <summary>
    /// A run that declares no resources at all.
    /// </summary>
    /// <remarks>
    /// For driving a step directly - a package unit-testing its own step, without a timeline around it.
    /// Reading anything from it fails saying nothing in this run supplies it, which is the truth for a
    /// step being exercised on its own. Handing this to something that runs inside a real run would hide
    /// that run's actual resources, so a step under a timeline always gets the run's own resolution.
    /// </remarks>
    public static ValueResolution Empty { get; } = new ValueResolution(new ResourceValueStore());

    private readonly ResourceValueStore store;

    internal ValueResolution(ResourceValueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        this.store = store;
    }

    /// <summary>
    /// Reads a value, or explains why this run cannot answer.
    /// </summary>
    /// <param name="value">What to read - built through the owning package's typed accessor.</param>
    /// <param name="vantage">Whose viewpoint the value must be built for.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FrameworkConfigurationException">Nothing in this run supplies it.</exception>
    public string Require(ValueRef value, ResourceVantage vantage)
    {
        ArgumentNullException.ThrowIfNull(value);

        return this.Find(value, vantage)?.Value ?? throw this.Missing(value, vantage);
    }

    /// <summary>
    /// Reads a value that the caller can do without.
    /// </summary>
    /// <remarks>
    /// Only for values that are optional by design. Reaching for this to avoid a failure message is how
    /// a run ends up silently doing the wrong thing; <see cref="Require"/> is the default.
    /// </remarks>
    /// <param name="value">What to read.</param>
    /// <param name="vantage">Whose viewpoint.</param>
    /// <param name="resolved">The value, when the run has it.</param>
    /// <returns>True when the run has it.</returns>
    public bool TryGet(ValueRef value, ResourceVantage vantage, out string? resolved)
    {
        ArgumentNullException.ThrowIfNull(value);

        resolved = this.Find(value, vantage)?.Value;

        return resolved is not null;
    }

    /// <summary>
    /// Reads a value together with where it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the one question a coordinate alone cannot answer: whether the run supplied it or a person did.
    /// It matters because <strong>a produced coordinate is complete</strong> - whatever made it knows the
    /// whole answer, the port and the credentials and the certificate setting, which is precisely why it
    /// could produce one. A declared coordinate is a person's best description of something they cannot see,
    /// and the rest of their entry qualifies it.
    /// </para>
    /// <para>
    /// Without this, a reader cannot tell the two apart and applies the entry's qualifications either way.
    /// The case that asked for it: a starting database used to write a whole record into a configuration
    /// store, clearing a declared server and integrated-security flag along with it, because a container owns
    /// the entire connection - and a produced value replaces one slot and cannot un-declare anything. So a
    /// database that is both configured and containerised had a developer's integrated security applied on
    /// top of the container's own connection string, which strips its user and password out.
    /// </para>
    /// <para>
    /// <see cref="TryGet"/> stays the normal call. Reach for this only where the origin changes what the
    /// caller does with the answer, not to inspect the run.
    /// </para>
    /// </remarks>
    /// <param name="value">What to read.</param>
    /// <param name="vantage">Whose viewpoint.</param>
    /// <param name="resolved">The value and its origin, when the run has it.</param>
    /// <returns>True when the run has it.</returns>
    public bool TryResolve(ValueRef value, ResourceVantage vantage, out ResolvedValue? resolved)
    {
        ArgumentNullException.ThrowIfNull(value);

        resolved = this.Find(value, vantage);

        return resolved is not null;
    }

    /// <summary>
    /// Everything the run knows about one resource, as it looks from one viewpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The read a package uses to rebuild its own configuration record: one call, values already resolved
    /// for the asking viewpoint and flattened to names, so the record comes out of the run rather than out
    /// of a store somebody resolved from a service provider. §7's rule is the reason - configuration a run
    /// was set up with is run data, and run data arrives on the context.
    /// </para>
    /// <para>
    /// Empty rather than a failure when the run knows nothing about the resource: what to do about that is
    /// the caller's, and the caller is the one that can name what it was looking for.
    /// </para>
    /// </remarks>
    /// <param name="kind">The kind that owns the resource.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="vantage">Whose viewpoint.</param>
    /// <returns>The values by name.</returns>
    public IReadOnlyDictionary<string, string> ValuesFor(ResourceKind kind, string identifier, ResourceVantage vantage)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return this.store.ValuesFor(kind.Name, identifier, vantage);
    }

    /// <summary>
    /// Which resources of a kind the run knows about, for a failure that has to list them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The identifiers.</returns>
    public IReadOnlyList<string> IdentifiersOf(ResourceKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return this.store.IdentifiersOf(kind.Name);
    }

    /// <summary>
    /// Everything the run knows, for an assertion, a log line or a failure message.
    /// </summary>
    /// <returns>The values.</returns>
    public IReadOnlyList<ResolvedValue> Snapshot() => this.store.Snapshot();

    /// <summary>
    /// Closes the values when the run ends.
    /// </summary>
    /// <remarks>
    /// Reading stays open - that is the point of keeping them - and only writing closes. Internal because a
    /// caller who could freeze a running run's values could stop its own environment from publishing.
    /// </remarks>
    internal void FreezeForRunEnd() => this.store.Freeze();

    private ResolvedValue? Find(ValueRef value, ResourceVantage vantage)
    {
        // A value built for one viewpoint is never handed to another - that substitution is exactly how a
        // test ends up dialling a container-internal alias it cannot route to. A value with no vantage
        // reads the same everywhere, so it answers either ask.
        return this.Lookup(value, new ValueKey(value.ValueName, vantage))
            ?? this.Lookup(value, new ValueKey(value.ValueName));
    }

    private ResolvedValue? Lookup(ValueRef value, ValueKey key)
    {
        if (value.ResourceKind is { } kind)
        {
            return this.store.TryGet(kind, value.Identifier, key, out ResolvedValue? resolved) ? resolved : null;
        }

        return this.store.TryGetByIdentifier(value.Identifier, key, out ResolvedValue? anyKind) ? anyKind : null;
    }

    private FrameworkConfigurationException Missing(ValueRef value, ResourceVantage vantage)
        => new FrameworkConfigurationException(
            $"Nothing in this run supplies {value.ValueName} ({vantage}) for {value.ResourceKind ?? "any kind"}/{value.Identifier}.",
            [
                "Include the definition that provisions it in the environment, or declare it in configuration.",
            ],
            [.. this.store.Snapshot().Select(static known => known.ToString())]);
}
