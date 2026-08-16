using System.ComponentModel;

namespace TestFramework.Core.Debugger;

/// <summary>
/// The shape of a value, which is what decides how it can be shown.
/// </summary>
/// <remarks>
/// <para>
/// A consumer choosing a renderer needs to know whether it is looking at one thing, a list of things
/// or a blob of bytes. It does not need to know the CLR type, and mostly cannot do anything with it:
/// a rail that can draw <c>Dictionary&lt;String, Int32&gt;</c> can draw every other dictionary too.
/// </para>
/// <para>
/// This is deliberately coarse. Every additional case is a case each consumer has to handle, and the
/// interesting detail — the element type, the entry count, the byte size — is already stated in the
/// description's fields.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum DebugValueShape
{
    /// <summary>The shape was never determined, as in a value replayed from an older journal.</summary>
    Unknown,

    /// <summary>There is no value.</summary>
    Null,

    /// <summary>One indivisible value: a number, a flag, a timestamp, an identifier.</summary>
    Scalar,

    /// <summary>Text, which may be long enough to want its own viewer.</summary>
    Text,

    /// <summary>Bytes.</summary>
    Binary,

    /// <summary>An ordered sequence of items.</summary>
    Collection,

    /// <summary>Keyed entries.</summary>
    Dictionary,

    /// <summary>A composite value with named members.</summary>
    Object
}
