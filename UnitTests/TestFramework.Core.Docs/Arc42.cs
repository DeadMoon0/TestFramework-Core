#region snippet:arc42-first-test
using TestFramework.Core.Timelines;
using TestFramework.Core.Timelines.Assertions;
using TestFramework.Core.Variables;
using TestFramework.Core.Steps.Options;
using Xunit;
using Xunit.Abstractions;
#endregion
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Docs.Arc42;

#region snippet:arc42-first-test
public class MyFirstTest(ITestOutputHelper output)
{
    [Fact]
    public async Task Simple_Timeline_Test()
    {
        // 1. BUILD - define the test plan
        Timeline timeline = Timeline.Create()
            .SetVariable("greeting", "Hello World")
            .Trigger(new MyCustomStep())
                .WithTimeout(TimeSpan.FromSeconds(30))
                .WithRetry(2, CalcDelays.Exponential)
            .Build();

        // 2. RUN - configure inputs and execute; the run's log goes wherever this method writes
        TimelineRun run = await timeline
            .SetupRun(output.WriteLine)
            .AddVariable("extraParam", 42)
            .RunAsync();

        // 3. ASSERT - evaluate the frozen result
        run.EnsureRanToCompletion();
        run.Variable<string>("greeting").Should().Exist().And().Be("Hello World");
    }
}
#endregion

#region snippet:arc42-custom-step
public class MyCustomStep : Step<EmptyStepResultContext>
{
    public override string Name => "My Custom Step";

    public override string Description => "Reads the greeting and writes what it made of it.";

    public override bool DoesReturn => false;

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(new VariableIdentifier("greeting"), typeof(string));
        contract.Output(new VariableIdentifier("result"), typeof(string));
    }

    public override Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        string? greeting = context.Variables.GetVariable<string>("greeting");
        context.Logger.LogInformation($"Greeting: {greeting}");
        context.Variables.SetVariable("result", $"Processed: {greeting}");
        return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    public override Step<EmptyStepResultContext> Clone() => new MyCustomStep().WithClonedOptions(this);

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
}
#endregion

public sealed class DeclaresIO : Step<EmptyStepResultContext>
{
    public override string Name => "Declares IO";
    public override string Description => "Declares IO";
    public override bool DoesReturn => false;

    #region snippet:arc42-declare-io
    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(new VariableIdentifier("orderId"), typeof(string));
        contract.Output(new VariableIdentifier("result"), typeof(int));
    }
    #endregion

    public override Task<EmptyStepResultContext?> Execute(RunContext context) => Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    public override Step<EmptyStepResultContext> Clone() => new DeclaresIO().WithClonedOptions(this);
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);
}

#region snippet:arc42-crtp
public sealed class OrderData : ArtifactData<OrderData, OrderDescriber, OrderReference>
#endregion
{
    public override string ToString() => nameof(OrderData);
}

#region snippet:arc42-crtp
public sealed class OrderDescriber : ArtifactDescriber<OrderDescriber, OrderData, OrderReference>
#endregion
{
    public override Task Setup(RunContext context, OrderData data, OrderReference reference) => Task.CompletedTask;
    public override Task Deconstruct(RunContext context, OrderReference reference) => Task.CompletedTask;
    public override string ToString() => nameof(OrderDescriber);
}

#region snippet:arc42-crtp
public sealed class OrderReference : ArtifactReference<OrderReference, OrderDescriber, OrderData>
#endregion
{
    public override Task<ArtifactResolveResult<OrderDescriber, OrderData, OrderReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
        => Task.FromResult(new ArtifactResolveResult<OrderDescriber, OrderData, OrderReference> { Found = true, Data = new OrderData() });

    public override void DeclareIO(StepIOContract contract)
    {
    }

    public override void OnPinReference(RunContext context)
    {
    }

    public override string ToString() => nameof(OrderReference);
}

internal static class Arc42Usage
{
    public static void Assertions(TimelineRun run)
    {
        #region snippet:arc42-assertions
        run.Step("upload").Should().HaveCompleted();
        run.Variable<int>("count").Should().Exist().And().BeGreaterThan(0);
        run.Artifact<OrderData>("order").Should().Exist();
        #endregion
    }

    public static async Task Artifacts(IServiceProvider serviceProvider, Action<string> writeLine, OrderReference myRef, OrderData myData)
    {
        #region snippet:arc42-artifacts
        Timeline timeline = Timeline.Create()
            .SetupArtifact("order")                // set up before the steps that need it
            .Trigger(new MyCustomStep())
            .CaptureArtifactVersion("order")       // snapshot its state after processing
            .Build();

        TimelineRun run = await timeline
            .SetupRun(serviceProvider, writeLine)
            .AddArtifact("order", myRef, myData)
            .RunAsync();

        run.EnsureRanToCompletion();
        run.Artifact<OrderData>("order").Should().Exist();
        // Torn down in the cleanup stage; MarkReadonly() on the finder keeps a found artifact.
        #endregion
    }

    public static Timeline Retries()
    {
        #region snippet:arc42-retry
        return Timeline.Create()
            .Trigger(new MyCustomStep()).WithRetry(3, CalcDelays.Exponential)                       // 2 s, 4 s, 8 s
            .Trigger(new MyCustomStep()).WithRetry(3, CalcDelays.Fixed(TimeSpan.FromSeconds(5)))    // 5 s, 5 s, 5 s
            .Trigger(new MyCustomStep()).WithRetry(3, CalcDelays.Linear(TimeSpan.FromSeconds(3)))   // 3 s, 6 s, 9 s
            .Trigger(new MyCustomStep()).WithRetry(3, CalcDelays.None)                              // no delay
            .Trigger(new MyCustomStep()).WithRetry(3, i => TimeSpan.FromSeconds(i * 10))            // custom; i starts at 1
            .Build();
        #endregion
    }

    public static async Task Configuration(Timeline timeline)
    {
        #region snippet:arc42-config
        ConfigInstance config = ConfigInstance
            .FromJsonFile("local.testSettings.json")
            .AddService(services =>
            {
                services.AddSingleton<IOrderClient, OrderClient>();
            })
            .Build();

        // The run builds its services from the configuration and disposes them when it ends.
        TimelineRun run = await timeline
            .SetupRun(config)
            .RunAsync();
        #endregion

        run.EnsureRanToCompletion();
    }

    public static void Scope(TimelineRun run)
    {
        #region snippet:arc42-assertion-scope
        // All assertions run; a single exception lists every failure
        using (var scope = run.AssertionScope())
        {
            run.Step("step1").Should().HaveCompleted();
            run.Step("step2").Should().HaveCompleted();
            run.Variable<int>("count").Should().BeGreaterThan(0);
        } // throws MultipleAssertionsFailedException if any failed
        #endregion
    }

    private interface IOrderClient { }

    private sealed class OrderClient : IOrderClient { }
}
