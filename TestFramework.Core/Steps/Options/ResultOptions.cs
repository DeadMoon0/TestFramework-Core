using System;
using TestFramework.Core;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Configures which variable receives the returned step result and how that result is declared in the IO contract.
/// </summary>
public class ResultOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private VariableIdentifier _resultVariable = new VariableIdentifier("out");
    private Type? _declaredType;

    /// <summary>
    /// Gets or sets the variable that receives the returned step result.
    /// Defaults to the conventional "out" variable.
    /// </summary>
    public VariableIdentifier ResultVariable { get => _resultVariable; set { ((IFreezable)this).EnsureNotFrozen(); _resultVariable = value; } }

    /// <summary>
    /// Gets or sets the declared contract type for the result variable.
    /// When null, the result output is tracked without an explicit declared type.
    /// </summary>
    public Type? DeclaredType { get => _declaredType; set { ((IFreezable)this).EnsureNotFrozen(); _declaredType = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(ResultOptions target)
    {
        target.ResultVariable = ResultVariable;
        target.DeclaredType = DeclaredType;
    }
}