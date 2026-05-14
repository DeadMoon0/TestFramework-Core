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
/// <param name="Variable">The destination variable that receives the projected value.</param>
/// <param name="MemberPath">The selected member-access path on the result context.</param>
/// <param name="DeclaredType">The declared CLR type of the projected value.</param>
/// <param name="Accessor">Accessor used to evaluate the projected value at runtime.</param>
public sealed record ResultBinding(VariableIdentifier Variable, string MemberPath, Type DeclaredType, Func<object?, object?> Accessor);