namespace TestFramework.Core.Artifacts;

/// <summary>
/// Identifies a specific version of an artifact.
/// </summary>
/// <param name="Identifier">The underlying version key.</param>
public record ArtifactVersionIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a version identifier to its string key.
    /// </summary>
    public static implicit operator string(ArtifactVersionIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a string key to a version identifier.
    /// </summary>
    public static implicit operator ArtifactVersionIdentifier(string id) => new ArtifactVersionIdentifier(id);

    /// <summary>
    /// Gets the default version identifier.
    /// </summary>
    public static readonly ArtifactVersionIdentifier Default = "";

    /// <summary>
    /// Returns the version identifier key.
    /// </summary>
    public override string ToString() => Identifier;
}