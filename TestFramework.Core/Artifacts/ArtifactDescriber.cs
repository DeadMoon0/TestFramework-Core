using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TestFramework.Core;
using TestFramework.Core.Debugger;
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
    /// Gets how setup for this artifact kind may be parallelized.
    /// </summary>
    public virtual ArtifactSetupParallelizationMode SetupParallelization => ArtifactSetupParallelizationMode.AllowParallel;

    /// <summary>
    /// Gets the resource key used to serialize setup work for this artifact instance.
    /// </summary>
    public virtual string? GetSetupParallelizationResourceKey(ArtifactInstanceGeneric artifactInstance)
    {
        return SetupParallelization switch
        {
            ArtifactSetupParallelizationMode.AllowParallel => null,
            ArtifactSetupParallelizationMode.SerializeByArtifactType => GetType().FullName,
            _ => null
        };
    }

    /// <summary>
    /// Gets the schema key used by debugger value envelopes for this artifact kind.
    /// </summary>
    public virtual string DebugValueSchemaKey => GetType().FullName ?? ToString();

    /// <summary>
    /// Creates an optional artifact-specific JSON payload for debugger value envelopes.
    /// </summary>
    public virtual JToken? CreateDebugValueCustomPayload(ArtifactInstanceGeneric instance) => null;

    /// <summary>
    /// Describes the artifact as facts a consumer can lay out for itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Override to say what this kind of artifact actually is — a row's table and key, a blob's
    /// container and content type, a file's path and size — instead of leaving a consumer to read it
    /// out of a serialised reference. The default states what is true of every artifact: its
    /// reference, its lifecycle state, and how many versions have been captured.
    /// </para>
    /// <para>
    /// This is the presentation contract. <see cref="CreateDebugValueCustomPayload"/> remains the
    /// place for machine-readable detail that no consumer is expected to render as text.
    /// </para>
    /// </remarks>
    public virtual DebugValueDescription Describe(ArtifactInstanceGeneric instance) => ArtifactDescription.Of(instance);

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