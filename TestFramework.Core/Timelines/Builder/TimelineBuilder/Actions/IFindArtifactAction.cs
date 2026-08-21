using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;
using System.Collections.Generic;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for locating artifacts through artifact finders.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IFindArtifactAction
{
    /// <summary>
    /// Adds a step that finds a single artifact.
    /// </summary>
    public IArtifactTimelineBuilderModifier<EmptyStepResultContext> FindArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>;

    /// <summary>
    /// Adds a step that finds multiple artifacts in one operation and assigns generated names.
    /// </summary>
    public IArtifactTimelineBuilderModifier<EmptyStepResultContext> FindArtifacts<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier baseName, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>;

    /// <summary>
    /// Adds a step that finds multiple artifacts in one operation and assigns exact names.
    /// </summary>
    public IArtifactTimelineBuilderModifier<EmptyStepResultContext> FindArtifactsAs<TArtifactReference, TArtifactDescriber, TArtifactData>(IReadOnlyList<ArtifactIdentifier> identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>;
}