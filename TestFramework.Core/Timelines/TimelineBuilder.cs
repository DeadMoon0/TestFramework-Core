using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Events;
using TestFramework.Core.Stages;
using TestFramework.Core.Steps;
using TestFramework.Core.Steps.Options;
using TestFramework.Core.Timelines.Builder.TimelineBuilder;
using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;
using TestFramework.Core.Variables;
using TestFramework.Core.Steps.Preprocessor;
using TestFramework.Core.Steps.SystemSteps;

namespace TestFramework.Core.Timelines;

internal class TimelineBuilder : ITimelineBuilder
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

    public ITimelineBuilderModifier<EmptyStepResultContext> RegisterArtifact<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier identifier, ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData> reference)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new RegisterArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, (TArtifactReference)reference)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new CaptureArtifactVersionStep(identifier, ArtifactVersionIdentifier.Default)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> CaptureArtifactVersion(ArtifactIdentifier identifier, ArtifactVersionIdentifier versionIdentifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new CaptureArtifactVersionStep(identifier, versionIdentifier)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> RemoveArtifact(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new DeconstructArtifactStep(identifier)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> SetupArtifact(ArtifactIdentifier identifier)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new SetupArtifactStep(identifier)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> SetVariable<T>(VariableIdentifier identifier, VariableReference<T> variable)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new SetVariableStep(identifier, variable)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<TStepResultContext> Trigger<TStepResultContext>(Step<TStepResultContext> triggerStep) where TStepResultContext : StepResultContext
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(triggerStep));
        return AsTypedModifier<TStepResultContext>();
    }

    public ITimelineBuilderModifier<TStepResultContext> WaitForEvent<TEvent, TStepResultContext>(Event<TEvent, TStepResultContext> sourceEvent)
        where TEvent : Event<TEvent, TStepResultContext>
        where TStepResultContext : StepResultContext
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(sourceEvent));
        return AsTypedModifier<TStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, TTo> transformer) => Transform(toVariable, fromVariable, (x) => Task.FromResult(transformer(x)));
    public ITimelineBuilderModifier<EmptyStepResultContext> Transform<TFrom, TTo>(VariableIdentifier toVariable, VariableReference<TFrom> fromVariable, Func<TFrom?, Task<TTo>> transformer)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new TransformStep<TFrom, TTo>(toVariable, fromVariable, transformer)));
        return AsTypedModifier<EmptyStepResultContext>();
    }

    public ITimelineBuilderModifier<EmptyStepResultContext> AssertVariable<T>(VariableReference<T> identifier, Func<T?, bool> predicate)
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new AssertVariableStep<T>(identifier, predicate)));
        return AsTypedModifier<EmptyStepResultContext>();
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
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifier, finder)));

        return this;
    }

    public ITimelineBuilder FindArtifacts<TArtifactReference, TArtifactDescriber, TArtifactData>(ArtifactIdentifier baseName, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(baseName, finder, FindArtifactNamingMode.Generated)));
        return this;
    }

    public ITimelineBuilder FindArtifactsAs<TArtifactReference, TArtifactDescriber, TArtifactData>(IReadOnlyList<ArtifactIdentifier> identifiers, ArtifactFinder<TArtifactDescriber, TArtifactData, TArtifactReference> finder)
        where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
        where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
        where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    {
        _mainStageEmitters.Steps.Add(new SingleStepEmitter(new FindArtifactStep<TArtifactDescriber, TArtifactData, TArtifactReference>(identifiers, finder)));
        return this;
    }

    internal void WithTimeOut(VariableReference<TimeSpan> timeout)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            variableTracker.GetReference(timeout);
            step.TimeOutOptions.TimeOut = timeout;
        });
    }

    internal void WithRetry(VariableReference<int> maxRetryCount, CalcDelay calcDelay) => WithRetry(maxRetryCount, (VariableReference<CalcDelay>)calcDelay);
    internal void WithRetry(VariableReference<int> maxRetryCount, VariableReference<CalcDelay> calcDelay)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            variableTracker.GetReference(maxRetryCount);
            variableTracker.GetReference(calcDelay);
            step.RetryOptions.MaxRetryCount = maxRetryCount;
            step.RetryOptions.CalcDelay = calcDelay;
        });
    }

    internal void ExpectExceptions(params Type[] exceptionTypes)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            foreach (var type in exceptionTypes)
            {
                if (!type.IsAssignableTo(typeof(Exception))) throw new InvalidOperationException("Only Exception Types are Allowed.");
                step.ErrorHandlingOptions.IgnoreExceptionTypes.Add(type);
            }
        });
    }

    internal void Name(string label)
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            step.LabelOptions.Label = label;
        });
    }

    internal void RunExclusively()
    {
        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            step.ExecutionOptions.RunExclusively = true;
        });
    }

    internal void BindResultProperty<TStepResultContext, TValue>(Expression<Func<TStepResultContext, TValue>> selector, VariableIdentifier identifier)
        where TStepResultContext : StepResultContext
    {
        if (selector is null) throw new ArgumentNullException(nameof(selector));

        string memberPath = GetMemberPath(selector.Body);
        Func<TStepResultContext, TValue> compiledSelector = selector.Compile();

        _mainStageEmitters.Steps.Last().AddModifier((step, variableTracker, artifactTracker) =>
        {
            if (!step.DoesReturn)
                throw new InvalidOperationException($"Step '{step.Name}' does not return a result context and cannot bind '{memberPath}' into variable '{identifier}'.");

            if (!typeof(TStepResultContext).IsAssignableFrom(step.ResultType))
                throw new InvalidOperationException($"Step '{step.Name}' returns '{step.ResultType.Name}' and cannot bind result context '{typeof(TStepResultContext).Name}'.");

            if (step.ResultOptions.ResultBindings.Any(existing => existing.Variable == identifier))
                throw new InvalidOperationException($"Step '{step.Name}' already binds a result value into variable '{identifier}'.");

            step.ResultOptions.ResultBindings.Add(new ResultBinding(
                identifier,
                memberPath,
                typeof(TValue),
                context =>
                {
                    if (context is null) return null;
                    return compiledSelector((TStepResultContext)context);
                }));
        });
    }

    private ITimelineBuilderModifier<TStepResultContext> AsTypedModifier<TStepResultContext>() where TStepResultContext : StepResultContext => new TypedTimelineBuilderModifier<TStepResultContext>(this);

    private static string GetMemberPath(Expression expression)
    {
        Expression current = expression;
        while (current is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            current = unary.Operand;

        Stack<string> segments = [];
        while (current is MemberExpression memberExpression)
        {
            segments.Push(memberExpression.Member.Name);
            current = memberExpression.Expression ?? throw new InvalidOperationException("Result binding expressions must resolve from the context parameter.");
        }

        if (current is not ParameterExpression)
            throw new InvalidOperationException("Only simple member-access expressions are supported for result bindings.");

        return string.Join(".", segments);
    }

}