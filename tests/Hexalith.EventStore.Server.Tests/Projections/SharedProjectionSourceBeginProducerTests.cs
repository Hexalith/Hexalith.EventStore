using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class SharedProjectionSourceBeginProducerTests
{
    [Fact]
    public async Task PermanentBeginRejectionRequestsRecoverableSealedAbort()
    {
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "rejected-begin", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool begun = false;
        bool sealedCut = false;
        string? fingerprint = null;
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { begun = true; return 5L; });
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => begun
            ? new SharedProjectionSourceCaptureProgress(5, sealedCut, true, false,
                fingerprint, 0, false) : null);
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>())
            .Returns(new SharedProjectionSourceRosterPage(5, []));
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(true);
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>())
            .Returns(new SharedProjectionCapturedSourcePage(5, []));
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        {
            sealedCut = true;
            fingerprint = call.ArgAt<string>(2);
            return true;
        });
        roster.AbortRejectedCaptureAsync(scope, identity.OperationId).Returns(true);
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        var gateway = Substitute.For<ISharedProjectionRebuildGateway>();
        gateway.SendAsync(Arg.Any<DomainSharedProjectionRebuildRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DomainSharedProjectionRebuildResponse(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Failed, 0, DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null, ProjectionDispatchReasonCodes.DeliveryIdentityConflict)));
        var producer = new SharedProjectionSourceBeginProducer(proxies,
            Substitute.For<IProjectionDeliveryHistoryReader>(), gateway,
            Options.Create(new EventStoreActorOptions()),
            Options.Create(new SharedProjectionSourceCaptureOptions { EnabledDomains = ["timesheets"] }));

        await Should.ThrowAsync<InvalidOperationException>(() => producer.RebuildAsync(identity));
        _ = await roster.Received(1).AbortRejectedCaptureAsync(scope, identity.OperationId);
        _ = await roster.DidNotReceiveWithAnyArgs().ReleaseAppendFenceAsync(default!, default!);
    }

    [Fact]
    public async Task TenThousandHighEntropyOwnersStreamMultipleEventsWithoutRetainingHistories()
    {
        const int ownerCount = 10_000;
        const int eventsPerOwner = 3;
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "many-owner-pages", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        AggregateIdentity[] owners = [.. Enumerable.Range(0, ownerCount)
            .Select(index => new AggregateIdentity("tenant-a", "timesheets", $"owner-{index:D5}"))];
        AggregateIdentity[][] pages = [.. Enumerable.Range(0, 64)
            .Select(partition => owners.Where((_, index) => index % 64 == partition).ToArray())];
        SharedProjectionCapturedSourceOwner[][] captured = new SharedProjectionCapturedSourceOwner[64][];
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool started = false;
        bool sealedCut = false;
        bool released = false;
        bool accumulated = false;
        string? inventoryFingerprint = null;
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { started = true; return 5L; });
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => started
            ? new SharedProjectionSourceCaptureProgress(5, sealedCut, !released,
                accumulated, inventoryFingerprint, ownerCount, false) : null);
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(5, pages[call.ArgAt<int>(2)]));
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
        {
            captured[call.ArgAt<int>(2)] = [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
            return true;
        });
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        { sealedCut = true; inventoryFingerprint = call.ArgAt<string>(2); return true; });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionCapturedSourcePage(5, captured[call.ArgAt<int>(2)]));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ => { released = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { accumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(true);

        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, eventsPerOwner));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregate);
        int activeReaders = 0;
        int peakReaders = 0;
        int pageReads = 0;
        async IAsyncEnumerable<ProjectionEventDto[]> History(AggregateIdentity owner)
        {
            activeReaders++;
            peakReaders = Math.Max(peakReaders, activeReaders);
            try
            {
                for (int sequence = 1; sequence <= eventsPerOwner; sequence++)
                {
                    pageReads++;
                    byte[] payload = SHA512.HashData(Encoding.UTF8.GetBytes($"{owner.AggregateId}:{sequence}"));
                    await Task.Yield();
                    yield return [new ProjectionEventDto("recorded", payload, "json", sequence,
                        DateTimeOffset.UnixEpoch, "correlation-a", $"{owner.AggregateId}-{sequence}",
                        "user-a", sequence)];
                }
            }
            finally { activeReaders--; }
        }

        var histories = Substitute.For<IProjectionDeliveryHistoryReader>();
        histories.ReadPagesAsync(Arg.Any<AggregateIdentity>(), eventsPerOwner,
            Arg.Any<CancellationToken>()).Returns(call => History(call.ArgAt<AggregateIdentity>(0)));
        histories.ReadAsync(Arg.Any<AggregateIdentity>(), eventsPerOwner,
            Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ProjectionEventDto>>>(_ =>
                throw new InvalidOperationException("Bulk history path must not run."));
        int sentOwners = 0;
        var beginParts = new List<DomainSharedProjectionRebuildRequest>();
        DomainSharedProjectionRebuildRequest? completedBegin = null;
        var gateway = Substitute.For<ISharedProjectionRebuildGateway>();
        gateway.SendAsync(Arg.Any<DomainSharedProjectionRebuildRequest>(),
            Arg.Any<CancellationToken>()).Returns(call =>
        {
            DomainSharedProjectionRebuildRequest request = call.ArgAt<DomainSharedProjectionRebuildRequest>(0);
            if (request.Action == DomainSharedProjectionRebuildAction.BeginPart)
            {
                beginParts.Add(request);
                JsonSerializer.SerializeToUtf8Bytes(request).Length.ShouldBeLessThanOrEqualTo(524_288);
            }
            if (request.Action == DomainSharedProjectionRebuildAction.Begin) completedBegin = request;
            if (request.Action == DomainSharedProjectionRebuildAction.Accumulate)
            {
                released.ShouldBeTrue();
                request.AggregateOrdinal.ShouldBe(sentOwners++);
                request.Events!.Length.ShouldBe(eventsPerOwner);
            }
            DomainSharedProjectionRebuildPhase phase = request.Action switch
            {
                DomainSharedProjectionRebuildAction.Finalize => DomainSharedProjectionRebuildPhase.Finalized,
                DomainSharedProjectionRebuildAction.Stage => DomainSharedProjectionRebuildPhase.Prepared,
                DomainSharedProjectionRebuildAction.Commit => DomainSharedProjectionRebuildPhase.Committed,
                _ => DomainSharedProjectionRebuildPhase.Accumulating
            };
            return Task.FromResult(new DomainSharedProjectionRebuildResponse(
                DomainSharedProjectionRebuildProtocol.Version, phase, ProjectionDispatchStatus.Completed,
                sentOwners, request.ExpectedInventoryFingerprint ?? request.CaptureInventoryFingerprint
                    ?? "fingerprint", null, null));
        });
        var producer = new SharedProjectionSourceBeginProducer(proxies, histories, gateway,
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));

        (await producer.RebuildAsync(identity)).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        sentOwners.ShouldBe(ownerCount);
        pageReads.ShouldBe(ownerCount * eventsPerOwner * 2);
        peakReaders.ShouldBe(1);
        producer.PeakRetainedOwnerMetadata.ShouldBeLessThanOrEqualTo(2_000);
        beginParts.Count.ShouldBeGreaterThan(0);
        completedBegin.ShouldNotBeNull().PartCount.ShouldBe(beginParts.Count);
        completedBegin.ExpectedAggregateCount.ShouldBe(ownerCount);
        beginParts.SelectMany(part => part.SourceHighWatermarks!.Keys).Distinct(StringComparer.Ordinal)
            .Count().ShouldBe(ownerCount);
    }

    [Fact]
    public async Task NearLimitPagedOwnerUsesSerializedBodyBoundForEveryTransportPart()
    {
        var owner = new AggregateIdentity("tenant-a", "timesheets", "owner-large");
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "near-limit-paged", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        ProjectionEventDto[] events =
        [
            new("recorded", RandomNumberGenerator.GetBytes(1_048_576), "json", 1,
                DateTimeOffset.UnixEpoch, "correlation-a", "message-a", "user-a", 1),
            new("recorded", RandomNumberGenerator.GetBytes(1_048_576), "json", 2,
                DateTimeOffset.UnixEpoch, "correlation-b", "message-b", "user-a", 2),
            .. Enumerable.Range(3, 9_998).Select(sequence => new ProjectionEventDto(
                "recorded", RandomNumberGenerator.GetBytes(128), "json", sequence,
                DateTimeOffset.UnixEpoch, "correlation-rest", "message-" + sequence,
                "user-a", sequence))
        ];
        async IAsyncEnumerable<ProjectionEventDto[]> Pages()
        {
            foreach (ProjectionEventDto item in events)
            {
                await Task.Yield();
                yield return [item];
            }
        }

        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool started = false;
        bool sealedCut = false;
        bool released = false;
        bool accumulated = false;
        string? fingerprint = null;
        SharedProjectionCapturedSourceOwner[] captured = [];
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { started = true; return 5L; });
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => started
            ? new SharedProjectionSourceCaptureProgress(5, sealedCut, !released,
                accumulated, fingerprint, 1, false) : null);
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(5, call.Arg<int>() == 0 ? [owner] : []));
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
        {
            if (call.ArgAt<int>(2) == 0) captured = [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
            return true;
        });
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        { sealedCut = true; fingerprint = call.ArgAt<string>(2); return true; });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionCapturedSourcePage(5, call.Arg<int>() == 0 ? captured : []));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ => { released = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { accumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(true);
        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 10_000));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), "AggregateActor")
            .Returns(aggregate);
        var histories = Substitute.For<IProjectionDeliveryHistoryReader>();
        histories.ReadPagesAsync(owner, 10_000, Arg.Any<CancellationToken>()).Returns(_ => Pages());
        histories.ReadAsync(owner, 10_000, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ProjectionEventDto>>>(_ =>
                throw new InvalidOperationException("Bulk history path must not run."));
        var gateway = new CapturingGateway(() => released);
        var producer = new SharedProjectionSourceBeginProducer(proxies, histories, gateway,
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));

        (await producer.RebuildAsync(identity)).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        DomainSharedProjectionRebuildRequest[] parts = [.. gateway.Requests.Where(request =>
            request.Action == DomainSharedProjectionRebuildAction.AccumulatePart)];
        parts.Length.ShouldBeGreaterThan(2);
        parts[0].Events!.Select(item => item.MessageId).ShouldBe(["message-a"]);
        parts[1].Events![0].MessageId.ShouldBe("message-b");
        ProjectionEventDto[] transmitted = [.. parts.SelectMany(part => part.Events!)];
        transmitted.Length.ShouldBe(10_000);
        transmitted[^1].MessageId.ShouldBe("message-10000");
        foreach (DomainSharedProjectionRebuildRequest part in parts)
            JsonSerializer.SerializeToUtf8Bytes(part).Length.ShouldBeLessThanOrEqualTo(2_097_152);
        gateway.Requests.ShouldNotContain(request =>
            request.Action == DomainSharedProjectionRebuildAction.Accumulate);
        gateway.Requests.ShouldContain(request =>
            request.Action == DomainSharedProjectionRebuildAction.AccumulateComplete
            && request.PartCount == parts.Length);
    }

    [Fact]
    public async Task UnconfiguredDomainCannotStartSourceCapture()
    {
        var producer = new SharedProjectionSourceBeginProducer(Substitute.For<IActorProxyFactory>(),
            Substitute.For<IProjectionDeliveryHistoryReader>(), Substitute.For<ISharedProjectionRebuildGateway>(),
            Options.Create(new EventStoreActorOptions()),
            Options.Create(new SharedProjectionSourceCaptureOptions()));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => producer.RebuildAsync(
            new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets", "capability", "operation-a",
                "catalog-a")));
    }

    [Theory]
    [InlineData("tenant-b", "timesheets", "v1")]
    [InlineData("tenant-a", "orders", "v1")]
    [InlineData("tenant-a", "timesheets", "v2")]
    public async Task GatewayRejectsMisroutedDomainRegistrationBeforeDaprInvocation(
        string tenant, string domain, string version)
    {
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        resolver.ResolveAsync("tenant-a", "timesheets", "v1", Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("domain-app", "process", tenant, domain, version));
        var gateway = new DaprSharedProjectionRebuildGateway(Substitute.For<DaprClient>(),
            Substitute.For<IHttpClientFactory>(), resolver);
        var request = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Begin,
            new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets", "capability",
                "operation-a", "catalog-a"));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => gateway.SendAsync(request));
    }

    [Fact]
    public async Task GatewayBoundsChunkedResponseWithoutContentLength()
    {
        byte[] oversized = new byte[1_048_577];
        using var compressed = new MemoryStream(Compress(oversized));
        using var content = new StreamContent(new GZipStream(compressed, CompressionMode.Decompress,
            leaveOpen: true));
        content.Headers.ContentLength.ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            DaprSharedProjectionRebuildGateway.ReadBoundedResponseAsync(
                content, TestContext.Current.CancellationToken));

        var valid = new DomainSharedProjectionRebuildResponse(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildPhase.Committed, ProjectionDispatchStatus.Completed, 0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null);
        using var validContent = new StreamContent(new GZipStream(
            new MemoryStream(Compress(JsonSerializer.SerializeToUtf8Bytes(valid))),
            CompressionMode.Decompress));
        validContent.Headers.ContentLength.ShouldBeNull();
        (await DaprSharedProjectionRebuildGateway.ReadBoundedResponseAsync(
            validContent, TestContext.Current.CancellationToken)).ShouldBe(valid);
    }

    [Fact]
    public async Task GatewayBoundsActualOwnerRequestBodyWithoutContentLength()
    {
        using var oversized = new StreamContent(new GZipStream(
            new MemoryStream(Compress(new byte[DaprSharedProjectionRebuildGateway.MaximumOwnerRequestBytes + 1])),
            CompressionMode.Decompress));
        oversized.Headers.ContentLength.ShouldBeNull();
        bool rejected = false;
        try
        {
            await DaprSharedProjectionRebuildGateway.BufferBoundedOwnerRequestAsync(
                oversized, TestContext.Current.CancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            rejected = true;
        }
        rejected.ShouldBeTrue();

        using var valid = new StreamContent(new MemoryStream(new byte[16_384]));
        await DaprSharedProjectionRebuildGateway.BufferBoundedOwnerRequestAsync(
            valid, TestContext.Current.CancellationToken);
    }

    private static byte[] Compress(byte[] source)
    {
        using var output = new MemoryStream();
        using (var compressor = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressor.Write(source);
        }

        return output.ToArray();
    }

    [Fact]
    public async Task PendingFirstWriteIsReconciledFromDurableActorHeadBeforeCapture()
    {
        var owner = new AggregateIdentity("tenant-a", "timesheets", "owner-before-first-event");
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "recover-pending", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool settled = false;
        bool started = false;
        bool sealedCut = false;
        bool released = false;
        bool accumulated = false;
        string? fingerprint = null;
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ =>
        {
            if (!settled) return (long?)null;
            started = true;
            return 5L;
        });
        roster.ReadPendingWritesAsync(scope, identity.OperationId).Returns(
            [new SharedProjectionSourcePendingWrite("intent-a", owner)]);
        roster.CompleteWriteAsync(owner, "intent-a").Returns(_ => { settled = true; return true; });
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => started
            ? new SharedProjectionSourceCaptureProgress(5, sealedCut, !released,
                accumulated, fingerprint, 0, false) : null);
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(_ =>
            new SharedProjectionSourceRosterPage(5, []));
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(true);
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        { sealedCut = true; fingerprint = call.ArgAt<string>(2); return true; });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(_ =>
            new SharedProjectionCapturedSourcePage(5, []));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ => { released = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { accumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(true);
        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(false, 0));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), "AggregateActor")
            .Returns(aggregate);
        var producer = new SharedProjectionSourceBeginProducer(proxies,
            Substitute.For<IProjectionDeliveryHistoryReader>(), new CapturingGateway(() => released),
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));

        (await producer.RebuildAsync(identity)).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        settled.ShouldBeTrue();
        _ = await aggregate.Received(1).GetStreamMetadataAsync();
    }

    [Fact]
    public async Task UnreadablePresealHistoryRequestsSafeAbort()
    {
        var owner = new AggregateIdentity("tenant-a", "timesheets", "owner-a");
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "abort-preseal", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(5L);
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(
            new SharedProjectionSourceCaptureProgress(5, false, true, false, null, 1, false));
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(5, call.Arg<int>() == 0 ? [owner] : []));
        roster.AbortUnsealedCaptureAsync(scope, identity.OperationId).Returns(true);
        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 1));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), "AggregateActor")
            .Returns(aggregate);
        var histories = Substitute.For<IProjectionDeliveryHistoryReader>();
        histories.ReadAsync(owner, 1, Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ProjectionEventDto>>>(
            _ => throw new IOException("Authoritative history unavailable"));
        var producer = new SharedProjectionSourceBeginProducer(proxies, histories,
            Substitute.For<ISharedProjectionRebuildGateway>(), Options.Create(new EventStoreActorOptions()),
            Options.Create(new SharedProjectionSourceCaptureOptions { EnabledDomains = ["timesheets"] }));

        _ = await Should.ThrowAsync<IOException>(() => producer.RebuildAsync(identity));
        _ = await roster.Received(1).AbortUnsealedCaptureAsync(scope, identity.OperationId);
    }

    [Theory]
    [InlineData(DomainSharedProjectionRebuildAction.Begin)]
    [InlineData(DomainSharedProjectionRebuildAction.Accumulate)]
    [InlineData(DomainSharedProjectionRebuildAction.Finalize)]
    [InlineData(DomainSharedProjectionRebuildAction.Stage)]
    [InlineData(DomainSharedProjectionRebuildAction.Commit)]
    public async Task DurablePhaseTransportLossResumesSameFrozenCut(
        DomainSharedProjectionRebuildAction lostResponse)
    {
        var owner = new AggregateIdentity("tenant-a", "timesheets", "owner-a");
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "recovery-a", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool begun = false;
        bool sealedCut = false;
        bool appendReleased = false;
        bool ownersAccumulated = false;
        bool closed = false;
        string? fingerprint = null;
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ => begun
            ? new SharedProjectionSourceCaptureProgress(5, sealedCut, !appendReleased,
                ownersAccumulated, fingerprint, 1, closed) : null);
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { begun = true; return 5L; });
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(5, call.Arg<int>() == 0 ? [owner] : []));
        SharedProjectionCapturedSourceOwner[] storedCapture = [];
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
        {
            if (call.ArgAt<int>(2) == 0)
                storedCapture = [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
            return true;
        });
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        { sealedCut = true; fingerprint = call.ArgAt<string>(2); return true; });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionCapturedSourcePage(5, call.Arg<int>() == 0
                ? storedCapture : []));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ =>
        { appendReleased = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { ownersAccumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(_ =>
        { closed = true; return true; });
        var aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 1));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(owner.ActorId), "AggregateActor")
            .Returns(aggregate);
        var histories = Substitute.For<IProjectionDeliveryHistoryReader>();
        histories.ReadAsync(owner, 1, Arg.Any<CancellationToken>()).Returns([
            new ProjectionEventDto("recorded", [1], "json", 1, DateTimeOffset.UnixEpoch,
                "correlation-a", "message-a", "user-a", 7)]);
        var gateway = new LostResponseGateway(lostResponse);
        var producer = new SharedProjectionSourceBeginProducer(proxies, histories, gateway,
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));

        _ = await Should.ThrowAsync<IOException>(() => producer.RebuildAsync(identity));
        sealedCut.ShouldBeTrue();
        if (lostResponse == DomainSharedProjectionRebuildAction.Begin)
            appendReleased.ShouldBeFalse();
        else
            appendReleased.ShouldBeTrue();
        if (lostResponse is DomainSharedProjectionRebuildAction.Begin
            or DomainSharedProjectionRebuildAction.Accumulate)
            ownersAccumulated.ShouldBeFalse();
        (await producer.RebuildAsync(identity)).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        closed.ShouldBeTrue();
        gateway.BeginCount.ShouldBe(lostResponse is DomainSharedProjectionRebuildAction.Begin
            or DomainSharedProjectionRebuildAction.Accumulate ? 2 : 1);
    }

    [Fact]
    public async Task ProducerSealsActorOwnedActiveAndErasedOwnersBeforeReleasingCapture()
    {
        var active = new AggregateIdentity("tenant-a", "timesheets", "owner-a");
        var erased = new AggregateIdentity("tenant-a", "timesheets", "owner-b");
        var identity = new DomainSharedProjectionRebuildIdentity("tenant-a", "timesheets",
            "capability", "operation-a", "catalog-a");
        var scope = new SharedProjectionSourceScope(identity.TenantId, identity.Domain);
        var roster = Substitute.For<ISharedProjectionSourceRosterActor>();
        bool sealedCapture = false;
        bool started = false;
        bool released = false;
        bool accumulated = false;
        string? sealedFingerprint = null;
        roster.ReadCaptureProgressAsync(scope, identity.OperationId).Returns(_ =>
            started ? new SharedProjectionSourceCaptureProgress(5, sealedCapture, !released,
                accumulated, sealedFingerprint, 2, false) : null);
        roster.BeginCaptureAsync(scope, identity.OperationId).Returns(_ => { started = true; return 5L; });
        roster.ReadPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionSourceRosterPage(5,
                call.Arg<int>() == 0 ? [active, erased] : []));
        SharedProjectionCapturedSourceOwner[] capturedOwners = [];
        roster.WriteCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>(),
            Arg.Any<IReadOnlyList<SharedProjectionCapturedSourceOwner>>()).Returns(call =>
        {
            if (call.ArgAt<int>(2) == 0)
                capturedOwners = [.. call.ArgAt<IReadOnlyList<SharedProjectionCapturedSourceOwner>>(3)];
            return true;
        });
        roster.SealCaptureAsync(scope, identity.OperationId, Arg.Any<string>(),
            identity.ProjectionType, identity.CatalogFingerprint).Returns(call =>
        {
            sealedCapture = true;
            sealedFingerprint = call.ArgAt<string>(2);
            return true;
        });
        roster.ReadCapturedPageAsync(scope, identity.OperationId, Arg.Any<int>()).Returns(call =>
            new SharedProjectionCapturedSourcePage(5, call.Arg<int>() == 0
                ? capturedOwners : []));
        roster.ReleaseAppendFenceAsync(scope, identity.OperationId).Returns(_ => { released = true; return true; });
        roster.MarkOwnersAccumulatedAsync(scope, identity.OperationId).Returns(_ =>
        { accumulated = true; return true; });
        roster.CompleteCaptureAsync(scope, identity.OperationId).Returns(_ => accumulated);
        var activeActor = Substitute.For<IAggregateActor>();
        activeActor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 1));
        var erasedActor = Substitute.For<IAggregateActor>();
        erasedActor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(false, 0, true));
        var proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(new ActorId(scope.ActorId),
            SharedProjectionSourceRosterActor.ActorTypeName).Returns(roster);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(active.ActorId), "AggregateActor")
            .Returns(activeActor);
        proxies.CreateActorProxy<IAggregateActor>(new ActorId(erased.ActorId), "AggregateActor")
            .Returns(erasedActor);
        var history = Substitute.For<IProjectionDeliveryHistoryReader>();
        history.ReadAsync(active, 1, Arg.Any<CancellationToken>()).Returns([
            new ProjectionEventDto("recorded", [1, 2], "json", 1, DateTimeOffset.UnixEpoch,
                "correlation-a", "message-a", "user-a", 1)]);
        var gateway = new CapturingGateway(() => released);
        var producer = new SharedProjectionSourceBeginProducer(proxies, history, gateway,
            Options.Create(new EventStoreActorOptions()), Options.Create(new SharedProjectionSourceCaptureOptions
                { EnabledDomains = ["timesheets"] }));

        DomainSharedProjectionRebuildResponse result = await producer.RebuildAsync(identity);

        result.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        released.ShouldBeTrue();
        gateway.Requests.Select(request => request.Action).ShouldBe([
            DomainSharedProjectionRebuildAction.Begin,
            DomainSharedProjectionRebuildAction.Accumulate,
            DomainSharedProjectionRebuildAction.Accumulate,
            DomainSharedProjectionRebuildAction.Finalize,
            DomainSharedProjectionRebuildAction.Stage,
            DomainSharedProjectionRebuildAction.Commit]);
        DomainSharedProjectionRebuildRequest begin = gateway.Requests[0];
        begin.SourceHighWatermarks!.ShouldBe(new Dictionary<string, long>
            { [active.AggregateId] = 1, [erased.AggregateId] = 0 });
        begin.ErasedOwnerIds.ShouldBe([erased.AggregateId]);
        gateway.Requests[2].IsErased.ShouldBeTrue();
        gateway.Requests[2].Events.ShouldBeEmpty();
    }

    private sealed class CapturingGateway(Func<bool> released) : ISharedProjectionRebuildGateway
    {
        public List<DomainSharedProjectionRebuildRequest> Requests { get; } = [];

        public Task<DomainSharedProjectionRebuildResponse> SendAsync(
            DomainSharedProjectionRebuildRequest request, CancellationToken cancellationToken = default)
        {
            if (request.Action != DomainSharedProjectionRebuildAction.Begin) released().ShouldBeTrue();
            Requests.Add(request);
            DomainSharedProjectionRebuildPhase phase = request.Action switch
            {
                DomainSharedProjectionRebuildAction.Finalize => DomainSharedProjectionRebuildPhase.Finalized,
                DomainSharedProjectionRebuildAction.Stage => DomainSharedProjectionRebuildPhase.Prepared,
                DomainSharedProjectionRebuildAction.Commit => DomainSharedProjectionRebuildPhase.Committed,
                _ => DomainSharedProjectionRebuildPhase.Accumulating
            };
            return Task.FromResult(new DomainSharedProjectionRebuildResponse(
                DomainSharedProjectionRebuildProtocol.Version, phase, ProjectionDispatchStatus.Completed,
                request.Action == DomainSharedProjectionRebuildAction.Begin ? 0 : 2,
                request.ExpectedInventoryFingerprint ?? request.CaptureInventoryFingerprint ?? "fingerprint",
                null, null));
        }
    }

    private sealed class LostResponseGateway(DomainSharedProjectionRebuildAction lostResponse)
        : ISharedProjectionRebuildGateway
    {
        private bool _lost;
        private DomainSharedProjectionRebuildPhase _phase = DomainSharedProjectionRebuildPhase.Accumulating;
        private int _accepted;

        public int BeginCount { get; private set; }

        public Task<DomainSharedProjectionRebuildResponse> SendAsync(
            DomainSharedProjectionRebuildRequest request, CancellationToken cancellationToken = default)
        {
            ProjectionDispatchStatus status = ProjectionDispatchStatus.Completed;
            switch (request.Action)
            {
                case DomainSharedProjectionRebuildAction.Begin:
                    BeginCount++;
                    status = BeginCount == 1 ? ProjectionDispatchStatus.Completed
                        : ProjectionDispatchStatus.AlreadyCompleted;
                    break;
                case DomainSharedProjectionRebuildAction.Accumulate:
                    status = _accepted++ == 0 ? ProjectionDispatchStatus.Completed
                        : ProjectionDispatchStatus.AlreadyCompleted;
                    break;
                case DomainSharedProjectionRebuildAction.Finalize:
                    status = _phase == DomainSharedProjectionRebuildPhase.Accumulating
                        ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted;
                    _phase = _phase < DomainSharedProjectionRebuildPhase.Finalized
                        ? DomainSharedProjectionRebuildPhase.Finalized : _phase;
                    break;
                case DomainSharedProjectionRebuildAction.Stage:
                    status = _phase == DomainSharedProjectionRebuildPhase.Finalized
                        ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted;
                    _phase = _phase < DomainSharedProjectionRebuildPhase.Prepared
                        ? DomainSharedProjectionRebuildPhase.Prepared : _phase;
                    break;
                case DomainSharedProjectionRebuildAction.Commit:
                    status = _phase == DomainSharedProjectionRebuildPhase.Prepared
                        ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.AlreadyCompleted;
                    _phase = DomainSharedProjectionRebuildPhase.Committed;
                    break;
            }
            if (!_lost && request.Action == lostResponse)
            {
                _lost = true;
                throw new IOException("The response was lost after a durable transition.");
            }
            return Task.FromResult(new DomainSharedProjectionRebuildResponse(
                DomainSharedProjectionRebuildProtocol.Version, _phase, status, _accepted,
                request.ExpectedInventoryFingerprint ?? request.CaptureInventoryFingerprint ?? "inventory",
                null, null));
        }
    }
}
