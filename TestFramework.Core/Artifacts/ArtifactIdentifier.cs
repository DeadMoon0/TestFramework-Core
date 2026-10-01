using TestFramework.Core.Conventions;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Identifies an artifact instance.
/// </summary>
/// <param name="Identifier">The underlying artifact key.</param>
[ClosedShape("An identifier is its name; anything more about an artifact belongs on the artifact.")]
public record ArtifactIdentifier(string Identifier)
{
    /// <summary>
    /// Converts an artifact identifier to its string key.
    /// </summary>
    public static implicit operator string(ArtifactIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a string key to an artifact identifier.
    /// </summary>
    public static implicit operator ArtifactIdentifier(string id) => new ArtifactIdentifier(id);

    /// <summary>
    /// Returns the artifact identifier key.
    /// </summary>
    public override string ToString() => Identifier;
}