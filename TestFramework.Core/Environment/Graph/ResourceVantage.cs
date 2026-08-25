namespace TestFramework.Core.Environment.Graph;

/// <summary>
/// Who is looking at a resource. The same resource commonly answers at two different coordinates.
/// </summary>
/// <remarks>
/// A container publishes a port to the machine running the tests and carries a network alias its peers
/// use; those are not the same address, and neither is derivable from the other by anyone except the
/// component that started it. Naming the viewpoint - rather than passing a boolean, or rewriting one
/// coordinate into the other later - is what keeps a value honest: a test asks for what IT can reach,
/// a generated settings file asks for what the CONTAINER can reach, and both get an answer that was
/// built for them.
/// </remarks>
public enum ResourceVantage
{
    /// <summary>As the process running the tests reaches it, for example a published container port.</summary>
    Host = 0,

    /// <summary>As a peer container on the environment's own network reaches it, for example an alias.</summary>
    Network = 1,
}
