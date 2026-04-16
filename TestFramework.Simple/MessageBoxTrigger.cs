using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Logging;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Variables;

namespace TestFrameworkSimple;

public class MessageBoxTrigger(VariableReference<string> msg, VariableReference<string> caption) : Step<object?>
{
    public enum MessageBoxIconWin32 : uint
    {
        None = 0x00000000, // no icon (default when no MB_ICON* flag is set)

        MBICONHAND = 0x00000010, // same as MB_ICONSTOP, critical error icon
        MBICONSTOP = MBICONHAND,
        MBICONERROR = MBICONHAND,

        MBICONQUESTION = 0x00000020, // question mark icon (deprecated for dialogs; use caution)

        MBICONEXCLAMATION = 0x00000030, // same as MB_ICONWARNING
        MBICONWARNING = MBICONEXCLAMATION,

        MBICONASTERISK = 0x00000040, // same as MB_ICONINFORMATION
        MBICONINFORMATION = MBICONASTERISK
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public override string Name => "MessageBox Trigger";

    public override string Description => "A Trigger which shows an MessageBox";

    public override bool DoesReturn => false;

    public override Step<object?> Clone()
    {
        return new MessageBoxTrigger(msg, caption).WithClonedOptions(this);
    }

    public override Task<object?> Execute(IServiceProvider serviceProvider, VariableStore variableStore, ArtifactStore artifactStore, ScopedLogger logger, CancellationToken cancellationToken)
    {
        MessageBox(IntPtr.Zero, msg.GetRequiredValue(variableStore), caption.GetRequiredValue(variableStore), (uint)MessageBoxIconWin32.None);
        return Task.FromResult((object?)null);
    }

    public override StepInstance<Step<object?>, object?> GetInstance() => new StepInstance<Step<object?>, object?>(this);

    public override void DeclareIO(StepIOContract contract)
    {
        if (msg.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(msg.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
        if (caption.HasIdentifier)
            contract.Inputs.Add(new StepIOEntry(caption.Identifier!.Identifier, StepIOKind.Variable, true, typeof(string)));
    }
}
