using System;
using System.Linq;
using System.Reflection;
using TestFramework.Core.Conventions;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
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

        // A caller who could freeze a running run's values would stop its environment from publishing where
        // it had just started something, and the run would then fail against a coordinate nobody supplied.
        // Worth pinning because the obvious way to write this - implementing the public IFreezable - hands
        // that out by construction.
        AssertNotPublic<ResourceValueStore>("Freeze");
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(ResourceValueStore)));

        // And the store itself is Core's to hand out, not anyone's to make.
        Assert.Empty(typeof(ResourceValueStore).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NobodyCanFreezeTheRunsVariablesMidRun()
    {
        // Damage: every later write in the run throws. The run freezes its own variables when it ends.
        // Both routes are pinned: the method, and the interface - an explicit IFreezable implementation
        // passes the first check while ((IFreezable)store).Freeze() still compiles in any package, which
        // is how this guard held in name only for a while.
        Assert.Null(typeof(VariableStore).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(VariableStore)));
    }

    [Fact]
    public void NobodyCanFreezeTheRunsArtifactsMidRun()
    {
        // Damage: the same, one store over - every later capture, registration or cleanup throws. Freezing
        // a single artifact is worse than freezing the store, because it fails only the test that touches
        // that one artifact and looks like a problem with the artifact. The interface route is pinned for
        // every part of an artifact, because freezing a reference before its setup pins it fails that step
        // for reasons the step cannot explain.
        Assert.Null(typeof(ArtifactStore).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactInstanceGeneric).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(ArtifactStore)));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(ArtifactInstanceGeneric)));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(ArtifactReferenceGeneric)));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(ArtifactDescriberGeneric)));

        // The run's component record is settled the same way, by the run and nobody else.
        Assert.Null(typeof(Core.Environment.EnvComponentContext).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.False(typeof(IFreezable).IsAssignableFrom(typeof(Core.Environment.EnvComponentContext)));
    }

    [Fact]
    public void AnArtifactChangesOnlyThroughTheStore()
    {
        // Damage: an artifact mutated on the instance skips the check that the writing attempt is still
        // the one that counts, so an abandoned step could still version an artifact or mark it cleaned -
        // and the second one makes the run skip a cleanup it owes somebody's database.
        Assert.Null(typeof(ArtifactInstanceGeneric).GetMethod("AddVersionGeneric", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactInstanceGeneric).GetProperty("State")!.GetSetMethod());

        // Pinning is a write too: it resolves the reference's variables and keeps the answer, so a stale
        // attempt pinning would retarget the artifact and its cleanup.
        Assert.Null(typeof(ArtifactReferenceGeneric).GetMethod("PinReference", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactReferenceGeneric).GetMethod("Pin", BindingFlags.Public | BindingFlags.Instance));

        // And freezing: a reference frozen before its setup step pins it fails that step for reasons the
        // step cannot explain.
        Assert.Null(typeof(ArtifactReferenceGeneric).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(ArtifactDescriberGeneric).GetMethod("Freeze", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void NothingOutsideCoreCanMintPermissionToWriteToAnArtifact()
    {
        // The mutators demand an ArtifactWriteTicket and only the store can produce one, so this is what
        // makes "every artifact write was checked first" a compiler guarantee rather than a convention.
        // Damage if it were forgeable: the whole quarantine, since a forged ticket writes unchecked.
        Assert.Empty(typeof(ArtifactWriteTicket).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        // private protected: constructible only by a type inside this assembly that derives from it, so a
        // package cannot make one and cannot subclass its way to one either.
        ConstructorInfo[] hidden = typeof(ArtifactWriteTicket).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.All(hidden, constructor => Assert.True(
            constructor.IsFamilyAndAssembly,
            $"the ticket's constructor is {Visibility(constructor)}, which lets code outside Core make one"));
    }

    private static string Visibility(ConstructorInfo constructor)
        => constructor switch
        {
            { IsPublic: true } => "public",
            { IsFamilyOrAssembly: true } => "protected internal",
            { IsFamily: true } => "protected",
            { IsAssembly: true } => "internal",
            _ => "private"
        };

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
    public void NothingInCoreStillAsksForTheRunsPiecesOneByOne()
    {
        // Damage: two shapes. Every hook that takes the stores loose is a hook with no deadline in it and
        // no attempt behind it - so it cannot know its budget and its writes are not quarantined, which is
        // exactly what handing it one context fixed. The pair below is the fingerprint of the old shape:
        // a VariableStore and a ScopedLogger side by side in a parameter list.
        string[] allowed =
        [
            // Builds a context out of the pieces, which is the one place that has to name them.
            $"{nameof(RunContext)}.{nameof(RunContext.Ambient)}",

            // Plan time, not run time. An emitter decides which steps exist by reading what the run was
            // seeded with; there is no attempt to belong to and no deadline to read, so handing one a
            // RunContext would be handing it a fiction. If these grow more parameters, the answer is a
            // context of their own rather than a longer list - see STRUCTURAL-DEBT.md entry 3.
            "StepEmitter.Emit",
            "SingleStepEmitter.Emit",
            "ConditionalStepEmitter.Emit",
            "ForEachStepEmitter`1.Emit",
        ];

        string[] offenders = [.. typeof(RunContext).Assembly
            .GetExportedTypes()
            .SelectMany(static type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => (Type: type, Method: method)))
            .Where(static entry => TakesTheLoosePieces(entry.Method))
            .Select(static entry => $"{entry.Type.Name}.{entry.Method.Name}")
            .Where(name => !allowed.Contains(name))
            .Distinct()
            .OrderBy(static name => name, StringComparer.Ordinal)];

        Assert.Empty(offenders);
    }

    private static bool TakesTheLoosePieces(MethodInfo method)
    {
        Type[] parameters = [.. method.GetParameters().Select(static parameter => parameter.ParameterType)];

        return parameters.Contains(typeof(VariableStore)) && parameters.Contains(typeof(ScopedLogger));
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

    [Fact]
    public void CoreKeepsItsInternalsToItself()
    {
        // The engine is not exempt from the rule it ships. No piece is special to Core and Core is special
        // to no piece: a grant here would be the engine picking a favourite, which is the one place it would
        // do the most damage - every other package would then be writing against a smaller surface than the
        // favoured one, without knowing it.
        // Throws when it finds one; the report is what it looked at.
        Assert.Equal(1, StepConventions.AssertNoPackageSeesAnothersInternals(typeof(ResourceGraph).Assembly).Checked);
    }

    private static void AssertNotPublic<T>(string method)
        => Assert.Null(typeof(T).GetMethod(method, BindingFlags.Public | BindingFlags.Instance));
}
