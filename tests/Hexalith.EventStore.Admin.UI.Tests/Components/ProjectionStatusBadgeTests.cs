using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the ProjectionStatusBadge component.
/// </summary>
public class ProjectionStatusBadgeTests : AdminUITestContext {
    [Theory]
    [InlineData(ProjectionStatusType.Running, "Running", "\u2714")]
    [InlineData(ProjectionStatusType.Paused, "Paused", "\u23F8")]
    [InlineData(ProjectionStatusType.Error, "Error", "\u2716")]
    [InlineData(ProjectionStatusType.Rebuilding, "Rebuilding", "\u21BB")]
    public void ProjectionStatusBadge_RendersCorrectIconAndLabel(
        ProjectionStatusType status,
        string expectedLabel,
        string expectedIcon) {
        // Act
        IRenderedComponent<ProjectionStatusBadge> cut = Render<ProjectionStatusBadge>(
            parameters => parameters.Add(p => p.Status, status));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain(expectedLabel);
        markup.ShouldContain(expectedIcon);
    }

    [Fact]
    public void ProjectionStatusBadge_Running_UsesSuccessColor() {
        // Act
        IRenderedComponent<ProjectionStatusBadge> cut = Render<ProjectionStatusBadge>(
            parameters => parameters.Add(p => p.Status, ProjectionStatusType.Running));

        // Assert
        cut.Markup.ShouldContain("--hexalith-status-success");
    }

    [Fact]
    public void ProjectionStatusBadge_Error_UsesErrorColor() {
        // Act
        IRenderedComponent<ProjectionStatusBadge> cut = Render<ProjectionStatusBadge>(
            parameters => parameters.Add(p => p.Status, ProjectionStatusType.Error));

        // Assert
        cut.Markup.ShouldContain("--hexalith-status-error");
    }

    [Fact]
    public void ProjectionStatusBadge_HasAriaLabel() {
        // Act
        IRenderedComponent<ProjectionStatusBadge> cut = Render<ProjectionStatusBadge>(
            parameters => parameters.Add(p => p.Status, ProjectionStatusType.Running));

        // Assert
        cut.Markup.ShouldContain("Projection status");
    }
}
