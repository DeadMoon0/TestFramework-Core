using System;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Simple;

/// <summary>
/// Writes a captioned message to the run log.
/// </summary>
/// <remarks>
/// The same shape as <see cref="MessageBoxTrigger"/>, but it reports through the logger instead of
/// showing a dialog. That makes it usable on any platform and in unattended runs, where a modal
/// dialog would simply hang the suite until something times out.
/// </remarks>
public class MessageTrigger(VariableReference<string> msg, VariableReference<string> caption) : Step<EmptyStepResultContext>
{
    /// <summary>
    /// Gets the display name shown in the timeline output.
    /// </summary>
    public override string Name => "Message Trigger";

    /// <summary>
    /// Gets a short description of what the trigger does.
    /// </summary>
    public override string Description => "A trigger that writes a captioned message to the run log.";

    /// <summary>
    /// Gets a value indicating whether the trigger produces a result payload.
    /// </summary>
    public override bool DoesReturn => false;

    /// <summary>
    /// Creates a copy of the trigger together with its configured step options.
    /// </summary>
    /// <returns>A cloned trigger with the same message, caption, and step options.</returns>
    public override Step<EmptyStepResultContext> Clone()
    {
        return new MessageTrigger(msg, caption).WithClonedOptions(this);
    }

    /// <summary>
    /// Writes the resolved caption and message to the run log.
    /// </summary>
    /// <returns>A completed task because the trigger does not produce a value.</returns>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public override Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        context.Logger.LogInformation($"[{caption.GetRequiredValue(context.Variables)}] {msg.GetRequiredValue(context.Variables)}");
        return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    /// <summary>
    /// Creates a runtime instance for this trigger.
    /// </summary>
    /// <returns>The runtime step instance used during timeline execution.</returns>
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    /// <summary>
    /// Declares the variable inputs consumed by the message.
    /// </summary>
    /// <param name="contract">The IO contract to populate.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        if (msg.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(msg.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (caption.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(caption.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
