using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class CaptureArtifactVersionStep(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier) : Step<EmptyStepResultContext>
{
    public override bool DoesReturn => false;

    public override string Name => "Version Artifact";
    public override string Description => "Get a new Version of an external Artifact";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new CaptureArtifactVersionStep(identifier, versionIdentifier).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactInstanceGeneric artifactInstance = artifactStore.GetArtifact(identifier);
        ArtifactResolveResultGeneric artifactDataResult = await artifactInstance.Reference.ResolveToDataGenericAsync(serviceProvider, versionIdentifier, variableStore, logger);
        if (artifactDataResult.Found && artifactDataResult.Data is null)
            throw new ArtifactResolutionInvariantException(identifier, "artifact version capture", versionIdentifier);
        artifactInstance.AddVersionGeneric(artifactDataResult.Data!);
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}