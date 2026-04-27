using System;
using System.Threading.Tasks;
using TestFramework.Core;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Describes how a typed artifact is set up and deconstructed.
/// </summary>
public abstract class ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactDescriberGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    /// <summary>
    /// Sets up the typed artifact for use in the environment.
    /// </summary>
    public abstract Task Setup(IServiceProvider serviceProvider, TArtifactData data, TArtifactReference reference, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Deconstructs the typed artifact from the environment.
    /// </summary>
    public abstract Task Deconstruct(IServiceProvider serviceProvider, TArtifactReference reference, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Sets up the artifact through the non-generic base contract.
    /// </summary>
    public override Task SetupGeneric(IServiceProvider serviceProvider, ArtifactDataGeneric data, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger) => Setup(serviceProvider, (TArtifactData)data, (TArtifactReference)reference, variableStore, logger);

    /// <summary>
    /// Deconstructs the artifact through the non-generic base contract.
    /// </summary>
    public override Task DeconstructGeneric(IServiceProvider serviceProvider, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger) => Deconstruct(serviceProvider, (TArtifactReference)reference, variableStore, logger);
}

/// <summary>
/// Represents the non-generic base contract for artifact describers.
/// </summary>
public abstract class ArtifactDescriberGeneric : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the describer has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the describer.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    /// <summary>
    /// Sets up the artifact through the non-generic contract.
    /// </summary>
    public abstract Task SetupGeneric(IServiceProvider serviceProvider, ArtifactDataGeneric data, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Deconstructs the artifact through the non-generic contract.
    /// </summary>
    public abstract Task DeconstructGeneric(IServiceProvider serviceProvider, ArtifactReferenceGeneric reference, VariableStore variableStore, ScopedLogger logger);

    /// <summary>
    /// Returns a human-readable description of the artifact kind.
    /// </summary>
    public abstract override string ToString();
}