using TestFramework.Core.Exceptions;

namespace TestFramework.Core;

/// <summary>
/// The one refusal for changing a result the runner has already recorded.
/// </summary>
/// <remarks>
/// A step's result is final when its attempt ends, long before the run does, so the sentence names the
/// result and not the run.
/// </remarks>
internal static class FinalResult
{
    internal static void EnsureOpen(bool isFinal)
    {
        if (isFinal)
            throw new FrameworkStateException("This result is final: the runner recorded it, so it cannot be changed.");
    }
}
