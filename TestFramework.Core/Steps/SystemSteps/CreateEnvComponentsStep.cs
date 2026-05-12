using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Internal;
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
        await EnvComponentLifecycleRunner.CreateAsync(
            environment,
            resolvedComponents,
            serviceProvider,
            variableStore,
            artifactStore,
            logger,
            cancellationToken,
            context.SetState);

        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }
}