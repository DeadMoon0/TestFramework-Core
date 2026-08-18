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
        DebugStepIo[] inputs = [new DebugStepIo { Key = "orderId", Kind = StepIOKind.Variable, DeclaredType = nameof(Int32) }];
        DebugStepIo[] outputs = [new DebugStepIo { Key = "receipt", Kind = StepIOKind.Artifact, Required = false, DeclaredType = nameof(String) }];

        PipeInitTimelineRunSignal signal = new()
        {
            SessionId = "session-1",
            Name = "Contract Run",
            ProjectPath = "project.csproj",
            RunStructure = CreateRunStructure(inputs, outputs)
        };

        PipeInitTimelineRunSignal restored = Assert.IsType<PipeInitTimelineRunSignal>(
            PipeSignalFactory.DeserializeSignal(JsonConvert.SerializeObject(signal)));

        DebugStepState restoredStep = restored.RunStructure.Stages[0].Steps[0];

        DebugStepIo input = Assert.Single(restoredStep.Inputs);
        Assert.Equal("orderId", input.Key);
        Assert.Equal(StepIOKind.Variable, input.Kind);
        Assert.True(input.Required);

        DebugStepIo output = Assert.Single(restoredStep.Outputs);
        Assert.Equal("receipt", output.Key);
        Assert.Equal(StepIOKind.Artifact, output.Kind);
        Assert.False(output.Required);

        // Type names, not CLR types. A consumer in another process cannot load the type and has no use for it
        // beyond the name, which is all that used to survive the round trip anyway.
        Assert.Equal(nameof(Int32), input.DeclaredType);
        Assert.Equal(nameof(String), output.DeclaredType);
    }

    [Fact]
    public void RunStructure_CarriesTheRetryCount_ThroughARoundTrip()
    {
        // This used to be a test of the opposite. The plan carried RetryOptions itself, whose MaxRetryCount is a
        // VariableReference<int> holding its value behind a method, so the number never serialized and the UI
        // could not say "retries: 3" — it received the wrapper. The plan now states the resolved policy.
        PipeInitTimelineRunSignal signal = new()
        {
            SessionId = "session-1",
            Name = "Retry Run",
            ProjectPath = "project.csproj",
            RunStructure = CreateRunStructure(maxRetries: 3)
        };

        PipeInitTimelineRunSignal restored = Assert.IsType<PipeInitTimelineRunSignal>(
            PipeSignalFactory.DeserializeSignal(JsonConvert.SerializeObject(signal, DebugJson.Settings)));

        Assert.Equal(3, restored.RunStructure.Stages[0].Steps[0].MaxRetries);
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

                    SchemaKey = "schema:string",

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
                    Template = "Hello {0} at attempt {1}",
                    Fields = [DebugLogField.Of("0", "protocol"), DebugLogField.Of("1", 1)],
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
                    Arguments = [DebugLogField.Of("expected", "Ada")],
                    Succeeded = true,
                    Actual = new DebugValueDescription { Summary = "\"Ada\"", Shape = DebugValueShape.Text }
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

    private static TimelineRunStructure CreateRunStructure(
        DebugStepIo[]? inputs = null,
        DebugStepIo[]? outputs = null,
        int? maxRetries = null)
    {
        return new TimelineRunStructure
        {
            Variables = new Dictionary<VariableIdentifier, DebugValue>
            {
                [new VariableIdentifier("input")] = new DebugValue
                {
                    Key = "input",
                    Envelope = new DebugValueEnvelope
                    {
                        Kind = DebugValueKind.Variable,
                        TypeName = "System.String",

                        SchemaKey = "schema:string",

                    }
                }
            },
            Artifacts = new Dictionary<ArtifactIdentifier, DebugValue>
            {
                [new ArtifactIdentifier("artifact")] = new DebugValue
                {
                    Key = "artifact",
                    Envelope = new DebugValueEnvelope
                    {
                        Kind = DebugValueKind.Artifact,
                        TypeName = "Artifact",

                        SchemaKey = "schema:artifact",

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
                            Phase = StepExecutionPhase.Act,
                            Parallelization = StepParallelizationMode.Parallelizable,
                            MaxRetries = maxRetries,
                            Inputs = inputs ?? [],
                            Outputs = outputs ?? []
                        }
                    ]
                }
            ]
        };
    }
}