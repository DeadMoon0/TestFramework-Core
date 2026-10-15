using System;

namespace TestFramework.Core.Exceptions;

/// <summary>
/// A verdict about the system under test: an assertion that did not hold.
/// </summary>
/// <remarks>
/// <para>
/// Part of the family, so <c>catch (TimelineFrameworkException)</c> sees every failure the framework raises, and
/// kept apart from it, so a test or a report can still tell "the system under test is wrong" (this type) from
/// "the framework or the setup is wrong" (every other one). It prints as an assertion, not as a framework error.
/// </para>
/// <para>
/// A failed assertion carries no recovery steps: the cure is in the system under test, which the framework
/// cannot see. What it does carry is where the assertion was written, so a failure collected in an
/// <c>AssertionScope</c> - which is thrown far from the line that made it - still points at that line.
/// </para>
/// </remarks>
public abstract class AssertionFailedException : TimelineFrameworkException
{
    /// <summary>
    /// Creates the failure.
    /// </summary>
    /// <param name="message">What was asserted and what was found.</param>
    /// <param name="site">Where the assertion was written, such as <c>OrdersTests.cs:42</c>, or <see langword="null"/> when unknown.</param>
    protected AssertionFailedException(string message, string? site)
        : base(site is null ? message : $"{message}{System.Environment.NewLine}  at {site}", [])
    {
        Site = site;
    }

    /// <summary>
    /// Where the assertion was written, such as <c>OrdersTests.cs:42</c>, or <see langword="null"/> when it could not be
    /// told - a build without line information, or an assertion made inside the framework itself.
    /// </summary>
    public string? Site { get; }

    /// <inheritdoc />
    protected override string Heading => "ASSERTION FAILED";
}
