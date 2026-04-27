namespace TestFramework.Core.Events;

/// <summary>
/// Represents an event step that performs its polling asynchronously in a single logical operation.
/// </summary>
/// <typeparam name="TEvent">The concrete event type.</typeparam>
/// <typeparam name="TResult">The result type produced by the event.</typeparam>
public abstract class AsyncEvent<TEvent, TResult> : Event<TEvent, TResult> where TEvent : AsyncEvent<TEvent, TResult> { }