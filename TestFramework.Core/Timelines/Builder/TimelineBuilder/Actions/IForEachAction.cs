using System;
using System.Collections.Generic;
using TestFramework.Core.Variables;

using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

/// <summary>
/// Adds the fluent verbs for composing repeated nested steps over collections.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IForEachAction
{
    /// <summary>
    /// Repeats a nested builder for each item in an immutable enumerable variable reference.
    /// </summary>
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<IEnumerable<TItem>>;

    /// <summary>
    /// Repeats a nested builder for each item in a constant enumerable.
    /// </summary>
    public ITimelineBuilder ForEach<TItem>(IEnumerable<TItem> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps);

    /// <summary>
    /// Repeats a nested builder for each item in an immutable array variable reference.
    /// </summary>
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, TItem[]> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<TItem[]>;

    /// <summary>
    /// Repeats a nested builder for each item in a constant array.
    /// </summary>
    public ITimelineBuilder ForEach<TItem>(TItem[] collection, VariableIdentifier variable, Action<ITimelineBuilder> steps);
}