using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a timeline tries to retrieve an artifact that was never registered.
/// </summary>
public class ArtifactNotFoundException : TimelineFrameworkException
{
    private readonly string _artifactName;
    private readonly IReadOnlyList<string> _declaredArtifacts;
    private readonly int? _stepIndex;

    /// <summary>
    /// Gets the name of the missing artifact.
    /// </summary>
    public string ArtifactName => _artifactName;

    /// <summary>
    /// Gets the list of artifacts that were declared in the timeline run.
    /// </summary>
    public IReadOnlyList<string> RegisteredArtifacts => _declaredArtifacts;

    /// <summary>
    /// Initializes a new instance of the ArtifactNotFoundException class.
    /// </summary>
    /// <param name="artifactName">Name of the artifact that was not found</param>
    /// <param name="registeredArtifacts">List of artifacts that are available</param>
    /// <param name="stepIndex">Index of the step where the error occurred</param>
    public ArtifactNotFoundException(
        string artifactName,
        IReadOnlyList<string> registeredArtifacts,
        int? stepIndex = null)
        : base(
            $"Artifact '{artifactName}' was not declared for this run.",
            new[]
            {
                $"Declare/setup path: timeline.SetupArtifact(\"{artifactName}\") and then Add...Artifact(\"{artifactName}\", ...)",
                $"Register path: timeline.RegisterArtifact(\"{artifactName}\", ...) in the step that creates it",
                $"Discovery path: timeline.FindArtifact(\"{artifactName}\", ...) or timeline.FindArtifacts(\"{artifactName}\", ...)",
                "Check artifact name spelling (case-sensitive)"
            },
            registeredArtifacts.Any()
                ? registeredArtifacts.ToList()
                : new List<string> { "No artifact identifiers declared yet" })
    {
        _artifactName = artifactName;
        _declaredArtifacts = registeredArtifacts;
        _stepIndex = stepIndex;
    }
}
