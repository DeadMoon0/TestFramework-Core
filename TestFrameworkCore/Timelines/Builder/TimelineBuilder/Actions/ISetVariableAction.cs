using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ISetVariableAction
{
    public ITimelineBuilderModifier SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable);
}