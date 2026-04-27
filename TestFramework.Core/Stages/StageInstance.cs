using System.Linq;
using TestFramework.Core.Steps;

namespace TestFramework.Core.Stages;

/// <summary>
/// Represents a runtime instance of a stage and the step instances it contains.
/// </summary>
public class StageInstance : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the stage instance has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the stage instance, its result, and its stage definition.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
        Result.Freeze();
        Stage.Freeze();
    }

    /// <summary>
    /// Gets the runtime result of the stage.
    /// </summary>
    public StageResult Result { get; } = new StageResult();

    /// <summary>
    /// Gets the stage definition represented by this runtime instance.
    /// </summary>
    public Stage Stage { get; private set; }

    /// <summary>
    /// Gets the runtime step instances contained in the stage.
    /// </summary>
    public IFreezableCollection<StepInstanceGeneric> Steps { get; }

    internal StageInstance(Stage stage)
    {
        Stage = stage;
        Steps = (FreezableCollection<StepInstanceGeneric>)[.. stage.Steps.Select(x => x.GetInstanceGeneric())];
    }
}