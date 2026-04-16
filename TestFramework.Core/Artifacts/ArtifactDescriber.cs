using System;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

public abstract class ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactDescriberGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public abstract Task Setup(IServiceProvider serviceProvider, TArtifactData data, TArtifactReference reference, VariableStore variableStore, ScopedLogger logger);
    public abstract Task Deconstruct(IServiceProvider serviceProvider, TArtifactReference reference, VariableStore variableStore, ScopedLogger logger);

    public override Task SetupGeneric(IServiceProvider serviceProvider, ArtifactDataGeneric data, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger) => Setup(serviceProvider, (TArtifactData)data, (TArtifactReference)reference, variableStore, logger);
    public override Task DeconstructGeneric(IServiceProvider serviceProvider, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger) => Deconstruct(serviceProvider, (TArtifactReference)reference, variableStore, logger);
}

public abstract class ArtifactDescriberGeneric : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    public abstract Task SetupGeneric(IServiceProvider serviceProvider, ArtifactDataGeneric data, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger);
    public abstract Task DeconstructGeneric(IServiceProvider serviceProvider, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger);
    public abstract override string ToString();
}