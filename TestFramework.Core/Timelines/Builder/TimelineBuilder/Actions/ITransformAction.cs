using System;
using System.Threading.Tasks;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

using System.ComponentModel;
using TestFramework.Core.Timelines.Builder;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for transforming variable values into new variables.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITransformAction : IImplementedByCore
{
    /// <summary>
    /// Adds a synchronous variable transformation.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer);

    /// <summary>
    /// Adds an asynchronous variable transformation.
    /// </summary>
    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer);
}