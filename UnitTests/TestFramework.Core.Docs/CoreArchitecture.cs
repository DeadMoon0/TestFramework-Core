using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Docs.CoreArchitecture;

internal static class BuilderShapes
{
    public static Timeline Modifiers(MyStep myStep)
    {
        #region snippet:arch-modifiers
        return Timeline.Create()
            .Trigger(myStep)                                   // an action
                .WithRetry(3, i => TimeSpan.FromSeconds(i))    // a modifier on that action
                .WithTimeout(TimeSpan.FromMinutes(5))          // another modifier
            .SetVariable("key", Var.Const("value"))            // the next action
            .Build();
        #endregion
    }

    public static Timeline ControlFlow(MyStep someStep, MyStep processStep)
    {
        #region snippet:arch-control-flow
        return Timeline.Create()
            .Conditional(Var.RefImmutable<bool>("flag"), thenBranch =>
            {
                thenBranch.Trigger(someStep);
                thenBranch.SetVariable("x", Var.Const(42));
            })
            .ForEach(Var.RefImmutable<string[]>("items"), "item", loop =>
            {
                loop.Trigger(processStep);
            })
            .Build();
        #endregion
    }

    public static void Conversions()
    {
        #region snippet:arch-conversions
        // string -> VariableIdentifier (implicit)
        VariableIdentifier id = "myVar";

        // T -> VariableReference<T> (implicit, a constant)
        VariableReference<int> five = 5;
        #endregion

        _ = (id, five);
    }

    public static void Transforms()
    {
        #region snippet:arch-transforms
        // Single transform - appends to the chain, returns a new reference
        VariableReference<string> doubled = Var.Ref<int>("count")
            .Transform(x => x * 2)
            .Transform(x => x.ToString());

        // Multi-variable transform - depends on other variables
        VariableReference<string> combined = Var.Ref<string>("greeting")
            .Transform((greeting, vars) => greeting + " " + vars[0], Var.Ref<string>("name"));
        #endregion

        _ = (doubled, combined);
    }

    public static async Task DependencyInjection(Timeline timeline, IServiceCollection myServiceCollection)
    {
        #region snippet:arch-services
        // The caller keeps a provider it passes; RunServices.Owned hands one to the run, which disposes it.
        TimelineRun run = await timeline
            .SetupRun(RunServices.Owned(myServiceCollection.BuildServiceProvider()))
            .AddVariable("key", "value")
            .RunAsync();
        #endregion

        run.EnsureRanToCompletion();
    }
}

#region snippet:arch-custom-step
public sealed record MyResult(string Summary) : StepResultContext;

public class MyStep : Step<MyResult>
{
    public override string Name => "My Custom Step";
    public override string Description => "Does something useful";
    public override bool DoesReturn => true;

    public override Task<MyResult?> Execute(RunContext context)
    {
        context.Logger.LogInformation("Executing my step...");

        // Everything the step is given is on the context: the run's stores, how long it has
        // (context.Deadline), and where the run's resources ended up (context.Values).
        return Task.FromResult<MyResult?>(new MyResult("done"));
    }

    public override void DeclareIO(StepIOContract contract)
    {
        // Declare what the step reads and writes, e.g. contract.Input(someVariableReference).
    }

    public override StepInstance<Step<MyResult>, MyResult> GetInstance()
        => new StepInstance<Step<MyResult>, MyResult>(this);

    public override Step<MyResult> Clone()
        => new MyStep().WithClonedOptions(this);
}
#endregion

#region snippet:arch-artifact-triple
// 1. Describer - how to create and destroy one
public sealed class BlobDescriber : ArtifactDescriber<BlobDescriber, BlobData, BlobReference>
{
    public override Task Setup(RunContext context, BlobData data, BlobReference reference)
    {
        // Upload data.Bytes to the pinned container and blob name.
        return Task.CompletedTask;
    }

    public override Task Deconstruct(RunContext context, BlobReference reference)
    {
        // Delete the blob.
        return Task.CompletedTask;
    }

    public override string ToString() => "Blob";
}

// 2. Data - the payload
public sealed class BlobData : ArtifactData<BlobData, BlobDescriber, BlobReference>
{
    public byte[] Bytes { get; init; } = [];

    public override string ToString() => $"{Bytes.Length} bytes";
}

