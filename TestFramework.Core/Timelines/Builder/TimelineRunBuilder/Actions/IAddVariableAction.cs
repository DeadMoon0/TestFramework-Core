using TestFramework.Core.Timelines.Builder.TimelineRunBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineRunBuilder.Actions;

public interface IAddVariableAction
{
    public ITimelineRunBuilder AddVariable<T>(VariableIdentifier identifier, T value);
}