using System;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// Proof that the run's store admitted a change to a reference: what setup learned only by creating the
/// artifact - a key the database assigned, say.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="ArtifactStore.Assign{TReference}"/> makes one, and only for the length of its callback. A
/// reference that can learn its identity during setup takes one in the method that records it, so the record
/// cannot be made except where the run's gate decided it may: an attempt the run has stopped waiting for would
/// otherwise aim the artifact, and the cleanup that deletes it, at what that attempt created instead of what
/// the live one did.
/// </para>
/// <para>
/// Call <see cref="EnsureOpen"/> first in such a method; a proof kept past its callback is refused there.
/// </para>
/// </remarks>
public sealed class ArtifactAssignment
{
    private bool isOpen = true;

    internal ArtifactAssignment() { }

    /// <summary>
    /// Refuses a proof used after the callback it was handed to has returned.
    /// </summary>
    /// <exception cref="InvalidOperationException">The callback has returned.</exception>
    public void EnsureOpen()
    {
        if (!isOpen)
            throw new InvalidOperationException("This assignment's callback has returned; assign through ArtifactStore.Assign, which admits the change against the run's gate.");
    }

    internal void Close() => isOpen = false;
}
