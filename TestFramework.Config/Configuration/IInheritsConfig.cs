namespace TestFramework.Config.Configuration;

/// <summary>
/// A configuration record whose entries may be declared in terms of one another.
/// </summary>
/// <remarks>
/// <para>
/// One entry names another and takes over everything it did not state itself, so a variant is a line rather
/// than a copy: the same application as a phone sees it, the same database with one collection renamed. What
/// makes that safe is what "did not state" means, and that is decided here rather than per package -
/// <see cref="ConfigInheritance"/> merges, and a record only says who its parent is.
/// </para>
/// <para>
/// The alternative is what the family had: one package wrote the merge by hand, twenty-one values deep, and
/// got the question wrong for seven of them - asking "does this differ from the default?" instead of "did
/// anybody set this?". Those cannot be told apart by comparing values, so a child that deliberately named the
/// default lost to its parent in silence. A package that does not write the merge cannot write that mistake
/// into it.
/// </para>
/// </remarks>
public interface IInheritsConfig
{
    /// <summary>
    /// The identifier this entry inherits from, or null when it stands alone.
    /// </summary>
    /// <remarks>
    /// Never inherited itself: a grandchild names its own parent, and the resolved entry names nobody,
    /// because by then there is nothing left to take over.
    /// </remarks>
    string? BasedOn { get; }
}
