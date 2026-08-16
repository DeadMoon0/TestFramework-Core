namespace TestFramework.Core.Debugger;

/// <summary>
/// The canonical schema keys for artifacts shipped with the TestFramework packages.
/// </summary>
/// <remarks>
/// <para>
/// A schema key tells a consumer which renderer to use for a value. It is deliberately not the CLR
/// type name: different runtime types can share one debug shape, and an artifact's display contract
/// should survive an implementation type being renamed. Keys are therefore stable identifiers, and
/// changing one is a breaking change for any consumer that draws that artifact.
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
}
