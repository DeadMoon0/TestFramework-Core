using System;
using TestFramework.Core.Artifacts;
using TestFramework.Core.Variables;

namespace TestFramework.Core.Timelines.Builder.TimelineBuilder.Actions;

public interface IStepIOModAction
{
    /// <summary>Declares that this step reads a variable. With required=true (default) the validator errors if no prior step produces it.</summary>
    ITimelineBuilderModifier DependsOnVariable(VariableIdentifier key, bool required = true);

    /// <summary>Declares that this step reads a typed variable, enabling type-compatibility validation against the producer.</summary>
    ITimelineBuilderModifier DependsOnVariable<T>(VariableIdentifier key, bool required = true);

    /// <summary>Declares that this step writes a variable, making it available to subsequent steps.</summary>
    ITimelineBuilderModifier ProducesVariable(VariableIdentifier key);

    /// <summary>Declares that this step writes a typed variable, enabling type-compatibility validation for downstream consumers.</summary>
    ITimelineBuilderModifier ProducesVariable<T>(VariableIdentifier key);

    /// <summary>Declares that this step reads an artifact. With required=true (default) the validator errors if no prior step produces it.</summary>
    ITimelineBuilderModifier DependsOnArtifact(ArtifactIdentifier key, bool required = true);

    /// <summary>Declares that this step reads a typed artifact, enabling type-compatibility validation against the producer.</summary>
    ITimelineBuilderModifier DependsOnArtifact<TDescriber>(ArtifactIdentifier key, bool required = true);

    /// <summary>Declares that this step writes an artifact, making it available to subsequent steps.</summary>
    ITimelineBuilderModifier ProducesArtifact(ArtifactIdentifier key);

    /// <summary>Declares that this step writes a typed artifact, enabling type-compatibility validation for downstream consumers.</summary>
    ITimelineBuilderModifier ProducesArtifact<TDescriber>(ArtifactIdentifier key);
}
