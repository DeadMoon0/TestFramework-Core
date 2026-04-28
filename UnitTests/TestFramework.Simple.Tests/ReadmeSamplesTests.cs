using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.Simple;

namespace TestFramework.Simple.Tests;

// README sync note: these tests mirror the public README samples for TestFramework.Simple.
// If you update a test here, update the corresponding README sample as well.
public class ReadmeSamplesTests
{
    [Fact]
    public async Task QuickStart_InlineAction_CompletesAndUpdatesState()
    {
        string? message = null;
        const string expectedMessage = "Action executed";

        Timeline timeline = Timeline.Create()
            .Trigger(Simple.Trigger.Action(() => message = expectedMessage))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public async Task VariableAwareAction_ReceivesResolvedVariables()
    {
        string? greeting = null;

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Alex"))
            .Trigger(Simple.Trigger.Action(vars =>
            {
                greeting = $"Hello {vars[new VariableIdentifier("name")]}";
            }, Var.Ref<string>("name")))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal("Hello Alex", greeting);
    }

    [Fact]
    public async Task ArtifactAwareAction_ReceivesVariablesAndArtifacts()
    {
        ArtifactIdentifier payloadArtifact = new("payload");
        string? captured = null;

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Alex"))
            .Trigger(Simple.Trigger.Action((vars, artifacts) =>
            {
                string? name = (string?)vars[new VariableIdentifier("name")];
                ArtifactInstanceGeneric payload = artifacts[payloadArtifact];
                captured = $"{name}:{payload.Identifier}";
            }, [Var.Ref<string>("name")], payloadArtifact))
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("payload", new ReadmeArtifactReference("payload-ref"), new ReadmeArtifactData("payload-data"))
            .RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal("Alex:payload", captured);
    }

    [Fact]
    public async Task FullContextAction_ReceivesServiceProviderLoggerVariablesAndArtifacts()
    {
        string? captured = null;

        Timeline timeline = Timeline.Create()
            .SetVariable("name", Var.Const("Alex"))
            .Trigger(Simple.Trigger.Action((serviceProvider, logger, vars, artifacts) =>
            {
                captured = $"{serviceProvider is not null}:{logger is not null}:{vars.Count}:{artifacts.Count}";
            }, [Var.Ref<string>("name")]))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal("True:True:1:0", captured);
    }

    private sealed class ReadmeArtifactReference : ArtifactReference<ReadmeArtifactReference, ReadmeArtifactDescriber, ReadmeArtifactData>
    {
        private readonly string _value;

        public ReadmeArtifactReference(string value)
        {
            _value = value;
        }

        public override Task<ArtifactResolveResult<ReadmeArtifactDescriber, ReadmeArtifactData, ReadmeArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<ReadmeArtifactDescriber, ReadmeArtifactData, ReadmeArtifactReference>
            {
                Found = true,
                Data = new ReadmeArtifactData(_value) { Identifier = versionIdentifier }
            });

        public override void DeclareIO(TestFramework.Core.Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => _value;
    }

    private sealed class ReadmeArtifactData : ArtifactData<ReadmeArtifactData, ReadmeArtifactDescriber, ReadmeArtifactReference>
    {
        public ReadmeArtifactData(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override string ToString() => Value;
    }

    private sealed class ReadmeArtifactDescriber : ArtifactDescriber<ReadmeArtifactDescriber, ReadmeArtifactData, ReadmeArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, ReadmeArtifactData data, ReadmeArtifactReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, ReadmeArtifactReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;

        public override string ToString() => "ReadmeArtifact";
    }
}