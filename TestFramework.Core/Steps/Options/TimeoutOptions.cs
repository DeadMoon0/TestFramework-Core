using System;
using TestFramework.Core;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Configures the timeout applied to a step.
/// </summary>
public class TimeoutOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private VariableReference<TimeSpan> _timeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the timeout value applied to the step.
    /// </summary>
    /// <remarks>
    /// Defaults to 10 minutes. The default is part of the contract. <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
    /// gives the step no deadline at all.
    /// </remarks>
    public VariableReference<TimeSpan> Timeout { get => _timeout; set { ((IFreezable)this).EnsureNotFrozen(); _timeout = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(TimeoutOptions target)
    {
        target.Timeout = Timeout;
    }
}
