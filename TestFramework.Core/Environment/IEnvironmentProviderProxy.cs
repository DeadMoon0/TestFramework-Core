namespace TestFramework.Core.Environment;

/// <summary>
/// Exposes the underlying environment provider when an implementation wraps another provider.
/// </summary>
public interface IEnvironmentProviderProxy : IEnvironmentProvider
{
    /// <summary>
    /// Gets the wrapped environment provider.
    /// </summary>
    IEnvironmentProvider InnerEnvironment { get; }

    /// <summary>
    /// A wrapper serves what the environment it wraps serves.
    /// </summary>
    /// <param name="resourceKind">The kind.</param>
    /// <returns>The wrapped environment's answer.</returns>
    bool IEnvironmentProvider.ProvidesEveryResourceOf(string resourceKind) => this.InnerEnvironment.ProvidesEveryResourceOf(resourceKind);
}