namespace TestFramework.Core.Variables;

public record VariableIdentifier(string Identifier)
{
    public static implicit operator string(VariableIdentifier id) => id.Identifier;
    public static implicit operator VariableIdentifier(string id) => new VariableIdentifier(id);

    public override string ToString() => Identifier;
}