using System.Linq;
using TestFrameworkCore.Steps;

namespace TestFrameworkCore.Stages;

public class StageInstance : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        Result.Freeze();
        Stage.Freeze();
    }

    public StageResult Result { get; } = new StageResult();

    public Stage Stage { get; private set; }

    public IFreezableCollection<StepInstanceGeneric> Steps { get; }

    internal StageInstance(Stage stage)
    {
        Stage = stage;
        Steps = (FreezableCollection<StepInstanceGeneric>)[.. stage.Steps.Select(x => x.GetInstanceGeneric())];
    }
}