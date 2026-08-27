using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// What a sort of resource is called, and which values every instance of it offers.
/// </summary>
/// <remarks>
/// <para>
/// Declared once by the package that owns the kind, and it is the single source for three things that
/// used to be stated separately and could therefore disagree: the value names an instance may offer, the
/// typed members other packages point at those values with, and what plan-time validation checks a route
/// against. A database kind that does not offer an address makes <c>Sql("orders-db").BaseUrl</c>
/// impossible to write and a route to it impossible to plan.
/// </para>
/// <para>
/// The schema is an upper bound rather than a guarantee. Every instance of a kind <em>may</em> offer
/// these values; a particular instance holds whichever were actually declared or produced - a relayed
/// configuration entry with no health path is a perfectly normal instance of an API. So the schema
/// settles authoring questions before a run starts, and the run's values settle what is actually there.
/// </para>
/// </remarks>
public sealed class ResourceKind
{
    private readonly Dictionary<string, ResourceValue> values;

    internal ResourceKind(string name, IReadOnlyList<ResourceValue> values)
    {
        this.Name = name;
        this.Values = values;
        this.values = values.ToDictionary(static value => value.ValueName, StringComparer.Ordinal);
    }

    /// <summary>What the kind is called, for example <c>web.sql</c>.</summary>
    public string Name { get; }

    /// <summary>Every value an instance of this kind may offer.</summary>
    public IReadOnlyList<ResourceValue> Values { get; }

    /// <summary>
    /// Starts declaring a kind.
    /// </summary>
    /// <param name="name">What the kind is called.</param>
    /// <returns>The builder.</returns>
    public static ResourceKindBuilder Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ResourceKindBuilder(name);
    }

    /// <summary>
    /// Whether instances of this kind offer a value at all.
    /// </summary>
    /// <remarks>
    /// Viewpoint deliberately does not enter into it: a per-viewpoint value exists for both, because the
    /// node that knows one knows the other, and a viewpoint-free value answers either ask. Which
    /// viewpoints a particular instance actually published is a question for the run's values.
    /// </remarks>
    /// <param name="valueName">Which value.</param>
    /// <returns>True when a route or a read for it is meaningful.</returns>
    public bool Offers(string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        return this.values.ContainsKey(valueName);
    }

    /// <summary>
    /// Whether a value of this kind must never be printed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked of the kind rather than carried on each value, because the kind is where the schema already
    /// lives: a connection string is a secret for every instance of every SQL database, and nothing that
    /// publishes one should have to remember that. Declaring it here also means two packages cannot disagree
    /// about it - interning refuses a second declaration of the same kind whose secrecy differs.
    /// </para>
    /// <para>
    /// A value nobody declared secret is not secret. That is the right default for the same reason the
    /// schema is an upper bound: a kind lists what it offers, and most of what a resource offers is an
    /// address or a name.
    /// </para>
    /// </remarks>
    /// <param name="valueName">Which value.</param>
    /// <returns>True when the value must be redacted wherever values are listed.</returns>
    public bool IsSecret(string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        return this.values.TryGetValue(valueName, out ResourceValue? value) && value.Secret;
    }

    /// <summary>
    /// Finds a value this kind offers.
    /// </summary>
    /// <param name="valueName">Which value.</param>
    /// <param name="value">The declared value, when the kind offers it.</param>
    /// <returns>True when the kind offers it.</returns>
    public bool TryGetValue(string valueName, out ResourceValue? value)
        => this.values.TryGetValue(valueName, out value);

    /// <summary>
    /// Reads as the kind's name.
    /// </summary>
    /// <returns>The name.</returns>
    public override string ToString() => this.Name;
}

/// <summary>
/// Declares the values a kind offers.
/// </summary>
public sealed class ResourceKindBuilder
{
    private readonly string name;
    private readonly List<ResourceValue> values = [];

    internal ResourceKindBuilder(string name) => this.name = name;

    /// <summary>
    /// Declares a value every instance may offer, built for each viewpoint separately.
    /// </summary>
    /// <remarks>
    /// The usual shape for a coordinate: an address or a connection string reads one way from the test
    /// process and another from inside the network, and the node that starts the resource publishes both.
    /// </remarks>
    /// <param name="valueName">Which value, from <see cref="ValueNames"/> or the package's own list.</param>
    /// <param name="optional">True when an instance may legitimately not have it.</param>
    /// <returns>The builder.</returns>
    public ResourceKindBuilder OffersPerVantage(string valueName, bool optional = false)
        => this.Add(valueName, perVantage: true, optional, secret: false);

    /// <summary>
    /// Declares a per-viewpoint value, saying whether it must never be printed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate overload rather than a third optional parameter, and the reason is worth keeping: an
    /// optional argument is baked into the <em>call site</em> when it compiles, so widening
    /// <c>OffersPerVantage(string, bool)</c> in place made every already-compiled package call a method that
    /// no longer existed. Source-compatible, binary-incompatible - and in a family of independently versioned
    /// packages that lands on a user holding one new package and one old, as a
    /// <c>MissingMethodException</c> at runtime rather than an error at build.
    /// </para>
    /// <para>
    /// Found by the suites that consume this package as a package rather than as a project, which is the only
    /// place it can be found.
    /// </para>
    /// </remarks>
    /// <param name="valueName">Which value.</param>
    /// <param name="optional">True when an instance may legitimately not have it.</param>
    /// <param name="secret">
    /// True when the value must never be printed. A connection string is the usual case: it is a coordinate
    /// and a credential in one indivisible string, so it travels as a value and is redacted everywhere values
    /// are listed.
    /// </param>
    /// <returns>The builder.</returns>
    public ResourceKindBuilder OffersPerVantage(string valueName, bool optional, bool secret)
        => this.Add(valueName, perVantage: true, optional, secret);

