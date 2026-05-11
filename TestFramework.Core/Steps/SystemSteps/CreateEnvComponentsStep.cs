using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class CreateEnvComponentsStep(IEnvironmentProvider environment, EnvComponentContext context, IReadOnlyCollection<EnvironmentRequirement> requirements) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Create Environment Components";
    public override string Description => "Creates all environment components required by the artifacts configured for this run.";

    public override Step<object?> Clone()
    {
        return new CreateEnvComponentsStep(environment, context, requirements).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<EnvComponentIdentifier> resolvedComponents = environment.ResolveComponents(artifactStore.GetAll(), requirements);
        if (!environment.SupportsParallelComponentCreation)
        {
            var orderedComponents = EnvComponentGraph.Order(environment, resolvedComponents);
            foreach (EnvComponent component in orderedComponents)
            {
                logger.LogInformation("Create EnvComponent ({0})", component.Id);
                object? state = await component.CreateAsync(environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken);
                context.SetState(component.Id, state);
            }

            return null;
        }

        IReadOnlyList<IReadOnlyList<EnvComponent>> componentLayers = EnvComponentGraph.Layers(environment, resolvedComponents);
        foreach (IReadOnlyList<EnvComponent> componentLayer in componentLayers)
        {
            foreach (EnvComponent component in componentLayer)
                logger.LogInformation("Create EnvComponent ({0})", component.Id);

            var creationTasks = componentLayer
                .Select(async component => (component.Id, State: await component.CreateAsync(environment, serviceProvider, variableStore, artifactStore, logger, cancellationToken)))
                .ToArray();

            (EnvComponentIdentifier Id, object? State)[] creationResults = await Task.WhenAll(creationTasks);
            foreach ((EnvComponentIdentifier componentId, object? state) in creationResults)
                context.SetState(componentId, state);
        }

        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }
}