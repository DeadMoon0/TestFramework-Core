using System;
using System.Linq;

namespace TestFrameworkCore.Variables;

internal record VariableTransform(Func<object?, object?[], object?> Transform, VariableReferenceGeneric[] Variables)
{
    internal static VariableTransform FromTransform(Func<object?, object?> transform)
    {
        return new VariableTransform((o, v) => transform(o), []);
    }

    internal static VariableTransform FromTransformAndVars(Func<object?, object?[], object?> transform, VariableReferenceGeneric[] variables)
    {
        return new VariableTransform(transform, variables);
    }

    internal object? GetTransformed(object? value, VariableStore variableStore)
    {
        return Transform(value, [.. Variables.Select(x => x.GetValueGeneric(variableStore))]);
    }
}