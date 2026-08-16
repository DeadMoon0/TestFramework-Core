namespace TestFramework.Core.Debugger;

/// <summary>
/// The canonical schema keys: what a value is, in the terms a consumer picks a renderer by.
/// </summary>
/// <remarks>
/// <para>
/// A schema key tells a consumer which renderer to use for a value. It is deliberately not the CLR
/// type name: different runtime types can share one debug shape, and an artifact's display contract
/// should survive an implementation type being renamed. Keys are therefore stable identifiers, and
/// changing one is a breaking change for any consumer that draws that artifact.
/// </para>
/// <para>
/// There are two families. <c>tf.artifact.*</c> names a kind of thing the framework knows how to set
/// up and tear down, and each shipped artifact declares its own. <c>tf.value.*</c> names a
/// <see cref="DebugValueShape"/> and is what a plain variable gets, because a variable has no schema
/// beyond its shape — it is whatever a step happened to assign.
/// </para>
/// <para>
/// Note that <c>SqlRow</c> is shared by two independent implementations — the EF-backed one in
/// TestFramework.Azure and the ADO-backed one in TestFramework.Web. That is the point: both present
/// a database row, so both should render as one, and a consumer needs a single icon and inspector
/// rather than two that happen to look alike.
/// </para>
/// <para>
/// The extension packages carry these as literals rather than referencing this class, because they
/// build against the published Core package. Each has a test pinning its literal to the value here,
/// so the two cannot drift apart silently.
/// </para>
/// </remarks>
public static class DebugValueSchemaKeys
{
    /// <summary>A row in a relational database.</summary>
    public const string SqlRow = "tf.artifact.sql.row";

    /// <summary>An item in a Cosmos DB container.</summary>
    public const string CosmosItem = "tf.artifact.cosmos.item";

    /// <summary>A blob in Azure Storage.</summary>
    public const string Blob = "tf.artifact.blob";

    /// <summary>An entity in Azure Table Storage.</summary>
    public const string TableEntity = "tf.artifact.table.entity";

    /// <summary>A file on the local file system.</summary>
    public const string File = "tf.artifact.file";

    /// <summary>A value whose shape was never determined.</summary>
    public const string Unknown = "tf.value.unknown";

    /// <summary>The absence of a value.</summary>
    public const string Null = "tf.value.null";

    /// <summary>A single indivisible value.</summary>
    public const string Scalar = "tf.value.scalar";

    /// <summary>Text.</summary>
    public const string Text = "tf.value.text";

    /// <summary>Bytes.</summary>
    public const string Binary = "tf.value.binary";

    /// <summary>An ordered sequence of items.</summary>
    public const string Collection = "tf.value.collection";

    /// <summary>Keyed entries.</summary>
    public const string Dictionary = "tf.value.dictionary";

    /// <summary>A composite value with named members.</summary>
    public const string Object = "tf.value.object";

    /// <summary>Gets the schema key a value of the given shape is published under.</summary>
    public static string Of(DebugValueShape shape) => shape switch
    {
        DebugValueShape.Null => Null,
        DebugValueShape.Scalar => Scalar,
        DebugValueShape.Text => Text,
        DebugValueShape.Binary => Binary,
        DebugValueShape.Collection => Collection,
        DebugValueShape.Dictionary => Dictionary,
        DebugValueShape.Object => Object,
        _ => Unknown
    };
}
