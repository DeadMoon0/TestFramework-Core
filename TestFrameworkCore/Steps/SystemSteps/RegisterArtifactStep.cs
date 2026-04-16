using System;
using System.Threading;
using System.Threading.Tasks;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps.SystemSteps;

//TODO: Make failable on NotFound
internal class RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(ArtifactIdentifier identifier, TArtifactReference reference) : Step<object?>
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public override bool DoesReturn => false;

    public override string Name => "Register Artifact";
    public override string Description => "Registers and Loads an external Artifact";

    public override Step<object?> Clone()
    {
        return new RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, reference).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        reference.OnPinReference(variableStore, logger);
        ArtifactResolveResult<TArtifactDescriber, TArtifactData, TArtifactReference> artifactDataResult = await reference.ResolveToDataAsync(serviceProvider, ArtifactVersionIdentifier.Default, variableStore, logger);
        if (artifactDataResult.Found && artifactDataResult.Data is null) throw new InvalidOperationException($"Artifact '{identifier}' resolved with Found=true but Data was null.");
        artifactStore.AddArtifact(new ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference>(reference.GetArtifactDescriber(), identifier, reference, artifactDataResult.Data)
        {
            State = artifactDataResult.Found ? ArtifactState.Setup : ArtifactState.NotFound
        });
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        reference.DeclareIO(contract);
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}