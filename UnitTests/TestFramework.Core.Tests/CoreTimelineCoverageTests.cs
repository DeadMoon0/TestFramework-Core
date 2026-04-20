using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

public class CoreTimelineCoverageTests
{
    [Fact]
    public async Task TimelineRun_SetVariableAndAssertVariable_PersistsPublicRunState()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("user", Var.Const("Ada"))
            .Name("set-user")
            .AssertVariable(Var.Ref<string>("user"), value => value == "Ada")
            .Name("assert-user")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();
        Assert.Equal(Steps.StepState.Complete, run.Step("set-user").State);
        Assert.Equal(Steps.StepState.Complete, run.Step("assert-user").State);
        run.Variable<string>("user").Should().Exist().And().Be("Ada");
    }

    [Fact]
    public async Task TimelineRun_EnsureRanToCompletion_ThrowsWhenAssertionFails()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("user", Var.Const("Ada"))
            .Name("set-user")
            .AssertVariable(Var.Ref<string>("user"), value => value == "Grace")
            .Name("assert-user")
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());
        Assert.Contains(exception.FailedSteps, step => step.StepException is AssertVariableException);
    }

    [Fact]
    public async Task TimelineRun_AddArtifact_ExposesArtifactThroughPublicHandle()
    {
        Timeline timeline = Timeline.Create().Build();

        TimelineRun run = await timeline.SetupRun()
            .AddArtifact("artifact", new TestArtifactReference("artifact-ref"), new TestArtifactData("payload"))
            .RunAsync();

        run.EnsureRanToCompletion();
        run.Artifact<TestArtifactData>("artifact").Select(data => data.Value).Should().Be("payload");
    }

    [Fact]
    public void EmptyServiceProvider_GetService_ReturnsNull()
    {
        EmptyServiceProvider provider = new();

        object? service = provider.GetService(typeof(string));

        Assert.Null(service);
    }

    [Fact]
    public void FreezableCollectionsAndDictionaries_SupportCommonMutableOperationsBeforeFreeze()
    {
        FreezableCollection<string> collection = new();
        collection.Add("alpha");
        collection.Add("beta");

        Assert.Contains("alpha", collection);
        string[] copied = new string[2];
        collection.CopyTo(copied, 0);
        Assert.Equal(new[] { "alpha", "beta" }, copied);
        Assert.True(collection.Remove("beta"));
        Assert.Single(collection);

        FreezableDictionary<string, int> dictionary = new();
        dictionary.Add("one", 1);
        dictionary["two"] = 2;

        Assert.True(dictionary.ContainsKey("one"));
        Assert.Contains(new KeyValuePair<string, int>("one", 1), dictionary);
        KeyValuePair<string, int>[] target = new KeyValuePair<string, int>[2];
        dictionary.CopyTo(target, 0);
        Assert.Equal(2, target.Length);
        Assert.True(dictionary.Remove("two"));
        Assert.True(dictionary.Remove(new KeyValuePair<string, int>("one", 1)));
        Assert.Empty(dictionary);
    }

    private sealed class TestArtifactReference : ArtifactReference<TestArtifactReference, TestArtifactDescriber, TestArtifactData>
    {
        private readonly string _value;

        public TestArtifactReference(string value)
        {
            _value = value;
        }

        public override Task<ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, Logging.ScopedLogger logger)
            => Task.FromResult(new ArtifactResolveResult<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
            {
                Found = true,
                Data = new TestArtifactData(_value) { Identifier = versionIdentifier }
            });

        public override void DeclareIO(Steps.Options.StepIOContract contract)
        {
        }

        public override void OnPinReference(VariableStore variableStore, Logging.ScopedLogger logger)
        {
        }

        public override string ToString() => _value;
    }

    private sealed class TestArtifactData : ArtifactData<TestArtifactData, TestArtifactDescriber, TestArtifactReference>
    {
        public TestArtifactData(string value)
        {
            Value = value;
        }

        public string Value { get; }
        public override string ToString() => Value;
    }

    private sealed class TestArtifactDescriber : ArtifactDescriber<TestArtifactDescriber, TestArtifactData, TestArtifactReference>
    {
        public override Task Setup(IServiceProvider serviceProvider, TestArtifactData data, TestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger) => Task.CompletedTask;
        public override Task Deconstruct(IServiceProvider serviceProvider, TestArtifactReference reference, VariableStore variableStore, Logging.ScopedLogger logger) => Task.CompletedTask;
        public override string ToString() => "TestArtifact";
    }
}