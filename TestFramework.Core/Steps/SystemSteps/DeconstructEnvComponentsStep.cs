using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructEnvComponentsStep(IEnvironmentProvider environment, EnvComponentContext context) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct Environment Components";
    public override string Description => "Deconstructs created environment components in reverse dependency order.";

    public override Step<object?> Clone()
    {
        return new DeconstructEnvComponentsStep(environment, context).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        foreach (EnvComponentIdentifier identifier in context.GetCreationOrder().Reverse())
        {
            EnvComponent component = environment.GetComponent(identifier);
            context.TryGetState(identifier, out object? state);
            logger.LogInformation("Deconstruct EnvComponent ({0})", component.Id);
            await component.DeconstructAsync(state, environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken);
        }

        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }
}