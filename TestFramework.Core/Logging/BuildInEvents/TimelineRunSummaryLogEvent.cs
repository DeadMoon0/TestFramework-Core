using System;
using System.Linq;
using TestFramework.Core.Logging;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class TimelineRunSummaryLogEvent(IFreezableCollection<StageInstance> stages, TimeSpan elapsed) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        int complete = 0, error = 0, timeout = 0, skipped = 0;
        foreach (var stage in stages)
        {
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
        }

        writer.WriteLine(PrefixLineWithIndentLevel(writer, "─────────────────────────────────────────────"));
        var summary = $"Run complete — {complete} PASS";
        if (error > 0) summary += $"  {error} FAIL";
        if (timeout > 0) summary += $"  {timeout} TIMEOUT";
        if (skipped > 0) summary += $"  {skipped} SKIPPED";
        summary += $"  ({(int)elapsed.TotalMilliseconds}ms total)";
        writer.WriteLine(PrefixLineWithIndentLevel(writer, summary));
    }
}
