using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

public class OutputRunDebuggerTests
{
    private const string DisableUnicodeOutEnvironmentVariable = "TestFramework_Disable_Unicode_Out";
    private static readonly object EnvironmentVariableGate = new();

    [Fact]
    public async Task Writes_ControlFlowMarkers_For_InterleavedParallelSteps()
    {
        await WithUnicodeOutputSettingAsync(null, async () =>
        {
            RecordingOutputHelper output = new();
            OutputRunDebugger debugger = new(output);

            await debugger.SignalInitTimelineRunAsync("session", "timeline", "project", new TimelineRunStructure
            {
                Stages =
                [
                    new DebugStageState
                    {
                        Name = "Main",
                        Description = "Primary execution flow",
                        Steps =
                        [
                            CreateStep("FetchUsers"),
                            CreateStep("RenderSummary", "parallel")
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, TestFramework.Core.Debugger.VariableState>(),
                Artifacts = new Dictionary<ArtifactIdentifier, TestFramework.Core.Debugger.ArtifactState>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Running);
            await debugger.SignalLogEntryAsync("session", new DebugLogEntry
            {
                Stage = "Main",
                StepId = 0,
                Iteration = 1,
                Message = "starting fetch"
            });
            await debugger.SignalValueUpdateAsync("session", "count", DebugValueKind.Variable, "Main", 1, new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = "System.Int32",
                DisplayText = "2",
                SchemaKey = "int32"
            });
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Error, DebugLifecycleState.Running);
            await debugger.SignalAssertionAsync("session", new DebugAssertionEntry
            {
                OccurredAtUtc = DateTimeOffset.UtcNow,
                TargetKind = DebugAssertionTargetKind.Value,
                Target = "summaryText",
                AssertionName = "Be",
                AssertionDisplay = "Be(\"ok\")",
                FailureReason = "expected \"ok\", was \"nope\"",
                Succeeded = false
            });

            Assert.Empty(output.Lines);

            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.Contains(output.Lines, line => line.Contains("INPUTS") && line.Contains("OUTPUTS") && line.Contains('│'));
            Assert.DoesNotContain(output.Lines, line => line.Length == 0);
            string flowTraceHeader = Assert.Single(output.Lines.Where(line => line.Contains("Flow Trace")));
            string stepHeader = Assert.Single(output.Lines.Where(line => line.Contains("Step [#0]  FetchUsers")));
            string lastStepHeader = Assert.Single(output.Lines.Where(line => line.Contains("Step [#1]  RenderSummary  [parallel]")));

            Assert.Contains("TIMELINE DEBUG VIEW  timeline", rendered);
            Assert.Contains("STAGE  Main", rendered);
            Assert.Contains("steps: 2 | parallel-capable: 2", rendered);
            Assert.Contains("╭", rendered);
            Assert.Contains("╰", rendered);
            Assert.Contains(output.Lines, line => line.StartsWith("│ ╭"));
            Assert.Contains(output.Lines, line => line.StartsWith("├─┤ STAGE  Main"));
            Assert.Contains(output.Lines, line => line == "│");
            Assert.Contains(output.Lines, line => line == "│ │");
            Assert.StartsWith("│ │ ╭─ Flow Trace ", flowTraceHeader);
            Assert.Equal(99, flowTraceHeader.Length);
            Assert.StartsWith("│ │ ╭─ Step [#0]  FetchUsers ", stepHeader);
            Assert.Equal(99, stepHeader.Length);
            Assert.StartsWith("│ │ ╭─ Step [#1]  RenderSummary  [parallel] ", lastStepHeader);
            Assert.Equal(99, lastStepHeader.Length);
            Assert.Contains(output.Lines, line => line.StartsWith("│ ├─┤  1. [RUN ] [#0]") || line.StartsWith("│ ├─┤  1. [RUN ] [#1]"));
            Assert.Contains(output.Lines, line => line.StartsWith("│   │ ACTIVITY"));
            Assert.Contains(output.Lines, line => line.StartsWith("│ │ │ ACTIVITY"));
            Assert.Contains(output.Lines, line => line.StartsWith("└─┤ STAGE") || line.StartsWith("│ └─┤ State:"));
            Assert.Contains("Flow Trace", rendered);
            Assert.Contains("1. [RUN ] [#0]     -> FetchUsers", rendered);
            Assert.Contains("2. [RUN ] [#1]     -> RenderSummary  [parallel]", rendered);
            Assert.Contains("3. [PASS] [#0]     <- FetchUsers", rendered);
            Assert.Contains("4. [FAIL] [#1]     <- RenderSummary  [parallel]", rendered);
            Assert.Contains("Step [#0]  FetchUsers", rendered);
            Assert.Contains("Step [#1]  RenderSummary  [parallel]", rendered);
            Assert.Contains("│ ACTIVITY", rendered);
            Assert.Contains("Variable count  [observed]  = 2", rendered);
            Assert.Contains("Assertions", rendered);
            Assert.Contains("[FAIL] summaryText  Be(\"ok\")", rendered);
            Assert.Contains("expected \"ok\", was \"nope\"", rendered);
        });
    }

    [Fact]
    public async Task Writes_Retry_And_Skipped_Badges_In_FlowTrace()
    {
        await WithUnicodeOutputSettingAsync(null, async () =>
        {
            RecordingOutputHelper output = new();
            OutputRunDebugger debugger = new(output);

            await debugger.SignalInitTimelineRunAsync("session", "timeline", "project", new TimelineRunStructure
            {
                Stages =
                [
                    new DebugStageState
                    {
                        Name = "Retries",
                        Description = "Retry and skip paths",
                        Steps =
                        [
                            CreateStep("FlakyCall"),
                            CreateStep("OptionalCleanup")
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, TestFramework.Core.Debugger.VariableState>(),
                Artifacts = new Dictionary<ArtifactIdentifier, TestFramework.Core.Debugger.ArtifactState>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Retries", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 0, DebugLifecycleState.WaitingForRetry, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 0, DebugLifecycleState.Running, DebugLifecycleState.WaitingForRetry);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 1, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Retries", 1, DebugLifecycleState.Skipped, DebugLifecycleState.Running);

            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);

            Assert.Contains("[WAIT] [#0]     <- FlakyCall waiting for retry", rendered);
            Assert.Contains("[RETRY] [#0:r2]  -> FlakyCall (retry 2)", rendered);
            Assert.Contains("[PASS] [#0:r2]  <- FlakyCall", rendered);
            Assert.Contains("[SKIP] [#1]     <- OptionalCleanup", rendered);
        });
    }

    [Fact]
    public async Task Uses_Ascii_Output_When_Unicode_Is_Disabled_By_Environment()
    {
        await WithUnicodeOutputSettingAsync("true", async () =>
        {
            RecordingOutputHelper output = new();
            OutputRunDebugger debugger = new(output);

            await debugger.SignalInitTimelineRunAsync("session", "timeline", "project", new TimelineRunStructure
            {
                Stages =
                [
                    new DebugStageState
                    {
                        Name = "Main",
                        Description = string.Empty,
                        Steps = [CreateStep("FetchUsers")]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, TestFramework.Core.Debugger.VariableState>(),
                Artifacts = new Dictionary<ArtifactIdentifier, TestFramework.Core.Debugger.ArtifactState>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.Contains("+=============================================================================================+", rendered);
            Assert.Contains("| ACTIVITY", rendered);
            Assert.DoesNotContain('│', rendered);
            Assert.DoesNotContain('╭', rendered);
            Assert.DoesNotContain('╰', rendered);
        });
    }

    private static DebugStepState CreateStep(string name, string? label = null)
    {
        return new DebugStepState
        {
            Name = name,
            Description = string.Empty,
            RetryOptions = new RetryOptions(),
            ErrorHandlingOptions = new ErrorHandlingOptions(),
            TimeOutOptions = new TimeOutOptions(),
            LabelOptions = new LabelOptions { Label = label },
            ExecutionOptions = new ExecutionOptions(),
            IOContract = new StepIOContract(),
            DoesReturn = false
        };
    }

    private sealed class RecordingOutputHelper : ITestOutputHelper
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message)
        {
            Lines.Add(message);
        }

        public void WriteLine(string format, params object[] args)
        {
            Lines.Add(string.Format(format, args));
        }
    }

    private static async Task WithUnicodeOutputSettingAsync(string? value, Func<Task> testAction)
    {
        lock (EnvironmentVariableGate)
        {
            string? original = global::System.Environment.GetEnvironmentVariable(DisableUnicodeOutEnvironmentVariable);
            global::System.Environment.SetEnvironmentVariable(DisableUnicodeOutEnvironmentVariable, value);

            try
            {
                testAction().GetAwaiter().GetResult();
            }
            finally
            {
                global::System.Environment.SetEnvironmentVariable(DisableUnicodeOutEnvironmentVariable, original);
            }
        }

        await Task.CompletedTask;
    }
}