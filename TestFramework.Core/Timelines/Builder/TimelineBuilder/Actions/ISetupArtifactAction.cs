using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetupArtifactAction
{
    public ITimelineBuilderModifier SetupArtifact(ArtifactIdentifier identifier);
}