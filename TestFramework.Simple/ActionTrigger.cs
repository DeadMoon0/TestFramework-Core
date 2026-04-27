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

namespace TestFramework.Simple;

/// <summary>
/// Executes an inline C# action within a timeline.
/// </summary>
public class ActionTrigger(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, ArtifactIdentifier[] artifacts) : Step<object?>
{
    private readonly Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> _action = action ?? throw new ArgumentNullException(nameof(action));
    private readonly VariableReferenceGeneric[] _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    private readonly ArtifactIdentifier[] _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));

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
    public override Step<object?> Clone()
    {
        return new ActionTrigger(_action, _variables, _artifacts).WithClonedOptions(this);
    }

    /// <summary>
    /// Executes the configured action with resolved variables and artifacts.
    /// </summary>
    /// <param name="serviceProvider">The runtime service provider for the current timeline run.</param>
    /// <param name="variableStore">The variable store used to resolve declared variable inputs.</param>
    /// <param name="artifactStore">The artifact store used to load declared artifact inputs.</param>
    /// <param name="logger">The scoped logger for the current step execution.</param>
    /// <param name="cancellationToken">The cancellation token for the current execution.</param>
    /// <returns>A completed task because the trigger does not produce a value.</returns>
    public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        _action
        (
            serviceProvider,
            logger,
            _variables.ToDictionary(x => x.Identifier ?? throw new ArgumentNullException("identifier", "A variable passed to Action requires a non-null identifier."), x => x.GetValueGeneric(variableStore)),
            _artifacts.ToDictionary(x => x, artifactStore.GetArtifact)
        );
        return Task.FromResult((object?)null);
    }

    /// <summary>
    /// Creates a runtime instance for this trigger.
    /// </summary>
    /// <returns>The runtime step instance used during timeline execution.</returns>
    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    /// <summary>
    /// Declares the variable and artifact inputs consumed by this trigger.
    /// </summary>
    /// <param name="contract">The IO contract to populate.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        foreach (var item in _variables)
            if (item.HasIdentifier)
                contract.Inputs.Add(new StepIOEntry(item.Identifier!.Identifier, StepIOKind.Variable));
        foreach (var item in _artifacts)
            contract.Inputs.Add(new StepIOEntry(item.Identifier, StepIOKind.Artifact));
    }
}
