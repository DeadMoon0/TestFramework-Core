using System;
using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// A resource somebody wrote down: which kind it is, what it is called, and the values it holds.
/// </summary>
/// <param name="Kind">The kind, which decides what values it may hold.</param>
/// <param name="Identifier">Which resource.</param>
/// <param name="Values">What it holds - only what is actually there.</param>
/// <param name="DeclaredBy">Where it came from, for a message: a configuration section, a fixture, a file.</param>
public sealed record DeclaredResource(
    ResourceKind Kind,
    string Identifier,
    IReadOnlyDictionary<ValueKey, string> Values,
    string DeclaredBy);

/// <summary>
/// The socket a package plugs declared resources into.
/// </summary>
/// <remarks>
/// <para>
/// A package says what it found - a section of a configuration file, a fixture's hand-built entries, a
/// manifest - and the engine turns that into resources the run can answer for. Deriving this rather than
/// implementing <see cref="IResourceNodeSource"/> directly is what gets a piece the engine's guarantees
/// instead of its own version of them: values are checked against their kind, nodes are built the one
/// way, and nothing needs to know how a graph is composed.
/// </para>
/// <para>
/// The check lives here rather than in any package, because a value a kind never offered is the same
/// mistake whether it arrives from a configuration file, a fixture or somebody's own plug-in - and the
/// engine is the only place that can promise it is caught every time.
/// </para>
/// </remarks>
public abstract class DeclaredNodeSource : IResourceNodeSource
{
    private IReadOnlyList<ResourceNode>? nodes;

    /// <inheritdoc />
    public abstract string SourceName { get; }

    /// <summary>
    /// What this piece found. Read once, when the graph is first composed.
    /// </summary>
    protected abstract IEnumerable<DeclaredResource> Declarations { get; }

    /// <inheritdoc />
    public IReadOnlyList<ResourceNode> Nodes => this.nodes ??= this.Build();

    private IReadOnlyList<ResourceNode> Build()
    {
        List<ResourceNode> built = [];

        foreach (DeclaredResource declaration in this.Declarations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(declaration.Identifier);

            foreach (ValueKey key in declaration.Values.Keys)
            {
                if (declaration.Kind.Offers(key.ValueName))
                {
                    continue;
                }

                throw new FrameworkConfigurationException(
                    $"{declaration.DeclaredBy} declares {key} for '{declaration.Identifier}', which {declaration.Kind} does not offer.",
                    [
                        "Declare the value on the kind, or stop declaring it here - the kind is what routes and reads are checked against.",
                    ],
                    [.. declaration.Kind.Values.Select(value => $"{declaration.Kind} offers {value.ValueName}")]);
            }

            built.Add(new DeclaredResourceNode(declaration));
        }

        return built;
    }

    /// <summary>
    /// A resource that exists because somebody said so: values as declared, nothing to start or tear down.
    /// </summary>
    private sealed class DeclaredResourceNode(DeclaredResource declaration) : ResourceNode
    {
        public override ResourceKind Kind => declaration.Kind;

        public override string Identifier => declaration.Identifier;

        public override IReadOnlyDictionary<ValueKey, string> DeclaredValues => declaration.Values;
    }
}
