using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Steps;

/// <summary>
/// A step that needs a cleanup step to run in the Cleanup Stage after the main stage completes.
/// Return null from CreateCleanupStep() when no cleanup is needed.
/// </summary>
public interface IHasCleanupStep
{
    StepGeneric? CreateCleanupStep(VariableStore variableStore);
}
