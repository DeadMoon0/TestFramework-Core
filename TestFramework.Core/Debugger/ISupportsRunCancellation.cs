using System;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Implemented by debuggers whose transport can carry a stop request back to the run.
/// </summary>
/// <remarks>
/// Kept off <see cref="IRunDebugger"/> deliberately: almost every debugger is write-only, and making
/// them all implement an inbound channel they do not have would be noise. The session asks whether a
/// debugger happens to support this and subscribes if so.
/// </remarks>
internal interface ISupportsRunCancellation
{
    /// <summary>Raised when a consumer asks the run to stop, with an optional reason.</summary>
    event Action<string?>? CancellationRequested;
}
