using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                            CreateStep("FetchUsers", phase: StepExecutionPhase.Prepare),
                            CreateStep("RenderSummary", phase: StepExecutionPhase.Prepare)
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Running);
            // The console renders from the event, not from what the transport carries, so this is the channel
            // a log reaches it on. A transported entry is for a consumer that builds its own display.
            debugger.WriteRenderedLog(["starting fetch"], new LogPlacement("Main", 0, 1, 0));
            await debugger.SignalValueUpdateAsync("session", "count", DebugValueKind.Variable, "Main", 1, new DebugValueEnvelope
            {
                Kind = DebugValueKind.Variable,
                TypeName = "System.Int32",
                Description = new DebugValueDescription { Summary = "2", Shape = DebugValueShape.Scalar },
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
            string lastStepHeader = Assert.Single(output.Lines.Where(line => line.Contains("Step [#1]  RenderSummary")));

            Assert.Contains("TIMELINE DEBUG VIEW  timeline", rendered);
            Assert.Contains("STAGE  Main", rendered);
            Assert.Contains("steps: 2 | layers: 1 | peak parallel: 2", rendered);
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
            Assert.StartsWith("│ │ ╭─ Step [#1]  RenderSummary ", lastStepHeader);
            Assert.Equal(99, lastStepHeader.Length);
            Assert.Contains("> L0  Prepare  x2", rendered);
            Assert.Contains(output.Lines, line => line.Contains("1. [RUN ] [#0]     -> FetchUsers") || line.Contains("1. [RUN ] [#1]     -> RenderSummary"));
            Assert.Contains(output.Lines, line => line.StartsWith("│   │ LOGS ATTEMPT 1"));
            Assert.Contains("State: PASS", rendered);
            Assert.Contains("State: FAIL", rendered);
            Assert.Contains("Flow Trace", rendered);
            Assert.Contains("1. [RUN ] [#0]     -> FetchUsers", rendered);
            Assert.Contains("2. [RUN ] [#1]     -> RenderSummary", rendered);
            Assert.Contains("3. [PASS] [#0]     <- FetchUsers", rendered);
            Assert.Contains("4. [FAIL] [#1]     <- RenderSummary", rendered);
            Assert.Contains("Step [#0]  FetchUsers", rendered);
            Assert.Contains("Step [#1]  RenderSummary", rendered);
            Assert.Contains("Phase: Prepare", rendered);
            Assert.Contains("1 attempt | Layer: L0", rendered);
            Assert.Contains("LOGS ATTEMPT 1", rendered);
            Assert.Contains("FINAL RESULT", rendered);
            Assert.Contains("Variable count  [observed]  = 2", rendered);
            Assert.Contains("Assertions", rendered);
            Assert.Contains("[FAIL] summaryText  Be(\"ok\")", rendered);
            Assert.Contains("expected \"ok\", was \"nope\"", rendered);
        });
    }

    [Fact]
    public async Task Suppresses_Layer_Banners_For_Rapid_SingleStep_Phase_Changes()
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
                        Description = "Rapid phase changes",
                        Steps =
                        [
                            CreateStep("Prepare", phase: StepExecutionPhase.Prepare),
                            CreateStep("Act", phase: StepExecutionPhase.Act),
                            CreateStep("Observe", phase: StepExecutionPhase.Observe),
                            CreateStep("Materialize Left", phase: StepExecutionPhase.Materialize),
                            CreateStep("Materialize Right", phase: StepExecutionPhase.Materialize)
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 2, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 2, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 3, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 4, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 3, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 4, DebugLifecycleState.Complete, DebugLifecycleState.Running);

            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.DoesNotContain("> Prepare phase", rendered);
            Assert.DoesNotContain("> Act phase", rendered);
            Assert.DoesNotContain("> Observe phase", rendered);
            Assert.Contains("> L3  Materialize  x2", rendered);
        });
    }

    [Fact]
    public async Task Captures_ForEach_Input_Value_Per_Iteration()
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
                        Description = "ForEach flow",
                        Steps =
                        [
                            CreateSetVariableStep("Set item", "item", "Ada"),
                            CreateMessageStep("Message A", "item"),
                            CreateSetVariableStep("Set item", "item", "Grace"),
                            CreateMessageStep("Message B", "item")
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalValueUpdateAsync("session", "item", DebugValueKind.Variable, "Main", 0, CreateValueEnvelope("Ada"));
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 1, DebugLifecycleState.Complete, DebugLifecycleState.Running);

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 2, DebugLifecycleState.Running);
            await debugger.SignalValueUpdateAsync("session", "item", DebugValueKind.Variable, "Main", 2, CreateValueEnvelope("Grace"));
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 2, DebugLifecycleState.Complete, DebugLifecycleState.Running);

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 3, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 3, DebugLifecycleState.Complete, DebugLifecycleState.Running);

            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.Contains("Variable item  [required]  = Ada", rendered);
            Assert.Contains("Variable item  [required]  = Grace", rendered);
            Assert.DoesNotContain("Variable item  [required]  <not available>", rendered);
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
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
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
            Assert.Single(output.Lines.Where(line => line.Contains("Step [#0]  FlakyCall", StringComparison.Ordinal)));
            Assert.Contains("LOGS ATTEMPT 1", rendered);
            Assert.Contains("LOGS ATTEMPT 2", rendered);
            Assert.Contains("FINAL RESULT", rendered);
            Assert.Contains("State: PASS", rendered);
            Assert.Contains("Attempts: 2", rendered);
            Assert.DoesNotContain("EmptyStepResultContext", rendered);
        });
    }

    [Fact]
    public async Task Ignores_Breakpoint_Hit_Lines_In_Text_Output()
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
                        Description = "Breakpoint suppression",
                        Steps =
                        [
                            CreateStep("Set Variable", label: "set intro", phase: StepExecutionPhase.Prepare)
                        ]
                    }
                ],
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalAndWaitBreakpointHitAsync("session", "Main", 0);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.DoesNotContain("BREAKPOINT HIT", rendered);
            Assert.Contains("Step [#0]  Set Variable  [set intro]", rendered);
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
                Variables = new Dictionary<VariableIdentifier, DebugValue>(),
                Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>()
            });

            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Stage, "Main", null, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Running);
            await debugger.SignalEntityTransitionAsync("session", DebugEntityKind.Step, "Main", 0, DebugLifecycleState.Complete, DebugLifecycleState.Running);
            await debugger.SignalTimelineRunFinishedAsync("session");

            string rendered = string.Join(System.Environment.NewLine, output.Lines);
            Assert.Contains("+===", rendered);
            Assert.Contains("FetchUsers", rendered);
            Assert.DoesNotContain('│', rendered);
            Assert.DoesNotContain('╭', rendered);
            Assert.DoesNotContain('╰', rendered);
        });
    }

    private static DebugStepState CreateStep(string name, string? label = null, StepExecutionPhase phase = StepExecutionPhase.Act)
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
            Phase = phase,
            DoesReturn = false
        };
    }

    private static DebugStepState CreateMessageStep(string name, string inputVariable)
    {
        DebugStepState step = CreateStep(name);
        step.IOContract.Inputs.Add(new StepIOEntry(inputVariable, StepIOKind.Variable, true, typeof(string)));
        return step;
    }

    private static DebugStepState CreateSetVariableStep(string name, string outputVariable, string label)
    {
        DebugStepState step = CreateStep(name, label, StepExecutionPhase.Prepare);
        step.IOContract.Outputs.Add(new StepIOEntry(outputVariable, StepIOKind.Variable, true, typeof(string)));
        return step;
    }

    private static DebugValueEnvelope CreateValueEnvelope(string value)
    {
        return new DebugValueEnvelope
        {
            Kind = DebugValueKind.Variable,
            TypeName = typeof(string).FullName!,
            Description = new DebugValueDescription { Summary = value, Shape = DebugValueShape.Text },
            SchemaKey = "tf.variable:System.String"
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