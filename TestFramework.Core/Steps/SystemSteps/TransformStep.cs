using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.SystemSteps;

internal class TransformStep<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer) : Step<EmptyStepResultContext>
{
    public override StepExecutionPhase Phase => StepExecutionPhase.Prepare;

    public override string Name => "Transform";

    public override string Description => "Transforms a Variable into another State";

    public override bool DoesReturn => false;

    public override Step<EmptyStepResultContext> Clone()
    {
        return new TransformStep<TFrom, TTo>(toVariable, fromVariable, transformer).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        context.Variables.SetVariable(toVariable, await transformer(fromVariable.GetValue(context.Variables)));
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (fromVariable.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(fromVariable.Identifier!.Identifier, StepIOKind.Variable, true, typeof(TFrom)));
        contract.Outputs.Add(new StepIOEntry(toVariable.Identifier, StepIOKind.Variable, true, typeof(TTo)));
    }
}