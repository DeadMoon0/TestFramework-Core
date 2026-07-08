using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact is requested before it has been created.
/// </summary>
/// <param name="identifier">The artifact identifier that is not yet available.</param>
public class ArtifactDoesNotYetExistException(ArtifactIdentifier identifier) : TimelineFrameworkException(
    $"Artifact '{identifier}' was declared, but no data version is available yet.",
    new[]
    {
        $"If this is a setup artifact, call Add...Artifact(\"{identifier}\", ...) before RunAsync().",
        $"If this is a registered artifact, make sure the producing step runs before you read '{identifier}'.",
        $"If this is a discovered artifact, wait until the FindArtifact/FindArtifacts step has executed."
    })
{
    /// <summary>
    /// Gets the artifact identifier that is not yet available.
    /// </summary>
    public ArtifactIdentifier Identifier { get; } = identifier;
}