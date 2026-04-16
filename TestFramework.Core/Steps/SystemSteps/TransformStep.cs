using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFrameworkCore.Steps.Options;

namespace TestFramework.Core.Steps.SystemSteps;

internal class TransformStep<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer) : Step<object?>
{
    public override string Name => "Transform";

    public override string Description => "Transforms a Variable into another State";

    public override bool DoesReturn => false;

    public override Step<object?> Clone()
    {
        return new TransformStep<TFrom, TTo>(toVariable, fromVariable, transformer).WithClonedOptions(this);
    }

    public override async Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        variableStore.SetVariable(toVariable, await transformer(fromVariable.GetValue(variableStore)));
        return null;
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (fromVariable.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(fromVariable.Identifier!.Identifier, StepIOKind.Variable, true, typeof(TFrom)));
        contract.Outputs.Add(new StepIOEntry(toVariable.Identifier, StepIOKind.Variable, true, typeof(TTo)));
    }
}