namespace TestFramework.Core.Events;

using TestFramework.Core.Steps;

/// <summary>
/// Represents an event step that performs its polling asynchronously in a single logical operation.
/// </summary>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
/// <typeparam name="TStepResultContext">The result context type produced by the event.</typeparam>
public abstract class AsyncEvent<TEvent, TStepResultContext> : Event<TEvent, TStepResultContext>
	where TEvent : AsyncEvent<TEvent, TStepResultContext>
	where TStepResultContext : StepResultContext
{ }