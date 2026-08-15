using Microsoft.Extensions.DependencyInjection;

namespace TestFramework.Config.Builder.InstanceBuilder.Actions;

/// <summary>
/// Materializes the fluent configuration builder into reusable runtime objects.
/// </summary>
public interface IBuildAction
{
    /// <summary>
    /// Builds a reusable <see cref="ConfigInstance"/> snapshot.
    /// </summary>
    /// <returns>The materialized configuration instance.</returns>
    public ConfigInstance Build();

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> directly from the current builder state.
    /// </summary>
    /// <returns>
    /// The resolved service provider. The caller owns it and is responsible for disposing it.
    /// </returns>
    public ServiceProvider BuildServiceProvider();
}