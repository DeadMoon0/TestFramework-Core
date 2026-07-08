using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Base exception for all TestFramework errors.
/// Provides consistent error messaging with recovery guidance.
/// </summary>
public abstract class TimelineFrameworkException : Exception
{
    private readonly string _friendlyMessage;
    private readonly IReadOnlyList<string> _recoverySteps;
    private readonly IReadOnlyList<string> _availableOptions;

    /// <summary>
    /// Friendly error message with recovery guidance
    /// </summary>
    public string FriendlyMessage => _friendlyMessage;

    /// <summary>
    /// Recovery steps (e.g., "Call SetEnv()", "Check variable name")
    /// </summary>
    public IReadOnlyList<string> RecoverySteps => _recoverySteps;

    /// <summary>
    /// Available alternatives (e.g., list of variables, environments, artifacts)
    /// </summary>
    public IReadOnlyList<string> AvailableOptions => _availableOptions;

    /// <summary>
    /// Initializes a new instance of the TimelineFrameworkException class.
    /// </summary>
    /// <param name="friendlyMessage">Friendly error message with recovery guidance</param>
    /// <param name="recoverySteps">Recovery steps to fix the error</param>
    /// <param name="availableOptions">Available alternatives or valid values</param>
    protected TimelineFrameworkException(
        string friendlyMessage,
        IReadOnlyList<string> recoverySteps,
        IReadOnlyList<string>? availableOptions = null)
        : base(BuildDetailedMessage(friendlyMessage, recoverySteps, availableOptions))
    {
        _friendlyMessage = friendlyMessage;
        _recoverySteps = recoverySteps;
        _availableOptions = availableOptions ?? new List<string>();
    }

    /// <summary>
    /// Initializes a new instance of the TimelineFrameworkException class with an inner exception.
    /// </summary>
    /// <param name="friendlyMessage">Friendly error message with recovery guidance</param>
    /// <param name="recoverySteps">Recovery steps to fix the error</param>
    /// <param name="availableOptions">Available alternatives or valid values</param>
    /// <param name="innerException">The underlying cause of the failure</param>
    protected TimelineFrameworkException(
        string friendlyMessage,
        IReadOnlyList<string> recoverySteps,
        IReadOnlyList<string>? availableOptions,
        Exception? innerException)
        : base(BuildDetailedMessage(friendlyMessage, recoverySteps, availableOptions), innerException)
    {
        _friendlyMessage = friendlyMessage;
        _recoverySteps = recoverySteps;
        _availableOptions = availableOptions ?? new List<string>();
    }

    private static string BuildDetailedMessage(
        string friendly,
        IReadOnlyList<string>? steps,
        IReadOnlyList<string>? options)
    {
        var sb = new StringBuilder();
        sb.AppendLine(friendly);

        if (steps?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recovery:");
            foreach (var step in steps)
            {
                sb.AppendLine($"  - {step}");
            }
        }

        if (options?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Available:");
            foreach (var opt in options)
            {
                sb.AppendLine($"  - {opt}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a formatted string representation of the exception.
    /// </summary>
    /// <returns>Formatted error message with recovery steps and available options</returns>
    public override string ToString()
    {
        var lines = new List<string>
        {
            $"[FRAMEWORK ERROR] {GetType().Name}",
            new string('=', 70),
            FriendlyMessage,
            ""
        };

        if (RecoverySteps?.Count > 0)
        {
            lines.Add("Recovery:");
            lines.AddRange(RecoverySteps.Select(s => $"  -> {s}"));
        }

        if (AvailableOptions?.Count > 0)
        {
            lines.Add("");
            lines.Add("Available:");
            lines.AddRange(AvailableOptions.Select(o => $"  * {o}"));
        }

        return string.Join(System.Environment.NewLine, lines);
    }
}
