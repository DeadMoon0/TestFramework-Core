using System;
using System.Threading.Tasks;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for transforming variable values into new variables.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITransformAction
{
    /// <summary>
    /// Adds a synchronous variable transformation.
    /// </summary>
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer);

    /// <summary>
    /// Adds an asynchronous variable transformation.
    /// </summary>
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer);
}