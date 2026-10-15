using System;
using System.ComponentModel;
using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for assigning a variable in the timeline: a value, or another variable's value.
/// </summary>
/// <remarks>
/// <para>
/// Two verbs, because C# cannot infer a generic type through an implicit conversion: one verb taking a
/// <see cref="VariableReference{T}"/> made every plain value need <c>Var.Const(...)</c>, and one verb taking a plain
/// value would have stored a <c>Var.Ref(...)</c> as the value. <see cref="SetVariable{T}(VariableIdentifier, T)"/>
/// takes what to store; <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/> takes the
/// variable to copy.
/// </para>
/// <para>
/// A variable is never a value: a reference stored as data could only be read by whatever step unwrapped it later,
/// which the plan check cannot see. So <see cref="SetVariable{T}(VariableIdentifier, T)"/> refuses a reference
/// while the timeline is built, naming <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/>.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetVariableAction : IImplementedByCore
{
    /// <summary>
    /// Adds a step that stores a value under the identifier.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="value">What to store.</param>
    /// <returns>The modifier for the step.</returns>
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, T value);

    /// <summary>
    /// Adds a step that stores another variable's value under the identifier, read when the step runs.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="source">The variable whose value is copied.</param>
    /// <returns>The modifier for the step.</returns>
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariableFrom<T>(VariableIdentifier identifier, VariableReference<T> source);

    /// <summary>
    /// Stores a constant: a constant is a value, so this is <see cref="SetVariable{T}(VariableIdentifier, T)"/>.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="value">The constant.</param>
    /// <returns>The modifier for the step.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, ConstVariable<T> value);

}
