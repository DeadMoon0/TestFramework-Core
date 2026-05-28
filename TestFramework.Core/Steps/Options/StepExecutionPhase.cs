namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes the workflow intent of a step for layer planning within the main stage.
/// </summary>
public enum StepExecutionPhase
{
    /// <summary>
    /// Establishes prerequisites such as variables, setup, or environment state.
    /// Steps in this phase may be merged into the same execution layer when their IO contracts do not conflict.
    /// </summary>
    Prepare,

    /// <summary>
    /// Causes work to happen in the system under test or the outside world.
    /// The built-in planner keeps authored act steps sequential to preserve causal readability and side-effect ordering.
    /// </summary>
    Act,

    /// <summary>
    /// Waits for or inspects the consequences of prior work.
    /// The built-in planner keeps authored observe steps sequential so observation order stays explicit.
    /// </summary>
    Observe,

    /// <summary>
    /// Binds or captures produced results into explicit runtime state such as artifacts.
    /// Steps in this phase may be merged into the same execution layer when their IO contracts do not conflict.
    /// </summary>
    Materialize
}