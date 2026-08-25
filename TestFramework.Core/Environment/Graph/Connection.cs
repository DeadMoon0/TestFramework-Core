using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Where one of another resource's values is written in the config this node generates.
/// </summary>
/// <remarks>
/// <para>
/// The whole of what a node borrows, in one statement: which value of which resource, from whose
/// viewpoint, and where it lands. Everything else follows from it - the connection to that resource, and
/// therefore the order the two are created in.
/// </para>
/// <para>
/// The value is a <see cref="ValueRef"/> built from the owning kind's schema, so a route to something a
/// resource does not offer cannot be written; and the viewpoint defaults to
/// <see cref="ResourceVantage.Network"/> because a generated config is read from inside the environment,
/// by the thing it configures.
/// </para>
/// </remarks>
public sealed record ValueRoute
{
    private ValueRoute(ValueRef value, ResourceVantage vantage, string documentPath, string valuePath)
    {
        this.Value = value;
        this.Vantage = vantage;
        this.DocumentPath = documentPath;
        this.ValuePath = valuePath;
    }

    /// <summary>Which value of which resource.</summary>
    public ValueRef Value { get; }

    /// <summary>Whose viewpoint the value must be built for.</summary>
    public ResourceVantage Vantage { get; }

    /// <summary>Which generated document, for example <c>appsettings.Testing.json</c>.</summary>
    public string DocumentPath { get; }

    /// <summary>Where inside that document, for example <c>ConnectionStrings:Sql</c>.</summary>
    public string ValuePath { get; }

    /// <summary>
    /// Routes a value into a generated document.
    /// </summary>
    /// <param name="value">Which value of which resource, from the owning kind's schema.</param>
    /// <param name="documentPath">Which document.</param>
    /// <param name="valuePath">Where inside it.</param>
    /// <param name="vantage">Whose viewpoint - the configured thing's own, unless stated otherwise.</param>
    /// <returns>The route.</returns>
    public static ValueRoute To(
        ValueRef value,
        string documentPath,
        string valuePath,
        ResourceVantage vantage = ResourceVantage.Network)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valuePath);

        if (value.ResourceKind is null)
        {
            throw new ArgumentException(
                "A route needs a resource of a known kind: the graph has to know what it is ordering and validating against.",
                nameof(value));
        }

        return new ValueRoute(value, vantage, documentPath, valuePath);
    }

    /// <summary>
    /// Reads as <c>web.sql/orders-db:ConnectionString (Network) -&gt; appsettings.json ConnectionStrings:Sql</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => $"{this.Value} ({this.Vantage}) -> {this.DocumentPath} {this.ValuePath}";
}

/// <summary>
/// Which resource, of which kind - the address of a node in the graph.
/// </summary>
/// <param name="KindName">The kind.</param>
/// <param name="Identifier">Which one.</param>
public sealed record ResourceAddress(string KindName, string Identifier)
{
    /// <summary>
    /// Reads as <c>web.sql/orders-db</c>.
    /// </summary>
    /// <returns>The description.</returns>
    public override string ToString() => $"{this.KindName}/{this.Identifier}";
}

/// <summary>
/// One resource a node needs, and what it borrows from it.
/// </summary>
/// <remarks>
/// Derived, never authored: a node declares its routes and, where it needs something without borrowing a
/// value from it, its ordering. Grouping those by target is what a connection is. Ordering used to be a
/// second hand-maintained list beside the wiring, and the two drifted; here one declaration yields both,
/// so a node that reads a neighbour's value cannot be created before that neighbour exists.
/// </remarks>
/// <param name="KindName">The kind of the resource needed.</param>
/// <param name="Identifier">Which one.</param>
/// <param name="Routes">What is borrowed from it, or empty when only its existence is needed.</param>
public sealed record Connection(string KindName, string Identifier, IReadOnlyList<ValueRoute> Routes)
{
    /// <summary>
    /// Reads as <c>web.sql/orders-db (1 route)</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => this.Routes.Count == 0
            ? $"{this.KindName}/{this.Identifier} (ordering only)"
            : $"{this.KindName}/{this.Identifier} ({this.Routes.Count} route(s))";
}

