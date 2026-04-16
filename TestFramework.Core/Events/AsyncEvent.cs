namespace TestFramework.Core.Events;

public abstract class AsyncEvent<TEvent, TResult> : Event<TEvent, TResult> where TEvent : AsyncEvent<TEvent, TResult> { }