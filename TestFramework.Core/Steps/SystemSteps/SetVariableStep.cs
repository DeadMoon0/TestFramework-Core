using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class SetVariableStep(VariableIdentifier identifier, VariableReferenceGeneric reference) : Step<EmptyStepResultContext>
{
    public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

    public override bool DoesReturn => false;

    public override string Name => "Set Variable";
    public override string Description => "Sets a Variable to a Value";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new SetVariableStep(identifier, reference).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        context.Variables.SetVariable(identifier, reference.GetValueGeneric(context.Variables));
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (reference.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(reference.Identifier!.Identifier, StepIOKind.Variable));
        contract.Outputs.Add(new StepIOEntry(identifier.Identifier, StepIOKind.Variable));
    }
}