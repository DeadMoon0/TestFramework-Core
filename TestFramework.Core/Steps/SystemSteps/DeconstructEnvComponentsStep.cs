using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Internal;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class DeconstructEnvComponentsStep(IEnvironmentProvider environment, EnvComponentContext components) : Step<EmptyStepResultContext>
{
    public override bool DoesReturn => false;

    public override string Name => "Deconstruct Environment Components";
    public override string Description => "Deconstructs created environment components in reverse dependency order.";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new DeconstructEnvComponentsStep(environment, components).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        await EnvComponentLifecycleRunner.DeconstructAsync(
            environment,
            components.GetCreationOrder(),
            context,
            identifier =>
            {
                components.TryGetState(identifier, out object? state);
                return state;
            },
            components.ScopeOf);

        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }
}