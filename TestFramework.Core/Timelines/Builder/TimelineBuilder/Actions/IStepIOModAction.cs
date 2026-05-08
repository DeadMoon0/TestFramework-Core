using System.ComponentModel;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds fluent modifiers for capturing a step result into explicit output variables.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStepIOModAction
{
    /// <summary>Captures the current step result into a variable and declares it as an output.</summary>
    ITimelineBuilderModifier CaptureResultAs(VariableIdentifier key);

    /// <summary>Captures the current step result into a typed variable and declares it as an output.</summary>
    ITimelineBuilderModifier CaptureResultAs<T>(VariableIdentifier key);
}
