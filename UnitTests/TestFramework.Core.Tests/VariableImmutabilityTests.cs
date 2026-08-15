using System.Collections.Generic;
using TestFramework.Core.Exceptions;
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
