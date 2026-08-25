using System;
using System.Linq;
using System.Reflection;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Steps;
using TestFramework.Core.Variables;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// What code outside this assembly is allowed to do to a running timeline.
/// </summary>
/// <remarks>
/// <para>
/// The framework has to assume its caller will reach for whatever is reachable, so anything that would
/// break or silently corrupt a run must not be reachable at all. The compiler is the enforcement where it
/// can be; these tests are what keep it that way, because widening a member is a one-word change that no
/// reviewer reliably notices.
/// </para>
/// <para>
/// Each case names the damage, so a future reader can weigh a request to open it up against what it would
/// cost.
/// </para>
/// </remarks>
public class PublicSurfaceTests
{
    [Fact]
    public void NobodyOutsideTheRunnerCanStartOrEndAStepAttempt()
    {
        // Damage: Begin abandons whatever attempt is current, so any caller could silently void every
        // write the running step goes on to make - the hardest kind of bug to trace back.
        AssertNotPublic<StepAttemptGate>("Begin");
        AssertNotPublic<StepAttemptGate>("End");
    }

    [Fact]
    public void NobodyOutsideCoreCanChangeTheRunsResourceValues()
    {
        // Damage: a redirected value points a test at a different system while still passing; a withdrawn
        // one strands a step mid-flight. Values may only arrive from a node, checked against its kind.
        AssertNotPublic<ResourceValueStore>("Declare");
        AssertNotPublic<ResourceValueStore>("Produce");
        AssertNotPublic<ResourceValueStore>("WithdrawProduced");

        // And the store itself is Core's to hand out, not anyone's to make.
        Assert.Empty(typeof(ResourceValueStore).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NobodyCanFreezeTheRunsVariablesMidRun()
    {
        // Damage: every later write in the run throws. The run freezes its own variables when it ends.
        Assert.Null(typeof(VariableStore).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NobodyCanFreezeTheRunsArtifactsMidRun()
    {
        // Damage: the same, one store over - every later capture, registration or cleanup throws. Freezing
        // a single artifact is worse than freezing the store, because it fails only the test that touches
        // that one artifact and looks like a problem with the artifact.
        Assert.Null(typeof(ArtifactStore).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactInstanceGeneric).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void AnArtifactChangesOnlyThroughTheStore()
    {
        // Damage: an artifact mutated on the instance skips the check that the writing attempt is still
        // the one that counts, so an abandoned step could still version an artifact or mark it cleaned -
        // and the second one makes the run skip a cleanup it owes somebody's database.
        Assert.Null(typeof(ArtifactInstanceGeneric).GetMethod("AddVersionGeneric", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactInstanceGeneric).GetProperty("State")!.GetSetMethod());
    }

    [Fact]
    public void AStepCannotSubstituteItsOwnWriterIdentity()
    {
        // Damage: an abandoned attempt could keep writing by claiming to be a live one, which is the whole
        // guarantee the attempt gate exists for.
        Assert.Null(typeof(VariableStore).GetMethod("ForAttempt", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactStore).GetMethod("ForAttempt", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(StepAttempt).GetMethod("Abandon", BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(StepAttempt).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NobodyCanForgeARunContextOrADeadline()
    {
        // Damage: a forged context is one whose deadline and attempt do not match the run - a step that
        // believes it has time it does not have, or a writer nothing is checking.
        Assert.Empty(typeof(RunContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(StepDeadline).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void AKindCannotBeBuiltAroundTheRegistry()
    {
        // Damage: two meanings for one kind name, which is exactly what interning prevents. The only way
        // to a kind is Named(...), which interns.
        Assert.Empty(typeof(ResourceKind).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(ResourceKindBuilder).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void WhatIsReachableIsReadableRatherThanWritable()
    {
        // The positive half: reading the run's state is meant to be easy, and stays public.
        Assert.NotNull(typeof(ResourceValueStore).GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(ValueResolution).GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Instance));
        Assert.NotNull(typeof(ResourceGraph).GetMethod("Validate", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void EveryPublicTypeInTheGraphNamespaceIsSealedOrMeantToBeDerivedFrom()
    {
        // An unsealed type nobody meant to be a base class is an invitation to override half a contract.
        // The exceptions are the two that exist to be derived from, plus the interfaces.
        string[] designedForDerivation =
        [
            nameof(ResourceNode),
            nameof(ProvisionedResourceNode),
            nameof(ConfigDocument),
        ];

        Assert.Empty(typeof(ResourceGraph).Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == typeof(ResourceGraph).Namespace)
            .Where(static type => type is { IsClass: true, IsAbstract: false, IsSealed: false })
            .Where(type => !designedForDerivation.Contains(type.Name))
            .Select(static type => type.Name));
    }

    [Fact]
    public void CoreSerialisesWithNewtonsoftAndNothingElse()
    {
        // The family picked one JSON library. Two of them means two sets of attributes, two notions of
        // what null means, and values that survive one round trip but not the other - so the rule is
        // checked against the compiled assembly rather than trusted to review.
        Assert.DoesNotContain(
            "System.Text.Json",
            typeof(ResourceGraph).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));

        Assert.Contains(
            "Newtonsoft.Json",
            typeof(ResourceGraph).Assembly.GetReferencedAssemblies().Select(static reference => reference.Name));
    }

    private static void AssertNotPublic<T>(string method)
        => Assert.Null(typeof(T).GetMethod(method, BindingFlags.Public | BindingFlags.Instance));
}
