namespace TestFrameworkCore.Stages;

public enum StageState
{
    NotRun,
    Complete,
    Error
}

public class StageResult : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private StageState _state = StageState.NotRun;
    public StageState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }
}