using System;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact is requested before it has been created.
/// </summary>
/// <param name="identifier">The artifact identifier that is not yet available.</param>
public class ArtifactDoesNotYetExistException(ArtifactIdentifier identifier) : Exception("The Artifact you try to Read does not yet Exist. Artifact: " + identifier)
{
    /// <summary>
    /// Gets the artifact identifier that is not yet available.
    /// </summary>
    public ArtifactIdentifier Identifier { get; } = identifier;
}