using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact resolver reports Found=true but does not return artifact data.
/// </summary>
public class ArtifactResolutionInvariantException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a resolver that violated the Found/data invariant.
    /// </summary>
    /// <param name="identifier">The artifact being resolved.</param>
    /// <param name="operation">The operation performing the resolution.</param>
    /// <param name="versionIdentifier">The requested version identifier, when relevant.</param>
    public ArtifactResolutionInvariantException(ArtifactIdentifier identifier, string operation, ArtifactVersionIdentifier? versionIdentifier = null)
        : base(
            versionIdentifier is null
                ? $"Artifact '{identifier}' reported Found=true during {operation}, but no artifact data was returned."
                : $"Artifact '{identifier}' reported Found=true during {operation} for version '{versionIdentifier}', but no artifact data was returned.",
            new[]
            {
                "Fix the artifact reference or finder so Found=true always returns a concrete artifact data payload.",
                "If the artifact is genuinely absent, return Found=false instead of Found=true with null data.",
                versionIdentifier is null
                    ? "Check the artifact resolver implementation for a missing data-mapping branch."
                    : $"Check the resolver branch for version '{versionIdentifier}' and ensure it materializes the requested payload."
            },
            BuildOptions(operation, versionIdentifier))
    {
    }

    private static IReadOnlyList<string> BuildOptions(string operation, ArtifactVersionIdentifier? versionIdentifier)
    {
        List<string> options = [$"Operation: {operation}"];
        if (versionIdentifier is not null)
            options.Add($"Version: {versionIdentifier}");
        return options;
    }
}