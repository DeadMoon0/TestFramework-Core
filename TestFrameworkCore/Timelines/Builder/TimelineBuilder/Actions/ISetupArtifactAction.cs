using TestFrameworkCore.Artifacts;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetupArtifactAction
{
    public ITimelineBuilderModifier SetupArtifact(ArtifactIdentifier identifier);
}