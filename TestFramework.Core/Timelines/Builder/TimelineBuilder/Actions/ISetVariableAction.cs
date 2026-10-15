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
/// which the plan check cannot see. So the hidden overloads below make passing a reference to
/// <see cref="SetVariable{T}(VariableIdentifier, T)"/> a compile error that names
/// <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/>, and the value verb refuses one that
/// reaches it anyway.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISetVariableAction : IImplementedByCore
{
    /// <summary>
    /// What the compiler says when a variable is passed where a value is stored.
    /// </summary>
    internal const string NotAValue = "A variable is not a value: SetVariable stores what it is given. Use SetVariableFrom(name, Var.Ref<T>(...)) to copy another variable.";

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

    /// <summary>Refused: a variable is not a value. Use <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/>.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="variable">The variable that was meant to be copied.</param>
    /// <returns>Never returns.</returns>
    [Obsolete(NotAValue, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable);

    /// <summary>Refused: a variable is not a value. Use <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/>.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="variable">The variable that was meant to be copied.</param>
    /// <returns>Never returns.</returns>
    [Obsolete(NotAValue, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, ResolvableVariable<T> variable);

    /// <summary>Refused: a variable is not a value. Use <see cref="SetVariableFrom{T}(VariableIdentifier, VariableReference{T})"/>.</summary>
    /// <typeparam name="TVar">The wrapped reference.</typeparam>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="identifier">The variable to set.</param>
    /// <param name="variable">The variable that was meant to be copied.</param>
    /// <returns>Never returns.</returns>
    [Obsolete(NotAValue, error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<TVar, T>(VariableIdentifier identifier, ImmutableVariable<TVar, T> variable)
        where TVar : VariableReference<T>;
}
