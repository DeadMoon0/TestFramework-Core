using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Runner;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines;
using TestFramework.Core.Variables;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// <see cref="ArtifactStore.Assign{TReference}"/>: how a reference learns what its setup created - a key the
/// database assigned - through the same gate that pinned it.
/// </summary>
/// <remarks>
/// The damage the gate prevents is a leak that reads as a clean run: an attempt the run stopped waiting for
/// records its own artifact on the reference, teardown deletes that one, and the live attempt's artifact
/// stays behind.
/// </remarks>
public class ArtifactAssignmentTests(ITestOutputHelper output)
{
    [Fact]
    public async Task WhatSetupAssigns_IsWhatTeardownDeletes()
    {
        Recorder recorder = new();

        Timeline timeline = Timeline.Create()
            .SetupArtifact("row")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(writeLine: output.WriteLine)
            .AddArtifact("row", new AssignedReference(recorder), new AssignedData())
            .RunAsync();

        run.EnsureRanToCompletion();

        Assert.Equal("created-7", recorder.Deconstructed);
    }

    [Fact]
    public async Task AVariableTheReferenceWritesDuringSetup_CanBeReadByALaterStep()
    {
        // Setup's reference is only known to the run's store, so the setup step used to declare nothing of
        // its own: a key it publishes was "never produced" as far as the plan could tell, and a later step
        // reading it was refused before anything ran.
        Recorder recorder = new();

        Timeline timeline = Timeline.Create()
            .SetupArtifact("row")
            .AssertVariable(Var.Ref<string>("assignedKey"), key => key == "created-7")
            .Build();

        TimelineRun run = await timeline
            .SetupRun(writeLine: output.WriteLine)
            .AddArtifact("row", new AssignedReference(recorder) { PublishesKeyTo = "assignedKey" }, new AssignedData())
            .RunAsync();

        run.EnsureRanToCompletion();
    }

    [Fact]
    public async Task AVariableTheReferenceReadsDuringSetup_IsCheckedAtPlanTime()
    {
        // The other half of the same declaration: a key value from a variable nothing writes is a sentence
        // before the run starts, not a setup failing against the database.
        Recorder recorder = new();

        Timeline timeline = Timeline.Create()
            .SetupArtifact("row")
            .Build();

        await Assert.ThrowsAsync<IOContractViolationException>(() => timeline
            .SetupRun(writeLine: output.WriteLine)
            .AddArtifact("row", new AssignedReference(recorder) { ReadsKeyFrom = "nobodyWritesThis" }, new AssignedData())
            .RunAsync());
    }

    [Fact]
    public void AnAbandonedAttempt_CannotAssign()
    {
        ArtifactStore store = new(new ScopedLogger(null), new DebuggingRunSession(new EmptyRunDebugger()), new RunWriteGate());
        StepAttemptGate gate = new();

        AssignedReference reference = new(new Recorder());
        store.AddArtifact(new ArtifactInstance<AssignedDescriber, AssignedData, AssignedReference>(new AssignedDescriber(), "row", reference, new AssignedData()));

        StepAttempt abandoned = gate.Begin("setup", 1);
        ArtifactStore abandonedView = store.ForAttempt(gate, abandoned);
        gate.Begin("setup", 2);

        bool admitted = abandonedView.Assign(reference, (held, proof) => held.Record(proof, "created-by-the-abandoned-attempt"));

        Assert.False(admitted);
        Assert.Null(reference.AssignedKey);
    }

    [Fact]
    public void AReferenceThisRunDoesNotHold_IsRefused()
    {
        // Accepted, the assignment would be judged by this run's gates and aim another run's cleanup.
        RunContext ownerRun = RunContext.Detached();
        RunContext otherRun = RunContext.Detached();
        AssignedReference reference = new(new Recorder());
        ownerRun.Artifacts.Add<AssignedDescriber, AssignedData, AssignedReference>("row", reference, new AssignedData());

        FrameworkConfigurationException refusal = Assert.Throws<FrameworkConfigurationException>(
            () => otherRun.Artifacts.Assign(reference, (held, proof) => held.Record(proof, "created-7")));

        Assert.Contains("not the reference of an artifact this run's store holds", refusal.Message);
        Assert.Null(reference.AssignedKey);
    }

    [Fact]
    public void AProofKeptPastItsCallback_IsRefused()
    {
        RunContext context = RunContext.Detached();
        AssignedReference reference = new(new Recorder());
        context.Artifacts.Add<AssignedDescriber, AssignedData, AssignedReference>("row", reference, new AssignedData());

        ArtifactAssignment? kept = null;
        context.Artifacts.Assign(reference, (_, proof) => kept = proof);

        Assert.Throws<InvalidOperationException>(() => reference.Record(kept!, "created-later"));
        Assert.Null(reference.AssignedKey);
    }

    /// <summary>Outside the artifact types, because the run clones the reference.</summary>
    private sealed class Recorder
    {
        public string? Deconstructed { get; set; }
    }

    private sealed class AssignedDescriber : ArtifactDescriber<AssignedDescriber, AssignedData, AssignedReference>
    {
        public override Task Setup(RunContext context, AssignedData data, AssignedReference reference)
        {
            context.Artifacts.Assign(reference, (held, proof) => held.Record(proof, "created-7"));

            if (reference.PublishesKeyTo is { } into)
                context.Variables.SetVariable(into, reference.AssignedKey);

            return Task.CompletedTask;
        }

        public override Task Deconstruct(RunContext context, AssignedReference reference)
        {
            reference.Recorder.Deconstructed = reference.AssignedKey;
            return Task.CompletedTask;
        }

        public override string ToString() => "assigned-artifact";
    }

    private sealed class AssignedData : ArtifactData<AssignedData, AssignedDescriber, AssignedReference>
    {
        public override string ToString() => "assigned-data";
    }

    private sealed class AssignedReference : ArtifactReference<AssignedReference, AssignedDescriber, AssignedData>
    {
        public AssignedReference(Recorder recorder)
        {
            Recorder = recorder;
            CanDeconstruct = true;
        }

        public Recorder Recorder { get; }

        public string? AssignedKey { get; private set; }

        public string? PublishesKeyTo { get; init; }

        public string? ReadsKeyFrom { get; init; }

        public void Record(ArtifactAssignment proof, string key)
        {
            proof.EnsureOpen();
            AssignedKey = key;
        }

        public override Task<ArtifactResolveResult<AssignedDescriber, AssignedData, AssignedReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier)
            => Task.FromResult(new ArtifactResolveResult<AssignedDescriber, AssignedData, AssignedReference> { Found = false });

        public override void DeclareIO(StepIOContract contract)
        {
            if (ReadsKeyFrom is { } from)
                contract.Input(new VariableIdentifier(from), typeof(string));

            if (PublishesKeyTo is { } into)
                contract.Output(new VariableIdentifier(into), typeof(string));
        }

        public override void OnPinReference(RunContext context) { }

        public override string ToString() => $"assigned({AssignedKey ?? "unassigned"})";
    }
}
