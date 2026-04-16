namespace TestFrameworkCore.Artifacts;

public record ArtifactVersionIdentifier(string Identifier)
{
    public static implicit operator string(ArtifactVersionIdentifier id) => id.Identifier;
    public static implicit operator ArtifactVersionIdentifier(string id) => new ArtifactVersionIdentifier(id);

    public static readonly ArtifactVersionIdentifier Default = "";

    public override string ToString() => Identifier;
}