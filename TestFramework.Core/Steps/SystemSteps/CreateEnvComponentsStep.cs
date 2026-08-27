using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Environment;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Environment.Internal;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class CreateEnvComponentsStep(IEnvironmentProvider environment, EnvComponentContext components, IReadOnlyCollection<EnvironmentRequirement> requirements, ResourcePublishing? publishing = null) : Step<EmptyStepResultContext>
{
    public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

    public override bool DoesReturn => false;

    public override string Name => "Create Environment Components";
    public override string Description => "Creates all environment components required by the artifacts configured for this run.";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new CreateEnvComponentsStep(environment, components, requirements, publishing).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        IReadOnlyCollection<EnvComponentIdentifier> resolvedComponents = environment.ResolveComponents(context.Artifacts.GetAll(), requirements);
        await EnvComponentLifecycleRunner.CreateAsync(environment, resolvedComponents, context, components.SetState, publishing);

        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
    }
}