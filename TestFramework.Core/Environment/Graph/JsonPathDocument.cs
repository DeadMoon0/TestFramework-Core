using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// A JSON document whose value paths are colon-separated, the way .NET configuration reads them.
/// </summary>
/// <remarks>
/// <para>
/// <c>ConnectionStrings:Sql</c> becomes the <c>Sql</c> property of the <c>ConnectionStrings</c> object.
/// Intermediate objects are created as needed, and everything already in the file that no route mentions
/// is left exactly as it was - generating configuration must never mean discarding configuration.
/// </para>
/// <para>
/// It lives in the engine because every package that starts something needs it and none of them should be
/// solving it: an API's settings file, a site's configuration file and a function app's settings are the
/// same problem three times. A package's job is its own job - serve this payload, run this app - not
/// building general machinery on the way there.
/// </para>
/// <para>
/// Output is ordered and indented so a generated file reads the same between runs and shows up honestly in
/// a diff.
/// </para>
/// </remarks>
public sealed class JsonPathDocument : ConfigDocument
{
    private readonly Func<string?> readExisting;

    /// <summary>
    /// Creates a JSON document.
    /// </summary>
    /// <param name="path">Where it goes, relative to the payload.</param>
    /// <param name="readExisting">How to read what the payload already ships there, or null when it ships nothing.</param>
    public JsonPathDocument(string path, Func<string?>? readExisting = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        this.Path = path;
        this.readExisting = readExisting ?? (static () => null);
    }

    /// <inheritdoc />
    public override string Path { get; }

    /// <inheritdoc />
    public override string? ReadExisting() => this.readExisting();

    /// <inheritdoc />
    public override string Compose(IReadOnlyDictionary<string, string> routed, string? existing)
    {
        ArgumentNullException.ThrowIfNull(routed);

        JObject root = Parse(existing, this.Path);

        foreach ((string valuePath, string value) in routed.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            Insert(root, valuePath, value, this.Path);
        }

        return root.ToString(Formatting.Indented);
    }

    private static JObject Parse(string? existing, string path)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return [];
        }

        JToken parsed;

        try
        {
            parsed = JToken.Parse(existing);
        }
        catch (JsonReaderException exception)
        {
            throw new FrameworkConfigurationException(
                $"The existing '{path}' is not valid JSON, so routed values cannot be merged into it.",
                [
                    "Fix the file the payload ships, or generate the document whole instead of merging into it.",
                ],
                [exception.Message],
                exception);
        }

        return parsed as JObject
            ?? throw new FrameworkConfigurationException(
                $"The existing '{path}' is valid JSON but not an object, so there is nothing to merge routed values into.",
                ["Make it an object, or generate the document whole instead of merging into it."],
                [$"it is a {parsed.Type}"]);
    }

    private static void Insert(JObject root, string valuePath, string value, string path)
    {
        string[] segments = valuePath.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            throw new FrameworkConfigurationException(
                $"A route into '{path}' has no value path, so there is nowhere to write it.",
                ["Give the route a path, for example 'ConnectionStrings:Sql'."],
                []);
        }

        JObject current = root;

        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (current[segments[index]] is JObject child)
            {
                current = child;
                continue;
            }

            // A route may deepen a path the payload left as a plain value. The route is the newer
            // statement of intent, so it wins - visibly, because the whole document is logged.
            JObject created = [];
            current[segments[index]] = created;
            current = created;
        }

        current[segments[^1]] = value;
    }
}
