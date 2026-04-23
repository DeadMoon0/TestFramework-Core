using TestFramework.Core.Environment;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

public interface ISetEnvAction
{
    public ITimelineRunBuilder SetEnv(IEnvironmentProvider environment);
}