namespace TestFramework.Core.Environment;

/// <summary>
/// An environment that hands each run its own instance.
/// </summary>
/// <remarks>
/// <para>
/// For an environment whose instance records what one run decided - which resources it resolved, what that run
/// was handed - so that a second run sharing the instance cannot read or overwrite it. The object a test
/// configures stays a description; <c>SetEnv</c> asks it for the instance the
/// run actually uses, and <see cref="Timelines.TimelineRun.Environment"/> is that instance.
/// </para>
/// <para>
/// An interface rather than a rule every environment keeps by discipline: an environment that holds nothing per run
/// does not implement it, and one that does cannot forget to be copied.
/// </para>
/// </remarks>
public interface IRunScopedEnvironmentFactory
{
    /// <summary>
    /// Creates the instance one run uses, carrying this environment's configuration and none of its runs' state.
    /// </summary>
    /// <returns>A fresh environment for one run.</returns>
    IEnvironmentProvider CreateForRun();
}
