using TestFramework.Core.Variables;

namespace TestFramework.Core.Steps;

/// <summary>
/// A step that needs a cleanup step to run in the Cleanup Stage after the main stage completes.
/// Return null from CreateCleanupStep() when no cleanup is needed.
/// </summary>
public interface IHasCleanupStep
{
    /// <summary>
    /// Creates the cleanup step that should run after the main stage finishes.
    /// </summary>
    StepGeneric? CreateCleanupStep(VariableStore variableStore);
}
