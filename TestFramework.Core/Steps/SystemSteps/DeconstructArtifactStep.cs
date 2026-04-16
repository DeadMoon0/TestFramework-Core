using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkCore.Steps.Options;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructArtifactStep(ArtifactIdentifier identifier) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct Artifact";
    public override string Description => "Deconstructs a setuped Artifact";

    public override Step<object?> Clone()
    {
        return new DeconstructArtifactStep(identifier).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactInstanceGeneric artifactInstance = artifactStore.GetArtifact(identifier);
        logger.LogInformation("Artifact: '{0}' of Type: '{1}'", identifier, artifactInstance.Artifact.GetType());
        if (artifactInstance.State != ArtifactState.Setup) return null;
        if (!artifactInstance.Reference.CanDeconstruct) throw new InvalidOperationException($"Artifact '{identifier}' cannot be deconstructed because its reference has no data.");
        await artifactInstance.Artifact.DeconstructGeneric(serviceProvider, artifactInstance.Reference, variableStore, logger);
        artifactInstance.State = ArtifactState.Cleaned;
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}