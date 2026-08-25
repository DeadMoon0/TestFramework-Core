using TestFramework.Core.Steps;
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
    public abstract Task Setup(RunContext context, TArtifactData data, TArtifactReference reference);

    /// <summary>
    /// Deconstructs the typed artifact from the environment.
    /// </summary>
    public abstract Task Deconstruct(RunContext context, TArtifactReference reference);

    /// <summary>
    /// Sets up the artifact through the non-generic base contract.
    /// </summary>
    public override Task SetupGeneric(RunContext context, ArtifactDataGeneric data, ArtifactReferenceGeneric reference) => Setup(context, (TArtifactData)data, (TArtifactReference)reference);

    /// <summary>
    /// Deconstructs the artifact through the non-generic base contract.
    /// </summary>
    public override Task DeconstructGeneric(RunContext context, ArtifactReferenceGeneric reference) => Deconstruct(context, (TArtifactReference)reference);
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
    /// <remarks>
    /// Reached through <see cref="IFreezable"/> like every other part of an artifact. A describer is
    /// behaviour rather than state, so this settles nothing today - it is here so that "a frozen artifact
    /// is frozen all the way down" stays true if a describer ever holds something.
    /// </remarks>
    void IFreezable.Freeze() { IsFrozen = true; }

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
    public abstract Task SetupGeneric(RunContext context, ArtifactDataGeneric data, ArtifactReferenceGeneric reference);

    /// <summary>
    /// Deconstructs the artifact through the non-generic contract.
    /// </summary>
    public abstract Task DeconstructGeneric(RunContext context, ArtifactReferenceGeneric reference);

    /// <summary>
    /// Returns a human-readable description of the artifact kind.
    /// </summary>
    public abstract override string ToString();
}