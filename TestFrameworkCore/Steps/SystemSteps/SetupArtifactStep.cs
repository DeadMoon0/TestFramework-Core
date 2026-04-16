using System;
using System.Threading;
using System.Threading.Tasks;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Logging;
using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps.SystemSteps;

internal class SetupArtifactStep(ArtifactIdentifier identifier) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Setup Artifact";
    public override string Description => "Sets an Artifact externally up";

    public override Step<object?> Clone()
    {
        return new SetupArtifactStep(identifier).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactInstanceGeneric artifactInstance = artifactStore.GetArtifact(identifier);
        artifactInstance.Reference.PinReference(variableStore, logger);
        await artifactInstance.Artifact.SetupGeneric(serviceProvider, artifactInstance.Last, artifactInstance.Reference, variableStore, logger);
        artifactInstance.State = ArtifactState.Setup;
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}