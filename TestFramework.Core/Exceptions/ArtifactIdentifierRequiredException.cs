namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact-discovery operation is configured without any identifiers.
/// </summary>
public class ArtifactIdentifierRequiredException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for an artifact-discovery operation that received no identifiers.
    /// </summary>
    /// <param name="operation">The artifact-discovery API that was configured incorrectly.</param>
    public ArtifactIdentifierRequiredException(string operation)
        : base(
            $"{operation} requires at least one artifact identifier.",
            new[]
            {
                $"Provide at least one artifact name when calling {operation}.",
                "Use FindArtifact(name, ...) when exactly one artifact is expected.",
                "Use FindArtifacts(baseName, ...) when the finder returns a dynamic number of artifacts."
            })
    {
    }
}