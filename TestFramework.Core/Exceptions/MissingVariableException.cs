using System;
using System.Collections.Generic;
using System.Linq;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a timeline tries to access a variable that was never set.
/// </summary>
public class MissingVariableException : TimelineFrameworkException
{
    private readonly string _variableName;
    private readonly IReadOnlyDictionary<string, object?> _availableVariables;
    private readonly int? _stepIndex;
    private readonly string? _stepName;

    /// <summary>
    /// Gets the name of the missing variable.
    /// </summary>
    public string VariableName => _variableName;

    /// <summary>
    /// Gets the dictionary of available variables in the timeline.
    /// </summary>
    public IReadOnlyDictionary<string, object?> AvailableVariables => _availableVariables;

    /// <summary>
    /// Initializes a new instance of the MissingVariableException class.
    /// </summary>
    /// <param name="variableName">Name of the variable that was not found</param>
    /// <param name="availableVariables">Dictionary of variables that are available</param>
    /// <param name="stepIndex">Index of the step where the error occurred</param>
    /// <param name="stepName">Name of the step where the error occurred</param>
    public MissingVariableException(
        string variableName,
        IReadOnlyDictionary<string, object?> availableVariables,
        int? stepIndex = null,
        string? stepName = null)
        : base(
            $"Variable '{variableName}' was never set in this timeline.",
            new[]
            {
                $"Define the variable using: timeline.SetVariable(\"{variableName}\", value)",
                $"Add it in a previous step before using it",
                $"Check the variable name spelling (case-sensitive)"
            },
            availableVariables.Keys.Any()
                ? availableVariables.Keys
                    .Select(k => $"{k} (type: {availableVariables[k]?.GetType().Name ?? "null"})")
                    .ToList()
                : new List<string> { "No variables defined yet" })
    {
        _variableName = variableName;
        _availableVariables = availableVariables;
        _stepIndex = stepIndex;
        _stepName = stepName;
    }
}
