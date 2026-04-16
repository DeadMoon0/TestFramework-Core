using TestFramework.Core.Logging;
using TestFramework.Core.Stages;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStageLogEvent(StageInstance stage) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        writer.WriteLine(PrefixLineWithIndentLevel(writer, "─────────────────────────────────────────────"));
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"Stage: {stage.Stage.Name}  ({stage.Steps.Count} steps)"));
        writer.WriteLine(PrefixLineWithIndentLevel(writer, stage.Stage.Description));
    }
}
