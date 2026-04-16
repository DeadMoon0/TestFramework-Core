using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkCore.Steps.Options;

namespace TestFramework.Core.Steps.SystemSteps;

internal class CaptureArtifactVersionStep(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Version Artifact";
    public override string Description => "Get a new Version of an external Artifact";

    public override Step<object?> Clone()
    {
        return new CaptureArtifactVersionStep(identifier, versionIdentifier).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        ArtifactInstanceGeneric artifactInstance = artifactStore.GetArtifact(identifier);
        ArtifactResolveResultGeneric artifactDataResult = await artifactInstance.Reference.ResolveToDataGenericAsync(serviceProvider, versionIdentifier, variableStore, logger);
        if (artifactDataResult.Found && artifactDataResult.Data is null) throw new InvalidOperationException($"Artifact '{identifier}' version resolved with Found=true but Data was null.");
        artifactInstance.AddVersionGeneric(artifactDataResult.Data!);
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}