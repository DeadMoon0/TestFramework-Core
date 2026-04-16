using System;
using System.Threading.Tasks;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface ITransformAction
{
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer);
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer);
}