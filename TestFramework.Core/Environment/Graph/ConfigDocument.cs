using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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
/// Two formats cover the family; anything else is a document of its own.
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

/// <summary>
/// A JSON document whose value paths are colon-separated, the way .NET configuration reads them.
/// </summary>
/// <remarks>
/// <c>ConnectionStrings:Sql</c> becomes the <c>Sql</c> property of the <c>ConnectionStrings</c> object.
/// Intermediate objects are created as needed; everything already in the file that no route mentions is
/// left exactly as it was.
/// </remarks>
public sealed class JsonPathDocument : ConfigDocument
{
    private readonly Func<string?> readExisting;

    /// <summary>
    /// Creates a JSON document.
    /// </summary>
    /// <param name="path">Where it goes.</param>
    /// <param name="readExisting">How to read what is already there, or null when nothing is.</param>
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

        JsonObject root = Parse(existing);

        foreach ((string valuePath, string value) in routed.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            Write(root, valuePath, value);
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject Parse(string? existing)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new Exceptions.FrameworkConfigurationException(
                "The existing configuration document is not valid JSON, so the routed values cannot be merged into it.",
                ["Fix the file the payload ships, or generate the document whole instead of merging."],
                [exception.Message]);
        }
    }

    private static void Write(JsonObject root, string valuePath, string value)
    {
        string[] segments = valuePath.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException($"'{valuePath}' is not a value path.", nameof(valuePath));
        }

        JsonObject current = root;

        for (int index = 0; index < segments.Length - 1; index++)
        {
            if (current[segments[index]] is JsonObject nested)
            {
                current = nested;
                continue;
            }

            // A route may deepen a path the payload left as a plain value; the route is the newer
            // statement of intent, so it wins - visibly, because the whole document is logged.
            JsonObject created = new JsonObject();
            current[segments[index]] = created;
            current = created;
        }

        current[segments[^1]] = value;
    }
}

/// <summary>
/// A document of <c>NAME=value</c> lines, for anything configured through the environment.
/// </summary>
public sealed class EnvironmentFileDocument : ConfigDocument
{
    private readonly Func<string?> readExisting;

    /// <summary>
    /// Creates an environment file.
    /// </summary>
    /// <param name="path">Where it goes.</param>
    /// <param name="readExisting">How to read what is already there, or null when nothing is.</param>
    public EnvironmentFileDocument(string path, Func<string?>? readExisting = null)
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

        Dictionary<string, string> lines = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string line in (existing ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Comments and blank lines are not settings, and a line without '=' is not one either;
            // keeping them out of the merge means they are also not silently rewritten.
            int separator = line.IndexOf('=', StringComparison.Ordinal);

            if (!line.StartsWith('#') && separator > 0)
            {
                lines[line[..separator]] = line[(separator + 1)..];
            }
        }

        foreach ((string name, string value) in routed)
        {
            lines[name] = value;
        }

        StringBuilder content = new StringBuilder();

        foreach ((string name, string value) in lines.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            content.Append(CultureInfo.InvariantCulture, $"{name}={value}").Append('\n');
        }

        return content.ToString();
    }
}
