using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TestFramework.Core.Exceptions;

namespace TestFramework.Config.Configuration;

/// <summary>
/// Resolves configuration entries that are declared in terms of one another.
/// </summary>
/// <remarks>
/// <para>
/// The merge lives here, once, and no package writes its own. What it does is the whole of it: for every
/// value an entry did not state, take the parent's. "Did not state" is <see langword="null"/> and nothing
/// else - not "equals the default", which is the question that cannot be answered by looking at a value and
/// is how a hand-written merge silently discards a child's deliberate choice.
/// </para>
/// <para>
/// Because that is the rule, a property that cannot hold null cannot take part, and being unable to take part
/// quietly is exactly the failure this replaces. So it is refused instead, by name, the first time such a
/// record is resolved: make the property nullable and the default belongs wherever the effective value is
/// read, not in the merge.
/// </para>
/// <para>
/// Resolution is eager and total. A chain that loops, or names an entry nobody declared, is a mistake in the
/// configuration rather than in the run that later touched it, and load time is the earliest it can be said.
/// </para>
/// </remarks>
public static class ConfigInheritance
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Inherited = new ConcurrentDictionary<Type, PropertyInfo[]>();

    /// <summary>
    /// Resolves every entry against the chain it declares.
    /// </summary>
    /// <typeparam name="TConfig">The configuration record.</typeparam>
    /// <param name="declared">What the author wrote, by identifier.</param>
    /// <returns>The same identifiers, each with everything it inherits filled in.</returns>
    /// <exception cref="FrameworkConfigurationException">
    /// A chain loops, names an entry that is not declared, or the record has a property that cannot express
    /// "not stated".
    /// </exception>
    public static IReadOnlyDictionary<string, TConfig> Resolve<TConfig>(IReadOnlyDictionary<string, TConfig> declared)
        where TConfig : class, IInheritsConfig, new()
    {
        ArgumentNullException.ThrowIfNull(declared);

        PropertyInfo[] properties = InheritedPropertiesOf<TConfig>();
        Dictionary<string, TConfig> resolved = new Dictionary<string, TConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (string identifier in declared.Keys)
        {
            resolved[identifier] = Resolve(identifier, declared, resolved, properties, []);
        }

        return resolved;
    }

    private static TConfig Resolve<TConfig>(
        string identifier,
        IReadOnlyDictionary<string, TConfig> declared,
        Dictionary<string, TConfig> resolved,
        PropertyInfo[] properties,
        List<string> chain)
        where TConfig : class, IInheritsConfig, new()
    {
        if (resolved.TryGetValue(identifier, out TConfig? already))
        {
            return already;
        }

        if (chain.Contains(identifier, StringComparer.OrdinalIgnoreCase))
        {
            throw new FrameworkConfigurationException(
                $"The configuration entry '{identifier}' inherits from itself: {string.Join(" -> ", chain.Append(identifier))}.",
                ["Break the cycle - one entry in the chain has to stand on its own."]);
        }

        if (!TryFind(declared, identifier, out TConfig? config) || config is null)
        {
            throw new FrameworkConfigurationException(
                $"The configuration entry '{identifier}' is not declared, so nothing can inherit from it.",
                ["Declare it, or point 'BasedOn' at one of the entries that exist."],
                [.. declared.Keys.OrderBy(static key => key, StringComparer.Ordinal)]);
        }

        if (config.BasedOn is not { Length: > 0 } parentIdentifier)
        {
            return config;
        }

        chain.Add(identifier);

        TConfig parent = Resolve(parentIdentifier, declared, resolved, properties, chain);

        chain.RemoveAt(chain.Count - 1);

        return Merge(config, parent, properties);
    }

    /// <summary>
    /// Takes the parent's value for everything the child left unstated.
    /// </summary>
    private static TConfig Merge<TConfig>(TConfig child, TConfig parent, PropertyInfo[] properties)
        where TConfig : class, new()
    {
        TConfig merged = new TConfig();

        foreach (PropertyInfo property in properties)
        {
            property.SetValue(merged, property.GetValue(child) ?? property.GetValue(parent));
        }

        return merged;
    }

    /// <summary>
    /// The properties that take part, checked once per record type.
    /// </summary>
    /// <remarks>
    /// <see cref="IInheritsConfig.BasedOn"/> is left out rather than merged: a resolved entry has already
    /// taken over what its parent held, so a pointer at that parent would say there is more to come.
    /// </remarks>
    private static PropertyInfo[] InheritedPropertiesOf<TConfig>()
        where TConfig : class, IInheritsConfig, new()
        => Inherited.GetOrAdd(typeof(TConfig), static type =>
        {
            PropertyInfo[] writable = [.. type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(static property => property.CanRead && property.CanWrite)
                .Where(static property => !string.Equals(property.Name, nameof(IInheritsConfig.BasedOn), StringComparison.Ordinal))
                .OrderBy(static property => property.Name, StringComparer.Ordinal)];

            string[] unstatable = [.. writable
                .Where(static property => !CanBeUnstated(property.PropertyType))
                .Select(static property => $"{property.Name} ({property.PropertyType.Name})")];

            if (unstatable.Length > 0)
            {
                throw new FrameworkConfigurationException(
                    $"'{type.Name}' has {unstatable.Length} propert(ies) that cannot express \"not stated\", so inheritance would silently discard what a child declared.",
                    [
                        "Make each of them nullable - 'bool?' rather than 'bool' - so that unset is null.",
                        "Apply the default where the effective value is read instead of in the declaration.",
                    ],
                    [.. unstatable]);
            }

            return writable;
        });

    /// <summary>
    /// Whether a value of this type can say "nobody set me".
    /// </summary>
    private static bool CanBeUnstated(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static bool TryFind<TConfig>(IReadOnlyDictionary<string, TConfig> declared, string identifier, out TConfig? config)
        where TConfig : class
    {
        // Identifiers are matched the way configuration matches them everywhere in the family, which is not
        // necessarily how the dictionary handed in was built.
        foreach ((string key, TConfig candidate) in declared)
        {
            if (string.Equals(key, identifier, StringComparison.OrdinalIgnoreCase))
            {
                config = candidate;

                return true;
            }
        }

        config = null;

        return false;
    }
}
