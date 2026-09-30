using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Tests;

/// <summary>
/// Pins that an assertion about a variable's value fails when the variable was never set.
/// </summary>
/// <remarks>
/// The damage it prevents is a false pass. A missing variable used to reach every value assertion as
/// the type's default, so <c>Be(0)</c>, <c>Be(false)</c> and <c>BeNull()</c> all passed for data that
/// did not exist - a test went green for a step that never wrote anything.
/// </remarks>
public sealed class VariableAssertionOnMissingVariableTests
{
    public static IEnumerable<object[]> ValueAssertions()
    {
        yield return [(Action<TimelineRun>)(run => run.Variable<int>("missing").Should().Be(0)), "Be"];
        yield return [(Action<TimelineRun>)(run => run.Variable<bool>("missing").Should().Be(false)), "Be"];
        yield return [(Action<TimelineRun>)(run => run.Variable<string>("missing").Should().BeNull()), "BeNull"];
        yield return [(Action<TimelineRun>)(run => run.Variable<string>("missing").Should().NotBe("x")), "NotBe"];
        yield return [(Action<TimelineRun>)(run => run.Variable<string>("missing").Should().Match(_ => true)), "Match"];
        yield return [(Action<TimelineRun>)(run => run.Variable<string>("missing").Should().NotContain("x")), "NotContain"];
        yield return [(Action<TimelineRun>)(run => run.Variable<string>("missing").Should().BeEmpty()), "BeEmpty"];
        yield return [(Action<TimelineRun>)(run => run.Variable<int>("missing").Should().BeLessThan(1)), "BeLessThan"];
        yield return [(Action<TimelineRun>)(run => run.Variable<int>("missing").Should().NotBeInRange(5, 9)), "NotBeInRange"];
    }

    [Theory]
    [MemberData(nameof(ValueAssertions))]
    public async Task AValueAssertionOnAVariableThatWasNeverSetFails(Action<TimelineRun> assertion, string name)
    {
        TimelineRun run = await EmptyRun();

        ValueAssertionException failure = Assert.Throws<ValueAssertionException>(() => assertion(run));

        Assert.Contains(name, failure.Message);
        Assert.Contains("was not set", failure.Message);
    }

    [Fact]
    public async Task NotExistStillPassesForAVariableThatWasNeverSet()
    {
        TimelineRun run = await EmptyRun();

        run.Variable<int>("missing").Should().NotExist();
    }

    [Fact]
    public async Task AValueAssertionOnASetVariableStillCompares()
    {
        // The other side of the fix: a variable that does exist is still compared as before, default
        // value included.
        TimelineRun run = await Timeline.Create()
            .SetVariable("count", Var.Const(0))
            .Build()
            .SetupRun()
            .RunAsync();

        run.Variable<int>("count").Should().Be(0);
        Assert.Throws<ValueAssertionException>(() => run.Variable<int>("count").Should().Be(1));
    }

    [Fact]
    public async Task SelectingFromAVariableThatWasNeverSetIsRefused()
    {
        TimelineRun run = await EmptyRun();

        MissingVariableException refusal = Assert.Throws<MissingVariableException>(
            () => run.Variable<string>("missing").Select(value => value.Length));

        Assert.Contains("missing", refusal.FriendlyMessage);
    }

    private static Task<TimelineRun> EmptyRun()
        => Timeline.Create()
            .SetVariable("present", Var.Const("here"))
            .Build()
            .SetupRun()
            .RunAsync();
}
