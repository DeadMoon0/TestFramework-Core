using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStepLogEvent(StepInstanceGeneric step) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        var labelSuffix = step.Step.LabelOptions.Label is not null ? $"  [{step.Step.LabelOptions.Label}]" : "";
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"Executing Step: {step.Step.Name}{labelSuffix}"));
    }
}