// 3. Reference - which one, and how to read it back
public sealed class BlobReference(VariableReference<string> containerUrl, VariableReference<string> blobName)
    : ArtifactReference<BlobReference, BlobDescriber, BlobData>
{
    public override Task<ArtifactResolveResult<BlobDescriber, BlobData, BlobReference>> ResolveToDataAsync(
        RunContext context, ArtifactVersionIdentifier versionIdentifier)
    {
        // Download the blob, or report it absent.
        return Task.FromResult(new ArtifactResolveResult<BlobDescriber, BlobData, BlobReference>
        {
            Found = true,
            Data = new BlobData(),
        });
    }

    public override void OnPinReference(RunContext context)
    {
        // Resolve variable-referenced fields to the values this run uses.
    }

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(containerUrl);
        contract.Input(blobName);
    }

    public override string ToString() => "Blob";
}
#endregion

#region snippet:arch-custom-event
public sealed record QueueMessage(string Body) : StepResultContext;

public class QueueMessageEvent : SequentialEvent<QueueMessageEvent, QueueMessage>
{
    public override string Name => "Wait for Queue Message";
    public override string Description => "Polls a queue until a message arrives";
    public override bool DoesReturn => true;

    public override async Task<SequentialPollingResult<QueueMessage>> OnSequentialPolling(RunContext context)
    {
        string? message = await PollQueueAsync(context);

        return message is not null
            ? SequentialPollingResult<QueueMessage>.Done(new QueueMessage(message))
            : SequentialPollingResult<QueueMessage>.NotYet(TimeSpan.FromSeconds(2));
    }

    // ... Clone(), GetInstance(), DeclareIO() as for any step ...
    #endregion

    public override Step<QueueMessage> Clone() => new QueueMessageEvent().WithClonedOptions(this);

    public override StepInstance<Step<QueueMessage>, QueueMessage> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }

    private static Task<string?> PollQueueAsync(RunContext context) => Task.FromResult<string?>(null);
}

internal static class MinimalExample
{
    public static async Task<string?> Run(Action<string> writeLine)
    {
        #region snippet:arch-minimal-example
        // ── 1. Build a Timeline ──────────────────────────────────────
        Timeline timeline = Timeline.Create()

            // Trigger a step and bind part of its result into a named variable.
            .Trigger(new GreetStep(Var.Ref<string>("userName")))
                .WithTimeout(TimeSpan.FromSeconds(30))
                .BindResultProperty(x => x.Greeting, "greeting")

            // Transform the projected variable
            .Transform<string, string>(Var.Ref<string>("greeting"), "shout",
                greeting => greeting!.ToUpperInvariant())

            // Assert the result
            .AssertVariable(Var.Ref<string>("shout"),
                s => s != null && s.StartsWith("HELLO", StringComparison.Ordinal))

            // Conditional logic
            .Conditional(Var.RefImmutable<bool>("verbose"), thenBranch =>
            {
                thenBranch.Trigger(new GreetStep(Var.Ref<string>("userName").Transform(n => n + " (verbose)")));
            })

            // ForEach loop
            .ForEach(Var.RefImmutable<string[]>("extraNames"), "item", loop =>
            {
                loop.Trigger(new GreetStep(Var.Ref<string>("item")));
            })

            .Build();

        // ── 2. Run the Timeline ──────────────────────────────────────
        TimelineRun run = await timeline
            .SetupRun(writeLine)                    // output.WriteLine in xunit
            .AddVariable("userName", "Alice")
            .AddVariable("verbose", true)
            .AddVariable("extraNames", new[] { "Bob", "Charlie" })
            .RunAsync();

        // ── 3. Verify ────────────────────────────────────────────────
        run.EnsureRanToCompletion();

        // Access variables from the run
        string? shout = run.VariableStore.GetVariable<string>("shout");
        // shout == "HELLO, ALICE!"
        #endregion

        return shout;
    }
}

#region snippet:arch-minimal-example
public sealed record GreetStepResultContext(string Greeting) : StepResultContext;

public class GreetStep(VariableReference<string> name) : Step<GreetStepResultContext>
{
    public override string Name => "Greet";
    public override string Description => "Generates a greeting";
    public override bool DoesReturn => true;

    public override Task<GreetStepResultContext?> Execute(RunContext context)
    {
        string greeting = $"Hello, {name.GetValue(context.Variables)}!";
        context.Logger.LogInformation(greeting);
        return Task.FromResult<GreetStepResultContext?>(new GreetStepResultContext(greeting));
    }

    public override void DeclareIO(StepIOContract contract) => contract.Input(name);

    public override StepInstance<Step<GreetStepResultContext>, GreetStepResultContext> GetInstance() => new(this);

    public override Step<GreetStepResultContext> Clone() => new GreetStep(name).WithClonedOptions(this);
}
#endregion
