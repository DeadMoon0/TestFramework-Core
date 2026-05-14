using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Steps.SystemSteps;

internal class AssertVariableStep<T>(VariableReference<T> variable, Func<T?, bool> predicate) : Step<EmptyStepResultContext>
{
    public override bool DoesReturn => false;

    public override string Name => "Assert Variable";
    public override string Description => "Assert that a Variable Value has a certain State.";

    public override Step<EmptyStepResultContext> Clone()
    {
        return new AssertVariableStep<T>(variable, predicate).WithClonedOptions(this);
    }

    public override async Task<EmptyStepResultContext?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        T? value = variable.GetValue(variableStore);
        if (!predicate(value)) throw new AssertVariableException(variable.Identifier, value);
        return EmptyStepResultContext.Instance;
    }

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (variable.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(variable.Identifier!.Identifier, StepIOKind.Variable, true, typeof(T)));
    }
}