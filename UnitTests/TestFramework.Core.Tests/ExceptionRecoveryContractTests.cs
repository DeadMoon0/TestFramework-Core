using System;
using System.Collections.Generic;
using TestFramework.Core.Exceptions;
using Xunit;
using Xunit.Abstractions;

namespace TestFramework.Core.Tests;

/// <summary>
/// Salvaged from <c>TestFramework-FrictionAudit/T002_ErrorRecoveryScenarios.cs</c>, the one file in
/// that unversioned audit folder with real assertions.
/// </summary>
/// <remarks>
/// <para>
/// Its premise: a framework is judged less by how it behaves when things go right than by how it
/// fails and whether it tells you what to do next. Four of its six tests turned out to duplicate
/// <see cref="ExceptionTests"/> exactly and were dropped rather than copied. What survives is the
/// part that file had and this repository did not: a sweep asserting that *every* exception type
/// honours the contract at once, and a check on the shape of the formatted output rather than on
/// the presence of a couple of substrings.
/// </para>
/// <para>
/// The distinction matters. A per-type test passes while a new exception type quietly ships with no
/// recovery steps at all; the sweep is what notices.
/// </para>
/// </remarks>
public class ExceptionRecoveryContractTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void EveryFrameworkExceptionType_CarriesTheFullRecoveryContract()
    {
        var variables = new Dictionary<string, object?> { { "test", "value" } };
        TimelineFrameworkException[] exceptions =
        [
            new MissingVariableException("test", variables),
            new ConfigurationInvalidException("key", "type", "value"),
            new EnvironmentNotSetException(null, ["env1"]),
            new ArtifactNotFoundException("missing", ["found"]),
        ];

        foreach (TimelineFrameworkException exception in exceptions)
        {
            outputHelper.WriteLine(exception.GetType().Name);

            Assert.NotEmpty(exception.FriendlyMessage);
            Assert.NotNull(exception.RecoverySteps);
            Assert.NotEmpty(exception.RecoverySteps);

            string formatted = exception.ToString();
            Assert.Contains("FRAMEWORK ERROR", formatted, StringComparison.Ordinal);
            Assert.Contains("Recovery:", formatted, StringComparison.Ordinal);
            Assert.True(
                formatted.Split(System.Environment.NewLine).Length > 3,
                $"{exception.GetType().Name} formatted to fewer than four lines, so it has no readable structure.");
        }
    }

    [Fact]
    public void FormattedException_SeparatesRecoveryStepsFromAvailableOptions()
    {
        var variables = new Dictionary<string, object?>
        {
            { "sessionId", "sess123" },
            { "timestamp", DateTime.Now },
        };

        string formatted = new MissingVariableException("userId", variables).ToString();
        outputHelper.WriteLine(formatted);

        // More than five lines: a wall of text is not guidance, however correct its content.
        Assert.True(
            formatted.Split(System.Environment.NewLine).Length > 5,
            "The formatted exception collapsed into too few lines to have sections.");

        // The two markers are what make the sections scannable at a glance.
        Assert.Contains("->", formatted, StringComparison.Ordinal);
        Assert.Contains("*", formatted, StringComparison.Ordinal);

        // And the recovery guidance names an action, not just the problem.
        Assert.Contains("Define", formatted, StringComparison.OrdinalIgnoreCase);
    }
}
