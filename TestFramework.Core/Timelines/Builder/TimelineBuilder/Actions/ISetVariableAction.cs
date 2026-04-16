using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetVariableAction
{
    public ITimelineBuilderModifier SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable);
}