using TestFramework.Core.Steps;
using TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder;

/// <summary>
/// The modifier returned by the verbs that produce artifacts, so <c>MarkReadonly()</c> is offered
/// only where there is an artifact to mark.
/// </summary>
/// <remarks>
/// This is <see cref="ITimelineBuilderModifier{TStepResultContext}"/> plus one verb, which keeps the
/// choice a compile-time matter: chaining <c>MarkReadonly()</c> onto a step that produces no artifact
/// does not compile, rather than failing at build or run time.
/// </remarks>
/// <typeparam name="TStepResultContext">The result context type produced by the current step.</typeparam>
public interface IArtifactTimelineBuilderModifier<TStepResultContext> :
    ITimelineBuilderModifier<TStepResultContext>,
    IMarkReadonlyModAction<TStepResultContext>
    where TStepResultContext : StepResultContext;
