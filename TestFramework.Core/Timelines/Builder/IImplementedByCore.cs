using System.ComponentModel;

namespace TestFramework.Core.Timelines.Builder;

/// <summary>
/// The base of every builder interface: they are Core's to implement and everyone else's to call.
/// </summary>
/// <remarks>
/// A consumer that implemented a builder interface would break on every verb Core adds to it, which would make
/// each new verb a breaking change. This member is internal, so only Core's assembly can implement it, and so only
/// Core can implement any interface that derives from it. Adding a verb therefore breaks nobody, and the compiler
/// rather than a promise in the documentation is what makes that so.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IImplementedByCore
{
    /// <summary>
    /// Never called; its being internal is the whole point.
    /// </summary>
    internal void ImplementedByCore();
}
