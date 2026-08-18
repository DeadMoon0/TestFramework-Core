using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class EnterStageLogEvent(StageInstance stage) : LogEvent
{
    /// <summary>
    /// Nothing. This event narrates the console: the stage's own transition signal says a stage started, and the run's plan named it.
    /// </summary>
    public override DebugLogFacts? Describe() => null;

    public override void FormatLogEvent(LogLineWriter writer)
    {
        writer.WriteLine(PrefixLineWithIndentLevel(writer, "─────────────────────────────────────────────"));
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"Stage: {stage.Stage.Name}  ({stage.Steps.Count} steps)"));
        writer.WriteLine(PrefixLineWithIndentLevel(writer, stage.Stage.Description));
    }
}
