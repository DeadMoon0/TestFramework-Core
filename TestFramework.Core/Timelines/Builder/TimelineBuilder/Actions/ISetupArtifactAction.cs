using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for preparing an artifact before later use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetupArtifactAction : IImplementedByCore
{
    /// <summary>
    /// Adds a step that performs setup for the specified artifact.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> SetupArtifact(ArtifactIdentifier identifier);
}