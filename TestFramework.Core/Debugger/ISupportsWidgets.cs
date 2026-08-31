using System.ComponentModel;
using System.Threading.Tasks;

namespace TestFramework.Core.Debugger;

/// <summary>
/// Implemented by debuggers that can carry the evidence a run produced.
/// </summary>
/// <remarks>
/// <para>
/// Kept off <see cref="IRunDebugger"/> for the reason <see cref="ISupportsRunCancellation"/> gives, and
/// one more: that interface is public and a caller can hand the run its own implementation, so widening
/// it would break every debugger compiled against an earlier Core the moment it was loaded. A debugger
/// that has never heard of widgets keeps working and simply does not receive them.
/// </para>
/// <para>
/// Note what is <em>not</em> conditional on this. The file a widget refers to is written whether or
/// not anything is listening, because it lives in the run's own output — where a person can open it and
/// a build publishes it as an artifact. This interface only decides whether the run also says so.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISupportsWidgets
{
    /// <summary>Reports a piece of evidence a step or component produced.</summary>
    Task SignalWidgetAsync(string sessionId, DebugWidgetEntry entry);
}
