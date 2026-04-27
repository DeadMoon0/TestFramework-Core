namespace TestFramework.Core.Variables;

/// <summary>
/// Provides the main factory methods for constant and referenced variables in timeline definitions.
/// </summary>
public static class Var
{
    /// <summary>
    /// Wraps a constant value as an immutable variable reference.
    /// </summary>
    /// <typeparam name="T">The constant value type.</typeparam>
    /// <param name="value">The constant value.</param>
    public static ConstVariable<T> Const<T>(T value)
    {
        return new ConstVariable<T>(value);
    }

    /// <summary>
    /// Creates a runtime-resolved variable reference.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve at run time.</param>
    public static ResolvableVariable<T> Ref<T>(VariableIdentifier identifier)
    {
        return new ResolvableVariable<T>(identifier, []);
    }

    /// <summary>
    /// Creates an immutable runtime-resolved variable reference.
    /// </summary>
    /// <typeparam name="T">The expected variable value type.</typeparam>
    /// <param name="identifier">The variable identifier to resolve at run time.</param>
    public static ImmutableVariable<ResolvableVariable<T>, T> RefImmutable<T>(VariableIdentifier identifier)
    {
        return new ImmutableVariable<ResolvableVariable<T>, T>(new ResolvableVariable<T>(identifier, []));
    }
}