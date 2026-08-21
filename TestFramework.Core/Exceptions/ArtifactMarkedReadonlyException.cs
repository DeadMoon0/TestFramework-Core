using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a timeline explicitly asks to remove an artifact that it also marked readonly.
/// </summary>
/// <remarks>
/// Silently skipping the removal would be worse than failing: the timeline states two opposite
/// intentions for the same artifact, and only the author can say which one was meant.
/// </remarks>
public class ArtifactMarkedReadonlyException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for an artifact that is marked readonly.
    /// </summary>
    /// <param name="identifier">The artifact identifier.</param>
    public ArtifactMarkedReadonlyException(ArtifactIdentifier identifier)
        : base(
            $"Artifact '{identifier}' is marked readonly, so RemoveArtifact() must not deconstruct it.",
            new[]
            {
                "Drop the MarkReadonly() call if this timeline is meant to own the artifact and clean it up.",
                "Drop the RemoveArtifact() call if the artifact really is readonly - cleanup already leaves it in place.",
                "Register or find the resource a second time under its own identifier when one timeline needs both."
            },
            new List<string> { $"Artifact: {identifier}" })
    {
    }
}
