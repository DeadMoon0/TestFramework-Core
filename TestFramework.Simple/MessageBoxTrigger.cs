using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFramework.Simple;

/// <summary>
/// Displays a Windows message box during timeline execution.
/// </summary>
[SupportedOSPlatform("windows")]
public class MessageBoxTrigger(VariableReference<string> msg, VariableReference<string> caption) : Step<object?>
{
    /// <summary>
    /// Defines the Win32 icon flags that can be passed to the native message box call.
    /// </summary>
    public enum MessageBoxIconWin32 : uint
    {
        /// <summary>
        /// Shows no icon.
        /// </summary>
        None = 0x00000000, // no icon (default when no MB_ICON* flag is set)

        /// <summary>
        /// Shows the critical error icon.
        /// </summary>
        MBICONHAND = 0x00000010, // same as MB_ICONSTOP, critical error icon
        /// <summary>
        /// Shows the stop icon.
        /// </summary>
        MBICONSTOP = MBICONHAND,
        /// <summary>
        /// Shows the error icon.
        /// </summary>
        MBICONERROR = MBICONHAND,

        /// <summary>
        /// Shows the question icon.
        /// </summary>
        MBICONQUESTION = 0x00000020, // question mark icon (deprecated for dialogs; use caution)

        /// <summary>
        /// Shows the warning icon.
        /// </summary>
        MBICONEXCLAMATION = 0x00000030, // same as MB_ICONWARNING
        /// <summary>
        /// Shows the warning icon.
        /// </summary>
        MBICONWARNING = MBICONEXCLAMATION,

        /// <summary>
        /// Shows the information icon.
        /// </summary>
        MBICONASTERISK = 0x00000040, // same as MB_ICONINFORMATION
        /// <summary>
        /// Shows the information icon.
        /// </summary>
        MBICONINFORMATION = MBICONASTERISK
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    internal static Func<string, string, int> MessageBoxInvoker { get; set; } = (text, title) => MessageBox(IntPtr.Zero, text, title, (uint)MessageBoxIconWin32.None);

    /// <summary>
    /// Gets the display name shown in the timeline output.
    /// </summary>
    public override string Name => "MessageBox Trigger";

    /// <summary>
    /// Gets a short description of what the trigger does.
    /// </summary>
    public override string Description => "A trigger that shows a Windows message box.";

    /// <summary>
    /// Gets a value indicating whether the trigger produces a result payload.
    /// </summary>
    public override bool DoesReturn => false;

    /// <summary>
    /// Creates a copy of the trigger together with its configured step options.
    /// </summary>
    /// <returns>A cloned trigger with the same message, caption, and step options.</returns>
    public override Step<object?> Clone()
    {
        return new MessageBoxTrigger(msg, caption).WithClonedOptions(this);
    }

    /// <summary>
    /// Shows the configured Windows message box.
    /// </summary>
    /// <param name="serviceProvider">The runtime service provider for the current timeline run.</param>
    /// <param name="variableStore">The variable store used to resolve message and caption values.</param>
    /// <param name="artifactStore">The artifact store for the current timeline run.</param>
    /// <param name="logger">The scoped logger for the current step execution.</param>
    /// <param name="cancellationToken">The cancellation token for the current execution.</param>
    /// <returns>A completed task because the trigger does not produce a value.</returns>
    /// <remarks>This trigger requires Windows because it calls <c>user32.dll</c>.</remarks>
    public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        MessageBoxInvoker(msg.GetRequiredValue(variableStore), caption.GetRequiredValue(variableStore));
        return Task.FromResult((object?)null);
    }

    /// <summary>
    /// Creates a runtime instance for this trigger.
    /// </summary>
    /// <returns>The runtime step instance used during timeline execution.</returns>
    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    /// <summary>
    /// Declares the variable inputs consumed by the message box.
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
