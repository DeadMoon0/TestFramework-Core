using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an exact artifact-name list does not match the number of artifacts produced by a finder.
/// </summary>
public class ArtifactCountMismatchException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception with the expected and actual artifact counts.
    /// </summary>
    /// <param name="expectedCount">The number of artifact identifiers the scenario declared.</param>
    /// <param name="actualCount">The number of artifacts the finder produced.</param>
    public ArtifactCountMismatchException(int expectedCount, int actualCount)
        : base(
            $"FindArtifactsAs expected {expectedCount} artifact name(s) but the finder produced {actualCount} result(s).",
            new[]
            {
                "Make the explicit identifier list match the number of artifacts the finder can return.",
                "Use FindArtifacts(baseName, ...) when generated names are acceptable.",
                "Reduce or expand the finder scope so it returns the exact expected count."
            },
            new List<string>
            {
                $"Expected names: {expectedCount}",
                $"Finder results: {actualCount}"
            })
    {
    }
}