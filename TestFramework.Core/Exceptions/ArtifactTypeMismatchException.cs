using System;
using System.Collections.Generic;
using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when an artifact assertion handle expects a different data type than the artifact actually contains.
/// </summary>
public class ArtifactTypeMismatchException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for an artifact whose actual data type does not match the requested typed handle.
    /// </summary>
    /// <param name="identifier">The artifact identifier being asserted.</param>
    /// <param name="expectedType">The artifact data type requested by the consumer.</param>
    /// <param name="actualType">The artifact data type currently stored for the artifact.</param>
    public ArtifactTypeMismatchException(ArtifactIdentifier identifier, Type expectedType, Type actualType)
        : base(
            $"Artifact '{identifier}' contains '{actualType.Name}', not the requested '{expectedType.Name}'.",
            new[]
            {
                $"Use run.Artifact(\"{identifier}\") when you only need untyped assertions.",
                $"Use the matching typed helper for '{actualType.Name}' instead of requesting '{expectedType.Name}'.",
                "Check whether the artifact identifier points to a different artifact than the one you intended to assert."
            },
            new List<string>
            {
                $"Requested type: {expectedType.FullName}",
                $"Actual type: {actualType.FullName}"
            })
    {
    }
}