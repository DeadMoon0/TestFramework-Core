using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines;

internal sealed class TypedTimelineBuilderModifier<TStepResultContext>(TimelineBuilder builder) : ITimelineBuilderModifier<TStepResultContext>
    where TStepResultContext : StepResultContext
{
    public Timeline Build() => builder.Build();

    public ITimelineBuilderModifier<EmptyStepResultContext> RegisterArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        => builder.RegisterArtifact(identifier, reference);

    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier) => builder.CaptureArtifactVersion(identifier);

    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier) => builder.CaptureArtifactVersion(identifier, versionIdentifier);

    public ITimelineBuilderModifier<EmptyStepResultContext> RemoveArtifact(ArtifactIdentifier identifier) => builder.RemoveArtifact(identifier);

    public ITimelineBuilderModifier<EmptyStepResultContext> SetupArtifact(ArtifactIdentifier identifier) => builder.SetupArtifact(identifier);

    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable) => builder.SetVariable(identifier, variable);

    public ITimelineBuilderModifier<TNextStepResultContext> Trigger<TNextStepResultContext>(Step<TNextStepResultContext> triggerStep) where TNextStepResultContext : StepResultContext => builder.Trigger(triggerStep);

    public ITimelineBuilderModifier<TNextStepResultContext> WaitForEvent<TEvent, TNextStepResultContext>(Event<TEvent, TNextStepResultContext> sourceEvent)
        where TEvent : Event<TEvent, TNextStepResultContext>
        where TNextStepResultContext : StepResultContext
        => builder.WaitForEvent(sourceEvent);

    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer) => builder.Transform(toVariable, fromVariable, transformer);

    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer) => builder.Transform(toVariable, fromVariable, transformer);

    public ITimelineBuilderModifier<EmptyStepResultContext> AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate) => builder.AssertVariable(identifier, predicate);

    public ITimelineBuilder Conditional(bool shouldRun, Action<ITimelineBuilder> steps) => builder.Conditional(shouldRun, steps);

    public ITimelineBuilder Conditional<TVar>(ImmutableVariable<TVar, bool> shouldRun, Action<ITimelineBuilder> steps) where TVar : VariableReference<bool> => builder.Conditional(shouldRun, steps);

    public ITimelineBuilder ForEach<TItem>(IEnumerable<TItem> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) => builder.ForEach(collection, variable, steps);

    public ITimelineBuilder ForEach<TItem>(TItem[] collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) => builder.ForEach(collection, variable, steps);

    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, TItem[]> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<TItem[]> => builder.ForEach(collection, variable, steps);

    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<IEnumerable<TItem>> => builder.ForEach(collection, variable, steps);

    public ITimelineBuilder FindArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        => builder.FindArtifact(identifier, finder);

    public ITimelineBuilder FindArtifacts<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier baseName, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        => builder.FindArtifacts(baseName, finder);

    public ITimelineBuilder FindArtifactsAs<TArtifactReference, TArtifactDescriber, TArtifactData>(IReadOnlyList<ArtifactIdentifier> identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
        => builder.FindArtifactsAs(identifiers, finder);

    public ITimelineBuilderModifier<TStepResultContext> WithTimeOut(VariableReference<TimeSpan> timeout)
    {
        builder.WithTimeOut(timeout);
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay)
    {
        builder.WithRetry(maxRetryCount, calcDelay);
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay)
    {
        builder.WithRetry(maxRetryCount, calcDelay);
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> ExpectExceptions(params Type[] exceptionTypes)
    {
        builder.ExpectExceptions(exceptionTypes);
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> Name(string label)
    {
        builder.Name(label);
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> DoNotParallelize()
    {
        builder.DoNotParallelize();
        return this;
    }

    public ITimelineBuilderModifier<TStepResultContext> BindResultProperty<TValue>(Expression<Func<TStepResultContext, TValue>> selector, VariableIdentifier key)
    {
        builder.BindResultProperty(selector, key);
        return this;
    }
}