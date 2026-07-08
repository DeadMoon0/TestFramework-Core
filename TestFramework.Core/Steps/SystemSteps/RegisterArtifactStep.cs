using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

//TODO: Make failable on NotFound
internal class RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, TArtifactReference reference) : Step<EmptyStepResultContext>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public override StepExecutionPhase Phase => StepExecutionPhase.Materialize;

    public override bool DoesReturn => false;

    public override string Name => "Register Artifact";
    public override string Description => "Registers and Loads an external Artifact";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, reference).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        reference.OnPinReference(variableStore, logger);
        ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> artifactDataResult = await reference.ResolveToDataAsync(serviceProvider, ArtifactVersionIdentifier.Default, variableStore, logger);
        if (artifactDataResult.Found && artifactDataResult.Data is null)
            throw new ArtifactResolutionInvariantException(identifier, "artifact registration");
        artifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(reference.GetArtifactDescriber(), identifier, reference, artifactDataResult.Data)
        {
            State = artifactDataResult.Found ? ArtifactState.Setup : ArtifactState.NotFound
        });
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        reference.DeclareIO(contract);
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}