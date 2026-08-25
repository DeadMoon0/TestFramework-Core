namespace TestFramework.Core.Artifacts;

/// <summary>
/// Proof that <see cref="ArtifactStore"/> checked whether a write to the run's artifacts may land.
/// </summary>
/// <remarks>
/// <para>
/// An artifact is changed in four ways - it is held, it gains a version, its lifecycle state moves, its
/// reference is pinned - and every one of them has to answer the same question first: is the attempt doing
/// this still the one the run is waiting for. Asking that in four places is how the fourth one ends up
/// forgetting, so the mutators do not ask it at all. They demand this, and it can only come from the store,
/// which asks once.
/// </para>
/// <para>
/// The constructor is <c>private protected</c>, so no code outside this assembly can make one or derive
/// something that is one: for a package, a test, or anyone holding an artifact, writing to a run's
/// artifacts without the check is not discouraged - it does not compile. Inside the assembly the only
/// nameable ticket is a private nested type of the store, so there is one place tickets come from.
/// </para>
/// </remarks>
public abstract class ArtifactWriteTicket
{
    /// <summary>
    /// Only the store's own ticket derives from this.
    /// </summary>
    private protected ArtifactWriteTicket()
    {
    }
}
