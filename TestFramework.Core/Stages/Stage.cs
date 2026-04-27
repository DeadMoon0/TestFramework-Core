using TestFramework.Core.Steps;

namespace TestFramework.Core.Stages;

/// <summary>
/// Represents an executable stage containing concrete step definitions.
/// </summary>
public class Stage : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the stage has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the stage and its step collection.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        Steps.Freeze();
    }

    private string _name = "";

    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string Name { get => _name; set { ((IFreezable)this).EnsureNotFrozen(); _name = value; } }

    private string _description = "";

    /// <summary>
    /// Gets or sets the stage description.
    /// </summary>
    public string Description { get => _description; set { ((IFreezable)this).EnsureNotFrozen(); _description = value; } }

    /// <summary>
    /// Gets the steps contained in the stage.
    /// </summary>
    public IFreezableCollection<StepGeneric> Steps { get; } = new FreezableCollection<StepGeneric>();
}