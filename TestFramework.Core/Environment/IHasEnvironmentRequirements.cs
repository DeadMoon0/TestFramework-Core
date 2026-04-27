using System.Collections.Generic;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

/// <summary>
/// Defines a type that can declare the environment requirements it needs for execution.
/// </summary>
public interface IHasEnvironmentRequirements
{
    /// <summary>
    /// Returns the environment requirements needed to execute the current instance.
    /// </summary>
    /// <param name="variableStore">The current variable store that can influence the required environment.</param>
    IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore);
}