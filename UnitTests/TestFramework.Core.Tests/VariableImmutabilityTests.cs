using System.Collections.Generic;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Preprocessor;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Immutability used to be enforced only by a validation method nothing called any more, so
/// <c>RefImmutable</c> silently meant nothing. These cover the end-to-end path.
/// </summary>
public class VariableImmutabilityTests
{
    [Fact]
    public async Task Run_Fails_WhenAVariableReadImmutablyIsWrittenByALaterStep()
    {
        // The first loop binds "items" immutably; the second one wants to write it.
        Timeline timeline = Timeline.Create()
            .ForEach(Var.RefImmutable<IEnumerable<int>>("items"), "item", _ => { })
            .ForEach(new[] { 1, 2 }, "items", _ => { })
            .Build();

        CannotSetImmutableVariableException exception = await Assert.ThrowsAsync<CannotSetImmutableVariableException>(
            () => timeline.SetupRun().AddVariable<IEnumerable<int>>("items", [1, 2]).RunAsync());

        Assert.Contains("items", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForEach_RejectsModifiers_AndNamesItselfInTheMessage()
    {
        ForEachStepEmitter<int> emitter = new(Var.Const<IEnumerable<int>>([1, 2]), "item", _ => { });

        UnsupportedFrameworkValueException exception = Assert.Throws<UnsupportedFrameworkValueException>(
            () => emitter.Emit(
                CreateArtifactStore(),
                CreateVariableStore(),
                new VariableTracker(),
                new ArtifactTracker(),
                [static (_, _, _) => { }]).ToArray());

        Assert.Contains("ForEach", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ConditionalStepEmitter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForEach_ReportsWhichCollectionVariableResolvedToNull()
    {
        ForEachStepEmitter<int> emitter = new(Var.Ref<IEnumerable<int>>("items"), "item", _ => { });
        VariableStore store = CreateVariableStore();
        store.SetVariable<IEnumerable<int>?>("items", null);

        FrameworkStateException exception = Assert.Throws<FrameworkStateException>(
            () => emitter.Emit(CreateArtifactStore(), store, new VariableTracker(), new ArtifactTracker(), []).ToArray());

        Assert.Contains("items", exception.Message, StringComparison.Ordinal);
    }

    private static VariableStore CreateVariableStore()
        => new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));

    private static ArtifactStore CreateArtifactStore()
        => new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()));

    [Fact]
    public async Task Run_Succeeds_WhenTheImmutablyReadVariableIsNeverWritten()
    {
        Timeline timeline = Timeline.Create()
            .ForEach(Var.RefImmutable<IEnumerable<int>>("items"), "item", _ => { })
            .Build();

        TimelineRun run = await timeline.SetupRun()
            .AddVariable<IEnumerable<int>>("items", [1, 2])
            .RunAsync();

        run.EnsureRanToCompletion();
    }
}
