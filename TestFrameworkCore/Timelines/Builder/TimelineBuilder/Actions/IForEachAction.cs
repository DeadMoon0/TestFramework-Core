using System;
using System.Collections.Generic;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Timelines.Builder.TimelineBuilder.Actions;

public interface IForEachAction
{
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<IEnumerable<TItem>>;
    public ITimelineBuilder ForEach<TItem>(IEnumerable<TItem> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps);
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, TItem[]> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<TItem[]>;
    public ITimelineBuilder ForEach<TItem>(TItem[] collection, VariableIdentifier variable, Action<ITimelineBuilder> steps);
}