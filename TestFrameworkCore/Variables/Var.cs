namespace TestFrameworkCore.Variables;

public static class Var
{
    public static ConstVariable<T> Const<T>(T value)
    {
        return new ConstVariable<T>(value);
    }

    public static ResolvableVariable<T> Ref<T>(VariableIdentifier identifier)
    {
        return new ResolvableVariable<T>(identifier, []);
    }

    public static ImmutableVariable<ResolvableVariable<T>, T> RefImmutable<T>(VariableIdentifier identifier)
    {
        return new ImmutableVariable<ResolvableVariable<T>, T>(new ResolvableVariable<T>(identifier, []));
    }
}