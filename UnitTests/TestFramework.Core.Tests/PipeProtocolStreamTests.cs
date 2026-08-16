using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public sealed class PipeProtocolStreamTests
{
    [Fact]
    public Task PipeSignalFactory_RoundTrips_AllSupportedSignals()
    {
        foreach (IPipeSignal expected in CreateSignals())
        {
            string json = JsonConvert.SerializeObject(expected);
            IPipeSignal actual = PipeSignalFactory.DeserializeSignal(json);

            Assert.IsType(expected.GetType(), actual);
            Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void RunStructure_RoundTrips_NonEmptyIOContract_FieldByField()
    {
        // The other round-trip test compares two serializations, so any loss that is symmetric
        // passes unnoticed — and it only ever exercises empty contracts and default options. This
        // one asserts the values a consumer actually reads.
        StepIOContract contract = new();
        contract.Inputs.Add(new StepIOEntry("orderId", StepIOKind.Variable, true, typeof(int)));
        contract.Outputs.Add(new StepIOEntry("receipt", StepIOKind.Artifact, false, typeof(string)));

        PipeInitTimelineRunSignal signal = new()
        {
            SessionId = "session-1",
            Name = "Contract Run",
            ProjectPath = "project.csproj",
            RunStructure = CreateRunStructure(contract)
        };

        PipeInitTimelineRunSignal restored = Assert.IsType<PipeInitTimelineRunSignal>(
            PipeSignalFactory.DeserializeSignal(JsonConvert.SerializeObject(signal)));

        StepIOContract restoredContract = restored.RunStructure.Stages[0].Steps[0].IOContract;

        StepIOEntry input = Assert.Single(restoredContract.Inputs);
        Assert.Equal("orderId", input.Key);
        Assert.Equal(StepIOKind.Variable, input.Kind);
        Assert.True(input.Required);

        StepIOEntry output = Assert.Single(restoredContract.Outputs);
        Assert.Equal("receipt", output.Key);
        Assert.Equal(StepIOKind.Artifact, output.Kind);
        Assert.False(output.Required);

        Assert.Equal(typeof(int), input.DeclaredType);
        Assert.Equal(typeof(string), output.DeclaredType);
    }

    [Fact]
    public void RunStructure_LosesTheRetryCountValue_WhenItIsALiteral()
    {
        // DOCUMENTS A KNOWN LOSS. RetryOptions.MaxRetryCount is a VariableReference<int> whose
        // literal value lives in an internal field, so it does not serialize: the UI receives the
        // reference shell and cannot show "retries: 3". StepIOEntry.DeclaredType, by contrast, does
        // survive — Newtonsoft writes System.Type as its assembly-qualified name.
        //
        // Fixing this means projecting the options into explicit debug DTOs, which belongs with the
        // wider structure work rather than here. The test exists so the loss is visible and the fix
        // has something to flip.
        RetryOptions retryOptions = new() { MaxRetryCount = 3 };

        PipeInitTimelineRunSignal signal = new()
        {
            SessionId = "session-1",
            Name = "Retry Run",
            ProjectPath = "project.csproj",
            RunStructure = CreateRunStructure(retryOptions: retryOptions)
        };

        PipeInitTimelineRunSignal restored = Assert.IsType<PipeInitTimelineRunSignal>(
            PipeSignalFactory.DeserializeSignal(JsonConvert.SerializeObject(signal)));

        RetryOptions restoredOptions = restored.RunStructure.Stages[0].Steps[0].RetryOptions;

        Assert.Equal(3, retryOptions.MaxRetryCount.GetValue(null!));
        Assert.NotEqual(3, restoredOptions.MaxRetryCount.GetValue(null!));
    }

    [Fact]
    public Task PipeSignalFactory_RejectsUnsupportedSignalKind()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PipeSignalFactory.DeserializeSignal("{\"Kind\":999}"));

        Assert.Equal("signalKind", exception.ParamName);
        return Task.CompletedTask;
    }

    private static IReadOnlyList<IPipeSignal> CreateSignals()
    {
        return
        [
            new PipeInitTimelineRunSignal
            {
                SessionId = "session-1",
                Name = "Protocol Run",
                ProjectPath = "project.csproj",
                RunStructure = CreateRunStructure()
            },
            new PipeEntityTransitionSignal
            {
                SessionId = "session-1",
                EntityKind = DebugEntityKind.Step,
                Stage = "Main",
                StepId = 0,
                PreviousState = DebugLifecycleState.Initialized,
                OutcomeState = DebugLifecycleState.Complete,
                State = DebugLifecycleState.Running,
                OccurredAtUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z")
            },
            new PipeValueUpdateSignal
            {
                SessionId = "session-1",
                Name = "user",
                ValueKind = DebugValueKind.Variable,
                Stage = "Main",
                StepId = 0,
                Envelope = new DebugValueEnvelope
                {
                    Kind = DebugValueKind.Variable,
                    TypeName = "System.String",
                    DisplayText = "Ada",
                    SchemaKey = "schema:string",
                    Core = new JObject { ["value"] = "Ada" }
                },
                ObservedAtUtc = DateTimeOffset.Parse("2026-05-26T12:00:01Z")
            },
            new PipeLogEntrySignal
            {
                SessionId = "session-1",
                Entry = new DebugLogEntry
                {
                    OccurredAtUtc = DateTimeOffset.Parse("2026-05-26T12:00:02Z"),
                    Level = DebugLogLevel.Information,
                    EventName = "ProtocolLog",
                    Message = "Hello protocol",
                    Lines = ["Hello protocol"],
                    Stage = "Main",
                    StepId = 0,
                    Iteration = 1
                }
            },
            new PipeAssertionSignal
            {
                SessionId = "session-1",
                Entry = new DebugAssertionEntry
                {
                    OccurredAtUtc = DateTimeOffset.Parse("2026-05-26T12:00:03Z"),
                    TargetKind = DebugAssertionTargetKind.Variable,
                    Target = "user",
                    AssertionName = "Be",
                    AssertionDisplay = "Be(\"Ada\")",
                    Succeeded = true,
                    Expected = "Ada",
                    Actual = "Ada"
                }
            },
            new PipeBreakpointHitRequestSignal
            {
                SessionId = "session-1",
                Stage = "Main",
                StepId = 0
            },
            new PipeBreakpointHitContinueSignal(),
            new PipeTimelineRunFinishedSignal
            {
                SessionId = "session-1"
            }
        ];
    }

    private static TimelineRunStructure CreateRunStructure(StepIOContract? ioContract = null, RetryOptions? retryOptions = null)
    {
        return new TimelineRunStructure
        {
            Variables = new Dictionary<VariableIdentifier, VariableState>
            {
                [new VariableIdentifier("input")] = new VariableState
                {
                    Key = "input",
                    Envelope = new DebugValueEnvelope
                    {
                        Kind = DebugValueKind.Variable,
                        TypeName = "System.String",
                        DisplayText = "seed",
                        SchemaKey = "schema:string",
                        Core = new JObject { ["value"] = "seed" }
                    }
                }
            },
            Artifacts = new Dictionary<ArtifactIdentifier, TestFramework.Core.Debugger.ArtifactState>
            {
                [new ArtifactIdentifier("artifact")] = new TestFramework.Core.Debugger.ArtifactState
                {
                    Key = "artifact",
                    Envelope = new DebugValueEnvelope
                    {
                        Kind = DebugValueKind.Artifact,
                        TypeName = "Artifact",
                        DisplayText = "artifact.json",
                        SchemaKey = "schema:artifact",
                        Core = new JObject { ["reference"] = "artifact://artifact.json" }
                    }
                }
            },
            Stages =
            [
                new DebugStageState
                {
                    Name = "Main",
                    Description = "Main stage",
                    Steps =
                    [
                        new DebugStepState
                        {
                            Name = "Step 1",
                            Description = "protocol step",
                            DoesReturn = true,
                            ErrorHandlingOptions = new ErrorHandlingOptions(),
                            ExecutionOptions = new ExecutionOptions(),
                            IOContract = ioContract ?? new StepIOContract(),
                            Phase = StepExecutionPhase.Act,
                            LabelOptions = new LabelOptions(),
                            RetryOptions = retryOptions ?? new RetryOptions(),
                            TimeOutOptions = new TimeOutOptions()
                        }
                    ]
                }
            ]
        };
    }
}