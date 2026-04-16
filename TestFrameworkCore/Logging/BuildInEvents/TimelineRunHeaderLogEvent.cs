using System;
using System.Collections.Generic;
using System.Linq;
using TestFrameworkCore.Artifacts;
using TestFrameworkCore.Stages;
using TestFrameworkCore.Steps;
using TestFrameworkCore.Steps.Options;
using TestFrameworkCore.Variables;

namespace TestFrameworkCore.Logging.BuildInEvents;

internal class TimelineRunHeaderLogEvent(
    DateTime startedAt,
    List<(VariableIdentifier Id, object? Value)> variables,
    List<(ArtifactIdentifier Id, ArtifactInstanceGeneric Instance)> artifacts,
    IFreezableCollection<StageInstance> stages,
    IReadOnlyList<StepGeneric> mainStageSteps) : LogEvent
{
    private const string Border = "══════════════════════════════════════════════════";

    public override void FormatLogEvent(LogLineWriter writer)
    {
        string P(string line) => PrefixLineWithIndentLevel(writer, line);

        writer.WriteLine(P(Border));
        writer.WriteLine(P($"  Timeline Run  —  {startedAt:yyyy-MM-dd  HH:mm:ss}"));
        writer.WriteLine(P(Border));

        // Variables
        if (variables.Count > 0)
        {
            writer.WriteLine(P($"  Variables ({variables.Count})"));
            int pad = variables.Max(v => v.Id.Identifier.Length);
            foreach (var (id, value) in variables)
                writer.WriteLine(P($"    {id.Identifier.PadRight(pad)}  {VariableFormatter.Format(value)}"));
            writer.WriteLine(P(""));
        }

        // Artifacts
        if (artifacts.Count > 0)
        {
            writer.WriteLine(P($"  Artifacts ({artifacts.Count})"));
            int pad = artifacts.Max(a => a.Id.Identifier.Length);
            foreach (var (id, instance) in artifacts)
            {
                string reference = VariableFormatter.Format(instance.Reference);
                string latest = instance.VersionCount == 0
                    ? "<no data>"
                    : VariableFormatter.Format(instance.Last);

                writer.WriteLine(P($"    {id.Identifier.PadRight(pad)}  [{instance.State}] v{instance.VersionCount}  ref={reference}  latest={latest}"));
            }
            writer.WriteLine(P(""));
        }

        // Stages
        writer.WriteLine(P("  Stages"));
        int stagePad = stages.Max(s => s.Stage.Name.Length);
        foreach (var stage in stages)
        {
            int count = stage.Steps.Count;
            string stepLabel = count == 1 ? "1 step" : $"{count} steps";
            writer.WriteLine(P($"    {stage.Stage.Name.PadRight(stagePad)}  {stepLabel}"));
        }

        // Dependency graph — only steps with manually declared inputs or outputs beyond the auto-generated "out"
        var graphSteps = mainStageSteps
            .Where(s => s.IOContract.Inputs.Count > 0 ||
                        s.IOContract.Outputs.Any(o => o.Key != "out"))
            .ToList();
        if (graphSteps.Count > 0)
        {
            writer.WriteLine(P(""));
            writer.WriteLine(P("  Dependency Graph"));
            foreach (var step in graphSteps)
            {
                string name = step.LabelOptions.Label ?? step.Name;
                writer.WriteLine(P($"    {name}"));

                foreach (var input in step.IOContract.Inputs)
                {
                    string kind = input.Kind == StepIOKind.Variable ? "var" : "art";
                    string req  = input.Required ? "" : " (optional)";
                    string type = input.DeclaredType is not null ? $" <{input.DeclaredType.Name}>" : "";
                    writer.WriteLine(P($"      \u2190 {input.Key}{type}  [{kind}]{req}"));
                }

                foreach (var output in step.IOContract.Outputs)
                {
                    string kind = output.Kind == StepIOKind.Variable ? "var" : "art";
                    string type = output.DeclaredType is not null ? $" <{output.DeclaredType.Name}>" : "";
                    writer.WriteLine(P($"      \u2192 {output.Key}{type}  [{kind}]"));
                }
            }
        }

        writer.WriteLine(P(Border));
    }
}
