using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TestFramework.Config.Configuration;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;

// Deliberately in RunContext's own namespace rather than this package's. An extension method is invisible
// until its namespace is imported, and a file holding a RunContext has already imported this one - so putting
// the family's primary way of reading configuration anywhere else means a reader has to know it exists before
// they can find it. The same convention the framework libraries use for their own extensions, and nothing
// about it is special to this package: any package may extend a Core type the same way.
namespace TestFramework.Core.Steps;

/// <summary>
/// Reads a resource's configuration from the run that is using it.
/// </summary>
/// <remarks>
/// <para>
/// One reader for the whole family. Every package used to have its own - the same twelve lines with a
/// different kind and a different noun in the failure message - and none of them needed to be its own,
/// because a shape already knows the kind it owns and the section it came from. So this is where §4 lands
/// on configuration: one way to ask what a run was set up with.
/// </para>
/// <para>
/// What comes back is the package's own typed record, rebuilt from what the run knows. A configured entry
/// became resource values when the run composed its resources; anything the environment started published
/// its own; and a caller never learns which happened, which is the point.
/// </para>
/// <para>
/// The shape is resolved from services and that is deliberate: a shape is machinery - it reads and writes
/// and holds nothing about a run - so §7 permits it. What §7 forbids, and what this replaced, is resolving
/// the run's *configuration* that way.
/// </para>
/// </remarks>
public static class RunConfigExtension
{
    /// <summary>
    /// The configuration of one resource, as this run knows it.
    /// </summary>
    /// <remarks>
    /// The host viewpoint unless asked otherwise, because the caller is usually the test process. Code
    /// generating a file that something inside the environment will read asks for the network viewpoint and
    /// gets the addresses that work there.
    /// </remarks>
    /// <typeparam name="TConfig">The configuration record.</typeparam>
    /// <param name="context">The run.</param>
    /// <param name="identifier">Which resource.</param>
    /// <param name="vantage">Whose viewpoint the coordinates should be built for.</param>
    /// <returns>The record.</returns>
    /// <exception cref="FrameworkConfigurationException">
    /// Nothing in the run supplies the resource, or no shape owns the record type.
    /// </exception>
    public static TConfig Configured<TConfig>(
        this RunContext context,
        string identifier,
        ResourceVantage vantage = ResourceVantage.Host)
        where TConfig : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        ConfigShape<TConfig> shape = context.Services.GetService<ConfigShape<TConfig>>()
            ?? throw new FrameworkConfigurationException(
                $"Nothing in this run reads {typeof(TConfig).Name}, so its configuration cannot be resolved.",
                [
                    $"Call the loader of the package that owns {typeof(TConfig).Name} on the config instance this run is set up with.",
                ]);

        IReadOnlyDictionary<string, string> values = context.Values.ValuesFor(shape.Kind, identifier, vantage);

        if (values.Count == 0)
        {
            throw new FrameworkConfigurationException(
                $"Nothing in this run supplies {shape.Kind}/'{identifier}'.",
                [
                    $"Add a '{shape.Section}:{identifier}' section, or include the definition that provisions it.",
                ],
                [.. context.Values.IdentifiersOf(shape.Kind)]);
        }

        return shape.Read(values, identifier);
    }
}
