namespace TestFramework.Core.Environment;

/// <summary>
/// Defines how an environment component participates in cross-run reuse.
/// </summary>
public enum EnvComponentReuseMode
{
    /// <summary>
    /// The component is created and deconstructed for each run.
    /// </summary>
    PerRun = 0,

    /// <summary>
    /// The component may be created once by a persistent environment context and reused across runs.
    /// </summary>
    PersistentContext = 1,
}