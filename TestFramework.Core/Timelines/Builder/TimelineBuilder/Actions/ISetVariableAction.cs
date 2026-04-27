using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for assigning a variable in the timeline.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetVariableAction
{
    /// <summary>
    /// Adds a step that assigns the provided variable reference to the identifier.
    /// </summary>
    public ITimelineBuilderModifier SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable);
}