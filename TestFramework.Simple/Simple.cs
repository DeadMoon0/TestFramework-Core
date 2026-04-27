using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Variables;

namespace TestFramework.Simple;

/// <summary>
/// Entry point for the lightweight trigger helpers provided by <c>TestFramework.Simple</c>.
/// </summary>
public static class Simple
{
    /// <summary>
    /// Gets the trigger factory for inline actions and message boxes.
    /// </summary>
    public static SimpleTrigger Trigger { get; } = new SimpleTrigger();
}

/// <summary>
/// Creates lightweight trigger steps for scenarios where a dedicated custom step type would be excessive.
/// </summary>
public class SimpleTrigger
{
    /// <summary>
    /// Creates a trigger that runs a parameterless action.
    /// </summary>
    /// <param name="action">The action to execute when the trigger runs.</param>
    /// <returns>A trigger that executes the supplied action.</returns>
    public ActionTrigger Action(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new ActionTrigger((_, _, _, _) => action(), [], []);
    }

    /// <summary>
    /// Creates a trigger that receives resolved variables by identifier.
    /// </summary>
    /// <param name="action">The action to execute with the resolved variable values.</param>
    /// <param name="variables">The variables that should be resolved before the action executes.</param>
    /// <returns>A trigger that exposes resolved variable values through the supplied dictionary.</returns>
    public ActionTrigger Action(Action<Dictionary<VariableIdentifier, object?>> action, params VariableReferenceGeneric[] variables)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new ActionTrigger((_, _, vars, _) => action(vars), variables, []);
    }

    /// <summary>
    /// Creates a trigger that receives resolved variables and referenced artifacts.
    /// </summary>
    /// <param name="action">The action to execute with resolved variables and loaded artifacts.</param>
    /// <param name="variables">The variables that should be resolved before the action executes.</param>
    /// <param name="artifacts">The artifacts that should be loaded before the action executes.</param>
    /// <returns>A trigger that exposes both variable and artifact inputs to the supplied action.</returns>
    public ActionTrigger Action(Action<Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new ActionTrigger((_, _, vars, artifacts) => action(vars, artifacts), variables, artifacts);
    }

    /// <summary>
    /// Creates a trigger that receives the full execution context.
    /// </summary>
    /// <param name="action">The action to execute with service provider, logger, variables, and artifacts.</param>
    /// <param name="variables">The variables that should be resolved before the action executes.</param>
    /// <param name="artifacts">The artifacts that should be loaded before the action executes.</param>
    /// <returns>A trigger that exposes the full runtime context to the supplied action.</returns>
    public ActionTrigger Action(Action<IServiceProvider, ScopedLogger, Dictionary<VariableIdentifier, object?>, Dictionary<ArtifactIdentifier, ArtifactInstanceGeneric>> action, VariableReferenceGeneric[] variables, params ArtifactIdentifier[] artifacts)
    {
        ArgumentNullException.ThrowIfNull(action);
        return new ActionTrigger(action, variables, artifacts);
    }

    /// <summary>
    /// Creates a Windows message box trigger with explicit message and caption variables.
    /// </summary>
    /// <param name="msg">The variable or constant that supplies the message body.</param>
    /// <param name="caption">The variable or constant that supplies the window caption.</param>
    /// <returns>A trigger that displays the supplied values in a Windows message box.</returns>
    [SupportedOSPlatform("windows")]
    public MessageBoxTrigger MessageBox(VariableReference<string> msg, VariableReference<string> caption)
    {
        return new MessageBoxTrigger(msg, caption);
    }

    /// <summary>
    /// Creates a Windows message box trigger with the default caption <c>MessageBox</c>.
    /// </summary>
    /// <param name="msg">The variable or constant that supplies the message body.</param>
    /// <returns>A trigger that displays the supplied message in a Windows message box.</returns>
    [SupportedOSPlatform("windows")]
    public MessageBoxTrigger MessageBox(VariableReference<string> msg)
    {
        return MessageBox(msg, "MessageBox");
    }
}