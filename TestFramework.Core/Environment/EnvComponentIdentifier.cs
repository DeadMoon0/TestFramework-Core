namespace TestFramework.Core.Environment;

public record EnvComponentIdentifier(string Identifier)
{
    public static implicit operator string(EnvComponentIdentifier id) => id.Identifier;
    public static implicit operator EnvComponentIdentifier(string id) => new EnvComponentIdentifier(id);

    public override string ToString() => Identifier;
}