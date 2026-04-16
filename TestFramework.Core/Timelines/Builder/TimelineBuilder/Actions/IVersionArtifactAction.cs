using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IVersionArtifactAction
{
    public ITimelineBuilderModifier CaptureArtifactVersion(ArtifactIdentifier identifier);
    public ITimelineBuilderModifier CaptureArtifactVersion(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier);
}