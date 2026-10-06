using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;

using Shouldly;
using NSubstitute;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Verifies retained Identity History Reader Tests.</summary>
public sealed class RetainedIdentityHistoryReaderTests
{
    private static readonly AggregateIdentity _identity = new("tenant-a", "party", "party-a");
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-06T12:00:00Z");
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();

    /// <summary>Verifies retained Identity History Reader Tests.</summary>
    public RetainedIdentityHistoryReaderTests() => _clock.GetUtcNow().Returns(_now);

    /// <summary>Verifies complete Authenticated Sparse Source Retains Its Original Head And Positions.</summary>
    [Fact]
    public async Task CompleteAuthenticatedSparseSourceRetainsItsOriginalHeadAndPositions()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream(), null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        RetainedIdentityHistoryReadResult result = await new RetainedIdentityHistoryReader(client, _clock)
            .ReadAsync(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose);

        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Head.ShouldBe(2);
        result.Stream.Events.Single().SequenceNumber.ShouldBe(2);
        result.Stream.ExcludedSequences.ShouldBe([1L]);
    }

    /// <summary>Verifies foreign Owner Response Has No Usable History.</summary>
    [Fact]
    public async Task ForeignOwnerResponseHasNoUsableHistory()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream() with { Identity = new("tenant-b", "party", "party-a") }, null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        RetainedIdentityHistoryReadResult result = await new RetainedIdentityHistoryReader(client, _clock)
            .ReadAsync(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose);
        result.Stream.ShouldBeNull();
    }

    /// <summary>Verifies missing Excluded Position Has No Usable History.</summary>
    [Fact]
    public async Task MissingExcludedPositionHasNoUsableHistory()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream() with { ExcludedSequences = [] }, null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        (await new RetainedIdentityHistoryReader(client, _clock).ReadAsync(_identity,
            RetainedIdentityHistoryReadRequest.AttributionPurpose)).Stream.ShouldBeNull();
    }

    /// <summary>Verifies server Failure Cannot Return Its Partial Payload.</summary>
    [Fact]
    public async Task ServerFailureCannotReturnItsPartialPayload()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream(), "history-unavailable"));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        (await new RetainedIdentityHistoryReader(client, _clock).ReadAsync(_identity,
            RetainedIdentityHistoryReadRequest.AttributionPurpose)).Stream.ShouldBeNull();
    }

    /// <summary>Verifies caller Cancellation Propagates.</summary>
    [Fact]
    public async Task CallerCancellationPropagates()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream(), null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        await Should.ThrowAsync<OperationCanceledException>(() => new RetainedIdentityHistoryReader(client, _clock)
            .ReadAsync(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, new CancellationToken(true)));
    }

    /// <summary>Verifies certificate Expired During Transit Cannot Release History.</summary>
    [Fact]
    public async Task CertificateExpiredDuringTransitCannotReleaseHistory()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream(), null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        _clock.GetUtcNow().Returns(_now.AddMinutes(1));
        (await new RetainedIdentityHistoryReader(client, _clock).ReadAsync(_identity,
            RetainedIdentityHistoryReadRequest.AttributionPurpose)).Stream.ShouldBeNull();
    }

    /// <summary>Verifies cancellation During Certificate Validation Cannot Release History.</summary>
    [Fact]
    public async Task CancellationDuringCertificateValidationCannotReleaseHistory()
    {
        using var handler = new RetainedHistoryResponseHandler(new(Stream(), null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        using var cancellation = new CancellationTokenSource();
        _clock.GetUtcNow().Returns(_ => { cancellation.Cancel(); return _now; });
        await Should.ThrowAsync<OperationCanceledException>(() => new RetainedIdentityHistoryReader(client, _clock)
            .ReadAsync(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, cancellation.Token));
    }

    /// <summary>Verifies maximum Readable Payload Fits The Shared Base64 Wire Bound.</summary>
    [Fact]
    public async Task MaximumReadablePayloadFitsTheSharedBase64WireBound()
    {
        RetainedIdentityHistoryStream source = Stream();
        source = source with { Events = [source.Events[0] with { Payload = new byte[RetainedIdentityHistoryLimits.MaxPayloadBytes] }] };
        using var handler = new RetainedHistoryResponseHandler(new(source, null));
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        (await new RetainedIdentityHistoryReader(client, _clock).ReadAsync(_identity,
            RetainedIdentityHistoryReadRequest.AttributionPurpose)).IsAuthoritative.ShouldBeTrue();
    }

    /// <summary>Stops every noncooperative HTTP stage while its operation remains incomplete.</summary>
    /// <param name="stage">Send, acquisition, ValueTask read, or Task read.</param>
    /// <param name="expireDeadline">Whether the unchanged thirty-second SDK timer expires.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task NoncooperativeTransportStopsBeforeCompletionAndDisposesLateMaterial(int stage, bool expireDeadline)
    {
        TimeSpan watchdog = TimeSpan.FromSeconds(2);
        var clock = new RetainedHistoryTimeProvider(_now);
        using var body = new RetainedHistorySuspendedStream(stage == 3);
        using var content = new RetainedHistorySuspendedContent(stage == 1, body);
        using var handler = new RetainedHistorySuspendedHandler(stage == 0, content);
        using var client = new HttpClient(handler) { BaseAddress = new("https://owner.invalid/") };
        using var cancellation = new CancellationTokenSource();
        Task<RetainedIdentityHistoryReadResult> reading = new RetainedIdentityHistoryReader(client, clock)
            .ReadAsync(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, cancellation.Token);
        Task started = stage switch
        {
            0 => handler.Started.Task,
            1 => content.Started.Task,
            _ => body.Started.Task,
        };
        Task pending = stage switch
        {
            0 => handler.Pending,
            1 => content.Pending,
            _ => body.Pending,
        };

        try
        {
            await started.WaitAsync(watchdog);
            pending.IsCompleted.ShouldBeFalse();
            reading.IsCompleted.ShouldBeFalse();
            clock.LastDueTime.ShouldBe(TimeSpan.FromSeconds(30));
            if (expireDeadline)
            {
                clock.Advance(TimeSpan.FromSeconds(29));
                reading.IsCompleted.ShouldBeFalse();
                clock.Advance(TimeSpan.FromSeconds(1));
                RetainedIdentityHistoryReadResult result = await reading.WaitAsync(watchdog);
                result.Stream.ShouldBeNull();
                result.IsAuthoritative.ShouldBeFalse();
                result.FailureReason.ShouldBe("history-time-bound-exceeded");
            }
            else
            {
                cancellation.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => reading.WaitAsync(watchdog));
            }

            pending.IsCompleted.ShouldBeFalse();
            content.Disposed.Task.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            // End the fixture operation only after the bounded read has stopped or its watchdog failed.
            handler.Complete();
            content.Complete();
            body.Complete();
        }

        await content.Disposed.Task.WaitAsync(watchdog);
        content.AcquisitionCount.ShouldBe(stage == 0 ? 0 : 1);
        body.ReadCount.ShouldBe(stage < 2 ? 0 : 1);
        if (stage > 0)
        {
            await body.Disposed.Task.WaitAsync(watchdog);
        }
    }

    private static RetainedIdentityHistoryStream Stream()
        => new(_identity, RetainedIdentityHistoryReadRequest.AttributionPurpose, 2, _now,
            [new StreamReadEvent(2, "History", "{}"u8.ToArray(), "json", 1, "", null, null,
                DateTimeOffset.Parse("2026-10-06T11:00:00Z"), null, EventStorePayloadProtectionMetadata.Unprotected())], [1], "source-checkpoint")
        {
            AuthorityRevision = "source-authority-r1",
            ValidUntil = _now.AddMinutes(1),
        };
}
