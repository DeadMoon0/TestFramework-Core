namespace TestFramework.Core.Steps;

/// <summary>
/// Base type for all result contexts returned by executable steps.
/// </summary>
public abstract record StepResultContext;

/// <summary>
/// Shared empty result context for steps that do not expose any bindable outputs.
/// </summary>
public sealed record EmptyStepResultContext : StepResultContext
{
    /// <summary>
    /// Gets the singleton empty result context instance.
    /// </summary>
    public static EmptyStepResultContext Instance { get; } = new();

    private EmptyStepResultContext() { }
}