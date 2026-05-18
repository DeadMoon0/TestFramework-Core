namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes the workflow intent of a step for layer planning within the main stage.
/// </summary>
public enum StepExecutionPhase
{
    /// <summary>
    /// Establishes prerequisites such as variables, setup, or environment state.
    /// </summary>
    Prepare,

    /// <summary>
    /// Causes work to happen in the system under test or the outside world.
    /// </summary>
    Act,

    /// <summary>
    /// Waits for or inspects the consequences of prior work.
    /// </summary>
    Observe,

    /// <summary>
    /// Binds or captures produced results into explicit runtime state such as artifacts.
    /// </summary>
    Materialize
}