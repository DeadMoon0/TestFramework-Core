using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// A value is stored as itself; another variable is copied with its own verb; and a variable is never stored as a value.
/// </summary>
public class SetVariableTests
{
    [Fact]
    public async Task APlainValue_IsStored()
    {
        TimelineRun run = await Timeline.Create().SetVariable("total", 42).Build().SetupRun().RunAsync();

        Assert.Equal(42, run.VariableStore.GetVariable<int>("total"));
    }

    [Fact]
    public async Task AnotherVariable_IsCopiedWhenTheStepRuns()
    {
        TimelineRun run = await Timeline.Create()
            .SetVariable("total", 42)
            .SetVariableFrom("copy", Var.Ref<int>("total"))
            .Build()
            .SetupRun()
            .RunAsync();

        Assert.Equal(42, run.VariableStore.GetVariable<int>("copy"));
    }

    [Fact]
    public async Task AConstant_IsAValue()
    {
        TimelineRun run = await Timeline.Create().SetVariable("total", Var.Const(42)).Build().SetupRun().RunAsync();

        Assert.Equal(42, run.VariableStore.GetVariable<int>("total"));
    }

    [Fact]
    public void AVariablePassedAsAValue_IsRefusedWhenTheTimelineIsBuilt()
    {
        FrameworkConfigurationException refused = Assert.Throws<FrameworkConfigurationException>(
            () => Timeline.Create().SetVariable("copy", Var.Ref<int>("total")));

        Assert.Contains("SetVariableFrom", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATransformedOrImmutableVariablePassedAsAValue_IsRefusedToo()
    {
        Assert.Throws<FrameworkConfigurationException>(() => Timeline.Create().SetVariable("a", Var.RefImmutable<int>("total")));
        Assert.Throws<FrameworkConfigurationException>(() => Timeline.Create().SetVariable("b", Var.Ref<int>("total").Transform(value => value + 1)));
    }
}
