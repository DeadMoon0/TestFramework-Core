using TestFramework.Core;
using TestFramework.Core.Steps.Preprocessor;

namespace TestFramework.Core.Stages;

public class PreProcessableStage : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        Steps.Freeze();
    }

    private string _name = "";
    public string Name { get => _name; set { ((IFreezable)this).EnsureNotFrozen(); _name = value; } }

    private string _description = "";
    public string Description { get => _description; set { ((IFreezable)this).EnsureNotFrozen(); _description = value; } }

    public IFreezableCollection<StepEmitter> Steps { get; } = new FreezableCollection<StepEmitter>();
}