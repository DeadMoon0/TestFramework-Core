using System;
using TestFrameworkCore.Artifacts;

namespace TestFrameworkCore.Exceptions;

public class ArtifactDoesNotYetExistException(ArtifactIdentifier identifier) : Exception("The Artifact you try to Read does not yet Exist. Artifact: " + identifier)
{
    public ArtifactIdentifier Identifier { get; } = identifier;
}