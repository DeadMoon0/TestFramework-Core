using TestFramework.Core;

namespace TestFramework.Core.Steps.Options;

public class LabelOptions : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze() { IsFrozen = true; }

    private string? _label = null;
    public string? Label { get => _label; set { ((IFreezable)this).EnsureNotFrozen(); _label = value; } }

    public void CloneTo(LabelOptions target)
    {
        target.Label = Label;
    }
}
