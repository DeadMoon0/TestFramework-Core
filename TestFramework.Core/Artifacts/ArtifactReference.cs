using System;
using System.Threading.Tasks;
using TestFramework.Core;
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
    public abstract Task<ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Resolves the reference through the untyped base contract.
    /// </summary>
    public override async Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger) => await ResolveToDataAsync(serviceProvider, versionIdentifier, variableStore, logger);

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
public abstract class ArtifactReferenceGeneric : IFreezable, IArtifactGettableGeneric
{
    /// <summary>
    /// Gets a value indicating whether the reference has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the artifact reference.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    /// <summary>
    /// Gets a value indicating whether the reference has already been pinned.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>
    /// Pins the reference against the current variable store.
    /// </summary>
    public void PinReference(VariableStore variableStore, ScopedLogger logger)
    {
        if (IsPinned) return;
        IsPinned = true;
        OnPinReference(variableStore, logger);
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
    public abstract Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Declares the IO contract implied by the reference.
    /// </summary>
    public abstract void DeclareIO(StepIOContract contract);

    /// <summary>
    /// Performs the pinning behavior for the reference.
    /// </summary>
    public abstract void OnPinReference(VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Returns a human-readable description of the reference.
    /// </summary>
    public abstract override string ToString();
}