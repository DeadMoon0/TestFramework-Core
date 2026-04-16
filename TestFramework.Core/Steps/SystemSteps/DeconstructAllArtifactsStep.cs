using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructAllArtifactsStep : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct All Artifacts";
    public override string Description => "Deconstructs all setuped Artifacts";

    public override Step<object?> Clone()
    {
        return new DeconstructAllArtifactsStep().WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        foreach (var artifactInstance in artifactStore.GetAll())
        {
            try
            {
                logger.LogInformation("Artifact: '{0}' of Type: '{1}'", artifactInstance.Identifier, artifactInstance.Artifact.GetType());
                if (artifactInstance.State != ArtifactState.Setup) return null;
                if (!artifactInstance.Reference.CanDeconstruct) throw new InvalidOperationException($"Artifact '{artifactInstance.Identifier}' cannot be deconstructed because its reference has no data.");
                await artifactInstance.Artifact.DeconstructGeneric(serviceProvider, artifactInstance.Reference, variableStore, logger);
                artifactInstance.State = ArtifactState.Cleaned;
            }
            catch (Exception e)
            {
                logger.LogError("Could not deconstruct artifact '{0}' due to an error:\n{1}", artifactInstance.Identifier, e.ToString());
            }
        }
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract) { }
}