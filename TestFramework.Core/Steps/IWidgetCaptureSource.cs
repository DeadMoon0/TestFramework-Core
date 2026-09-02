using System.Threading.Tasks;

namespace TestFramework.Core.Steps;

/// <summary>
/// Takes a fresh look at whatever a run is holding, when someone watching asks for one.
/// </summary>
/// <remarks>
/// <para>
/// The companion to <see cref="IStepObserver"/>, and the difference is who decides when. An observer
/// is told that something happened and gathers evidence about it; this is asked, out of the blue, for
/// evidence about right now — because a run stopped at a breakpoint is precisely when the last
/// picture taken is out of date. The browser is sitting on a page nobody has photographed.
/// </para>
/// <para>
/// Registered as a service and resolved once per run, exactly as an observer is. A run with none
/// registered answers the asker saying so, which is the whole reason the request has a reply.
/// </para>
/// <para>
/// Nothing here reports what it captured, deliberately. Evidence goes where all evidence goes —
/// <see cref="RunContext.Widgets"/> — and the run counts what arrived, so a source cannot
/// accidentally claim a picture it failed to take. Anything thrown is logged and dropped, for the
/// reason <see cref="IStepObserver"/> gives: gathering evidence must never be able to change what a
/// run did, and that includes failing it.
/// </para>
/// </remarks>
public interface IWidgetCaptureSource
{
    /// <summary>Captures whatever this source has to show, publishing it as a widget.</summary>
    /// <param name="run">
    /// The run to publish into and to read from. Carries no attempt and a budget of its own, the way
    /// the context handed to an observer does — nothing is running, which is why anyone asked.
    /// </param>
    /// <returns>A task that completes when the capture is done.</returns>
    Task CaptureAsync(RunContext run);
}
