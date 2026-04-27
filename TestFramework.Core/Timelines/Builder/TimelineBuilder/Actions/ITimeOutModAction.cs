using System;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent modifier for configuring step timeouts.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITimeOutModAction
{
    /// <summary>
    /// Sets the timeout for the current step.
    /// </summary>
    public ITimelineBuilderModifier WithTimeOut(VariableReference<TimeSpan> timeout);
}