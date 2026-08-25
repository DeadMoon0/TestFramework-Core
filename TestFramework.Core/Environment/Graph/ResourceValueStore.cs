using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Every value the run knows about its resources, whoever supplied them.
/// </summary>
/// <remarks>
/// <para>
/// One store rather than one per package, because a value's consumer is regularly in a package that has
/// never heard of its producer: a browser step needs the address of a site a container published, and
/// neither package may depend on the other to learn it.
/// </para>
/// <para>
/// Values arrive two ways and are told apart on purpose. A <see cref="Declare"/>d value was written by
/// an author and is relayed unchanged; a <see cref="Produce"/>d value was made by something this run
/// started. Produced wins, because a run's own reality beats a file - and the difference is logged
/// rather than swallowed, so a stale configuration entry is visible instead of merely overridden.
/// </para>
/// <para>
/// Components may be created in parallel, so the store is concurrent.
/// </para>
/// <para>
/// Reading is public; changing is not. A value redirected from outside the run would point a test at a
/// different system, and a withdrawn one would strand a step mid-flight, so values arrive only two ways:
/// a node declaring them, or a node producing them through its own context - which checks them against
/// its kind first.
/// </para>
/// </remarks>
public sealed class ResourceValueStore
{
    private readonly ConcurrentDictionary<Slot, ResolvedValue> values = new ConcurrentDictionary<Slot, ResolvedValue>();

    internal ResourceValueStore()
    {
    }

    /// <summary>
    /// Records a value an author wrote. Relayed exactly as declared.
    /// </summary>
    /// <param name="resourceKind">The kind that owns it.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="key">Which value.</param>
    /// <param name="value">The value.</param>
    /// <param name="source">Who declared it, for messages.</param>
    internal void Declare(string resourceKind, string identifier, ValueKey key, string value, string source)
        => this.Set(new ResolvedValue(resourceKind, identifier, key, value, ValueOrigin.Declared, source));

    /// <summary>
    /// Records a value something this run provisioned produced.
    /// </summary>
    /// <param name="resourceKind">The kind that owns it.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="key">Which value.</param>
    /// <param name="value">The value.</param>
    /// <param name="source">Who produced it, for messages.</param>
    internal void Produce(string resourceKind, string identifier, ValueKey key, string value, string source)
        => this.Set(new ResolvedValue(resourceKind, identifier, key, value, ValueOrigin.Produced, source));

    /// <summary>
    /// Forgets everything a resource produced, at teardown.
    /// </summary>
    /// <remarks>
    /// Declared values survive: an author's configuration entry does not stop existing because a
    /// container stopped. What must not survive is a coordinate nobody answers at any more.
    /// </remarks>
    /// <param name="resourceKind">The kind that owned it.</param>
    /// <param name="identifier">Which resource.</param>
    internal void WithdrawProduced(string resourceKind, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        foreach (Slot slot in this.values
            .Where(entry => entry.Value.Origin == ValueOrigin.Produced
                && string.Equals(entry.Key.ResourceKind, resourceKind, StringComparison.Ordinal)
                && string.Equals(entry.Key.Identifier, identifier, StringComparison.Ordinal))
            .Select(static entry => entry.Key)
            .ToList())
        {
            this.values.TryRemove(slot, out _);
        }
    }

    /// <summary>
    /// Looks a value up, for a stated kind.
    /// </summary>
    /// <param name="resourceKind">The kind that owns it.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="key">Which value.</param>
    /// <param name="value">The value, when there is one.</param>
    /// <returns>True when the run knows it.</returns>
    public bool TryGet(string resourceKind, string identifier, ValueKey key, out ResolvedValue? value)
        => this.values.TryGetValue(new Slot(resourceKind, identifier, key), out value);

    /// <summary>
    /// Looks a value up by identifier alone, for a consumer that is kind-agnostic by design.
    /// </summary>
    /// <remarks>
    /// A browser does not care whether the thing it opens is a static site or an application's own
    /// front end. Two kinds answering to one name is an authoring mistake rather than a choice to make
    /// silently, so it is stated instead of guessed.
    /// </remarks>
    /// <param name="identifier">Which resource.</param>
    /// <param name="key">Which value.</param>
    /// <param name="value">The value, when exactly one kind offers it.</param>
    /// <returns>True when exactly one kind answers.</returns>
    /// <exception cref="FrameworkConfigurationException">More than one kind answers to the identifier.</exception>
    public bool TryGetByIdentifier(string identifier, ValueKey key, out ResolvedValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        List<ResolvedValue> matches = [.. this.values
            .Where(entry => string.Equals(entry.Key.Identifier, identifier, StringComparison.Ordinal) && entry.Key.Key == key)
            .Select(static entry => entry.Value)];

        if (matches.Count > 1)
        {
            throw new FrameworkConfigurationException(
                $"'{identifier}' names {matches.Count} different kinds of resource, so asking for its {key} without saying which is ambiguous.",
                ["Ask for the kind as well, or rename one of the resources."],
                [.. matches.Select(static match => $"{match.ResourceKind}/{match.Identifier} from {match.Source}")]);
        }

        value = matches.Count == 1 ? matches[0] : null;

        return value is not null;
    }

    /// <summary>
    /// Everything the run knows, for a log line, an assertion or a failure message.
    /// </summary>
    /// <returns>The values, ordered so two runs of the same test read the same.</returns>
    public IReadOnlyList<ResolvedValue> Snapshot()
        => [.. this.values.Values
            .OrderBy(static value => value.ResourceKind, StringComparer.Ordinal)
            .ThenBy(static value => value.Identifier, StringComparer.Ordinal)
            .ThenBy(static value => value.Key.ToString(), StringComparer.Ordinal)];

    private void Set(ResolvedValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ResourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.Identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.Key.ValueName);
        ArgumentNullException.ThrowIfNull(value.Value);

        Slot slot = new Slot(value.ResourceKind, value.Identifier, value.Key);

        this.values.AddOrUpdate(slot, value, (_, existing) => Resolve(existing, value));
    }

    /// <summary>
    /// Decides which of two values for one slot stands.
    /// </summary>
    /// <remarks>
    /// Produced beats declared, and a second producer of the same slot is a conflict rather than a
    /// last-writer-wins race: two components claiming to be the same resource is a mistake worth
    /// hearing about, whereas a container overriding a configuration entry is the point.
    /// </remarks>
    private static ResolvedValue Resolve(ResolvedValue existing, ResolvedValue incoming)
    {
        if (existing.Origin == incoming.Origin)
        {
            if (existing.Origin == ValueOrigin.Produced && !string.Equals(existing.Source, incoming.Source, StringComparison.Ordinal))
            {
                throw new FrameworkConfigurationException(
                    $"'{existing.Source}' and '{incoming.Source}' both produced {existing.Key} for {existing.ResourceKind}/{existing.Identifier}.",
                    ["Two providers claim the same resource. Include only one of them, or give them different identifiers."],
                    [$"{existing.Source} produced '{existing.Value}'", $"{incoming.Source} produced '{incoming.Value}'"]);
            }

            return incoming;
        }

        return incoming.Origin == ValueOrigin.Produced ? incoming : existing;
    }

    private readonly record struct Slot(string ResourceKind, string Identifier, ValueKey Key);
}
