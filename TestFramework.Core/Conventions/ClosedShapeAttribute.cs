using System;

namespace TestFramework.Core.Conventions;

/// <summary>
/// Declares that a public positional record will never gain another member, and says why.
/// </summary>
/// <remarks>
/// <para>
/// A positional record's constructor is its member list. Adding a member changes that constructor, and every
/// assembly compiled against the old one throws <see cref="MissingMethodException"/> the first time it builds
/// one - a break the compiler cannot see, because each package is compiled on its own. So a public record is
/// positional only when its members <em>are</em> what it is: an identifier is its string, an address is its kind
/// and name. Everything else uses <c>init</c> properties, which can grow without breaking anyone.
/// </para>
/// <para>
/// <see cref="StepConventions.AssertPublicSurfaceCanGrow"/> fails on a public positional record without this
/// attribute, so a closed shape is a decision somebody wrote down next to the type, not an accident.
/// </para>
/// </remarks>
/// <param name="why">Why the record's members are complete for good.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ClosedShapeAttribute(string why) : Attribute
{
    /// <summary>
    /// Why the record's members are complete for good.
    /// </summary>
    public string Why { get; } = why;
}
