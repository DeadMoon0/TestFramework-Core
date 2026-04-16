using TestFramework.Core.Artifacts;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IRemoveArtifactAction
{
    public ITimelineBuilderModifier RemoveArtifact(ArtifactIdentifier identifier);
}