using TestFramework.Core.Steps;
using System;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Represents the typed outcome of resolving an artifact reference to concrete data.
/// </summary>
public record ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactResolveResultGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    /// <summary>
    /// Gets or sets the typed resolved artifact data.
    /// </summary>
    public new TArtifactData? Data { get => (TArtifactData?)base.Data; set => base.Data = (TArtifactData?)value; }
}

/// <summary>
/// Represents the untyped outcome of resolving an artifact reference.
/// </summary>
public record ArtifactResolveResultGeneric
{
    /// <summary>
    /// Gets or sets a value indicating whether the artifact was found.
    /// </summary>
    public required bool Found { get; set; }

    /// <summary>
    /// Gets or sets the resolved artifact data.
    /// </summary>
    public ArtifactDataGeneric? Data { get; set; }
}

/// <summary>
/// Represents a typed artifact reference that can resolve artifact data at run time.
/// </summary>
public abstract class ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> : ArtifactReferenceGeneric, IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
{
    /// <summary>
    /// Resolves the reference to typed artifact data.
    /// </summary>
    public abstract Task<ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference>> ResolveToDataAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier);

    /// <summary>
    /// Resolves the reference through the untyped base contract.
    /// </summary>
    public override async Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier) => await ResolveToDataAsync(context, versionIdentifier);

    /// <summary>
    /// Returns the typed artifact describer associated with the reference.
    /// </summary>
    public virtual TArtifactDescriber GetArtifactDescriber()
    {
        return new TArtifactDescriber();
    }

    /// <summary>
    /// Returns the artifact describer through the untyped base contract.
    /// </summary>
    public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => GetArtifactDescriber();
}

/// <summary>
/// Represents the non-generic base contract for artifact references.
/// </summary>
public abstract class ArtifactReferenceGeneric : IArtifactGettableGeneric
{
    /// <summary>
    /// Gets a value indicating whether the reference has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the artifact reference against further pinning, when its run ends.
    /// </summary>
    /// <remarks>
    /// Internal, and the reference deliberately does not implement the public <see cref="IFreezable"/>:
    /// that interface's <c>Freeze</c> is reachable through a cast from any package, and freezing a
    /// reference before its setup step pins it would fail that step for reasons the step cannot explain.
    /// The run settles its artifacts when it ends, and nothing else may.
    /// </remarks>
    internal void FreezeForRunEnd() { IsFrozen = true; }

    /// <summary>
    /// Gets a value indicating whether the reference has already been pinned.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>
    /// Pins the reference against the current variable store.
    /// </summary>
    /// <remarks>
    /// Asked of the store - <c>ArtifactStore.PinReference</c> - rather than called on the reference.
    /// Pinning resolves this reference's variables and keeps the answer, so a pin from an attempt the run
    /// has stopped waiting for would aim the artifact, and the cleanup that deletes it, at whatever that
    /// attempt's stale values pointed to. The ticket is what makes the store the only route.
    /// </remarks>
    /// <param name="context">What the pinning code is given, including the variables to resolve against.</param>
    /// <param name="ticket">Proof the store allowed this write.</param>
    internal void Pin(RunContext context, ArtifactWriteTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (IsFrozen) throw new FrameworkStateException("This instance has been frozen and is read-only.");

        if (IsPinned) return;
        IsPinned = true;
        OnPinReference(context);
    }

    /// <summary>
    /// Creates a run-scoped copy of the reference so that repeated runs of the same built timeline
    /// never share pinned state.
    /// </summary>
    /// <remarks>
    /// The default implementation shallow-copies the reference and resets the pinned and frozen flags.
    /// Override when a reference owns mutable state that must not be shared between runs.
    /// </remarks>
    public virtual ArtifactReferenceGeneric CloneForRun()
    {
        ArtifactReferenceGeneric clone = (ArtifactReferenceGeneric)MemberwiseClone();
        clone.IsPinned = false;
        clone.IsFrozen = false;
        return clone;
    }

    /// <summary>
    /// Gets a value indicating whether the reference supports deconstruction.
    /// </summary>
    public bool CanDeconstruct { get; protected set; }

    /// <summary>
    /// Returns the artifact describer through the non-generic base contract.
    /// </summary>
    public abstract ArtifactDescriberGeneric GetArtifactDescriberGeneric();

    /// <summary>
    /// Resolves the reference to artifact data through the non-generic base contract.
    /// </summary>
    public abstract Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(RunContext context, ArtifactVersionIdentifier versionIdentifier);

    /// <summary>
    /// Declares the IO contract implied by the reference.
    /// </summary>
    public abstract void DeclareIO(StepIOContract contract);

    /// <summary>
    /// Performs the pinning behavior for the reference.
    /// </summary>
    public abstract void OnPinReference(RunContext context);

    /// <summary>
    /// Returns a human-readable description of the reference.
    /// </summary>
    public abstract override string ToString();
}