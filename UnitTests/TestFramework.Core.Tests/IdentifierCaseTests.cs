using System;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// Every name in a run is case-sensitive: step labels, variables and artifacts alike.
/// </summary>
/// <remarks>
/// One rule, so a name that differs only by case is a different name everywhere. Before, a label was looked up
/// ignoring case while the variable store did not, and the plan check ignored case while the store it promised
/// to protect did not - so a timeline that wrote "Count" and read "count" was approved and then failed mid-run.
/// </remarks>
public class IdentifierCaseTests
{
    [Fact]
    public async Task AStepLabel_IsFoundOnlyByItsExactSpelling()
    {
        TimelineRun run = await Timeline.Create()
            .SetVariable("x", Var.Const(1)).Label("Count")
            .Build()
            .SetupRun()
            .RunAsync();

        StepLabelNotFoundException refused = Assert.Throws<StepLabelNotFoundException>(() => run.Step("count"));

        Assert.Contains("Count", refused.AvailableOptions);
        Assert.NotNull(run.Step("Count"));
    }

    [Fact]
    public async Task AVariableReadWithADifferentCase_IsRefusedBeforeTheRunStarts()
    {
        Timeline timeline = Timeline.Create()
            .SetVariable("Count", Var.Const(1))
            .AssertVariable(Var.Ref<int>("count"), value => value == 1)
            .Build();

        IOContractViolationException refused = await Assert.ThrowsAsync<IOContractViolationException>(() => timeline.SetupRun().RunAsync());

        Assert.Contains("'count'", refused.Message, StringComparison.Ordinal);
    }
}
