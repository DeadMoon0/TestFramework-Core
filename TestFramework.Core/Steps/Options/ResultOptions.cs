using TestFramework.Core;
using System;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes the projected outputs that should be extracted from a returned step result context.
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
    public void Freeze()
    {
        ResultBindings.Freeze();
        IsFrozen = true;
    }

    /// <summary>
    /// Gets the configured projected result bindings for the step.
    /// </summary>
    public IFreezableCollection<ResultBinding> ResultBindings { get; } = new FreezableCollection<ResultBinding>();

    /// <summary>
    /// Gets a value indicating whether any result bindings are configured.
    /// </summary>
    public bool HasBindings => ResultBindings.Count > 0;

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(ResultOptions target)
    {
        foreach (ResultBinding binding in ResultBindings)
            target.ResultBindings.Add(binding);
    }
}

/// <summary>
/// Describes how a property from a step result context should be projected into the variable store.
/// </summary>
public sealed record ResultBinding
{
    internal ResultBinding(
        VariableIdentifier variable,
        string memberPath,
        Type declaredType,
        Func<object?, object?> accessor)
    {
        this.Variable = variable;
        this.MemberPath = memberPath;
        this.DeclaredType = declaredType;
        this.Accessor = accessor;
    }

    /// <summary>
    /// The destination variable that receives the projected value.
    /// </summary>
    public VariableIdentifier Variable { get; internal init; }

    /// <summary>
    /// The selected member-access path on the result context.
    /// </summary>
    public string MemberPath { get; internal init; }

    /// <summary>
    /// The declared CLR type of the projected value.
    /// </summary>
    public Type DeclaredType { get; internal init; }

    /// <summary>
    /// Accessor used to evaluate the projected value at runtime.
    /// </summary>
    public Func<object?, object?> Accessor { get; internal init; }
}