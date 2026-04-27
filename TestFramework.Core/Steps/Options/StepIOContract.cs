namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Describes the declared input and output dependencies of a step.
/// </summary>
public class StepIOContract : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the contract has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the contract and its declared input and output collections.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        Inputs.Freeze();
        Outputs.Freeze();
    }

    /// <summary>
    /// Gets the declared input dependencies.
    /// </summary>
    public IFreezableCollection<StepIOEntry> Inputs { get; } = new FreezableCollection<StepIOEntry>();

    /// <summary>
    /// Gets the declared output dependencies.
    /// </summary>
    public IFreezableCollection<StepIOEntry> Outputs { get; } = new FreezableCollection<StepIOEntry>();

    /// <summary>
    /// Gets a value indicating whether the contract declares any inputs or outputs.
    /// </summary>
    public bool HasDeclarations => Inputs.Count > 0 || Outputs.Count > 0;

    /// <summary>
    /// Copies the current contract entries to another instance.
    /// </summary>
    /// <param name="target">The target contract instance.</param>
    public void CloneTo(StepIOContract target)
    {
        foreach (var input in Inputs) target.Inputs.Add(input);
        foreach (var output in Outputs) target.Outputs.Add(output);
    }
}
