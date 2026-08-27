using System;
using System.Collections.Generic;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// The channel a starting environment component publishes what it started on.
/// </summary>
/// <remarks>
/// <para>
/// The graph always had a producer half - <c>NodeContext.Produce</c> - and nothing could reach it. A node
/// publishes its own values, but nothing ever drove a node: what actually starts containers is an
/// <see cref="EnvComponent"/>, and a component was handed a <c>RunContext</c> whose values are read-only. So
/// two lifecycles existed, one able to publish and never driven, one driven and unable to publish, and the
/// packages did the only thing left - they wrote a started resource's address back into somebody else's
/// configuration store. This is the missing wire.
/// </para>
/// <para>
/// It takes the resource as an argument rather than being bound to one, because that is what components are:
/// a single Azurite container serves every storage identifier a run declared, and one Service Bus emulator
/// serves every topic. A node is one resource; a component is not.
/// </para>
/// <para>
/// Unforgeable on purpose. The constructor is internal and the underlying store's writers are not public at
/// all, because a caller who could publish a value could point a passing test at a different system than the
/// one it was meant to prove. What a component may do here it may do because the engine handed it this;
/// there is no way to make one.
/// </para>
/// </remarks>
public sealed class EnvironmentResources
{
    private readonly ResourceValueStore values;
    private readonly string source;
    private readonly List<ResolvedValue> published = [];

    internal EnvironmentResources(ResourceValueStore values, string source)
    {
        this.values = values;
        this.source = source;
    }

    /// <summary>
    /// Publishes a value a resource now holds, for one viewpoint.
    /// </summary>
    /// <remarks>
    /// Both viewpoints are published by whoever knows them - it holds the container and the network alias -
    /// rather than one being derived from the other afterwards, which is what made connection strings
    /// something to be parsed and patched.
    /// </remarks>
    /// <param name="kind">The resource's kind, which is what says this value may exist.</param>
    /// <param name="identifier">Which resource of that kind.</param>
    /// <param name="valueName">Which value, from the kind's own schema.</param>
    /// <param name="vantage">Whose viewpoint this particular value is built for.</param>
    /// <param name="value">The value.</param>
    public void Produce(ResourceKind kind, string identifier, string valueName, ResourceVantage vantage, string value)
        => this.Publish(kind, identifier, valueName, vantage, value);

    /// <summary>
    /// Publishes a value that reads the same from every viewpoint, such as a name.
    /// </summary>
    /// <param name="kind">The resource's kind.</param>
    /// <param name="identifier">Which resource of that kind.</param>
    /// <param name="valueName">Which value, from the kind's own schema.</param>
    /// <param name="value">The value.</param>
    public void Produce(ResourceKind kind, string identifier, string valueName, string value)
        => this.Publish(kind, identifier, valueName, vantage: null, value);

    /// <summary>
    /// Forgets everything a resource produced, because it has stopped.
    /// </summary>
    /// <remarks>
    /// Declared values survive: a person's configuration entry does not stop existing because a container
    /// did. What must not survive is a coordinate nobody answers at any more.
    /// </remarks>
    /// <param name="kind">The resource's kind.</param>
    /// <param name="identifier">Which resource of that kind.</param>
    public void Withdraw(ResourceKind kind, string identifier)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        this.values.WithdrawProduced(kind.Name, identifier);
    }

    /// <summary>
    /// Everything published here, so a component that only runs once can be replayed into a later run.
    /// </summary>
    internal IReadOnlyList<ResolvedValue> Published
    {
        get
        {
            lock (this.published)
                return [.. this.published];
        }
    }

    /// <summary>
    /// Publishes a value this component worked out on an earlier occasion.
    /// </summary>
    /// <remarks>
    /// For a persistent component, whose body ran once in a bootstrap and will not run again: every later
    /// run gets a stand-in that hands back the same container, and this is how it hands back the same
    /// addresses. The value keeps its kind and key because those were checked against the kind's schema
    /// when it was first published; checking again would only ask the same question of the same answer.
    /// </remarks>
    /// <param name="value">A value published earlier, on this component's own channel.</param>
    internal void Republish(ResolvedValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        this.Write(value with { Source = this.source });
    }

    private void Publish(ResourceKind kind, string identifier, string valueName, ResourceVantage? vantage, string value)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentNullException.ThrowIfNull(value);

        ValueKey key = ResourceValueContract.KeyFor(kind, $"'{identifier}' ({this.source})", valueName, vantage);

        // Asked of the kind, which is the only place that knows and the only place it is declared.
        this.Write(new ResolvedValue(kind.Name, identifier, key, value, ValueOrigin.Produced, this.source)
        {
            IsSecret = kind.IsSecret(valueName),
        });
    }

    private void Write(ResolvedValue value)
    {
        this.values.Produce(value.ResourceKind, value.Identifier, value.Key, value.Value, value.Source, value.IsSecret);

        lock (this.published)
            this.published.Add(value);
    }
}
