namespace TestFramework.Core.Environment;

/// <summary>
/// Whether a component in this run belongs to it, or was already running when it started.
/// </summary>
/// <remarks>
/// <para>
/// Not the same question as <see cref="EnvComponentReuseMode"/>, and the difference is the reason this
/// exists. A reuse mode is a *declaration* - what a component permits - and it is fixed when the component
/// is written. This is a *fact about one run*: the same component marked
/// <see cref="EnvComponentReuseMode.PersistentContext"/> is created and torn down by a run that has no
/// persistent context around it, and is merely borrowed by a run that has one. A declaration cannot answer
/// "who takes this down", because the answer differs between two runs of the same test.
/// </para>
/// <para>
/// It is the answer to the two questions a run has to settle about every resource it touches: what is mine,
/// and what am I only using. Everything that follows - who tears it down, whose addresses stop being true
/// when the run ends, what a failure report should call borrowed - follows from those.
/// </para>
/// </remarks>
public enum EnvComponentScope
{
    /// <summary>
    /// This run created it and this run takes it down. Its addresses stop being true when the run ends.
    /// </summary>
    Run = 0,

    /// <summary>
    /// It was already running when this run started, and will still be running when the run ends.
    /// </summary>
    /// <remarks>
    /// The run may use it and must never take it down: something outside the run owns it, and other runs
    /// are still to come. What it published reaches the run as a produced value rather than by running
    /// again, because a container's port is decided once - when it starts.
    /// </remarks>
    Reused = 1,
}
