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
public class JsonPathDocumentTests
{
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
