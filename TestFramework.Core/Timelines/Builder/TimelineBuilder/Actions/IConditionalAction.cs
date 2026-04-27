using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for conditionally composing nested timeline steps.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IConditionalAction
{
    /// <summary>
    /// Adds steps that are emitted only when the referenced condition evaluates to <see langword="true"/>.
    /// </summary>
    public ITimelineBuilder Conditional<TVar>(ImmutableVariable<TVar, bool> shouldRun, Action<ITimelineBuilder> steps) where TVar : VariableReference<bool>;

    /// <summary>
    /// Adds steps that are emitted only when the provided constant condition is <see langword="true"/>.
    /// </summary>
    public ITimelineBuilder Conditional(bool shouldRun, Action<ITimelineBuilder> steps);
}