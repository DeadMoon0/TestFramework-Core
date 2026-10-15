using System;
using System.Linq;
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
public class MessageBoxTrigger(VariableReference<string> msg, VariableReference<string> caption) : Step<EmptyStepResultContext>
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

    private readonly Func<string, string, int> _present = ShowNative;

    /// <summary>
    /// Creates the trigger with a presenter of its own instead of the native dialog.
    /// </summary>
    /// <remarks>
    /// The seam for tests. It used to be a public static anyone could set, which changed every message box in the
    /// process for whoever ran next; a presenter handed to one trigger changes that trigger.
    /// </remarks>
    /// <param name="msg">The message body.</param>
    /// <param name="caption">The caption.</param>
    /// <param name="present">Shows the message, in place of the native dialog.</param>
    internal MessageBoxTrigger(VariableReference<string> msg, VariableReference<string> caption, Func<string, string, int> present)
        : this(msg, caption)
    {
        _present = present ?? throw new ArgumentNullException(nameof(present));
    }

    private static int ShowNative(string text, string title) => MessageBox(IntPtr.Zero, text, title, (uint)MessageBoxIconWin32.None);

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
    public override Step<EmptyStepResultContext> Clone()
    {
        return new MessageBoxTrigger(msg, caption, _present).WithClonedOptions(this);
    }

    /// <summary>
    /// Shows the configured Windows message box.
    /// </summary>
    /// <returns>A completed task because the trigger does not produce a value.</returns>
    /// <remarks>This trigger requires Windows because it calls <c>user32.dll</c>.</remarks>
    /// <param name="context">What this step is given.</param>
    /// <returns>The step's result.</returns>
    public override Task<EmptyStepResultContext?> Execute(RunContext context)
    {
        string text = msg.GetRequiredValue(context.Variables);
        string title = caption.GetRequiredValue(context.Variables);

        // Looked at, not asked for: whether anyone could answer the dialog is read from where the run is, and
        // TESTFRAMEWORK_MESSAGEBOX only overrides that answer.
        MessageBoxDisplay display = MessageBoxDisplay.Decide(
            System.Environment.GetEnvironmentVariable(MessageBoxDisplay.SwitchVariable),
            System.Environment.UserInteractive,
            MessageBoxDisplay.CiMarkers.Any(marker => !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(marker))),
            OperatingSystem.IsWindows());

        if (display.Show)
            _present(text, title);
        else
            context.Logger.LogInformation("Message box '{0}' not shown ({1}): {2}", title, display.Reason, text);

        return Task.FromResult<EmptyStepResultContext?>(EmptyStepResultContext.Instance);
    }

    /// <summary>
    /// Creates a runtime instance for this trigger.
    /// </summary>
    /// <returns>The runtime step instance used during timeline execution.</returns>
    public override StepInstance<Step<EmptyStepResultContext>, EmptyStepResultContext> GetInstance() => new(this);

    /// <summary>
    /// Declares the variable inputs consumed by the message box.
    /// </summary>
    /// <param name="contract">The IO contract to populate.</param>
    public override void DeclareIO(StepIOContract contract)
    {
        contract.Input(msg);
        contract.Input(caption);
    }
}
