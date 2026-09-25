using System.Collections.Generic;
using TestFramework.Core.Environment.Graph;
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

    /// <summary>
    /// Returns the requirements with the run's declared resources in view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the overload the engine calls, once, while it plans the run - after every source has
    /// declared its resources and before anything starts. A requirement that depends on what the run
    /// declares is decided here by the piece that owns the convention: a browser that takes its address
    /// from the site of the same name, a hosted call whose service is registered under its open generic
    /// definition. The engine then checks every requirement against the same resources, so whatever this
    /// returns has to name one of them.
    /// </para>
    /// <para>
    /// A default that forwards to <see cref="GetEnvironmentRequirements(VariableStore)"/>, so a piece
    /// compiled against an earlier engine keeps declaring exactly what it did.
    /// </para>
    /// </remarks>
    /// <param name="variableStore">The current variable store that can influence the required environment.</param>
    /// <param name="resources">Every resource this run declares, from configuration, fixtures and environments.</param>
    /// <returns>The requirements.</returns>
    IReadOnlyCollection<EnvironmentRequirement> GetEnvironmentRequirements(VariableStore variableStore, ResourceGraph resources)
        => this.GetEnvironmentRequirements(variableStore);
}