using TestFramework.Core;
using TestFramework.Core.Steps.Preprocessor;

namespace TestFramework.Core.Stages;

/// <summary>
/// Represents a stage before preprocessing, containing step emitters instead of concrete step instances.
/// </summary>
public class PreProcessableStage : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the stage has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the stage and its emitter collection.
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
    /// Gets the step emitters contained in the stage before preprocessing.
    /// </summary>
    public IFreezableCollection<StepEmitter> Steps { get; } = new FreezableCollection<StepEmitter>();
}