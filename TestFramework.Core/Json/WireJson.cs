using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestFramework.Core.Json;

/// <summary>
/// Reading JSON that somebody else wrote.
/// </summary>
/// <remarks>
/// <para>
/// Every package that talks to something reads a payload it did not write: a stub server's request log, a
/// management API's list of runs, a container's health response. This is the one way to parse one, because
/// it is the same problem each of them would otherwise solve slightly differently - and a package's job is
/// its own job, not general JSON machinery.
/// </para>
/// <para>
/// The reason it exists at all is that <see cref="JToken.Parse(string)"/> does not leave a payload alone.
/// By default it recognises ISO-8601 text and hands back a <c>Date</c> token instead of the string that was
/// actually sent, so code reading that field as a string finds nothing there. Web lost every timestamp in a
/// stub server's call log that way, and Azure reads Logic App start times the same shape.
/// </para>
/// <para>
/// A payload nothing here wrote is data to be read, not text to be reinterpreted.
/// </para>
/// </remarks>
public static class WireJson
{
    /// <summary>
    /// Parses a payload exactly as it arrived.
    /// </summary>
    /// <param name="payload">The JSON text.</param>
    /// <returns>The parsed tree, with strings still strings.</returns>
    /// <exception cref="JsonReaderException">The payload is not valid JSON.</exception>
    public static JToken Parse(string payload)
    {
        using JsonTextReader reader = new JsonTextReader(new StringReader(payload))
        {
            DateParseHandling = DateParseHandling.None,
        };

        return JToken.ReadFrom(reader);
    }
}
