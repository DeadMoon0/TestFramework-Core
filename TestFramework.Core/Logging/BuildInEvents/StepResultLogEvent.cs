using System;
using System.Linq;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Logging.BuildInEvents;

internal class StepResultLogEvent(string stepName, string? label, StepResultGeneric stepResult, TimeSpan elapsed) : LogEvent
{
    private const int MaxExceptionMessageLength = 400;
    private const int MaxStackTraceLines = 10;

    private static string StateSymbol(StepState state) => state switch
    {
        StepState.Complete => "[PASS]",
        StepState.Error => "[FAIL]",
        StepState.Timeout => "[TIMEOUT]",
        StepState.Skipped => "[SKIPPED]",
        _ => "?"
    };

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Length <= max) return s;
        return s[..max] + $"… [+{s.Length - max} chars]";
    }

    public override void FormatLogEvent(LogLineWriter writer)
    {
        string symbol = StateSymbol(stepResult.State);
        var labelSuffix = label is not null ? $"  [{label}]" : "";
        writer.WriteLine(PrefixLineWithIndentLevel(writer, $"{symbol}  {stepName}{labelSuffix}  ({(int)elapsed.TotalMilliseconds}ms)"));

        CurrentIndentLevel++;

        if (stepResult.Result is not null)
            writer.WriteLine(PrefixLineWithIndentLevel(writer, "Result: " + VariableFormatter.Format(stepResult.Result)));

        if (stepResult.Exception is not null)
        {
            var ex = stepResult.Exception;
            writer.WriteLine(PrefixLineWithIndentLevel(writer, $"{ex.GetType().Name}: {Truncate(ex.Message, MaxExceptionMessageLength)}"));

            // Stack trace — first N non-empty lines
            string[] allStackLines = SpitStringByCommonLineBreaks(ex.StackTrace ?? string.Empty)
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            int omitted = Math.Max(0, allStackLines.Length - MaxStackTraceLines);
            foreach (var line in allStackLines.Take(MaxStackTraceLines))
                writer.WriteLine(PrefixLineWithIndentLevel(writer, "  " + line.TrimStart()));
            if (omitted > 0)
                writer.WriteLine(PrefixLineWithIndentLevel(writer, $"  … [{omitted} more stack frame(s)]"));

            // Inner exception chain — type + truncated message only
            var inner = ex.InnerException;
            while (inner is not null)
            {
                writer.WriteLine(PrefixLineWithIndentLevel(writer, $"  └─ {inner.GetType().Name}: {Truncate(inner.Message, 160)}"));
                inner = inner.InnerException;
            }
        }

        CurrentIndentLevel--;
    }
}

