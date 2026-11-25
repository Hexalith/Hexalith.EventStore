using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public sealed class AdminTraceCorrelationIndexTests
{
    private const string CorrelationId = "trace-correlation";
    private const string MessageId = "command-message";

    [Fact]
    public async Task UniqueCorrelationReadsMessagePrimaryStatusAndFiltersEventsByCorrelation()
    {
        string tenant = Dw3TestUtilities.TenantId;
        var index = new InMemoryCommandCorrelationIndex();
        _ = await index.AddAsync(tenant, CorrelationId, MessageId);
        var statuses = new InMemoryCommandStatusStore();
        await statuses.WriteStatusAsync(tenant, MessageId, CreateStatus(MessageId));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, "{}", corrId: CorrelationId),
            Dw3TestUtilities.BuildEnvelope(2, "{}", corrId: MessageId),
        ]);
        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, statuses, correlationIndex: index);

        IActionResult result = await controller.GetCorrelationTraceMap(tenant, CorrelationId,
            Dw3TestUtilities.Domain, aggregateId: null, TestContext.Current.CancellationToken);

        var map = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CorrelationTraceMap>();
        map.CorrelationId.ShouldBe(CorrelationId);
        map.CommandStatus.ShouldBe(nameof(CommandStatus.Completed));
        map.AggregateId.ShouldBe(Dw3TestUtilities.AggregateId);
        map.ProducedEvents.Single().SequenceNumber.ShouldBe(1);
    }

    [Fact]
    public async Task AmbiguousCorrelationReturnsConflictWithoutSelectingAnOwner()
    {
        string tenant = Dw3TestUtilities.TenantId;
        var index = new InMemoryCommandCorrelationIndex();
        _ = await index.AddAsync(tenant, CorrelationId, MessageId);
        _ = await index.AddAsync(tenant, CorrelationId, "second-message");
        ICommandStatusStore statuses = Substitute.For<ICommandStatusStore>();
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, statuses, correlationIndex: index);

        IActionResult result = await controller.GetCorrelationTraceMap(tenant, CorrelationId,
            Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId, TestContext.Current.CancellationToken);

        ObjectResult conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        conflict.Value.ShouldBeOfType<ProblemDetails>().Title.ShouldBe("Ambiguous Command Correlation");
        statuses.ReceivedCalls().ShouldBeEmpty();
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingAdvisoryIndexDoesNotDiscoverAnUnindexedCommand()
    {
        string tenant = Dw3TestUtilities.TenantId;
        var index = new InMemoryCommandCorrelationIndex();
        var statuses = new InMemoryCommandStatusStore();
        await statuses.WriteStatusAsync(tenant, MessageId, CreateStatus(MessageId));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, statuses, correlationIndex: index);

        IActionResult result = await controller.GetCorrelationTraceMap(tenant, CorrelationId,
            domain: null, aggregateId: null, TestContext.Current.CancellationToken);

        var map = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<CorrelationTraceMap>();
        map.CommandStatus.ShouldBe("Unknown");
        map.ProducedEvents.ShouldBeEmpty();
        map.ErrorMessage.ShouldNotBeNull().ShouldContain("Command status not found");
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    private static CommandStatusRecord CreateStatus(string messageId)
        => new(CommandStatus.Completed, DateTimeOffset.UtcNow, Dw3TestUtilities.AggregateId,
            1, null, null, null, MessageId: messageId, CorrelationId: CorrelationId);
}
