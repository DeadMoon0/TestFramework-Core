using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFrameworkSimple;

public class ActionTrigger(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, ArtifactIdentifier[] artifacts) : Step<object?>
{
    public override string Name => "Action Trigger";

    public override string Description => "A Trigger which executes an C# Action";

    public override bool DoesReturn => false;

    public override Step<object?> Clone()
    {
        return new ActionTrigger(action, variables, artifacts).WithClonedOptions(this);
    }

    public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        action
        (
            serviceProvider,
            logger,
            variables.ToDictionary(x => x.Identifier ?? throw new ArgumentNullException("identifier", "A variable passed to Action requires a non-null identifier."), x => x.GetValueGeneric(variableStore)),
            artifacts.ToDictionary(x => x, artifactStore.GetArtifact)
        );
        return Task.FromResult((object?)null);
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        foreach (var item in variables)
            if (item.HasIdentifier)
                contract.Inputs.Add(new StepIOEntry(item.Identifier!.Identifier, StepIOKind.Variable));
        foreach (var item in artifacts)
            contract.Inputs.Add(new StepIOEntry(item.Identifier, StepIOKind.Artifact));
    }
}
