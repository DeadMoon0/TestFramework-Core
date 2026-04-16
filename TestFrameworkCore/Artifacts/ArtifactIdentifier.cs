namespace TestFrameworkCore.Artifacts;

public record ArtifactIdentifier(string Identifier)
{
    public static implicit operator string(ArtifactIdentifier id) => id.Identifier;
    public static implicit operator ArtifactIdentifier(string id) => new ArtifactIdentifier(id);

    public override string ToString() => Identifier;
}