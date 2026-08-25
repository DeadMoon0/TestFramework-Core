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
    /// Everything the run knows, for an assertion, a log line or a failure message.
    /// </summary>
    /// <returns>The values.</returns>
    public IReadOnlyList<ResolvedValue> Snapshot() => this.store.Snapshot();

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
