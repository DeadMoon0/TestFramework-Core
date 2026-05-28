using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStepLogEvent(StepInstanceGeneric step, int attempt) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        var labelSuffix = step.Step.LabelOptions.Label is not null ? $"  [{step.Step.LabelOptions.Label}]" : "";
        string prefix = attempt <= 1 ? "Executing Step" : $"Retry Attempt {attempt}";
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"{prefix}: {step.Step.Name}{labelSuffix} | phase {step.Step.Phase}"));
    }
}
