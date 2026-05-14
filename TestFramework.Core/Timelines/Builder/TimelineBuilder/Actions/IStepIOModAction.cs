using System;
using System.ComponentModel;
using System.Linq.Expressions;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds fluent modifiers for binding step result context properties into explicit output variables.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStepIOModAction<TStepResultContext> where TStepResultContext : StepResultContext
{
    /// <summary>Binds a result context property from the current step into a variable and declares it as an output.</summary>
    ITimelineBuilderModifier<TStepResultContext> BindResultProperty<TValue>(Expression<Func<TStepResultContext, TValue>> selector, VariableIdentifier key);
}
