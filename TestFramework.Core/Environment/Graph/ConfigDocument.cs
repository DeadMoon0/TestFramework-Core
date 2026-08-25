using System.Collections.Generic;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// A configuration file the framework generates for a resource it starts.
/// </summary>
/// <remarks>
/// <para>
/// Always the same three moves: read whatever the payload already ships, merge the values this node's
/// connections routed in, write the result. Reading first is what makes the framework a good guest -
/// an application's own settings file keeps every key nobody routed over, so generating configuration
/// never means discarding configuration.
/// </para>
/// <para>
/// General formats live here, filled in by the engine: <see cref="JsonPathDocument"/> composes the
/// colon-path JSON that an API's settings, a site's configuration file and a function app's settings all
/// need. A package deriving this directly is for a format only that package has - not for solving JSON
/// again, which is how <c>SiteConfigFile</c> came to exist in Container.Web and why it goes away.
/// </para>
/// </remarks>
public abstract class ConfigDocument
{
    /// <summary>Where the document goes, relative to the payload, for example <c>appsettings.Testing.json</c>.</summary>
    public abstract string Path { get; }

    /// <summary>
    /// The content the payload already ships at <see cref="Path"/>, or null when the framework writes it whole.
    /// </summary>
    public virtual string? ReadExisting() => null;

    /// <summary>
    /// Produces the final content.
    /// </summary>
    /// <param name="routed">The routed values, keyed by the route's value path.</param>
    /// <param name="existing">What <see cref="ReadExisting"/> returned.</param>
    /// <returns>The content to write.</returns>
    public abstract string Compose(IReadOnlyDictionary<string, string> routed, string? existing);
}
