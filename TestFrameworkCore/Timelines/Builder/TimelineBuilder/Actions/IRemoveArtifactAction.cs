using TestFrameworkCore.Artifacts;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface IRemoveArtifactAction
{
    public ITimelineBuilderModifier RemoveArtifact(ArtifactIdentifier identifier);
}