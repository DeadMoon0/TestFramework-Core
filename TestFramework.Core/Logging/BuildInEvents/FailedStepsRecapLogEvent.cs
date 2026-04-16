using System.Linq;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class FailedStepsRecapLogEvent(IFreezableCollection<StageInstance> stages) : LogEvent
{
    public override void FormatLogEvent(LogLineWriter writer)
    {
        string P(string line) => PrefixLineWithIndentLevel(writer, line);

        var failures = stages
            .SelectMany(stage => stage.Steps
                .Where(step => step.RetryResults.Any() &&
                               step.RetryResults.Last().State is StepState.Error or StepState.Timeout)
                .Select(step => (Stage: stage, Step: step)))
            .ToList();

        if (failures.Count == 0) return;

        writer.WriteLine(P("─────────────────────────────────────────────"));
        writer.WriteLine(P($"FAILURES ({failures.Count})"));
        writer.WriteLine(P(""));

        string? lastStageName = null;
        foreach (var (stage, step) in failures)
        {
            if (stage.Stage.Name != lastStageName)
            {
                lastStageName = stage.Stage.Name;
                writer.WriteLine(P($"  Stage: {stage.Stage.Name}"));
            }
            var result = step.RetryResults.Last();
            string state = result.State == StepState.Timeout ? "TIMEOUT" : "FAIL";
            writer.WriteLine(P($"    [{state}]  {step.Step.Name}"));
            if (result.Exception is not null)
            {
                string msg = Truncate(result.Exception.Message, 120);
                writer.WriteLine(P($"           {result.Exception.GetType().Name}: {msg}"));
            }
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }
}
