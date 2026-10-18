using System.Threading;

namespace TestFramework.Core.Debugger;

/// <summary>
/// The sequence a run's envelopes are numbered in, for as long as the run lasts.
/// </summary>
/// <remarks>
/// Owned by the client, not by a connection: a run that loses its pipe and connects again keeps counting, so a
/// consumer that drops what it has already seen does not drop everything the run sends after the reconnect.
/// </remarks>
internal sealed class EnvelopeSequence
{
    private long last;

    internal long Next() => Interlocked.Increment(ref last);
}
