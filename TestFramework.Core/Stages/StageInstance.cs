using System.Linq;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Stages;

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