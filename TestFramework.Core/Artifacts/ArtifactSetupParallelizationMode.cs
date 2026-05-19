namespace TestFramework.Core.Artifacts;

/// <summary>
/// Controls whether artifact setup may run concurrently for the same artifact kind.
/// </summary>
public enum ArtifactSetupParallelizationMode
{
    /// <summary>
    /// Artifact setup may run concurrently for this artifact kind.
    /// </summary>
    AllowParallel,

    /// <summary>
    /// Artifact setup must be serialized for a single artifact describer type.
    /// </summary>
    SerializeByArtifactType
}