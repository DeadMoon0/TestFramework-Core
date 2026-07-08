using System.Collections.Generic;
using System.Linq;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a specific artifact version identifier cannot be found on an existing artifact instance.
/// </summary>
public class ArtifactVersionNotFoundException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a missing artifact version.
    /// </summary>
    /// <param name="artifactIdentifier">The artifact whose version was requested.</param>
    /// <param name="versionIdentifier">The missing version identifier.</param>
    /// <param name="availableVersions">The version identifiers currently stored for the artifact.</param>
    public ArtifactVersionNotFoundException(ArtifactIdentifier artifactIdentifier, ArtifactVersionIdentifier versionIdentifier, IReadOnlyList<ArtifactVersionIdentifier> availableVersions)
        : base(
            $"Artifact '{artifactIdentifier}' does not contain version '{versionIdentifier}'.",
            new[]
            {
                $"Capture version '{versionIdentifier}' before reading it.",
                $"If you only need the latest data, use the artifact's latest version instead of requesting '{versionIdentifier}'.",
                "Inspect the available version identifiers and align the lookup with the scenario's capture order."
            },
            availableVersions.Count > 0
                ? availableVersions.Select(x => x.ToString()).Distinct(System.StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray()
                : new[] { "No versions are stored for this artifact yet." })
    {
    }
}