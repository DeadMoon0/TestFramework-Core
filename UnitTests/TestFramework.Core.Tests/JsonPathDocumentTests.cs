using Newtonsoft.Json;
using TestFramework.Core.Json;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TestFramework.Core.Environment.Graph;
using TestFramework.Core.Exceptions;
using Xunit;

namespace TestFramework.Core.Tests;

/// <summary>
/// Composing a generated configuration file: what the payload keeps, what a route overwrites, and what a
/// broken file is told.
/// </summary>
public class WireJsonTests
{
    [Fact]
    public void ATimestampStaysTheStringItWasSentAs()
    {
        // JToken.Parse would hand back a Date token here, and code reading the field as a string would
        // find nothing. Web lost every timestamp in a stub server's call log exactly this way.
        JToken parsed = WireJson.Parse("""{ "startTime": "2026-08-13T10:00:01Z" }""");

        Assert.Equal(JTokenType.String, parsed["startTime"]!.Type);
        Assert.Equal("2026-08-13T10:00:01Z", parsed["startTime"]!.Value<string>());
    }

    [Fact]
    public void EverythingElseParsesAsItNormallyWould()
    {
        JToken parsed = WireJson.Parse("""{ "count": 3, "ok": true, "nested": { "name": "ada" }, "list": [1, 2] }""");

        Assert.Equal(3, parsed["count"]!.Value<int>());
        Assert.True(parsed["ok"]!.Value<bool>());
        Assert.Equal("ada", parsed["nested"]!["name"]!.Value<string>());
        Assert.Equal(2, Assert.IsType<JArray>(parsed["list"]).Count);
    }

    [Fact]
    public void AnArrayPayloadIsAnArray()
    {
        // A management API answering with a bare array is normal; the parser must not require an object.
        Assert.IsType<JArray>(WireJson.Parse("""[ { "name": "first" } ]"""));
    }

    [Fact]
    public void BrokenJsonSaysSoRatherThanReturningNothing()
        => Assert.ThrowsAny<JsonException>(() => WireJson.Parse("{ not json"));
}

public class JsonPathDocumentTests
{
    [Fact]
    public void TwoRoutesThatContradictEachOtherAreRefused()
    {
        // 'Features' says that name holds a value; 'Features:UseFakeClock' says it holds an object. Both
        // cannot be true, and whichever is written second would win silently - so one of the two values
        // the caller asked for would simply vanish, depending on ordering.
        //
        // Container.Web's own settings composer refused this, and Core's did not. That difference is why
        // deleting the duplicate had to come with this check rather than without it.
        JsonPathDocument document = new JsonPathDocument("appsettings.Testing.json");

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => document.Compose(
                new Dictionary<string, string>
                {
                    ["Features"] = "on",
                    ["Features:UseFakeClock"] = "true",
                },
                existing: null));

        Assert.Contains("Features", failure.Message, StringComparison.Ordinal);
        Assert.Contains("silently replace", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameThatMerelyStartsTheSameIsNotAContradiction()
    {
        // 'Feature' is not inside 'Features'; only a colon makes one path contain another.
        JsonPathDocument document = new JsonPathDocument("appsettings.Testing.json");

        string composed = document.Compose(
            new Dictionary<string, string> { ["Feature"] = "one", ["Features"] = "two" },
            existing: null);

        Assert.Contains("\"Feature\": \"one\"", composed, StringComparison.Ordinal);
        Assert.Contains("\"Features\": \"two\"", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void EverythingNobodyRoutedOverSurvives()
    {
        // Generating configuration must never mean discarding configuration: an application's own settings
        // are still its own settings.
        JsonPathDocument document = new JsonPathDocument("appsettings.Testing.json");

        string composed = document.Compose(
            new Dictionary<string, string> { ["ConnectionStrings:Sql"] = "Server=orders-db" },
            existing: """
                {
                  "Logging": { "LogLevel": { "Default": "Warning" } },
                  "ConnectionStrings": { "Audit": "Server=audit" }
                }
                """);

        JObject result = JObject.Parse(composed);

        Assert.Equal("Warning", result["Logging"]!["LogLevel"]!["Default"]!.Value<string>());
        Assert.Equal("Server=audit", result["ConnectionStrings"]!["Audit"]!.Value<string>());
        Assert.Equal("Server=orders-db", result["ConnectionStrings"]!["Sql"]!.Value<string>());
    }

    [Fact]
    public void AColonPathBecomesNestedObjects()
    {
        JsonPathDocument document = new JsonPathDocument("appsettings.json");

        string composed = document.Compose(
            new Dictionary<string, string> { ["Services:Payments:BaseUrl"] = "http://payments:8080/" },
            existing: null);

        Assert.Equal(
            "http://payments:8080/",
            JObject.Parse(composed)["Services"]!["Payments"]!["BaseUrl"]!.Value<string>());
    }

    [Fact]
    public void ARouteWinsOverALeafInItsWay()
    {
        // The payload shipped a value where a route needs a section. The route is the newer statement of
        // intent - and the whole document is logged, so the replacement is visible.
        JsonPathDocument document = new JsonPathDocument("appsettings.json");

        string composed = document.Compose(
            new Dictionary<string, string> { ["Services:Payments"] = "http://payments:8080/" },
            existing: """{ "Services": "none" }""");

        Assert.Equal("http://payments:8080/", JObject.Parse(composed)["Services"]!["Payments"]!.Value<string>());
    }

    [Fact]
    public void TheSameRoutesComposeTheSameFileEveryRun()
    {
        // Ordered output is what makes a generated file readable in a diff and comparable across runs.
        JsonPathDocument document = new JsonPathDocument("appsettings.json");

        Dictionary<string, string> routed = new Dictionary<string, string>
        {
            ["Zulu"] = "last",
            ["Alpha"] = "first",
        };

        Assert.Equal(document.Compose(routed, null), document.Compose(routed, null));
        Assert.True(
            document.Compose(routed, null).IndexOf("Alpha", StringComparison.Ordinal)
                < document.Compose(routed, null).IndexOf("Zulu", StringComparison.Ordinal),
            "routes are written in a stable order");
    }

    [Fact]
    public void ABrokenPayloadFileSaysSoAndSaysWhatToDo()
    {
        JsonPathDocument document = new JsonPathDocument("appsettings.json");

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => document.Compose(new Dictionary<string, string> { ["A"] = "b" }, existing: "{ not json"));

        Assert.Contains("is not valid JSON", failure.Message, StringComparison.Ordinal);
        Assert.Contains("generate the document whole", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APayloadFileThatIsNotAnObjectSaysWhatItIsInstead()
    {
        JsonPathDocument document = new JsonPathDocument("appsettings.json");

        FrameworkConfigurationException failure = Assert.Throws<FrameworkConfigurationException>(
            () => document.Compose(new Dictionary<string, string> { ["A"] = "b" }, existing: "[ 1, 2 ]"));

        Assert.Contains("valid JSON but not an object", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Array", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentReadsWhatThePayloadShipsThroughTheHookItWasGiven()
    {
        JsonPathDocument document = new JsonPathDocument(
            "appsettings.json",
            static () => """{ "Kept": "yes" }""");

        string composed = document.Compose(new Dictionary<string, string>(), document.ReadExisting());

        Assert.Equal("yes", JObject.Parse(composed)["Kept"]!.Value<string>());
    }
}
