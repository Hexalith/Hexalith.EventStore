using Bunit;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.UI.Components;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// bUnit tests for the TypeDetailPanel component.
/// </summary>
public class TypeDetailPanelTests : AdminUITestContext
{
    [Fact]
    public void TypeDetailPanel_RendersEventDetail_WithRelatedCommands()
    {
        // Arrange
        EventTypeInfo selectedEvent = new("OrderCreated", "ordering", false, 1);
        IReadOnlyList<EventTypeInfo> allEvents =
        [
            new("OrderCreated", "ordering", false, 1),
            new("PaymentProcessed", "payments", false, 2),
        ];
        IReadOnlyList<CommandTypeInfo> allCommands =
        [
            new("CreateOrder", "ordering", "OrderAggregate"),
            new("ProcessPayment", "payments", "PaymentAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> allAggregates =
        [
            new("OrderAggregate", "ordering", 1, 1, true),
        ];

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedEvent, selectedEvent)
            .Add(p => p.AllEvents, allEvents)
            .Add(p => p.AllCommands, allCommands)
            .Add(p => p.AllAggregates, allAggregates));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("OrderCreated");
        markup.ShouldContain("ordering");
        markup.ShouldContain("Related Commands");
        markup.ShouldContain("CreateOrder");
        markup.ShouldContain("Related Aggregates");
        markup.ShouldContain("OrderAggregate");
    }

    [Fact]
    public void TypeDetailPanel_RendersCommandDetail_WithTargetAggregateLink()
    {
        // Arrange
        CommandTypeInfo selectedCommand = new("CreateOrder", "ordering", "OrderAggregate");
        IReadOnlyList<EventTypeInfo> allEvents =
        [
            new("OrderCreated", "ordering", false, 1),
        ];
        IReadOnlyList<CommandTypeInfo> allCommands =
        [
            new("CreateOrder", "ordering", "OrderAggregate"),
            new("CancelOrder", "ordering", "OrderAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> allAggregates =
        [
            new("OrderAggregate", "ordering", 1, 2, true),
        ];

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedCommand, selectedCommand)
            .Add(p => p.AllEvents, allEvents)
            .Add(p => p.AllCommands, allCommands)
            .Add(p => p.AllAggregates, allAggregates));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("CreateOrder");
        markup.ShouldContain("ordering");
        markup.ShouldContain("Target Aggregate:");
        markup.ShouldContain("OrderAggregate");
        markup.ShouldContain("Related Events");
        markup.ShouldContain("OrderCreated");
        markup.ShouldContain("Sibling Commands");
        markup.ShouldContain("CancelOrder");
    }

    [Fact]
    public void TypeDetailPanel_RendersAggregateDetail_WithCountsAndProjectionsBadge()
    {
        // Arrange
        AggregateTypeInfo selectedAggregate = new("OrderAggregate", "ordering", 3, 2, true);
        IReadOnlyList<EventTypeInfo> allEvents =
        [
            new("OrderCreated", "ordering", false, 1),
            new("OrderShipped", "ordering", false, 1),
        ];
        IReadOnlyList<CommandTypeInfo> allCommands =
        [
            new("CreateOrder", "ordering", "OrderAggregate"),
        ];
        IReadOnlyList<AggregateTypeInfo> allAggregates =
        [
            new("OrderAggregate", "ordering", 3, 2, true),
        ];

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedAggregate, selectedAggregate)
            .Add(p => p.AllEvents, allEvents)
            .Add(p => p.AllCommands, allCommands)
            .Add(p => p.AllAggregates, allAggregates));

        // Assert
        string markup = cut.Markup;
        markup.ShouldContain("OrderAggregate");
        markup.ShouldContain("ordering");
        markup.ShouldContain("3 events");
        markup.ShouldContain("2 commands");
        markup.ShouldContain("Has Projections");
        markup.ShouldContain("OrderCreated");
        markup.ShouldContain("OrderShipped");
        markup.ShouldContain("CreateOrder");
    }

    [Fact]
    public void TypeDetailPanel_HasCloseButton()
    {
        // Arrange
        EventTypeInfo selectedEvent = new("OrderCreated", "ordering", false, 1);

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedEvent, selectedEvent)
            .Add(p => p.AllEvents, Array.Empty<EventTypeInfo>())
            .Add(p => p.AllCommands, Array.Empty<CommandTypeInfo>())
            .Add(p => p.AllAggregates, Array.Empty<AggregateTypeInfo>()));

        // Assert
        cut.Markup.ShouldContain("Back to List");
    }

    [Fact]
    public void TypeDetailPanel_EventDetail_ShowsRejectionBadge()
    {
        // Arrange
        EventTypeInfo rejectionEvent = new("OrderRejected", "ordering", true, 1);

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedEvent, rejectionEvent)
            .Add(p => p.AllEvents, Array.Empty<EventTypeInfo>())
            .Add(p => p.AllCommands, Array.Empty<CommandTypeInfo>())
            .Add(p => p.AllAggregates, Array.Empty<AggregateTypeInfo>()));

        // Assert
        cut.Markup.ShouldContain("Rejection");
    }

    [Fact]
    public void TypeDetailPanel_AggregateDetail_ShowsNoProjectionsBadge()
    {
        // Arrange
        AggregateTypeInfo aggregate = new("PaymentAggregate", "payments", 1, 1, false);

        // Act
        IRenderedComponent<TypeDetailPanel> cut = Render<TypeDetailPanel>(parameters => parameters
            .Add(p => p.SelectedAggregate, aggregate)
            .Add(p => p.AllEvents, Array.Empty<EventTypeInfo>())
            .Add(p => p.AllCommands, Array.Empty<CommandTypeInfo>())
            .Add(p => p.AllAggregates, Array.Empty<AggregateTypeInfo>()));

        // Assert
        cut.Markup.ShouldContain("No Projections");
    }
}
