using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkCore.Steps.Options;

namespace TestFramework.Core.Steps.SystemSteps;

internal class SetVariableStep(VariableIdentifier identifier, VariableReferenceGeneric reference) : Step<object?>
{
    public override bool DoesReturn => false;

    public override string Name => "Set Variable";
    public override string Description => "Sets a Variable to a Value";

    public override Step<object?> Clone()
    {
        return new SetVariableStep(identifier, reference).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        variableStore.SetVariable(identifier, reference.GetValueGeneric(variableStore));
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (reference.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(reference.Identifier!.Identifier, StepIOKind.Variable));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Variable));
    }
}