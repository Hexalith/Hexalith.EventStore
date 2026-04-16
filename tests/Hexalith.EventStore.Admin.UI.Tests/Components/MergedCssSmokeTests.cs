using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Components;
using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Story 21-8 bUnit render-snapshot micro-tests.
/// Option A merged 9 .razor.css files into wwwroot/css/app.css and deleted the sources.
/// These tests assert each component still emits the exact class attributes the merged
/// CSS rules target — catching selector-collision regressions at build time rather than
/// at DevTools-inspection time during visual sweep.
/// Exact-class matches are used per Pre-mortem F5 — no substring or regex fuzzy matching.
/// </summary>
public class MergedCssSmokeTests : AdminUITestContext {
    [Fact]
    public void ProjectionStatusBadge_Renders_WithExpectedClassAttributes() {
        // Arrange / Act — ProjectionStatusBadge delegates to StatusBadge which renders
        // <span class="status-badge" ...><span class="status-badge__icon">...</span>
        // <span class="status-badge__label">...</span></span>.
        IRenderedComponent<ProjectionStatusBadge> cut = Render<ProjectionStatusBadge>(
            parameters => parameters.Add(p => p.Status, ProjectionStatusType.Running));

        // Assert — merged app.css targets .status-badge__label in a forced-colors @media block;
        // the class hook must remain in the rendered tree.
        AngleSharp.Dom.IElement root = cut.Find("span.status-badge");
        root.ClassList.ShouldContain("status-badge");

        AngleSharp.Dom.IElement icon = cut.Find("span.status-badge__icon");
        icon.ClassList.ShouldContain("status-badge__icon");

        AngleSharp.Dom.IElement label = cut.Find("span.status-badge__label");
        label.ClassList.ShouldContain("status-badge__label");
    }

    [Fact]
    public void StateDiffViewer_Renders_WithRootClass() {
        // Arrange / Act — StateDiffViewer root element emits <div class="state-diff-viewer" ...>.
        // Without a real ApiClient the component stays in the loading branch, but the
        // root class is emitted before loading completes.
        IRenderedComponent<StateDiffViewer> cut = Render<StateDiffViewer>(parameters => parameters
            .Add(p => p.TenantId, "tenant-1")
            .Add(p => p.Domain, "Counter")
            .Add(p => p.AggregateId, "agg-1")
            .Add(p => p.FromSequence, 1L)
            .Add(p => p.ToSequence, 2L));

        // Assert — merged app.css targets .state-diff-viewer, .diff-field-path,
        // .diff-old-value, .diff-new-value. Root must carry the anchor class.
        AngleSharp.Dom.IElement root = cut.Find("div.state-diff-viewer");
        root.ClassList.ShouldContain("state-diff-viewer");
    }

    [Fact]
    public void JsonViewer_Renders_WithSyntaxHighlightClasses() {
        // Arrange — minimal valid JSON exercising key/number/string/boolean/null token classes.
        const string json = "{\"k\":1,\"s\":\"x\",\"b\":true,\"n\":null}";

        // Act
        IRenderedComponent<JsonViewer> cut = Render<JsonViewer>(parameters => parameters
            .Add(p => p.Json, json));

        // Assert — root has .json-viewer. Content renders .json-line / .json-line-number
        // wrappers and .json-key / .json-string / .json-number / .json-boolean / .json-null
        // syntax spans. All are selectors in the merged JsonViewer block.
        AngleSharp.Dom.IElement root = cut.Find("div.json-viewer");
        root.ClassList.ShouldContain("json-viewer");

        string markup = cut.Markup;
        markup.ShouldContain("class=\"json-line\"");
        markup.ShouldContain("class=\"json-line-number\"");
        markup.ShouldContain("class=\"json-key\"");
        markup.ShouldContain("class=\"json-string\"");
        markup.ShouldContain("class=\"json-number\"");
        markup.ShouldContain("class=\"json-boolean\"");
        markup.ShouldContain("class=\"json-null\"");
    }
}
