using System.Collections.Generic;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Environment;

public interface IHasEnvironmentRequirements
{
    IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore);
}