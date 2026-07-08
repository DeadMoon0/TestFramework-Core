using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact is requested but no such artifact exists.
/// </summary>
/// <param name="identifier">The missing artifact identifier.</param>
public class ArtifactDoesNotExistException(ArtifactIdentifier identifier) : TimelineFrameworkException(
    $"Artifact '{identifier}' has no available data for this run.",
    new[]
    {
        $"Check whether '{identifier}' was ever found or populated in the scenario.",
        "If the resource comes from discovery, verify the finder actually produced a result.",
        "Inspect the run's artifact assertions first to confirm whether the artifact existed at all."
    })
{
    /// <summary>
    /// Gets the missing artifact identifier.
    /// </summary>
    public ArtifactIdentifier Identifier { get; } = identifier;
}