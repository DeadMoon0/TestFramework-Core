using System.Collections.Generic;
using System.Globalization;
using TestFramework.Core.Debugger;

namespace TestFramework.Core.Artifacts;

/// <summary>
/// What is true of every artifact, whatever kind it is.
/// </summary>
/// <remarks>
/// The fallback an artifact kind gets until it overrides
/// <see cref="ArtifactDescriberGeneric.Describe"/> with something that knows what it is looking at.
/// What this replaced was a single line reading <c>ref=…; state=…; versions=…; latest=…</c>, which
/// pushed four facts through one string so that every consumer had to parse them back out — and none
/// of them did, so the line was shown verbatim, semicolons and all.
/// </remarks>
public static class ArtifactDescription
{
    /// <summary>Describes an artifact from what the store knows about it.</summary>
    public static DebugValueDescription Of(ArtifactInstanceGeneric instance)
    {
        System.ArgumentNullException.ThrowIfNull(instance);

        bool hasData = instance.VersionCount > 0;
        DebugValueDescription latest = hasData
            ? DebugValueDescriber.DescribeForArtifact(instance.Last)
            : DebugValueDescription.Empty;

        List<DebugValueField> fields =
        [
            new() { Name = "kind", Value = instance.Artifact.ToString() },
            new() { Name = "reference", Value = DebugValueDescriber.Line(instance.Reference) },
            new() { Name = "versions", Value = instance.VersionCount.ToString(CultureInfo.InvariantCulture) }
        ];

        if (hasData)
            fields.Add(new DebugValueField { Name = "latest", Value = latest.Summary });

        return new DebugValueDescription
        {
            // The state leads, because it is the thing that decides whether anything else on the
            // card can be trusted: a cleaned artifact's reference points at something gone.
            Summary = hasData ? $"{instance.State} · {latest.Summary}" : $"{instance.State} · no data",

            // The shape of the data, not of the artifact. An artifact keys its renderer off the kind
            // that produced it; this says what the payload inside looks like once you open it.
            Shape = latest.Shape,
            Badges = [instance.State.ToString()],
            Fields = [.. fields],
            Preview = latest.Preview
        };
    }
}
