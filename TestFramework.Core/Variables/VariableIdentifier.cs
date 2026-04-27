namespace TestFramework.Core.Variables;

/// <summary>
/// Identifies a variable within a timeline definition and run.
/// </summary>
/// <param name="Identifier">The underlying variable key.</param>
public record VariableIdentifier(string Identifier)
{
    /// <summary>
    /// Converts a variable identifier to its string key.
    /// </summary>
    public static implicit operator string(VariableIdentifier id) => id.Identifier;

    /// <summary>
    /// Converts a string key to a variable identifier.
    /// </summary>
    public static implicit operator VariableIdentifier(string id) => new VariableIdentifier(id);

    /// <summary>
    /// Returns the identifier key.
    /// </summary>
    public override string ToString() => Identifier;
}