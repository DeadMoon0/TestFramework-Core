using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// A failed assertion is one of the family's failures, reads as a verdict rather than a framework error, and
/// says which line of the test made it.
/// </summary>
public class AssertionFailureTests
{
    private static Task<TimelineRun> RunWithCount(int count) => Timeline.Create()
        .SetVariable("count", Var.Const(count))
        .Build()
        .SetupRun()
        .RunAsync();

    private static int Line([CallerLineNumber] int line = 0) => line;

    [Fact]
    public async Task AFailedAssertion_IsAFrameworkFailure_ThatReadsAsAnAssertion()
    {
        TimelineRun run = await RunWithCount(6);

        ValueAssertionException failure = Assert.Throws<ValueAssertionException>(() => run.Variable<int>("count").Should().Be(7));

        Assert.IsAssignableFrom<TimelineFrameworkException>(failure);
        Assert.StartsWith("[ASSERTION FAILED] ValueAssertionException", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedAssertion_NamesTheLineThatMadeIt()
    {
        TimelineRun run = await RunWithCount(6);

        int line = 0;
        ValueAssertionException failure = Assert.Throws<ValueAssertionException>(() =>
        {
            line = Line(); run.Variable<int>("count").Should().Be(7);
        });

        Assert.Equal($"AssertionFailureTests.cs:{line}", failure.Site);
    }

    [Fact]
    public async Task EachFailureCollectedByAScope_NamesItsOwnLine()
    {
        TimelineRun run = await RunWithCount(6);
        int first = 0, second = 0;

        MultipleAssertionsFailedException failure = Assert.Throws<MultipleAssertionsFailedException>(() =>
        {
            using AssertionScope scope = run.AssertionScope();
            first = Line(); run.Variable<int>("count").Should().Be(7);
            second = Line(); run.Variable<int>("count").Should().Be(8);
        });

        Assert.Contains($"at AssertionFailureTests.cs:{first}", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"at AssertionFailureTests.cs:{second}", failure.Message, StringComparison.Ordinal);
        Assert.StartsWith("[ASSERTION FAILED]", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunThatDidNotComplete_ReadsAsAFailedRun()
    {
        TimelineRun run = await Timeline.Create()
            .AssertVariable(Var.Const(1), value => value == 2)
            .Build()
            .SetupRun()
            .RunAsync();

        TimelineRunFailedException failure = Assert.Throws<TimelineRunFailedException>(run.EnsureRanToCompletion);

        Assert.StartsWith("[RUN FAILED]", failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("[ASSERTION FAILED] AssertVariableException", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryExceptionCoreThrows_IsAFrameworkException()
    {
        string[] strays = [.. typeof(Timeline).Assembly.GetExportedTypes()
            .Where(type => typeof(Exception).IsAssignableFrom(type))
            .Where(type => !typeof(TimelineFrameworkException).IsAssignableFrom(type))
            .Select(type => type.FullName!)];

        Assert.Empty(strays);
    }
}
