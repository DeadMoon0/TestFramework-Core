using TestFramework.Core.Steps;

namespace TestFramework.Core.Stages;

public class Stage : IFreezable
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

    public IFreezableCollection<StepGeneric> Steps { get; } = new FreezableCollection<StepGeneric>();
}