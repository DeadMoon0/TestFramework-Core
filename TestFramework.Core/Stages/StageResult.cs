using TestFramework.Core;

namespace TestFramework.Core.Stages;

/// <summary>
/// Represents the execution state of a stage.
/// </summary>
public enum StageState
{
    /// <summary>
    /// The stage has not been executed yet.
    /// </summary>
    NotRun,

    /// <summary>
    /// The stage completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// The stage ended with an error.
    /// </summary>
    Error
}

/// <summary>
/// Represents the result state of a stage execution.
/// </summary>
public class StageResult : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the stage result has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the stage result against further mutation.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private StageState _state = StageState.NotRun;

    /// <summary>
    /// Gets or sets the execution state of the stage.
    /// </summary>
    public StageState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }
}