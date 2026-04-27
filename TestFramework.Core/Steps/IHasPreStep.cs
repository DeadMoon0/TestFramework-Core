using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps;

/// <summary>
/// A step that needs a preparatory step to run before any send-triggers in the same stage.
/// The pre-step is inserted at the START of the Main Stage, before regular steps.
/// </summary>
public interface IHasPreStep
{
    /// <summary>
    /// Creates the preparatory step that should run before the main stage begins.
    /// </summary>
    StepGeneric? CreatePreStep(VariableStore variableStore);
}
