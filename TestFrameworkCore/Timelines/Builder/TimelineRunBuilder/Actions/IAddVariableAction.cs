using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineRunBuilder.Actions;

public interface IAddVariableAction
{
    public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value);
}