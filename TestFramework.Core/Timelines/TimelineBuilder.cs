using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Variables;
using TestFramework.Core.Steps.Preprocessor;
using TestFramework.Core.Steps.SystemSteps;

namespace TestFramework.Core.Timelines;

internal class TimelineBuilder : ITimelineBuilderModifier
{
    internal readonly PreProcessableStage _mainStageEmitters = new PreProcessableStage() { Name = "Main Stage", Description = "The Stage where all Main Steps are Executed." };

    internal TimelineBuilder() { }

    public Timeline Build()
    {
        _mainStageEmitters.Freeze();

        Timeline timeline = new Timeline(_mainStageEmitters);
        timeline.ReadyToRun = true;
        timeline.Freeze();
        return timeline;
    }

    public ITimelineBuilderModifier RegisterArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, (TArtifactReference)reference)));
        return this;
    }

    public ITimelineBuilderModifier CaptureArtifactVersion(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new CaptureArtifactVersionStep(identifier, ArtifactVersionIdentifier.Default)));
        return this;
    }

    public ITimelineBuilderModifier CaptureArtifactVersion(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new CaptureArtifactVersionStep(identifier, versionIdentifier)));
        return this;
    }

    public ITimelineBuilderModifier RemoveArtifact(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new DeconstructArtifactStep(identifier)));
        return this;
    }

    public ITimelineBuilderModifier SetupArtifact(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new SetupArtifactStep(identifier)));
        return this;
    }

    public ITimelineBuilderModifier SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new SetVariableStep(identifier, variable)));
        return this;
    }

    public ITimelineBuilderModifier Trigger<TResult>(Step<TResult> triggerStep)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(triggerStep));
        return this;
    }

    public ITimelineBuilderModifier WaitForEvent<TEvent, TResult>(Event<TEvent, TResult> sourceEvent) where TEvent : Event<TEvent, TResult>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(sourceEvent));
        return this;
    }

    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer) => Transform(toVariable, fromVariable, (x) => Task.FromResult(transformer(x)));
    public ITimelineBuilderModifier Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new TransformStep<TFrom, TTo>(toVariable, fromVariable, transformer)));
        return this;
    }

    public ITimelineBuilderModifier AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new AssertVariableStep<T>(identifier, predicate)));
        return this;
    }

    public ITimelineBuilder Conditional(bool shouldRun, Action<ITimelineBuilder> steps) => Conditional((ImmutableVariable<ConstVariable<bool>, bool>)Var.Const(shouldRun), steps);
    public ITimelineBuilder Conditional<TVar>(ImmutableVariable<TVar, bool> shouldRun, Action<ITimelineBuilder> steps) where TVar : VariableReference<bool>
    {
        _mainStageEmitters.Steps.Add(new ConditionalStepEmitter(shouldRun, steps));
        return this;
    }

    public ITimelineBuilder ForEach<TItem>(IEnumerable<TItem> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) => ForEach((ImmutableVariable<ConstVariable<IEnumerable<TItem>>, IEnumerable<TItem>>)Var.Const(collection), variable, steps);
    public ITimelineBuilder ForEach<TItem>(TItem[] collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) => ForEach((ImmutableVariable<ConstVariable<TItem[]>, TItem[]>)Var.Const(collection), variable, steps);
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, TItem[]> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<TItem[]> => ForEach(new ImmutableVariable<VariableReference<IEnumerable<TItem>>, IEnumerable<TItem>>(collection.Transform(x => (IEnumerable<TItem>?)x)), variable, steps);
    public ITimelineBuilder ForEach<TVar, TItem>(ImmutableVariable<TVar, IEnumerable<TItem>> collection, VariableIdentifier variable, Action<ITimelineBuilder> steps) where TVar : VariableReference<IEnumerable<TItem>>
    {
        _mainStageEmitters.Steps.Add(new ForEachStepEmitter<TItem>(collection, variable, steps));
        return this;
    }

    public ITimelineBuilder FindArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>([identifier], finder, true)));

        return this;
    }

    public ITimelineBuilder FindArtifactMulti<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier[] identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifiers, finder, false)));
        return this;
    }

    public ITimelineBuilderModifier WithTimeOut(VariableReference<TimeSpan> timeout)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            variableTracker.GetReference(timeout);
            step.TimeOutOptions.TimeOut = timeout;
        });
        return this;
    }

    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay) => WithRetry(maxRetryCount, (VariableReference<CalcDelay>)calcDelay);
    public ITimelineBuilderModifier WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            variableTracker.GetReference(maxRetryCount);
            variableTracker.GetReference(calcDelay);
            step.RetryOptions.MaxRetryCount = maxRetryCount;
            step.RetryOptions.CalcDelay = calcDelay;
        });

        return this;
    }

    public ITimelineBuilderModifier ExpectExceptions(params Type[] exceptionTypes)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            foreach (var type in exceptionTypes)
            {
                if (!type.IsAssignableTo(typeof(Exception))) throw new InvalidOperationException("Only Exception Types are Allowed.");
                step.ErrorHandlingOptions.IgnoreExceptionTypes.Add(type);
            }
        });

        return this;
    }

    public ITimelineBuilderModifier Name(string label)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            step.LabelOptions.Label = label;
        });

        return this;
    }

    public ITimelineBuilderModifier RunExclusively()
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            step.ExecutionOptions.RunExclusively = true;
        });

        return this;
    }

}