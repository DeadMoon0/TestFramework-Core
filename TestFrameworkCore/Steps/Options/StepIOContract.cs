namespace TestFrameworkCore.Steps.Options;

public class StepIOContract : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
        Inputs.Freeze();
        Outputs.Freeze();
    }

    public IFreezableCollection<StepIOEntry> Inputs { get; } = new FreezableCollection<StepIOEntry>();
    public IFreezableCollection<StepIOEntry> Outputs { get; } = new FreezableCollection<StepIOEntry>();

    public bool HasDeclarations => Inputs.Count > 0 || Outputs.Count > 0;

    public void CloneTo(StepIOContract target)
    {
        foreach (var input in Inputs) target.Inputs.Add(input);
        foreach (var output in Outputs) target.Outputs.Add(output);
    }
}
