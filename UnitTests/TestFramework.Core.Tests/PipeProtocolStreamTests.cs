using System;
using System.Collections.Generic;
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

    private static TimelineRunStructure CreateRunStructure()
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
                            IOContract = new StepIOContract(),
                            Phase = StepExecutionPhase.Act,
                            LabelOptions = new LabelOptions(),
                            RetryOptions = new RetryOptions(),
                            TimeOutOptions = new TimeOutOptions()
                        }
                    ]
                }
            ]
        };
    }
}