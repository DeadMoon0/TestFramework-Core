using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact cannot be deconstructed because its reference does not support deconstruction.
/// </summary>
public class ArtifactDeconstructionUnavailableException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for an artifact whose reference cannot be deconstructed.
    /// </summary>
    /// <param name="identifier">The artifact identifier.</param>
    public ArtifactDeconstructionUnavailableException(ArtifactIdentifier identifier)
        : base(
            $"Artifact '{identifier}' cannot be deconstructed because its reference has no deconstruction data.",
            new[]
            {
                "Only deconstruct artifacts that were set up or registered with a reference that supports cleanup.",
                "Check whether the artifact reference type is cleanup-capable before calling RemoveArtifact() or relying on cleanup deconstruction.",
                "If the artifact represents discovery-only state, skip deconstruction for that identifier."
            },
            new List<string> { $"Artifact: {identifier}" })
    {
    }
}