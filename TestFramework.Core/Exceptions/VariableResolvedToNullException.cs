using System.Collections.Generic;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// Thrown when a variable reference resolves successfully but the value is null where a non-null value is required.
/// </summary>
public class VariableResolvedToNullException : TimelineFrameworkException
{
    /// <summary>
    /// Initializes the exception for a variable that resolved to null where a value was required.
    /// </summary>
    /// <param name="variableName">The variable that resolved to null.</param>
    /// <param name="requiredReason">Additional context about why a non-null value was required.</param>
    public VariableResolvedToNullException(string variableName, string requiredReason)
        : base(
            $"Variable '{variableName}' resolved to null where a non-null value was required.",
            new[]
            {
                $"Set '{variableName}' to a non-null value before the step that consumes it.",
                "If null is valid earlier in the flow, add a Transform(...) or guard before calling an API that requires a value.",
                string.IsNullOrWhiteSpace(requiredReason) ? "Check the consuming step or artifact for its non-null contract." : requiredReason
            },
            new List<string> { $"Variable: {variableName}" })
    {
    }
}