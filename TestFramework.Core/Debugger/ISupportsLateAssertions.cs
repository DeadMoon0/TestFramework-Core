using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Implemented by debuggers that can still take an assertion after the run they watched has finished.
/// </summary>
/// <remarks>
/// <para>
/// Most checks are written against the run <c>RunAsync</c> handed back - <c>run.Step("x").Should()</c> - and so
/// happen after the session has sent its finish and closed its queue. They used to be dropped there without a
/// word, and a test whose every check passed showed in the UI as having proved nothing.
/// </para>
/// <para>
/// Opt-in, and kept internal: a debugger someone wrote against <see cref="IRunDebugger"/> may well rely on
/// "finished" being the last thing it hears, so only the debuggers that ship with Core are told anything after it.
/// </para>
/// </remarks>
internal interface ISupportsLateAssertions
{
    /// <summary>Reports an assertion made against a run that has already finished.</summary>
    /// <param name="sessionId">The finished run's session.</param>
    /// <param name="entry">The assertion and what it found.</param>
    Task SignalLateAssertionAsync(string sessionId, DebugAssertionEntry entry);
}
