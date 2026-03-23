using Bunit;

using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the JsonViewer component.
/// </summary>
public class JsonViewerTests : AdminUITestContext
{
    [Fact]
    public void JsonViewer_RendersIndentedJson_WithSyntaxHighlighting()
    {
        // Arrange
        string json = """{"name":"test","count":42,"active":true,"data":null}""";

        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, json)
            .Add(p => p.AriaLabel, "Test JSON"));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("json-viewer");
        markup.ShouldContain("json-key");
        markup.ShouldContain("role=\"code\"");
        markup.ShouldContain("aria-label=\"Test JSON\"");
    }

    [Fact]
    public void JsonViewer_ShowsNoData_WhenJsonIsNull()
    {
        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, null as string));

        // Assert
        cut.Markup.ShouldContain("No data");
    }

    [Fact]
    public void JsonViewer_ShowsNoData_WhenJsonIsEmpty()
    {
        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, ""));

        // Assert
        cut.Markup.ShouldContain("No data");
    }

    [Fact]
    public void JsonViewer_ShowsWarning_WhenJsonIsInvalid()
    {
        // Arrange
        string invalidJson = "not valid json {{{";

        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, invalidJson));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("Invalid JSON");
        markup.ShouldContain(invalidJson);
    }

    [Fact]
    public void JsonViewer_RendersLineNumbers()
    {
        // Arrange
        string json = """{"a":1,"b":2}""";

        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert
        cut.Markup.ShouldContain("json-line-number");
    }
}
