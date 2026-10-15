using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// Text, order and counting are asked only of the types they mean something for.
/// </summary>
public class ValueAssertionsTests
{
    [Fact]
    public void EveryAsserter_OffersOnlyWhatHoldsForEveryType()
    {
        // The type-specific assertions are constrained extensions; on the asserter itself they would accept any T
        // again, and asking a list whether it contains "3" would check its type name.
        string[] typeSpecific = ["Contain", "NotContain", "StartWith", "EndWith", "BeEmpty", "NotBeEmpty", "BeGreaterThan",
            "BeGreaterThanOrEqualTo", "BeLessThan", "BeLessThanOrEqualTo", "BeInRange", "NotBeInRange", "HaveCount", "HaveNoItems", "HaveItems"];

        foreach (Type asserter in new[] { typeof(ValueAsserter<>), typeof(VariableAsserter<>) })
            Assert.Empty(asserter.GetMethods().Select(method => method.Name).Intersect(typeSpecific));
    }

    [Fact]
    public async Task CountingAMissingCollection_FailsSayingSo()
    {
        TimelineRun run = await Timeline.Create()
            .SetVariable("items", Var.Const<List<int>?>(null))
            .Build()
            .SetupRun()
            .RunAsync();

        ValueAssertionException failure = Assert.Throws<ValueAssertionException>(() => run.Variable<List<int>?>("items").Should().HaveCount(2));

        Assert.Contains("was null", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextAndOrder_StillWorkWhereTheyMeanSomething()
    {
        TimelineRun run = await Timeline.Create()
            .SetVariable("name", Var.Const("order-42"))
            .SetVariable("total", Var.Const(42))
            .Build()
            .SetupRun()
            .RunAsync();

        run.Variable<string>("name").Should().StartWith("order-").And().Contain("42");
        run.Variable<int>("total").Should().BeInRange(40, 50).And().BeGreaterThan(41);
    }
}
