namespace TestFramework.Core.Artifacts;

public interface IStaticArtifactKind<TArtifactKind>
{
    public static abstract TArtifactKind Kind { get; }
}