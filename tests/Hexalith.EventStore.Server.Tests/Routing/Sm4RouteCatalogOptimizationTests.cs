using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Verifies bounded SM4 routing-codec and activation-backoff optimizations preserve exact protocol semantics.</summary>
public sealed class Sm4RouteCatalogOptimizationTests
{
    /// <summary>Direct v2 binding serialization produces the exact byte-for-byte SHA-256 of the legacy JsonNode preimage.</summary>
    [Fact]
    public void DirectV2BindingDigestMatchesLegacyJsonNodePreimageByteForByte()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogActivation candidate = fixture.Candidate();
        RouteCatalogActivation withTechnical = candidate with
        {
            TechnicalHttpReceivers =
            [
                new(
                    candidate.RequiredHosts[0].AppId,
                    RouteCatalogTechnicalHttpPolicy.FenceVersion,
                    [
                        new(
                            "/api/v1/streams/read/workload",
                            ["POST"],
                            "Hexalith.EventStore.Controllers.StreamsController.ReadWorkloadStreamAsync",
                            new string('a', 64),
                            new string('b', 64),
                            ["DaprInternal:eventstore:stream-read"],
                            new string('c', 64),
                            new string('d', 64))
                        {
                            CredentialKind = "workload",
                        },
                        new(
                            "/api/v1/streams/read/health",
                            ["GET"],
                            "Hexalith.EventStore.Controllers.StreamsController.HealthAsync",
                            new string('e', 64),
                            new string('f', 64),
                            ["EventStoreHuman"],
                            new string('1', 64),
                            new string('2', 64)),
                    ])
                {
                    Invocations =
                    [
                        new("folders", "api/v1/streams/read/workload", ["POST", "GET"]),
                        new("tenants", "api/v1/streams/read/health", ["GET"]),
                    ],
                },
            ],
        };

        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(withTechnical.CanonicalCatalog, withTechnical.RootDigest);
        var capabilities = withTechnical.ProjectionCapabilities ?? [];
        byte[] legacyV1 = JsonSerializer.SerializeToUtf8Bytes(new
        {
            purpose = "route-catalog-activation",
            version = 1,
            withTechnical.AttemptId,
            withTechnical.RootDigest,
            withTechnical.PredecessorRootDigest,
            envelope.Generation,
            envelope.Profile,
            envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest,
            withTechnical.Owner,
            hosts = withTechnical.RequiredHosts.OrderBy(h => h.HostId, StringComparer.Ordinal).ToArray(),
            auxiliary = (withTechnical.AuxiliaryOperations ?? []).OrderBy(a => a.AppId, StringComparer.Ordinal)
                .ThenBy(a => a.Domain, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal)
                .ThenBy(a => a.Operation, StringComparer.Ordinal).ToArray(),
            capabilities = capabilities.OrderBy(c => c.AppId, StringComparer.Ordinal)
                .ThenBy(c => c.Domain, StringComparer.Ordinal)
                .ThenBy(c => c.ContractVersion, StringComparer.Ordinal)
                .Select(c => c with { ProjectionTypes = c.ProjectionTypes.Order(StringComparer.Ordinal).ToArray() })
                .ToArray(),
        });
        JsonObject versioned = JsonNode.Parse(legacyV1)!.AsObject();
        versioned["version"] = 2;
        versioned["technicalHttpReceivers"] = JsonSerializer.SerializeToNode(
            withTechnical.TechnicalHttpReceivers!.OrderBy(receiver => receiver.AppId, StringComparer.Ordinal)
                .Select(receiver => receiver with
                {
                    Endpoints = receiver.Endpoints.OrderBy(TechnicalHttpCatalogBinding.Key, StringComparer.Ordinal).ToArray(),
                    Invocations = receiver.Invocations?.OrderBy(TechnicalHttpCatalogBinding.InvocationKey, StringComparer.Ordinal)
                        .Select(invocation => invocation with { HttpMethods = invocation.HttpMethods.Order(StringComparer.Ordinal).ToArray() })
                        .ToArray(),
                }).ToArray());
        string legacyExpected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(versioned.ToJsonString())));

        RouteCatalogActivationBinding.ComputeDigest(withTechnical).ShouldBe(legacyExpected);
    }

    /// <summary>Default-mode ReadProfile returns the exact validated profile and still rejects wrong root digests or altered bytes.</summary>
    [Fact]
    public void ReadProfileWithoutDecodeScopeVerifiesExactBytesAndExpectedRoot()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogActivation candidate = fixture.Candidate();

        // Prime validated encoding through ordinary Decode.
        RouteCatalogEnvelope decoded = RouteCatalogCodec.Decode(candidate.CanonicalCatalog, candidate.RootDigest);
        RouteCatalogCodec.ReadProfile(candidate.CanonicalCatalog, candidate.RootDigest).ShouldBe(decoded.Profile);
        RouteCatalogCodec.ReadProfile(candidate.CanonicalCatalog, null).ShouldBe(decoded.Profile);

        // Wrong expected root must still fail closed.
        Should.Throw<RouteCatalogValidationException>(() =>
            RouteCatalogCodec.ReadProfile(candidate.CanonicalCatalog, new string('0', 64)));

        // Altered bytes must still fail closed.
        byte[] tampered = candidate.CanonicalCatalog.ToArray();
        tampered[^1] = (byte)' ';
        Should.Throw<RouteCatalogValidationException>(() =>
            RouteCatalogCodec.ReadProfile(tampered, candidate.RootDigest));
    }

    /// <summary>ProjectionActivationWorker backs off exponentially across repeated deferrals on both refusal and exception paths and clamps at RetryMaxDelay.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectionActivationWorkerAppliesBoundedExponentialBackoffAcrossRepeatedDeferrals(bool throwException)
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        var identity = new AggregateIdentity("tenant-a", "folders", "folder-a");
        var options = new ProjectionDispatchOptions
        {
            RetryBaseDelay = TimeSpan.FromMilliseconds(100),
            RetryMaxDelay = TimeSpan.FromSeconds(120),
        };
        ProjectionActivationWorkItem current = ProjectionActivationWorkItem.Create(identity, clock.GetUtcNow());
        var deferredUntil = new List<DateTimeOffset>();
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        outbox.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => new[] { current });
        outbox.DeferAsync(Arg.Any<ProjectionActivationWorkItem>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                DateTimeOffset due = call.ArgAt<DateTimeOffset>(1);
                deferredUntil.Add(due);
                current = current with { Attempt = current.Attempt + 1, NextDueUtc = due };
                return Task.CompletedTask;
            });

        IProjectionSourceGuardDelivery guard = Substitute.For<IProjectionSourceGuardDelivery>();
        guard.ReclaimCompletedReplayAsync(identity, Arg.Any<CancellationToken>()).Returns(true);
        if (throwException)
        {
            guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>())
                .Returns<Task<ProjectionSourceGuardDeliveryResult>>(_ => throw new IOException("transient failure"));
        }
        else
        {
            guard.TryDeliverAsync(identity, Arg.Any<CancellationToken>())
                .Returns(new ProjectionSourceGuardDeliveryResult(true, false, "delivery_state_unavailable"));
        }

        using var worker = new ProjectionActivationWorker(
            outbox,
            Substitute.For<IProjectionUpdateOrchestrator>(),
            Options.Create(options),
            clock,
            guard);

        DateTimeOffset t0 = clock.GetUtcNow();
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt 0 -> +100ms
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt 1 -> +200ms
        clock.Advance(TimeSpan.FromMilliseconds(200));
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt 2 -> +400ms
        // Jump to high attempt > 10 and extreme overflow boundaries (62, int.MaxValue) to prove full clamp without overflow.
        current = current with { Attempt = 12, NextDueUtc = clock.GetUtcNow() };
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt 12 -> +120s
        current = current with { Attempt = 62, NextDueUtc = clock.GetUtcNow() };
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt 62 -> +120s
        current = current with { Attempt = int.MaxValue, NextDueUtc = clock.GetUtcNow() };
        await worker.RunOnceAsync(TestContext.Current.CancellationToken); // Attempt int.MaxValue -> +120s

        deferredUntil.ShouldBe(
        [
            t0 + TimeSpan.FromMilliseconds(100),
            t0 + TimeSpan.FromMilliseconds(300),
            t0 + TimeSpan.FromMilliseconds(700),
            t0 + TimeSpan.FromMilliseconds(300) + TimeSpan.FromSeconds(120),
            t0 + TimeSpan.FromMilliseconds(300) + TimeSpan.FromSeconds(120),
            t0 + TimeSpan.FromMilliseconds(300) + TimeSpan.FromSeconds(120),
        ]);
    }

    /// <summary>ObservedRouteCatalogHost and CommittedRouteCatalogSnapshot reject or recompute invalid binding digests.</summary>
    [Fact]
    public async Task ValidatedBindingHelpersRejectOrFallbackOnMalformedDigests()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogActivation candidate = fixture.Candidate();
        string expectedDigest = RouteCatalogActivationBinding.ComputeDigest(candidate);
        var host = new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0]));

        await Should.ThrowAsync<ArgumentException>(() =>
            host.ObserveWithValidatedBindingAsync(candidate, string.Empty, fixture.Store.Coordinates, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentException>(() =>
            host.ObserveWithValidatedBindingAsync(candidate, "not-a-hex-digest", fixture.Store.Coordinates, TestContext.Current.CancellationToken));

        RouteCatalogHostIdentity observed = await host.ObserveWithValidatedBindingAsync(
            candidate, expectedDigest, fixture.Store.Coordinates, TestContext.Current.CancellationToken);
        observed.ShouldBe(fixture.Hosts[0]);

        var fallbackSnapshot = new CommittedRouteCatalogSnapshot(candidate, 1, fixture.Hosts[0].AppId, fixture.Store.Coordinates, string.Empty);
        fallbackSnapshot.CompleteActivationBindingDigest.ShouldBe(expectedDigest);

        var reusedSnapshot = new CommittedRouteCatalogSnapshot(candidate, 1, fixture.Hosts[0].AppId, fixture.Store.Coordinates, expectedDigest);
        reusedSnapshot.CompleteActivationBindingDigest.ShouldBe(expectedDigest);
    }

    /// <summary>Read-only metadata verification leases are never borrowed by AcquireOrBorrowScopeAsync as durable command fences.</summary>
    [Fact]
    public async Task AcquireOrBorrowScopeNeverBorrowsReadOnlyVerificationLease()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        await fixture.ActivateAsync(fixture.Candidate());
        int writes = 0;
        IRouteCatalogStateStore store = Substitute.For<IRouteCatalogStateStore>();
        store.Coordinates.Returns(fixture.Store.Coordinates);
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call => fixture.Store.ReadAsync(call.Arg<CancellationToken>()));
        store.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref writes);
                return fixture.Store.CompareExchangeAsync(
                    call.ArgAt<RouteCatalogActivationState>(0),
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2));
            });

        var options = Options.Create(new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
        });
        IHostEnvironment environment = CreateDevelopmentEnvironment();
        using var authority = new RouteCatalogAuthority(
            store,
            new DevelopmentRouteCatalogTrustValidator(options, environment),
            options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])),
            environment: environment);

        // A read-only lease must never satisfy a durable command fence request.
        await using RouteCatalogDispatchLease readOnly = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken);
        using (RouteCatalogRemoteCall.Enter(readOnly))
        {
            int writesBeforeDurable = writes;
            await using RouteCatalogDispatchLease durable = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(authority, TestContext.Current.CancellationToken);
            durable.ShouldNotBeSameAs(readOnly);
            writes.ShouldBeGreaterThan(writesBeforeDurable);
        }

        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>AcquireReadOnlyAsync verifies fresh committed authority with one read, zero CAS writes, and fails closed when a successor is pending.</summary>
    [Fact]
    public async Task AcquireReadOnlyVerifiesAuthorityWithoutStoreWritesAndFailsClosedOnPendingGeneration()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        await fixture.ActivateAsync(fixture.Candidate());
        int reads = 0;
        int writes = 0;
        IRouteCatalogStateStore store = Substitute.For<IRouteCatalogStateStore>();
        store.Coordinates.Returns(fixture.Store.Coordinates);
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            Interlocked.Increment(ref reads);
            return fixture.Store.ReadAsync(call.Arg<CancellationToken>());
        });
        store.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref writes);
                return fixture.Store.CompareExchangeAsync(
                    call.ArgAt<RouteCatalogActivationState>(0),
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2));
            });

        var options = Options.Create(new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
        });
        IHostEnvironment environment = CreateDevelopmentEnvironment();
        using var authority = new RouteCatalogAuthority(
            store,
            new DevelopmentRouteCatalogTrustValidator(options, environment),
            options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])),
            environment: environment);

        await using (RouteCatalogDispatchLease first = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            reads.ShouldBe(2);
            writes.ShouldBe(0);
            (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        }

        writes.ShouldBe(0);

        // Preparing a successor must immediately fail closed read-only verification.
        RouteCatalogActivation nextCandidate = fixture.Candidate(generation: 2, predecessor: fixture.Candidate().RootDigest);
        await fixture.Authorities[0].PrepareAsync(nextCandidate, TestContext.Current.CancellationToken);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken));
        writes.ShouldBe(0);
    }

    /// <summary>Read-only classification predicates admit only strictly side-effect-free native proof probes and enrolled Gateway stream-read endpoints.</summary>
    [Fact]
    public void ReadOnlyClassificationPredicatesAllowOnlyStrictSideEffectFreePaths()
    {
        var methodTransport = new RouteCatalogNativeActorTransport("method", "PUT", new string('a', 64), new string('b', 64), new string('c', 64));
        var reminderTransport = methodTransport with { Kind = "reminder" };
        var timerTransport = methodTransport with { Kind = "timer" };

        foreach (string allowedMethod in new[]
        {
            "InspectSourceProofAsync",
            "InspectSourceMetadataPairAsync",
            "ReadNativeSourceBatchAsync",
            "ReadNativeEventsBoundedRangeAsync",
        })
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = HttpMethods.Put;
            ctx.Request.RouteValues["actorTypeName"] = "AggregateActor";
            ctx.Request.RouteValues["methodName"] = allowedMethod;
            TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(ctx, methodTransport).ShouldBeTrue();

            ctx.Request.Method = HttpMethods.Post;
            TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(ctx, methodTransport).ShouldBeFalse();

            ctx.Request.Method = HttpMethods.Put;
            TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(ctx, reminderTransport).ShouldBeFalse();
            TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(ctx, timerTransport).ShouldBeFalse();
        }

        // Barrier-recovery read methods, mutating actor methods, and non-AggregateActor types must stay durably fenced.
        foreach (string excludedMethod in new[] { "GetStreamMetadataAsync", "ReadEventsRangeAsync", "ProcessCommandAsync", "BootstrapSourceProofAsync" })
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = HttpMethods.Put;
            ctx.Request.RouteValues["actorTypeName"] = "AggregateActor";
            ctx.Request.RouteValues["methodName"] = excludedMethod;
            TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(ctx, methodTransport).ShouldBeFalse();
        }

        var wrongActor = new DefaultHttpContext();
        wrongActor.Request.Method = HttpMethods.Put;
        wrongActor.Request.RouteValues["actorTypeName"] = "IdempotencyAdmissionActor";
        wrongActor.Request.RouteValues["methodName"] = "InspectSourceProofAsync";
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyAggregateActorMethod(wrongActor, methodTransport).ShouldBeFalse();

        var gatewayEndpoint = new RouteCatalogTechnicalHttpEndpoint(
            "/api/v1/streams/read/workload", ["POST"],
            "Hexalith.EventStore.Gateway,Hexalith.EventStore.Controllers.StreamsController.ReadWorkloadStreamAsync",
            new string('a', 64), new string('b', 64), ["DaprInternal:eventstore:stream-read"], new string('c', 64), new string('d', 64))
        { CredentialKind = "workload" };
        var nonGatewayEndpoint = gatewayEndpoint with { ActionIdentity = "Hexalith.Other.Server,OtherController.ReadAsync" };
        var humanEndpoint = gatewayEndpoint with { CredentialKind = "human" };

        var streamCtx = new DefaultHttpContext();
        streamCtx.Request.Method = HttpMethods.Post;
        streamCtx.Request.Path = "/api/v1/streams/read/workload";
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyStreamWorkloadEndpoint(streamCtx, gatewayEndpoint).ShouldBeTrue();
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyStreamWorkloadEndpoint(streamCtx, nonGatewayEndpoint).ShouldBeFalse();
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyStreamWorkloadEndpoint(streamCtx, humanEndpoint).ShouldBeFalse();
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyStreamWorkloadEndpoint(streamCtx, null).ShouldBeFalse();

        streamCtx.Request.Method = HttpMethods.Get;
        TechnicalHttpRouteCatalogReceiver.IsReadOnlyStreamWorkloadEndpoint(streamCtx, gatewayEndpoint).ShouldBeFalse();

        RouteCatalogInvocationHandler.IsReadOnlyStreamWorkloadRequest("api/v1/streams/read/workload", HttpMethod.Post).ShouldBeTrue();
        RouteCatalogInvocationHandler.IsReadOnlyStreamWorkloadRequest("/api/v1/streams/read/workload/", HttpMethod.Post).ShouldBeTrue();
        RouteCatalogInvocationHandler.IsReadOnlyStreamWorkloadRequest("api/v1/streams/read/workload", HttpMethod.Get).ShouldBeFalse();
        RouteCatalogInvocationHandler.IsReadOnlyStreamWorkloadRequest("api/v1/commands", HttpMethod.Post).ShouldBeFalse();
    }

    /// <summary>AcquireReadOnlyAsync fails closed on unactivated store, post-validation cancellation, mid-verification authority drift, and concurrent PrepareAsync.</summary>
    [Theory]
    [InlineData("unactivated")]
    [InlineData("cancel")]
    [InlineData("identity")]
    [InlineData("profile")]
    [InlineData("coordinates")]
    [InlineData("concurrent-prepare")]
    public async Task AcquireReadOnlyFailsClosedOnUnactivatedStoreCancellationDriftAndConcurrentPrepare(string scenario)
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        if (scenario != "unactivated")
        {
            await fixture.ActivateAsync(fixture.Candidate());
        }

        int writes = 0;
        RouteCatalogStateCoordinates activeCoordinates = fixture.Store.Coordinates;
        IRouteCatalogStateStore store = Substitute.For<IRouteCatalogStateStore>();
        store.Coordinates.Returns(_ => activeCoordinates);
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call => fixture.Store.ReadAsync(call.Arg<CancellationToken>()));
        store.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Interlocked.Increment(ref writes);
                return fixture.Store.CompareExchangeAsync(
                    call.ArgAt<RouteCatalogActivationState>(0),
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2));
            });

        var local = new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
        };
        using var cts = new CancellationTokenSource();
        var options = Options.Create(local);
        IHostEnvironment environment = CreateDevelopmentEnvironment();
        using var authority = new RouteCatalogAuthority(
            store,
            new DevelopmentRouteCatalogTrustValidator(options, environment),
            options,
            new ObservedRouteCatalogHost(async _ =>
            {
                if (scenario == "cancel") cts.Cancel();
                if (scenario == "identity") local.Identity = fixture.Hosts[1];
                if (scenario == "profile") local.Profile = "Production";
                if (scenario == "coordinates") activeCoordinates = activeCoordinates with { StateKey = "drifted-key" };
                if (scenario == "concurrent-prepare")
                {
                    RouteCatalogActivation next = fixture.Candidate(generation: 2, predecessor: fixture.Candidate().RootDigest);
                    await fixture.Authorities[0].PrepareAsync(next, CancellationToken.None);
                }

                return fixture.Observations[0];
            }),
            environment: environment);

        if (scenario == "cancel")
        {
            await Should.ThrowAsync<OperationCanceledException>(() => authority.AcquireReadOnlyAsync(cts.Token));
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken));
        }

        writes.ShouldBe(0);
    }

    /// <summary>Snapshot memo reuses immutable snapshots when enabled, invalidates on LocalAppId change, clears on opt-out, and never retains after Dispose.</summary>
    [Fact]
    public async Task SnapshotMemoReusesInvalidatesOnAppIdClearsOnOptOutAndDoesNotRetainAfterDispose()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        await fixture.ActivateAsync(fixture.Candidate());
        var local = new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
            ScopedDecodeReuse = true,
            RetainValidatedCatalogBytes = true,
        };
        int hostIndex = 0;
        var options = Options.Create(local);
        IHostEnvironment environment = CreateDevelopmentEnvironment();
        using var authority = new RouteCatalogAuthority(
            fixture.Store,
            new DevelopmentRouteCatalogTrustValidator(options, environment),
            options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[hostIndex])),
            environment: environment);

        CommittedRouteCatalogSnapshot firstSnapshot;
        await using (RouteCatalogDispatchLease first = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            firstSnapshot = first.Snapshot;
        }

        await using (RouteCatalogDispatchLease second = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            second.Snapshot.ShouldBeSameAs(firstSnapshot);
        }

        // Switching LocalAppId within the same RequiredHosts set must produce a new snapshot with the updated AppId.
        hostIndex = 1;
        local.Identity = fixture.Hosts[1];
        await using (RouteCatalogDispatchLease otherHost = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            otherHost.Snapshot.ShouldNotBeSameAs(firstSnapshot);
            otherHost.Snapshot.LocalAppId.ShouldBe(fixture.Hosts[1].AppId);
        }

        // Disabling RetainValidatedCatalogBytes must bypass and clear the cached snapshot.
        local.RetainValidatedCatalogBytes = false;
        CommittedRouteCatalogSnapshot uncachedA;
        await using (RouteCatalogDispatchLease leaseA = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            uncachedA = leaseA.Snapshot;
        }

        await using (RouteCatalogDispatchLease leaseB = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            leaseB.Snapshot.ShouldNotBeSameAs(uncachedA);
        }

        // Re-enable, populate, then Dispose and verify post-disposal never retains a cached snapshot.
        local.RetainValidatedCatalogBytes = true;
        await using (await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken)) { }
        authority.Dispose();
        System.Reflection.FieldInfo cachedField = typeof(RouteCatalogAuthority).GetField(
            "_cachedSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        cachedField.GetValue(authority).ShouldBeNull();

        await using (RouteCatalogDispatchLease afterDispose = await authority.AcquireReadOnlyAsync(TestContext.Current.CancellationToken))
        {
            afterDispose.Snapshot.LocalAppId.ShouldBe(fixture.Hosts[1].AppId);
        }

        cachedField.GetValue(authority).ShouldBeNull();
    }

    /// <summary>RouteCatalogInvocationHandler uses zero-write read-only verification only for enrolled Gateway stream reads and retains durable CAS leases for other calls.</summary>
    [Fact]
    public async Task InvocationHandlerUsesReadOnlyVerificationForGatewayStreamReadsAndDurableLeasesForMutations()
    {
        await using var fixture = new HeldMetadataDispatchFixture();
        await fixture.StartAsync();
        int writesBefore = fixture.Writes;
        using var handler = new RouteCatalogInvocationHandler(
            fixture.Authority,
            new StaticOkHandler(),
            Options.Create(fixture.Options));
        using var client = new HttpClient(handler);

        // 1. Enrolled Gateway POST api/v1/streams/read/workload uses AcquireReadOnlyAsync (0 CAS writes).
        using HttpRequestMessage streamRead = HeldMetadataDispatchFixture.Request();
        using HttpResponseMessage streamResponse = await client.SendAsync(streamRead, TestContext.Current.CancellationToken);
        streamResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        fixture.Writes.ShouldBe(writesBefore);

        // 2. A non-stream-read POST (an enrolled domain route) must still acquire and release a durable CAS lease (writes > 0).
        RouteCatalogActivation active = fixture.Receiver.Catalog.Candidate();
        RouteCatalogEntry enrolledRoute = RouteCatalogCodec.Decode(active.CanonicalCatalog, active.RootDigest).Routes[0];
        using var processRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(fixture.Store.Coordinates.HttpEndpoint.TrimEnd('/') + $"/v1.0/invoke/{enrolledRoute.AppId}/method/{enrolledRoute.Method}"))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        using HttpResponseMessage processResponse = await client.SendAsync(processRequest, TestContext.Current.CancellationToken);
        processResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
        fixture.Writes.ShouldBeGreaterThan(writesBefore);
    }

    private sealed class StaticOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    private static IHostEnvironment CreateDevelopmentEnvironment()
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        return environment;
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
