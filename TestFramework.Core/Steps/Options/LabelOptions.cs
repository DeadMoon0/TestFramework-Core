using TestFramework.Core;

namespace TestFramework.Core.Steps.Options;

/// <summary>
/// Configures the optional consumer-facing label assigned to a step.
/// </summary>
public class LabelOptions : IFreezable
{
    /// <summary>
    /// Gets a value indicating whether the options object has been frozen against further mutation.
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Freezes the options object.
    /// </summary>
    public void Freeze() { IsFrozen = true; }

    private string? _label = null;

    /// <summary>
    /// Gets or sets the optional step label.
    /// </summary>
    public string? Label { get => _label; set { ((IFreezable)this).EnsureNotFrozen(); _label = value; } }

    /// <summary>
    /// Copies the current options to another instance.
    /// </summary>
    /// <param name="target">The target options instance.</param>
    public void CloneTo(LabelOptions target)
    {
        target.Label = Label;
    }
}
