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

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        ArtifactInstanceGeneric artifactInstance = context.Artifacts.GetArtifact(identifier);
        ArtifactResolveResultGeneric artifactDataResult = await artifactInstance.Reference.ResolveToDataGenericAsync(context, versionIdentifier);
        if (artifactDataResult.Found && artifactDataResult.Data is null)
            throw new ArtifactResolutionInvariantException(identifier, "artifact version capture", versionIdentifier);
        // Asked of the store, not of the instance: the store is what checks that this attempt is still
        // the one that counts, and what tells a debugger the artifact moved on.
        context.Artifacts.CaptureVersion(artifactInstance, artifactDataResult.Data!);

        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        contract.Inputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Artifact));
    }
}