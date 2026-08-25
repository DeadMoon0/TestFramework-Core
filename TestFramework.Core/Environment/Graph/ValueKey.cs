using System;

namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Which value of a resource, and from whose viewpoint.
/// </summary>
/// <remarks>
/// A resource offers more than one value - an address, a connection string, the name of a database it
/// created - so a value needs a name of its own rather than being "the resource's value". The vantage is
/// optional because plenty of values read the same from everywhere: a database name is a name, whoever
/// is asking.
/// </remarks>
/// <param name="ValueName">What the value is, from <see cref="ValueNames"/> or a package's own list.</param>
/// <param name="Vantage">Whose viewpoint it is built for, or <see langword="null"/> when it does not depend on one.</param>
public readonly record struct ValueKey(string ValueName, ResourceVantage? Vantage = null)
{
    /// <summary>
    /// Reads as <c>BaseUrl (Network)</c>, or <c>DatabaseName</c> for a value with no vantage.
    /// </summary>
    /// <returns>The description, for messages and logs.</returns>
    public override string ToString()
        => this.Vantage is { } vantage ? $"{this.ValueName} ({vantage})" : this.ValueName;
}

/// <summary>
/// The value names every package shares. Anything narrower belongs to the package that owns the kind.
/// </summary>
/// <remarks>
/// Constants rather than strings at call sites, for the same reason the rest of the framework refuses
/// magic values: a typo in a name should not be a value that silently never resolves.
/// </remarks>
public static class ValueNames
{
    /// <summary>Where something addressable answers, for example <c>http://localhost:32771/</c>.</summary>
    public const string BaseUrl = nameof(BaseUrl);

    /// <summary>How something dialled rather than addressed is reached.</summary>
    public const string ConnectionString = nameof(ConnectionString);
}
