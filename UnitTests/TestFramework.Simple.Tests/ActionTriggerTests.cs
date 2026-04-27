using TestFramework.Core;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Simple;

namespace TestFramework.Simple.Tests;

public class ActionTriggerTests
{
    [Fact]
    public async Task Execute_PassesResolvedVariablesToAction()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        VariableIdentifier identifier = new("user");
        runtime.VariableStore.SetVariable(identifier, "Ada");

        Dictionary<VariableIdentifier, object?>? captured = null;
        ActionTrigger trigger = Simple.Trigger.Action(vars => captured = vars, Var.Ref<string>(identifier));

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Ada", captured![identifier]);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForParameterlessAction()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        int callCount = 0;

        ActionTrigger trigger = Simple.Trigger.Action(() => callCount++);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForArtifactAwareAction()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        bool executed = false;

        ActionTrigger trigger = Simple.Trigger.Action(
            (vars, artifacts) =>
            {
                executed = true;
                Assert.Empty(vars);
                Assert.Empty(artifacts);
            },
            [],
            []);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(executed);
    }

    [Fact]
    public async Task ActionFactory_CreatesExecutableTriggerForFullContextAction()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        bool executed = false;

        ActionTrigger trigger = Simple.Trigger.Action(
            (serviceProvider, logger, vars, artifacts) =>
            {
                executed = true;
                Assert.Same(runtime.ServiceProvider, serviceProvider);
                Assert.Same(runtime.Logger, logger);
                Assert.Empty(vars);
                Assert.Empty(artifacts);
            },
            [],
            []);

        await trigger.Execute(runtime.ServiceProvider, runtime.VariableStore, runtime.ArtifactStore, runtime.Logger, CancellationToken.None);

        Assert.True(executed);
    }

    [Fact]
    public void ActionFactory_ThrowsImmediatelyForNullAction()
    {
        Assert.Throws<ArgumentNullException>(() => Simple.Trigger.Action((Action)null!));
    }

    [Fact]
    public void DeclareIO_AddsVariableIdentifiers()
    {
        ActionTrigger trigger = Simple.Trigger.Action(_ => { }, Var.Ref<string>("input"), Var.Ref<int>("count"));
        StepIOContract contract = new();

        trigger.DeclareIO(contract);

        Assert.Collection(
            contract.Inputs,
            entry =>
            {
                Assert.Equal("input", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
            },
            entry =>
            {
                Assert.Equal("count", entry.Key);
                Assert.Equal(StepIOKind.Variable, entry.Kind);
                Assert.True(entry.Required);
            });
    }

    private sealed class RuntimeContext
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(EmptyRunDebugger.CreateNew());
        public VariableStore VariableStore { get; }
        public ArtifactStore ArtifactStore { get; }

        private RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
            ArtifactStore = new ArtifactStore(Logger, DebuggingSession);
        }

        public static RuntimeContext Create() => new();
    }
}