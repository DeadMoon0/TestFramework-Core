using TestFramework.Core;

namespace TestFramework.Core.Steps.Options;

public class ExecutionOptions : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private bool _runExclusively = false;

    /// <summary>
    /// When true, this step must not run concurrently with any other step.
    /// </summary>
    public bool RunExclusively { get => _runExclusively; set { ((IFreezable)this).EnsureNotFrozen(); _runExclusively = value; } }

    public void CloneTo(ExecutionOptions target)
    {
        target.RunExclusively = RunExclusively;
    }
}
