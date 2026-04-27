using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

/// <summary>
/// Adds the fluent verb for seeding variables into a timeline run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAddVariableAction
{
    /// <summary>
    /// Adds an initial variable value to the run configuration.
    /// </summary>
    public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value);
}