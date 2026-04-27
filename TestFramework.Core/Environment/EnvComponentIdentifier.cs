namespace TestFramework.Core.Environment;

/// <summary>
/// Identifies an environment component.
/// </summary>
/// <param name="Identifier">The underlying component key.</param>
public record EnvComponentIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a component identifier to its string key.
    /// </summary>
    public static implicit operator string(EnvComponentIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a string key to a component identifier.
    /// </summary>
    public static implicit operator EnvComponentIdentifier(string id) => new EnvComponentIdentifier(id);

    /// <summary>
    /// Returns the component identifier key.
    /// </summary>
    public override string ToString() => Identifier;
}