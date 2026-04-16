using System.Linq;

namespace TestFramework.Core.Artifacts;

public enum ArtifactState
{
    NotSetup,
    Setup,
    Cleaned,
    NotFound
}

public class ArtifactInstance<TArtifactDescriber, TArtifactData, TArtifactReference> : ArtifactInstanceGeneric
    where TArtifactDescriber : ArtifactDescriber<TArtifactDescriber, TArtifactData, TArtifactReference>, new()
    where TArtifactData : ArtifactData<TArtifactData, TArtifactDescriber, TArtifactReference>
    where TArtifactReference : ArtifactReference<TArtifactReference, TArtifactDescriber, TArtifactData>
{
    public new TArtifactDescriber Artifact { get => (TArtifactDescriber)base.Artifact; }
    public new TArtifactReference Reference { get => (TArtifactReference)base.Reference; }

    public new TArtifactData this[int index]
    {
        get { return (TArtifactData)base[index]; }
    }

    public new TArtifactData this[ArtifactVersionIdentifier identifier]
    {
        get { return (TArtifactData)base[identifier]; }
    }

    public new TArtifactData First { get => (TArtifactData)base.First; }
    public new TArtifactData Last { get => (TArtifactData)base.Last; }

    internal ArtifactInstance(TArtifactDescriber artifact, ArtifactIdentifier identifier, TArtifactReference reference, TArtifactData? firstVersionData) : base(artifact, identifier, reference, firstVersionData) { }

    public void AddVersion(TArtifactData artifact) => base.AddVersionGeneric(artifact);

    public override string ToString()
    {
        return $"Ref: {Reference}; Describer: {Artifact}; Data: {(VersionCount != 0 ? this[VersionCount - 1] : null)}";
    }
}

public class ArtifactInstanceGeneric : IFreezable
{
    public bool IsFrozen { get; private set; }
    public void Freeze()
    {
        IsFrozen = true;
    }

    private readonly FreezableCollection<ArtifactDataGeneric> _dataVersions = [];

    public ArtifactDataGeneric this[int index]
    {
        get { return _dataVersions.ElementAt(index); }
    }

    public ArtifactDataGeneric this[ArtifactVersionIdentifier identifier]
    {
        get { return _dataVersions.FirstOrDefault(x => x.Identifier == identifier) ?? throw new System.InvalidOperationException("No Version has the Identifier: '" + identifier + "'."); }
    }

    public ArtifactDataGeneric First { get => _dataVersions.First(); }
    public ArtifactDataGeneric Last { get => _dataVersions.Last(); }
    public int VersionCount { get => _dataVersions.Count; }

    public ArtifactIdentifier Identifier { get; }
    public ArtifactDescriberGeneric Artifact { get; }
    public ArtifactReferenceGeneric Reference { get; }

    private ArtifactState _state = ArtifactState.NotSetup;
    public ArtifactState State { get => _state; set { ((IFreezable)this).EnsureNotFrozen(); _state = value; } }

    internal ArtifactInstanceGeneric(ArtifactDescriberGeneric artifact, ArtifactIdentifier identifier, ArtifactReferenceGeneric reference, ArtifactDataGeneric? firstVersionData)
    {
        Artifact = artifact;
        Identifier = identifier;
        Reference = reference;
        if (firstVersionData is not null) AddVersionGeneric(firstVersionData);
    }

    public void AddVersionGeneric(ArtifactDataGeneric data)
    {
        _dataVersions.Add(data);
    }
}