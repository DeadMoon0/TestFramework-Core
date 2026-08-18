using System.ComponentModel;
using Newtonsoft.Json;
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
    /// Gets what the value is, stated as facts a consumer can lay out for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only account of the value on the envelope. It used to sit beside a pre-rendered one-line
    /// display text and a full JSON copy of the value, so every write serialised the same thing three
    /// times and a consumer had to guess which of the three to trust.
    /// </para>
    /// <para>
    /// <see cref="ObjectCreationHandling.Replace"/> is load-bearing, not tidiness. The default is
    /// <see cref="ObjectCreationHandling.Auto"/>, under which a deserializer finding a non-null value
    /// already on the property <em>populates that instance</em> rather than building a new one — and
    /// the value already on this property is the shared <see cref="DebugValueDescription.Empty"/>
    /// singleton. Every value update in the process would write its own facts into the one object
    /// that is supposed to mean "nothing was described", corrupting it for every other reader and
    /// making every real description arrive as the same instance.
    /// </para>
    /// </remarks>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DebugValueDescription Description { get; init; } = DebugValueDescription.Empty;

    /// <summary>
    /// Gets a stable schema identifier that consumers may use for specialized rendering.
    /// </summary>
    public required string SchemaKey { get; init; }

    /// <summary>
    /// Gets the value's lifecycle and version history, for a value that has one.
    /// </summary>
    /// <remarks>
    /// Null for a plain variable, which is its current value and nothing more. Replaced rather than
    /// populated on deserialization for the same reason as <see cref="Description"/>.
    /// </remarks>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DebugValueLifecycle? Lifecycle { get; init; }

    /// <summary>
    /// Gets an optional artifact-specific or value-specific JSON payload.
    /// </summary>
    public JToken? Custom { get; init; }
}