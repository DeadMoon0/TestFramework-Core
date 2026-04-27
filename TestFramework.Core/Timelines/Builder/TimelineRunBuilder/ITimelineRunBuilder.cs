using TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder;

/// <summary>
/// Exposes the fluent API for configuring a specific timeline execution before calling <c>RunAsync()</c>.
/// </summary>
public interface ITimelineRunBuilder :
    ISetEnvAction,
    IAddVariableAction,
    IAddArtifactAction,
    IRunAsyncAction;