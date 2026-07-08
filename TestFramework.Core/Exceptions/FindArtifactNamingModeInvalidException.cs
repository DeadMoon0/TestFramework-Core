namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a find-artifact step reaches an unsupported internal naming mode.
/// </summary>
public class FindArtifactNamingModeInvalidException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for an unsupported find-artifact naming mode.
    /// </summary>
    /// <param name="namingMode">The unexpected naming mode value.</param>
    public FindArtifactNamingModeInvalidException(object namingMode)
        : base(
            $"FindArtifactStep reached unsupported naming mode '{namingMode}'.",
            new[]
            {
                "This indicates an internal framework inconsistency rather than a scenario authoring mistake.",
                "Check the code path that constructed the find-artifact step and verify the naming mode mapping.",
                "If this was triggered by a new builder API, align it with the existing Single, Generated, or Exact naming modes."
            })
    {
    }
}