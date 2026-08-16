using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Debugger;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;
using TestFramework.Core.Exceptions;

namespace TestFramework.Core.Steps.SystemSteps;

internal class AssertVariableStep<T>(VariableReference<T> variable, Func<T?, bool> predicate) : Step<EmptyStepResultContext>
{
    /// <summary>
    /// What the assertion is called where it is reported.
    /// </summary>
    /// <remarks>
    /// The predicate is a compiled delegate, so there is nothing to render of the condition itself.
    /// The step's own name carries what was checked; this says how.
    /// </remarks>
    private const string AssertionName = "MatchesPredicate";

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
        bool held = predicate(value);

        // Reported whether it held or not, and reported before the throw. A consumer decides whether
        // a run proved anything from the assertions it was told about, so an assertion written in the
        // timeline has to reach the same channel as one written after the run — otherwise a timeline
        // full of checks looks like a run that checked nothing.
        logger.SignalAssertion(
            DebugAssertionTargetKind.Variable,
            variable.HasIdentifier ? variable.Identifier!.Identifier : "<const>",
            AssertionName,
            AssertionName,
            held,
            "the predicate holds",
            Describe(value),
            held ? "" : $"the predicate rejected {Describe(value)}");

        if (!held) throw new AssertVariableException(variable.Identifier, value);
        return EmptyStepResultContext.Instance;
    }

    private static string Describe(T? value) => value?.ToString() ?? "null";

    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (variable.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(variable.Identifier!.Identifier, StepIOKind.Variable, true, typeof(T)));
    }
}