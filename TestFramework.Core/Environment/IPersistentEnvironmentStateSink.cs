namespace TestFramework.Core.Environment;

/// <summary>
/// Accepts persistent component state seeded by a persistent environment context when a fresh environment instance is created.
/// </summary>
public interface IPersistentEnvironmentStateSink
{
    /// <summary>
    /// Seeds a previously created persistent component state into the environment instance.
    /// </summary>
    void SetPersistentState(EnvComponentIdentifier identifier, object? state);
}