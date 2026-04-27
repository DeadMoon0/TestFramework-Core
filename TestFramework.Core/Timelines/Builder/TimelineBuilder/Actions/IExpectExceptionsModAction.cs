using System;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for declaring expected exception types on a step.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IExpectExceptionsModAction
{
    /// <summary>
    /// Declares the exception types that are considered expected for the current step.
    /// </summary>
    public ITimelineBuilderModifier ExpectExceptions(params Type[] exceptionTypes);
}