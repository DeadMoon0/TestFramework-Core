using System;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps.Options;

public class TimeOutOptions : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private VariableReference<TimeSpan> _timeOut = TimeSpan.FromMinutes(10);
    public VariableReference<TimeSpan> TimeOut { get => _timeOut; set { ((IFreezable)this).EnsureNotFrozen(); _timeOut = value; } }

    public void CloneTo(TimeOutOptions target)
    {
        target.TimeOut = TimeOut;
    }
}
