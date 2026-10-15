using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Exceptions;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Simple;

/// <summary>
/// Executes an inline C# action within a timeline.
/// </summary>
public class ActionTrigger(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, ArtifactIdentifier[] artifacts) : Step<EmptyStepResultContext>
{
    private readonly Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> _action = action ?? throw new ArgumentNullException(nameof(action));
    private readonly VariableReferenceGeneric[] _variables = RequireNamedOnce(variables ?? throw new ArgumentNullException(nameof(variables)));
    private readonly ArtifactIdentifier[] _artifacts = RequireOnce(artifacts ?? throw new ArgumentNullException(nameof(artifacts)));

    /// <summary>
    /// Refuses, while the timeline is built, the inputs the action's dictionaries could not hold: a constant has no
    /// identifier to key it by, and a name given twice would be one key.
    /// </summary>
    private static VariableReferenceGeneric[] RequireNamedOnce(VariableReferenceGeneric[] variables)
    {
        if (variables.Any(variable => variable.Identifier is null))
        {
            throw new FrameworkConfigurationException("An Action trigger was handed a constant where it reads variables: the action receives its inputs by identifier, and a constant has none.")
            {
                RecoverySteps = ["Pass Var.Ref<T>(\"name\") for a value the run holds, or capture a constant in the action itself."],
            };
        }

        if (variables.GroupBy(variable => variable.Identifier!.Identifier, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } repeated)
        {
            throw new FrameworkConfigurationException($"An Action trigger was handed the variable '{repeated.Key}' more than once.")
            {
                RecoverySteps = [$"Pass '{repeated.Key}' once; the action reads it from the dictionary under that name."],
            };
        }

        return variables;
    }

    private static ArtifactIdentifier[] RequireOnce(ArtifactIdentifier[] artifacts)
    {
        if (artifacts.GroupBy(artifact => artifact.Identifier, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } repeated)
        {
            throw new FrameworkConfigurationException($"An Action trigger was handed the artifact '{repeated.Key}' more than once.")
            {
                RecoverySteps = [$"Pass '{repeated.Key}' once; the action reads it from the dictionary under that name."],
            };
        }

        return artifacts;
    }

    /// <summary>
    /// Gets the display name shown in the timeline output.
    /// </summary>
    public override string Name => "Action Trigger";

    /// <summary>
    /// Gets a short description of what the trigger does.
    /// </summary>
    public override string Description => "A trigger that executes a C# action.";

    /// <summary>
    /// Gets a value indicating whether the trigger produces a result payload.
    /// </summary>
    public override bool DoesReturn => false;

    /// <summary>
    /// Creates a copy of the trigger together with its configured step options.
    /// </summary>
    /// <returns>A cloned trigger with the same action, inputs, and step options.</returns>
    public override Step<EmptyStepResultContext> Clone()
    {
        return new ActionTrigger(_action, _variables, _artifacts).WithClonedOptions(this);
    }

    /// <summary>
    /// Executes the configured action with resolved variables and artifacts.
    /// </summary>
    /// <returns>A completed task because the trigger does not produce a value.</returns>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public override Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        _action
        (
            context.Services,
            context.Logger,
            _variables.ToDictionary(x => x.Identifier!, x => x.GetValueGeneric(context.Variables)),
            _artifacts.ToDictionary(x => x, context.Artifacts.GetArtifact)
        );
        return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    /// <summary>
    /// Creates a runtime instance for this trigger.
    /// </summary>
    /// <returns>The runtime step instance used during timeline execution.</returns>
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    /// <summary>
    /// Declares the variable and artifact inputs consumed by this trigger.
    /// </summary>
    /// <param name="contract">The IO contract to populate.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        foreach (var item in _variables)
            contract.Input(item);
        foreach (var item in _artifacts)
            contract.InputArtifact(item);
    }
}
