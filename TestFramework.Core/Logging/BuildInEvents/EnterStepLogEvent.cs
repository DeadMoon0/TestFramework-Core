using TestFramework.Core.Steps;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStepLogEvent(StepInstanceGeneric step, int attempt) : LogEvent
{
    /// <summary>
    /// Nothing. This event narrates the console: the step's transition signal says it started, and the plan carries its phase and label.
    /// </summary>
    public override DebugLogFacts? Describe() => null;

    public override void FormatLogEvent(LogLineWriter writer)
    {
        var labelSuffix = step.Step.LabelOptions.Label is not null ? $"  [{step.Step.LabelOptions.Label}]" : "";
        string prefix = attempt <= 1 ? "Executing Step" : $"Retry Attempt {attempt}";
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"{prefix}: {step.Step.Name}{labelSuffix} | phase {step.Step.Phase}"));
    }
}
