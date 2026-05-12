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
}