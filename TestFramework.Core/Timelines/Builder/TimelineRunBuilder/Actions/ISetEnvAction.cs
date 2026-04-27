using TestFramework.Core.Environment;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

/// <summary>
/// Adds the fluent verb for assigning the environment provider used by a timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetEnvAction
{
    /// <summary>
    /// Sets the environment provider used to resolve run-time environment components.
    /// </summary>
    public ITimelineRunBuilder SetEnv(IEnvironmentProvider environment);
}