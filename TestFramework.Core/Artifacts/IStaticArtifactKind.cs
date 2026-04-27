namespace TestFramework.Core.Artifacts;

/// <summary>
/// Defines a static singleton-like access point for an artifact kind type.
/// </summary>
public interface IStaticArtifactKind<TArtifactKind>
{
    /// <summary>
    /// Gets the canonical kind instance for the artifact type.
    /// </summary>
    public static abstract TArtifactKind Kind { get; }
}