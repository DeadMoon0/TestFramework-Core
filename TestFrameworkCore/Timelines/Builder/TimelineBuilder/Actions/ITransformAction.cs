using System;
using System.Threading.Tasks;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface ITransformAction
{
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer);
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer);
}