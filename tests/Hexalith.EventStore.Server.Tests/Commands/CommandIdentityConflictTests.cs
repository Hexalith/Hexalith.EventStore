using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class CommandIdentityConflictTests
{
    [Fact]
    public async Task SubmitCommandHandler_IdentityConflict_ThrowsDedicatedExceptionWithoutRejectedStatus()
    {
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(
                false,
                ErrorMessage: "command_identity_conflict",
                CorrelationId: "trace-correlation"));
        var handler = new SubmitCommandHandler(
            statusStore,
            archiveStore,
            router,
            NullLogger<SubmitCommandHandler>.Instance);
        SubmitCommand command = CreateCommand();

        CommandIdentityConflictException exception = await Should.ThrowAsync<CommandIdentityConflictException>(
            () => handler.Handle(command, CancellationToken.None));

        exception.MessageId.ShouldBe(command.MessageId);
        exception.CorrelationId.ShouldBe(command.CorrelationId);
        exception.TenantId.ShouldBe(command.Tenant);
        statusStore.GetStatusHistory(command.Tenant, command.MessageId)
            .ShouldNotContain(record => record.Status == Hexalith.EventStore.Contracts.Commands.CommandStatus.Rejected);
        statusStore.GetStatusHistory(command.Tenant, command.MessageId).ShouldBeEmpty();
        statusStore.GetStatusHistory(command.Tenant, command.CorrelationId).ShouldBeEmpty();
        ArchivedCommand? archived = await archiveStore.ReadCommandAsync(
            command.Tenant,
            command.MessageId,
            CancellationToken.None);
        archived.ShouldBeNull();
    }

    [Fact]
    public async Task ExceptionHandler_IdentityConflict_ReturnsNonRetryableSupportSafe409()
    {
        var handler = new CommandIdentityConflictExceptionHandler(
            NullLogger<CommandIdentityConflictExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers.RetryAfter = "9";
        var exception = new CommandIdentityConflictException(
            "incoming-message-id",
            "trace-correlation",
            "tenant-a");

        bool handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        context.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(context.Response.Body);
        JsonElement root = document.RootElement;
        root.GetProperty("type").GetString().ShouldBe(ProblemTypeUris.CommandIdentityConflict);
        string detail = root.GetProperty("detail").GetString().ShouldNotBeNull();
        detail.ShouldContain("new MessageId");
        detail.ShouldNotContain("tenant-a");
        detail.ShouldNotContain("incoming-message-id");
    }

    [Fact]
    public async Task SubmitCommandHandler_IdentityConflict_PreservesExistingStatusAndArchiveEvidence()
    {
        var statusStore = new InMemoryCommandStatusStore();
        var archiveStore = new InMemoryCommandArchiveStore();
        SubmitCommand command = CreateCommand();
        var originalStatus = new CommandStatusRecord(
            CommandStatus.Completed,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            command.AggregateId,
            1,
            null,
            null,
            null,
            command.MessageId,
            command.CorrelationId);
        var originalArchive = new ArchivedCommand(
            command.Tenant,
            command.Domain,
            command.AggregateId,
            "OriginalCommandType",
            [9, 8, 7],
            null,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            command.MessageId,
            command.CorrelationId);
        await statusStore.WriteStatusAsync(command.Tenant, command.MessageId, originalStatus);
        await archiveStore.WriteCommandAsync(command.Tenant, command.MessageId, originalArchive);
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(command, Arg.Any<CancellationToken>())
            .Returns(new CommandProcessingResult(false, ErrorMessage: "command_identity_conflict"));
        var handler = new SubmitCommandHandler(
            statusStore,
            archiveStore,
            router,
            NullLogger<SubmitCommandHandler>.Instance);

        _ = await Should.ThrowAsync<CommandIdentityConflictException>(
            () => handler.Handle(command, CancellationToken.None));

        CommandStatusRecord persistedStatus = (await statusStore.ReadStatusAsync(
            command.Tenant,
            command.MessageId,
            CancellationToken.None)).ShouldNotBeNull();
        persistedStatus.ShouldBe(originalStatus);
        statusStore.GetStatusHistory(command.Tenant, command.MessageId).ShouldBe([originalStatus]);
        ArchivedCommand persistedArchive = (await archiveStore.ReadCommandAsync(
            command.Tenant,
            command.MessageId,
            CancellationToken.None)).ShouldNotBeNull();
        persistedArchive.CommandType.ShouldBe("OriginalCommandType");
        persistedArchive.Payload.ShouldBe([9, 8, 7]);
    }

    private static SubmitCommand CreateCommand() => new(
        MessageId: "01IDENTITYCONFLICT0000000001",
        Tenant: "tenant-a",
        Domain: "orders",
        AggregateId: "order-1",
        CommandType: "CreateOrder",
        Payload: [1],
        CorrelationId: "01IDENTITYTRACE000000000001",
        UserId: "user-1");
}
