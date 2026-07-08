using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using TestFramework.Core.Logging;
using TestFramework.Core.Debugger;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Core.Tests;

public class NamingTests
{
    [Fact]
    public async Task FindArtifacts_GeneratedNames_StartWith_0()
    {
        Timeline timeline = Timeline.Create()
            .FindArtifacts("file", new DummyFinder(2))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        run.EnsureRanToCompletion();

        string[] identifiers = run.ArtifactStore.GetAll().Select(x => x.Identifier.Identifier).ToArray();

        Assert.Contains("file_0", identifiers);
        Assert.Contains("file_1", identifiers);
    }

    [Fact]
    public async Task FindArtifactsAs_Throws_When_Count_Mismatch()
    {
        Timeline timeline = Timeline.Create()
            .FindArtifactsAs(new TestFramework.Core.Artifacts.ArtifactIdentifier[] { "file0" }, new DummyFinder(2))
            .Build();

        TimelineRun run = await timeline.SetupRun().RunAsync();

        TimelineRunFailedException exception = Assert.Throws<TimelineRunFailedException>(() => run.EnsureRanToCompletion());

        Assert.Contains(exception.FailedSteps, step =>
            step.StepException is ArtifactCountMismatchException mismatchException &&
            mismatchException.Message.Contains("expected 1 artifact name", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindArtifactsAs_Throws_When_Identifier_List_Is_Empty()
    {
        ArtifactIdentifierRequiredException exception = Assert.Throws<ArtifactIdentifierRequiredException>(() => Timeline.Create()
            .FindArtifactsAs(System.Array.Empty<TestFramework.Core.Artifacts.ArtifactIdentifier>(), new DummyFinder(1)));

        Assert.Contains("FindArtifactsAs", exception.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FindArtifact(name, ...)", exception.ToString(), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Name_Throws_When_Label_Is_Blank()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Timeline.Create()
            .SetVariable("user", Var.Const("Ada"))
            .Name(" "));

        Assert.Contains("label", exception.ParamName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DummyFinder : ArtifactFinder<DummyDescriber, DummyData, DummyReference>
    {
        private readonly int _count;

        public DummyFinder(int count) { _count = count; }

        public override Task<ArtifactFinderResult?> FindAsync(System.IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            // single not used in these tests
            return Task.FromResult<ArtifactFinderResult?>(null);
        }

        public override Task<ArtifactFinderResultMulti> FindMultiAsync(System.IServiceProvider serviceProvider, VariableStore variableStore, ScopedLogger logger, CancellationToken cancellationToken)
        {
            var results = new ArtifactFinderResult[_count];
            for (int i = 0; i < _count; i++)
            {
                results[i] = new ArtifactFinderResult(new DummyReference($"ref{i}", new DummyData()));
            }
            return Task.FromResult(new ArtifactFinderResultMulti(results));
        }
    }

    private sealed class DummyDescriber : ArtifactDescriber<DummyDescriber, DummyData, DummyReference>
    {
        public override System.Threading.Tasks.Task Deconstruct(IServiceProvider serviceProvider, DummyReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;
        public override System.Threading.Tasks.Task Setup(IServiceProvider serviceProvider, DummyData data, DummyReference reference, VariableStore variableStore, ScopedLogger logger) => Task.CompletedTask;
        public override string ToString() => "dummy";
    }

    private sealed class DummyData : ArtifactData<DummyData, DummyDescriber, DummyReference>
    {
        public override string ToString() => "dummy-data";
    }

    private sealed class DummyReference : ArtifactReference<DummyReference, DummyDescriber, DummyData>
    {
        private readonly string _name;
        private readonly DummyData _data;

        public DummyReference(string name, DummyData data)
        {
            _name = name;
            _data = data;
        }

        public override Task<ArtifactResolveResult<DummyDescriber, DummyData, DummyReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger)
        {
            return Task.FromResult(new ArtifactResolveResult<DummyDescriber, DummyData, DummyReference> { Found = true, Data = _data });
        }

        public override void DeclareIO(StepIOContract contract) { }

        public override void OnPinReference(VariableStore variableStore, ScopedLogger logger) { }

        public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => new DummyDescriber();

        public override string ToString() => _name;
    }
}