    /// <summary>
    /// Declares a value every instance may offer, which reads the same from every viewpoint.
    /// </summary>
    /// <param name="valueName">Which value.</param>
    /// <param name="optional">True when an instance may legitimately not have it.</param>
    /// <returns>The builder.</returns>
    public ResourceKindBuilder Offers(string valueName, bool optional = false)
        => this.Add(valueName, perVantage: false, optional, secret: false);

    /// <summary>
    /// Declares a viewpoint-free value, saying whether it must never be printed.
    /// </summary>
    /// <remarks>
    /// An overload rather than a third optional parameter - see
    /// <see cref="OffersPerVantage(string, bool, bool)"/> for why that distinction is binary rather than
    /// cosmetic.
    /// </remarks>
    /// <param name="valueName">Which value.</param>
    /// <param name="optional">True when an instance may legitimately not have it.</param>
    /// <param name="secret">True when the value must never be printed.</param>
    /// <returns>The builder.</returns>
    public ResourceKindBuilder Offers(string valueName, bool optional, bool secret)
        => this.Add(valueName, perVantage: false, optional, secret);

    /// <summary>
    /// Completes the kind, or hands back the one already declared under this name.
    /// </summary>
    /// <returns>The kind.</returns>
    /// <exception cref="Exceptions.FrameworkConfigurationException">
    /// The name is already declared with a different schema.
    /// </exception>
    public ResourceKind Build() => ResourceKindRegistry.Intern(this.name, this.values);

    /// <summary>
    /// Completes the kind, so a declaration reads as one expression.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static implicit operator ResourceKind(ResourceKindBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Build();
    }

    private ResourceKindBuilder Add(string valueName, bool perVantage, bool optional, bool secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        if (this.values.Any(existing => string.Equals(existing.ValueName, valueName, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"'{this.name}' already offers '{valueName}'.", nameof(valueName));
        }

        this.values.Add(new ResourceValue(this.name, valueName, perVantage, optional) { Secret = secret });

        return this;
    }
}

/// <summary>
/// One value a kind offers, and the way to point at it on a particular instance.
/// </summary>
/// <param name="KindName">The kind that offers it.</param>
/// <param name="ValueName">Which value.</param>
/// <param name="PerVantage">True when it is built separately for each viewpoint.</param>
/// <param name="Optional">True when an instance may legitimately not have it.</param>
public sealed record ResourceValue(string KindName, string ValueName, bool PerVantage, bool Optional)
{
    /// <summary>
    /// True when the value must never be printed wherever values are listed.
    /// </summary>
    /// <remarks>
    /// An init property rather than a fifth positional parameter, for the same binary reason the builder has
    /// overloads: widening a record's primary constructor changes the constructor every compiled caller
    /// resolved to.
    /// </remarks>
    public bool Secret { get; init; }

    /// <summary>
    /// How the owning node names this value about itself.
    /// </summary>
    /// <param name="vantage">The viewpoint, used only when the value is built per viewpoint.</param>
    /// <returns>The key.</returns>
    public ValueKey KeyFor(ResourceVantage vantage)
        => this.PerVantage ? new ValueKey(this.ValueName, vantage) : new ValueKey(this.ValueName);

    /// <summary>
    /// Points at this value on one instance - the reference other packages read and route with.
    /// </summary>
    /// <param name="identifier">Which instance.</param>
    /// <returns>The reference.</returns>
    public ValueRef Of(string identifier) => ValueRef.For(this.KindName, identifier, this.ValueName);

    /// <summary>
    /// Reads as <c>web.sql:ConnectionString</c>.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString() => $"{this.KindName}:{this.ValueName}";
}

/// <summary>
/// The one meaning of each kind name in this process.
/// </summary>
/// <remarks>
/// <para>
/// What an enum would give - one canonical definition per name, impossible to declare twice differently -
/// without closing the set, because kinds are declared by whichever package owns them and by users who
/// write their own. Declaring the same name with the same values hands back the same instance, which
/// matters because static initialization order across packages is nobody's to control. Declaring it with
/// different values is refused at once, naming both schemas.
/// </para>
/// <para>
/// Deliberately process-wide and append-only: a kind's schema is a fact about code, not about a run, so
/// nothing here is per-run state and nothing is ever removed.
/// </para>
/// </remarks>
internal static class ResourceKindRegistry
{
    private static readonly ConcurrentDictionary<string, ResourceKind> Kinds = new ConcurrentDictionary<string, ResourceKind>(StringComparer.Ordinal);

    public static ResourceKind Intern(string name, IReadOnlyList<ResourceValue> values)
    {
        ResourceKind candidate = new ResourceKind(name, values);
        ResourceKind existing = Kinds.GetOrAdd(name, candidate);

        if (ReferenceEquals(existing, candidate) || Describe(existing) == Describe(candidate))
        {
            return existing;
        }

        throw new Exceptions.FrameworkConfigurationException(
            $"'{name}' is already declared as a resource kind with different values.",
            [
                "Two packages cannot mean different things by one kind name. Rename one, or declare the kind once and share it.",
            ],
            [$"already declared: {Describe(existing)}", $"now declared: {Describe(candidate)}"]);
    }

    /// <summary>
    /// Forgets every declaration, for tests that declare conflicting kinds on purpose.
    /// </summary>
    internal static void Reset() => Kinds.Clear();

    private static string Describe(ResourceKind kind)
        => string.Join(
            ", ",
            kind.Values
                .OrderBy(static value => value.ValueName, StringComparer.Ordinal)
                .Select(static value => $"{value.ValueName}{(value.PerVantage ? " per-vantage" : string.Empty)}{(value.Optional ? " optional" : string.Empty)}{(value.Secret ? " secret" : string.Empty)}"));
}
