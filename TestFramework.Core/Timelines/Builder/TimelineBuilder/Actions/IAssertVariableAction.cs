using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verb for asserting a variable during timeline composition.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAssertVariableAction
{
    /// <summary>
    /// Adds a step that asserts a variable against the provided predicate.
    /// </summary>
    public ITimelineBuilderModifier AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate);
}