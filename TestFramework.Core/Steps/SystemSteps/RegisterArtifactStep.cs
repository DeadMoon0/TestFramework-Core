using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

// Current behaviour: an artifact that does not resolve is still registered, in the NotFound state,
// and the step completes. Registration reports what is there rather than demanding that it exists.
internal class RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, TArtifactReference reference) : Step<EmptyStepResultContext>, IMarkArtifactsReadonly
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public bool MarkArtifactsReadonly { get; set; }

    public override StepExecutionPhase Phase => StepExecutionPhase.Materialize;

    public override bool DoesReturn => false;

    public override string Name => "Register Artifact";
    public override string Description => "Registers and Loads an external Artifact";

    public override Step<EmptyStepResultContext> Clone()
    {
        // Each run gets its own reference instance: the reference carries pinned state and would otherwise
        // be shared by every run of the same built timeline.
        return new RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, (TArtifactReference)reference.CloneForRun())
        {
            MarkArtifactsReadonly = MarkArtifactsReadonly
        }.WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        context.Artifacts.PinNewReference(identifier, reference, context);
        ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> artifactDataResult = await reference.ResolveToDataAsync(context, ArtifactVersionIdentifier.Default);
        if (artifactDataResult.Found && artifactDataResult.Data is null)
            throw new ArtifactResolutionInvariantException(identifier, "artifact registration");
        context.Artifacts.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(
            reference.GetArtifactDescriber(),
            identifier,
            reference,
            artifactDataResult.Data,
            artifactDataResult.Found ? ArtifactState.Setup : ArtifactState.NotFound,
            MarkArtifactsReadonly));
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        reference.DeclareIO(contract);
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}