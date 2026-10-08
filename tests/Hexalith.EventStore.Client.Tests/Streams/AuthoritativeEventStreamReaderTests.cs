using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

public sealed class AuthoritativeEventStreamReaderTests
{
    [Fact]
    public async Task CompleteStablePrefix_ReturnsOneCoherentObservation()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 2, call.Arg<StreamReadRequest>().ToSequence == 0 ? [] : [Event(1), Event(2)]));
        var reader = new AuthoritativeEventStreamReader(gateway, TimeProvider.System);
        AuthoritativeStreamReadResult result = await reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Head.ShouldBe(2);
        result.Stream.Events.Select(item => item.SequenceNumber).ShouldBe([1L, 2L]);
        result.Stream.ObservationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public async Task GapReorderOrDuplicate_Denies(long first, long second)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 2, call.Arg<StreamReadRequest>().ToSequence == 0 ? [] : [Event(first), Event(second)]));
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangedHead_NeverReturnsMixedObservation()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int current = ++calls;
                long head = ((current - 1) / 3) + 1 + (current % 3 == 0 ? 1 : 0);
                StreamReadRequest request = call.Arg<StreamReadRequest>();
                return Page(request, head, request.ToSequence == 0 ? [] : Enumerable.Range(1, (int)request.ToSequence!).Select(value => Event(value)).ToArray());
            });
        AuthoritativeStreamReadResult result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.FailureReason.ShouldBe("source-head-changed");
        calls.ShouldBe(9);
    }

    [Fact]
    public async Task WholeReadDeadline_StopsUncooperativeGateway()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new AuthoritativeReadTimeProvider();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            started.TrySetResult();
            return new TaskCompletionSource<StreamReadPage>().Task;
        });
        var reader = new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3));
        var pending = reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        (await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken))
            .FailureReason.ShouldBe("source-time-bound-exceeded");
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), canceled.Token));
    }

    [Fact]
    public async Task ForeignPageOrExcessiveHead_Denies()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 10_001, []));
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Page(call.Arg<StreamReadRequest>(), 0, []) with { Tenant = "tenant-b" });
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).IsAuthoritative.ShouldBeFalse();
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30001)]
    public async Task InvalidOperationalDeadline_DeniesBeforeGateway(int milliseconds)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var reader = new AuthoritativeEventStreamReader(gateway, TimeProvider.System, TimeSpan.FromMilliseconds(milliseconds));
        var result = await reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeFalse();
        result.FailureReason.ShouldBe("source-invalid-time-bound");
        await gateway.DidNotReceiveWithAnyArgs().ReadStreamAsync(default!);
    }

    [Fact]
    public async Task BlockingProviderCancellationCallback_CannotRetainDeadlineWait()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        CancellationToken providerToken = default;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            providerToken = call.Arg<CancellationToken>();
            providerToken.Register(() => { entered.TrySetResult(); release.Wait(); });
            started.TrySetResult();
            return new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        });
        var clock = new AuthoritativeReadTimeProvider();
        var reader = new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3));
        Task<AuthoritativeStreamReadResult> pending = reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(3));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            result.Stream.ShouldBeNull();
            result.FailureReason.ShouldBe("source-time-bound-exceeded");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            providerToken.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task SynchronousGatewayInvocation_CannotRetainReadPastDeadline()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            started.TrySetResult();
            release.Wait();
            return Page(call.Arg<StreamReadRequest>(), 0, []);
        });
        var clock = new AuthoritativeReadTimeProvider();
        var reader = new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3));
        Task<AuthoritativeStreamReadResult> pending = Task.Run(() => reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(3));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            result.Stream.ShouldBeNull();
            result.FailureReason.ShouldBe("source-time-bound-exceeded");
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task CumulativeBudget_AndDelayedTimerRejectLateFinalHead()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var clock = new AuthoritativeReadTimeProvider();
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            calls++;
            clock.Advance(TimeSpan.FromSeconds(1), fireTimers: false);
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [Event(1)]);
        });
        var reader = new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3));
        var result = await reader.ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("source-time-bound-exceeded");
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task ShortenedBudget_SucceedsImmediatelyBeforeExclusiveBoundary()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var clock = new AuthoritativeReadTimeProvider();
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (++calls == 3) { clock.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromTicks(1), fireTimers: false); }
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [Event(1)]);
        });
        var result = await new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3))
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task CallerCancellationWithBlockingProviderCallback_PreservesOriginalToken()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.Arg<CancellationToken>().Register(() => release.Wait());
            started.TrySetResult();
            return new TaskCompletionSource<StreamReadPage>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        });
        var pending = new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), caller.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var cancel = caller.CancelAsync();
            var exception = await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            exception.CancellationToken.ShouldBe(caller.Token);
            await cancel.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task SampledPayload_CannotBeMutatedByFinalHeadProvider()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        StreamReadEvent retained = Event(1);
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (++calls == 3) { retained.Payload[0] = (byte)'!'; }
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [retained]);
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Events.Single().Payload.ShouldBe("{}"u8.ToArray());
        retained.Payload[0].ShouldBe((byte)'!');
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderFaultBeforeCallerCancellation_PreservesCallerVerdict(bool expireFirst)
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var clock = new AuthoritativeReadTimeProvider();
        using var caller = new CancellationTokenSource();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns<StreamReadPage>(_ =>
        {
            if (expireFirst) { clock.Advance(TimeSpan.FromSeconds(3), fireTimers: false); }
            caller.Cancel();
            throw new HttpRequestException("Synthetic unavailable gateway.");
        });
        var exception = await Should.ThrowAsync<OperationCanceledException>(() =>
            new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3))
                .ReadAsync(new("tenant-a", "party", "party-1"), caller.Token));
        exception.CancellationToken.ShouldBe(caller.Token);
    }

    [Fact]
    public async Task ExpiredProviderFault_ReturnsDeadlineWithoutEvidence()
    {
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        var clock = new AuthoritativeReadTimeProvider();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns<StreamReadPage>(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(3), fireTimers: false);
            throw new HttpRequestException("Synthetic unavailable gateway.");
        });
        var result = await new AuthoritativeEventStreamReader(gateway, clock, TimeSpan.FromSeconds(3))
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.Stream.ShouldBeNull();
        result.FailureReason.ShouldBe("source-time-bound-exceeded");
    }

    [Fact]
    public async Task ProviderCollectionIsCapturedOnceThenExactSnapshotValidated()
    {
        var changing = Substitute.For<IReadOnlyList<StreamReadEvent>>();
        changing.Count.Returns(1);
        changing[0].Returns(Event(1));
        int traversals = 0;
        changing.GetEnumerator().Returns(_ => new[]
        {
            ++traversals == 1 ? Event(1) : Event(1) with { EventTypeName = "", MessageId = "", MetadataVersion = 0 },
        }.AsEnumerable().GetEnumerator());
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : changing);
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        traversals.ShouldBe(1);
        result.Stream!.Events.Single().EventTypeName.ShouldBe("Created");
        result.Stream.Events.Single().MetadataVersion.ShouldBe(1);
    }

    [Fact]
    public async Task MisreportedFlagCountCannotCauseUnboundedCapture()
    {
        var flags = Substitute.For<IReadOnlyDictionary<string, string>>();
        flags.Count.Returns(0);
        int visited = 0;
        flags.GetEnumerator().Returns(_ => Enumerable.Range(0, 100).Select(index =>
        {
            visited++;
            return new KeyValuePair<string, string>($"flag{index}", "safe");
        }).GetEnumerator());
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [Event(1) with
            {
                ProtectionMetadata = new(PayloadProtectionState.Unprotected, 1, null, null, null, flags),
            }]);
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.Stream.ShouldBeNull();
        visited.ShouldBe(EventStorePayloadProtectionMetadata.MaxCompatibilityFlagCount + 1);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("opaque")]
    [InlineData("future")]
    [InlineData("scheme")]
    [InlineData("scheme-secret")]
    [InlineData("alias")]
    [InlineData("hint")]
    [InlineData("flag-key")]
    [InlineData("flag-value")]
    [InlineData("missing-protected-scheme")]
    public async Task MalformedProtectionMetadataNeverBecomesAuthoritative(string variant)
    {
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Unprotected, 1, null, null, null, null);
        metadata = variant switch
        {
            "state" => metadata with { State = (PayloadProtectionState)999 },
            "opaque" => metadata with { State = PayloadProtectionState.ProviderOpaque },
            "future" => metadata with { MetadataVersion = 2 },
            "scheme" => metadata with { Scheme = new string('a', 65) },
            "scheme-secret" => metadata with { Scheme = "private-key" },
            "alias" => metadata with { KeyAlias = new string('a', 257) },
            "hint" => metadata with { ContentHint = "application/\njson" },
            "flag-key" => metadata with { CompatibilityFlags = new Dictionary<string, string> { ["nonce"] = "safe" } },
            "flag-value" => metadata with { CompatibilityFlags = new Dictionary<string, string> { ["mode"] = "secret" } },
            _ => metadata with { State = PayloadProtectionState.Protected },
        };
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [Event(1) with { ProtectionMetadata = metadata }]);
        });
        (await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken)).Stream.ShouldBeNull();
    }

    [Fact]
    public async Task SupportedProtectedGatewayProvenanceAndCapturedFlagsRemainReadable()
    {
        var flags = new Dictionary<string, string> { ["mode"] = "safe" };
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "aes-gcm-256", "alias", "application/json", flags);
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        int calls = 0;
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (++calls == 3) { flags["mode"] = "secret"; }
            var request = call.Arg<StreamReadRequest>();
            return Page(request, 1, request.ToSequence == 0 ? [] : [Event(1) with { ProtectionMetadata = metadata }]);
        });
        var result = await new AuthoritativeEventStreamReader(gateway, TimeProvider.System)
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.IsAuthoritative.ShouldBeTrue();
        result.Stream!.Events.Single().ProtectionMetadata!.State.ShouldBe(PayloadProtectionState.Protected);
        result.Stream.Events.Single().ProtectionMetadata!.CompatibilityFlags!["mode"].ShouldBe("safe");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnexpectedFaultPreservesUncancelledFaultOrOriginalCallerToken(bool cancel)
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        using var caller = new CancellationTokenSource();
        var failure = new FormatException("Unexpected synthetic provider fault.");
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns<StreamReadPage>(_ =>
        {
            if (cancel) { caller.Cancel(); }
            throw failure;
        });
        var reader = new AuthoritativeEventStreamReader(gateway, TimeProvider.System);
        if (cancel)
        {
            var exception = await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(new("tenant-a", "party", "party-1"), caller.Token));
            exception.CancellationToken.ShouldBe(caller.Token);
        }
        else
        {
            (await Should.ThrowAsync<FormatException>(() => reader.ReadAsync(new("tenant-a", "party", "party-1"), caller.Token))).ShouldBeSameAs(failure);
        }
    }

    [Fact]
    public async Task ProviderOnlyCancellationIsUnavailableWithoutClaimingBudgetExpired()
    {
        var gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns<StreamReadPage>(_ =>
            throw new OperationCanceledException(new CancellationToken(true)));
        var result = await new AuthoritativeEventStreamReader(gateway, new AuthoritativeReadTimeProvider())
            .ReadAsync(new("tenant-a", "party", "party-1"), TestContext.Current.CancellationToken);
        result.FailureReason.ShouldBe("source-unavailable");
        result.Stream.ShouldBeNull();
    }

    private static StreamReadEvent Event(long sequence) => new(sequence, "Created", "{}"u8.ToArray(), "json", 1, "message", null, null, DateTimeOffset.UnixEpoch, "actor");
    private static StreamReadPage Page(StreamReadRequest request, long head, IReadOnlyList<StreamReadEvent> events)
        => new(request.Tenant, request.Domain, request.AggregateId, events,
            new(request.FromSequence, request.ToSequence, events.Count == 0 ? null : events[^1].SequenceNumber, head, events.Count, false, null));
}
