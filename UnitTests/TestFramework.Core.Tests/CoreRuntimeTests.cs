using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Tests;

public class CoreRuntimeTests
{
    [Fact]
    public void FreezableDictionary_FreezePreventsMutationAndFreezesNestedValues()
    {
        FreezableDictionary<string, TestFreezable> dictionary = new();
        TestFreezable nested = new();
        dictionary.Add("child", nested);

        dictionary.Freeze();

        Assert.True(dictionary.IsFrozen);
        Assert.True(dictionary.IsReadOnly);
        Assert.True(nested.IsFrozen);
        Assert.Throws<FrameworkStateException>(() => dictionary.Add("second", new TestFreezable()));
    }

    [Fact]
    public void FreezableCollection_CastPreservesItemsAndFrozenState()
    {
        FreezableCollection<string> source = new();
        source.Add("alpha");
        source.Add("beta");
        source.Freeze();

        IFreezableCollection<object> casted = source.Cast<object>();

        Assert.True(casted.IsFrozen);
        Assert.Equal(["alpha", "beta"], casted.Cast<object>());
    }

    [Fact]
    public void VariableStore_SetVariable_ReplacesAndRetrievesTypedValues()
    {
        RuntimeContext runtime = RuntimeContext.Create();
        VariableIdentifier identifier = new("user");

        runtime.VariableStore.SetVariable(identifier, "Ada");
        runtime.VariableStore.SetVariable(identifier, "Grace");

        Assert.Equal("Grace", runtime.VariableStore.GetVariable<string>(identifier));
        Assert.True(runtime.VariableStore.TryGetVariable<string>(identifier, out string? resolved));
        Assert.Equal("Grace", resolved);
    }

    [Fact]
    public void VariableStore_DebuggingState_UsesCommonEnvelope()
    {
        DebugValue state = VariableStore.GetDebuggingStateFromValue("Grace", new VariableIdentifier("user"));

        Assert.Equal("user", state.Key);
        Assert.Equal(DebugValueKind.Variable, state.Envelope.Kind);
        // The description is the whole account of the value now. It used to sit beside a one-line rendering
        // of it and a full JSON copy of it, and a consumer had to pick which of the three to believe.
        Assert.Contains("Grace", state.Envelope.Description.Summary, StringComparison.Ordinal);
        Assert.Equal("String", Assert.Single(state.Envelope.Description.Fields, field => field.Name == "type").Text);
    }

    [Fact]
    public void ArtifactStore_DebuggingState_UsesCommonEnvelopeAndCustomPayload()
    {
        DebugValue state = ArtifactStore.GetDebuggingStateFromInstance(new ArtifactInstance<TestArtifactDescriber, TestArtifactData, TestArtifactReference>(
            new TestArtifactDescriber(),
            new ArtifactIdentifier("artifact"),
            new TestArtifactReference(),
            new TestArtifactData()));

        Assert.Equal("artifact", state.Key);
        Assert.Equal(DebugValueKind.Artifact, state.Envelope.Kind);
        Assert.Equal("test-artifact-schema", state.Envelope.SchemaKey);
        Assert.Equal("debug", state.Envelope.Custom!["mode"]!.Value<string>());
    }

    [Fact]
    public void ArtifactStore_DebuggingState_StatesItsLifecycleRatherThanBuryingItInJson()
    {
        // A consumer needs the state and the history to draw an artifact at all, and reading them out
        // of an untyped payload means knowing the property names and coping with them being absent.
        DebugValue state = ArtifactStore.GetDebuggingStateFromInstance(new ArtifactInstance<TestArtifactDescriber, TestArtifactData, TestArtifactReference>(
            new TestArtifactDescriber(),
            new ArtifactIdentifier("artifact"),
            new TestArtifactReference(),
            new TestArtifactData()));

        Assert.NotNull(state.Envelope.Lifecycle);
        Assert.Equal(nameof(TestFramework.Core.Artifacts.ArtifactState.NotSetup), state.Envelope.Lifecycle!.State);
        Assert.Single(state.Envelope.Lifecycle.Versions);
        Assert.Equal(Assert.Single(state.Envelope.Lifecycle.Versions), state.Envelope.Lifecycle.CurrentVersion);
    }

    [Fact]
    public void VariableStore_DebuggingState_HasNoLifecycleBecauseAVariableHasNone()
    {
        // The absence is the statement: a variable is its current value and nothing else, so a
        // consumer has nothing to draw a history from and should not be invited to try.
        DebugValue state = VariableStore.GetDebuggingStateFromValue("Grace", new VariableIdentifier("user"));

        Assert.Null(state.Envelope.Lifecycle);
    }

    private sealed class RuntimeContext
    {
        public ScopedLogger Logger { get; } = new(null);
        public DebuggingRunSession DebuggingSession { get; } = new(new EmptyRunDebugger());
        public VariableStore VariableStore { get; }

        private RuntimeContext()
        {
            VariableStore = new VariableStore(Logger, DebuggingSession);
        }

        public static RuntimeContext Create() => new();
    }

    private sealed class TestFreezable : IFreezable
    {
        public bool IsFrozen { get; private set; }

        public void Freeze()
        {
            IsFrozen = true;
        }
    }

    private sealed class TestArtifactDescriber : ArtifactDescriber<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
    {
        public override string DebugValueSchemaKey => "test-artifact-schema";

        public override JObject? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance)
        {
            return new JObject { ["mode"] = "debug" };
        }

        public override Task Setup(IServiceProvider serviceProvider, TestArtifactData data, TestArtifactReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;

        public override Task Deconstruct(IServiceProvider serviceProvider, TestArtifactReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;

        public override string ToString() => "test-artifact";
    }

    private sealed class TestArtifactData : ArtifactData<TestArtifactData, TestArtifactDescriber, TestArtifactReference>
    {
        public override string ToString() => "artifact-data";
    }

    private sealed class TestArtifactReference : ArtifactReference<TestArtifactReference, TestArtifactDescriber, TestArtifactData>
    {
        public override Task<ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
        {
            return Task.FromResult(new ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
            {
                Found = true,
                Data = new TestArtifactData()
            });
        }

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger)
        {
        }

        public override string ToString() => "artifact-reference";
    }
}