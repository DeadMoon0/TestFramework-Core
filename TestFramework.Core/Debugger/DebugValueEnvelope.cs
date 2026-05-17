using System.ComponentModel;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Carries a JSON-first debugger representation for a variable or artifact value.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public record DebugValueEnvelope
{
    /// <summary>
    /// Gets the kind of value represented by this envelope.
    /// </summary>
    public required DebugValueKind Kind { get; init; }

    /// <summary>
    /// Gets the CLR or artifact type name represented by the value.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Gets a short human-readable description of the value.
    /// </summary>
    public required string DisplayText { get; init; }

    /// <summary>
    /// Gets a stable schema identifier that consumers may use for specialized rendering.
    /// </summary>
    public required string SchemaKey { get; init; }

    /// <summary>
    /// Gets an optional version identifier for versioned values.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the stable shared JSON payload for the value.
    /// </summary>
    public JToken? Core { get; init; }

    /// <summary>
    /// Gets an optional artifact-specific or value-specific JSON payload.
    /// </summary>
    public JToken? Custom { get; init; }
}