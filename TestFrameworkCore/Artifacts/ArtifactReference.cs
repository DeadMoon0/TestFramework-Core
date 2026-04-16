using System;
using System.Threading.Tasks;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Artifacts;

public record ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactResolveResultGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public new TArtifactData? Data { get => (TArtifactData?)base.Data; set => base.Data = (TArtifactData?)value; }
}

public record ArtifactResolveResultGeneric
{
    public required bool Found { get; set; }
    public ArtifactDataGeneric? Data { get; set; }
}

public abstract class ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> : ArtifactReferenceGeneric, IArtifactGettable<TArtifactDescriber, TArtifactData, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
{
    public abstract Task<ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference>> ResolveToDataAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger);

    public override async Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger) => await ResolveToDataAsync(serviceProvider, versionIdentifier, variableStore, logger);

    public virtual TArtifactDescriber GetArtifactDescriber()
    {
        return new TArtifactDescriber();
    }

    public override ArtifactDescriberGeneric GetArtifactDescriberGeneric() => GetArtifactDescriber();
}

public abstract class ArtifactReferenceGeneric : IFreezable, IArtifactGettableGeneric
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    public bool IsPinned { get; private set; }
    public void PinReference(VariableStore variableStore, ScopedLogger logger)
    {
        if (IsPinned) return;
        IsPinned = true;
        OnPinReference(variableStore, logger);
    }

    public bool CanDeconstruct { get; protected set; }

    public abstract ArtifactDescriberGeneric GetArtifactDescriberGeneric();

    public abstract Task<ArtifactResolveResultGeneric> ResolveToDataGenericAsync(IServiceProvider serviceProvider, ArtifactVersionIdentifier versionIdentifier, VariableStore variableStore, ScopedLogger logger);
    public abstract void DeclareIO(StepIOContract contract);

    public abstract void OnPinReference(VariableStore variableStore, ScopedLogger logger);
    public abstract override string ToString();
}