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
    public void AVariableThatArrivesAsAPlainValue_IsRefused()
    {
        // The compiler catches a reference passed directly; this is one hidden behind a caller's own generic helper.
        static ITimelineBuilder StoreThrough<T>(ITimelineBuilder builder, T value) => builder.SetVariable("copy", value);

        FrameworkConfigurationException refused = Assert.Throws<FrameworkConfigurationException>(
            () => StoreThrough(Timeline.Create(), Var.Ref<int>("total")));

        Assert.Contains("SetVariableFrom", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PassingAShippedReferenceTypeAsAValue_DoesNotCompile()
    {
        // Each reference type Core ships has a SetVariable overload the compiler refuses, which overload resolution
        // prefers over the value verb for that type - so a Var.Ref, a RefImmutable or a transformed reference passed
        // as a value is a compile error naming SetVariableFrom instead of a variable stored as data.
        Type[] guarded = [.. typeof(ISetVariableAction).GetMethods()
            .Where(method => method.Name == nameof(ISetVariableAction.SetVariable))
            .Where(method => method.GetCustomAttribute<ObsoleteAttribute>() is { IsError: true })
            .Select(method => method.GetParameters()[1].ParameterType.GetGenericTypeDefinition())];

        Assert.Equal(
            new[] { typeof(VariableReference<>), typeof(ResolvableVariable<>), typeof(ImmutableVariable<,>) }.OrderBy(type => type.Name),
            guarded.OrderBy(type => type.Name));
    }
}
