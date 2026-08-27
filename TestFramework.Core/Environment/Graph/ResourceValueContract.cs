using System.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Checks a produced value against the kind that promised it, and works out which key it lands under.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, because there is more than one way into producing: a resource node publishes its own
/// values, and a component that starts several resources publishes theirs. Both have to be held to the same
/// promise, and two copies of that check are two rules - the second one written being the one that quietly
/// disagrees.
/// </para>
/// <para>
/// The promise is what plan-time validation checked routes against. Producing a value the kind does not
/// offer, or offering one per viewpoint and publishing it without one, would make that validation something
/// the run does not keep.
/// </para>
/// </remarks>
internal static class ResourceValueContract
{
    /// <summary>
    /// The key a produced value belongs under, or a stated failure.
    /// </summary>
    /// <param name="kind">The kind that owns the value.</param>
    /// <param name="source">Who is producing, for the message.</param>
    /// <param name="valueName">Which value.</param>
    /// <param name="vantage">Whose viewpoint, or null for a value that reads the same from all of them.</param>
    /// <returns>The key.</returns>
    /// <exception cref="FrameworkConfigurationException">The kind does not offer this value, or not this way.</exception>
    internal static ValueKey KeyFor(ResourceKind kind, string source, string valueName, ResourceVantage? vantage)
    {
        if (!kind.TryGetValue(valueName, out ResourceValue? offered))
        {
            throw new FrameworkConfigurationException(
                $"{source} produced '{valueName}', which {kind} does not offer.",
                ["Declare the value on the kind, so a route to it can be checked before anything starts."],
                [.. kind.Values.Select(static value => value.ToString())]);
        }

        if (offered!.PerVantage && vantage is null)
        {
            throw new FrameworkConfigurationException(
                $"{source} produced '{valueName}' without a viewpoint, but {kind} offers it per viewpoint.",
                ["Publish it once for each viewpoint: what the test process reaches is not what a peer container reaches."],
                []);
        }

        if (!offered.PerVantage && vantage is not null)
        {
            throw new FrameworkConfigurationException(
                $"{source} produced '{valueName}' for {vantage}, but {kind} offers it as one value for every viewpoint.",
                ["Publish it once, without a viewpoint."],
                []);
        }

        return offered.KeyFor(vantage ?? ResourceVantage.Host);
    }
}
