namespace TestFramework.Simple;

/// <summary>
/// Entry point for the lightweight trigger helpers provided by <c>TestFramework.Simple</c>.
/// </summary>
public static class SimpleExt
{
    /// <summary>
    /// Gets the trigger factory for inline actions and message boxes.
    /// </summary>
    public static SimpleTrigger Trigger { get; } = new SimpleTrigger();
}