/// <summary>
/// What a node may reach while it is being created: its declared neighbours, and nothing else.
/// </summary>
/// <remarks>
/// Deliberately narrower than "everything in the run". A node that quietly reads a resource it never
/// declared is a dependency the graph cannot see, cannot order and cannot validate - the class of bug
/// that hand-listed dependencies used to hide. Asking outside the declared set fails naming the fix.
/// </remarks>
public sealed class ConnectionSet
{
    private readonly IReadOnlyList<Connection> connections;
    private readonly ResourceValueStore values;
    private readonly Func<string, string, object?> stateLookup;
    private readonly string owner;

    internal ConnectionSet(
        string owner,
        IReadOnlyList<Connection> connections,
        ResourceValueStore values,
        Func<string, string, object?> stateLookup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stateLookup);

        this.owner = owner;
        this.connections = connections;
        this.values = values;
        this.stateLookup = stateLookup;
    }

    /// <summary>
    /// Reads a neighbour's value, or explains what this node is allowed to ask for.
    /// </summary>
    /// <param name="value">Which value of which neighbour - built from that kind's schema.</param>
    /// <param name="vantage">Whose viewpoint the value must be built for.</param>
    /// <returns>The value.</returns>
    /// <exception cref="Exceptions.FrameworkConfigurationException">
    /// The node declared no connection to that neighbour, or the neighbour never supplied the value.
    /// </exception>
    public string Require(ValueRef value, ResourceVantage vantage)
    {
        ArgumentNullException.ThrowIfNull(value);

        string kindName = value.ResourceKind
            ?? throw new ArgumentException("A connection is always to a resource of a known kind.", nameof(value));

        this.EnsureDeclared(kindName, value.Identifier);

        return this.values.TryGet(kindName, value.Identifier, new ValueKey(value.ValueName, vantage), out ResolvedValue? forVantage)
            ? forVantage!.Value
            : this.values.TryGet(kindName, value.Identifier, new ValueKey(value.ValueName), out ResolvedValue? vantageFree)
                ? vantageFree!.Value
                : throw new Exceptions.FrameworkConfigurationException(
                    $"'{this.owner}' needs {value.ValueName} ({vantage}) of {kindName}/{value.Identifier}, which never supplied it.",
                    ["The neighbour exists but produced no such value. Check the viewpoint it was produced for."],
                    [.. this.values.Snapshot()
                        .Where(known => string.Equals(known.ResourceKind, kindName, StringComparison.Ordinal)
                            && string.Equals(known.Identifier, value.Identifier, StringComparison.Ordinal))
                        .Select(static known => known.Key.ToString())]);
    }

    /// <summary>
    /// Reaches a neighbour's runtime state - the objects that are not values, such as a network or a
    /// running container handle.
    /// </summary>
    /// <typeparam name="TState">The state type the neighbour produced.</typeparam>
    /// <param name="address">Which neighbour.</param>
    /// <returns>The state.</returns>
    /// <exception cref="Exceptions.FrameworkConfigurationException">Not declared, or not of that type.</exception>
    public TState State<TState>(ResourceAddress address)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(address);

        this.EnsureDeclared(address.KindName, address.Identifier);

        object? state = this.stateLookup(address.KindName, address.Identifier);

        return state as TState
            ?? throw new Exceptions.FrameworkConfigurationException(
                $"'{this.owner}' asked {address} for {typeof(TState).Name} state, which it did not produce.",
                ["Check the state type the neighbour returns from its creation."],
                [state is null ? "it produced no state" : $"it produced {state.GetType().Name}"]);
    }

    private void EnsureDeclared(string kindName, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kindName);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (this.connections.Any(connection =>
            string.Equals(connection.KindName, kindName, StringComparison.Ordinal)
            && string.Equals(connection.Identifier, identifier, StringComparison.Ordinal)))
        {
            return;
        }

        throw new Exceptions.FrameworkConfigurationException(
            $"'{this.owner}' reached for {kindName}/{identifier} without declaring a connection to it.",
            ["Declare a route or an ordering dependency, so the graph can order the two and validate the pair before anything starts."],
            [.. this.connections.Select(static connection => connection.ToString())]);
    }
}
