using System;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact is requested but no such artifact exists.
/// </summary>
/// <param name="identifier">The missing artifact identifier.</param>
public class ArtifactDoesNotExistException(ArtifactIdentifier identifier) : Exception("The Artifact you try to Read does not Exist. Artifact: " + identifier)
{
    /// <summary>
    /// Gets the missing artifact identifier.
    /// </summary>
    public ArtifactIdentifier Identifier { get; } = identifier;
}