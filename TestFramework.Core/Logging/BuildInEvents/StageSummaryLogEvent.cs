using System;
using System.Linq;
using System.Text;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class StageSummaryLogEvent(StageInstance stage, TimeSpan elapsed) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        int complete = 0, error = 0, timeout = 0, skipped = 0;
        foreach (var step in stage.Steps)
        {
            if (!step.RetryResults.Any()) continue;
            switch (step.RetryResults.Last().State)
            {
                case StepState.Complete: complete++; break;
                case StepState.Error: error++; break;
                case StepState.Timeout: timeout++; break;
                case StepState.Skipped: skipped++; break;
            }
        }

        var sb = new StringBuilder();
        sb.Append($"{complete} PASS");
        if (error > 0) sb.Append($"  {error} FAIL");
        if (timeout > 0) sb.Append($"  {timeout} TIMEOUT");
        if (skipped > 0) sb.Append($"  {skipped} SKIPPED");
        sb.Append($"  ({(int)elapsed.TotalMilliseconds}ms)");
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"Stage Done  {sb}"));
    }
}
