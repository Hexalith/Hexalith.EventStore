using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves exact issuer/receiver provenance, fresh validation and real remote lifetime ownership for nested metadata.</summary>
public sealed class HeldMetadataDispatchTests
{
#if DEBUG
    private static bool Enabled => true;
#else
    private static bool Enabled => false;
#endif
    /// <summary>A genuine receiver avoids a new fence while fresh host and trust validation still execute.</summary>
    [Fact]
    public async Task GenuineReceiverRevalidatesHostAndTrustWithoutStateOrFenceAcquisition()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, TestContext.Current.CancellationToken);
        int reads = fixture.Reads, writes = fixture.Writes, trust = fixture.TrustCalls, observed = fixture.ObserverCalls;
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", TestContext.Current.CancellationToken);
        if (!Enabled) { held.ShouldBeNull(); return; }
        held.ShouldNotBeNull().Lease.ShouldBeSameAs(parent);
        held.Snapshot.ShouldBeSameAs(parent.Snapshot);
        fixture.Reads.ShouldBe(reads); fixture.Writes.ShouldBe(writes);
        fixture.TrustCalls.ShouldBe(trust + 1); fixture.ObserverCalls.ShouldBe(observed + 1);
        using var response = await held.InvokeAsync(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => held.InvokeAsync(_ => Task.FromResult(new HttpResponseMessage())));
    }

    /// <summary>Concurrent exact metadata observations retain the same actual open receiver and drain every enrolled call.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentMetadataCallsOwnTheirActualSharedReceiverUntilBothDrain(bool cancel)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, caller.Token);
        int reads = fixture.Reads, writes = fixture.Writes;
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task InvokeAsync(TaskCompletionSource started)
        {
            using var request = HeldMetadataDispatchFixture.Request();
            using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request,
                "eventstore", "api/v1/streams/read/workload", caller.Token).ConfigureAwait(false);
            if (!Enabled) { held.ShouldBeNull(); started.TrySetResult(); return; }
            held.ShouldNotBeNull().Lease.ShouldBeSameAs(parent);
            using var response = await held.InvokeAsync(async token =>
            {
                started.TrySetResult();
                await finish.Task.WaitAsync(token).ConfigureAwait(false);
                return new(HttpStatusCode.NoContent);
            }).ConfigureAwait(false);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        Task first = InvokeAsync(firstStarted), second = InvokeAsync(secondStarted);
        try
        {
            await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            if (!Enabled) { await Task.WhenAll(first, second); return; }
            parent.DrainRemoteCallsAsync().IsCompleted.ShouldBeFalse();
            fixture.Reads.ShouldBe(reads); fixture.Writes.ShouldBe(writes);
            if (cancel)
            {
                await caller.CancelAsync();
                await Should.ThrowAsync<OperationCanceledException>(() => first);
                await Should.ThrowAsync<OperationCanceledException>(() => second);
            }
            else { finish.SetResult(); await Task.WhenAll(first, second); }
            await parent.DrainRemoteCallsAsync();
            await parent.DisposeAsync();
            (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(cancel ? 1 : 0);
        }
        finally
        {
            finish.TrySetResult();
            await caller.CancelAsync();
            try { await Task.WhenAll(first, second); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Default, nonprivate, nontechnical, nonDevelopment and DEBUG-absent selections remain inert.</summary>
    [Theory]
    [InlineData("flag")]
    [InlineData("fixture")]
    [InlineData("profile")]
    [InlineData("technical")]
    [InlineData("environment")]
    public async Task EveryEffectiveSelectorIsRequired(string changed)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, TestContext.Current.CancellationToken);
        if (changed == "flag") fixture.Options.ReuseHeldMetadataDispatch = false;
        if (changed == "fixture") fixture.Options.DevelopmentFixture = false;
        if (changed == "profile") fixture.Options.Profile = "Production";
        if (changed == "technical") fixture.Options.RequireTechnicalHttpBinding = false;
        if (changed == "environment") fixture.Receiver.App.Environment.EnvironmentName = "Production";
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        held.ShouldBeNull();
    }

    /// <summary>Public genuine scopes, public constructed leases and mixed scopes cannot mint receiver provenance.</summary>
    [Theory]
    [InlineData("public-genuine")]
    [InlineData("public-constructed")]
    [InlineData("mixed")]
    public async Task PublicScopesNeverConferReceiverBorrowing(string mode)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        using var receiver = mode == "mixed" ? RouteCatalogRemoteCall.EnterReceiver(parent, default) : null;
        await using var constructed = new RouteCatalogDispatchLease(parent.Snapshot, () => Task.CompletedTask);
        using var scope = RouteCatalogRemoteCall.Enter(mode == "public-constructed" ? constructed : parent);
        using var request = HeldMetadataDispatchFixture.Request();
        if (mode == "mixed" && Enabled)
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default));
        else
        {
            using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
            held.ShouldBeNull();
        }
    }

    /// <summary>Changed host inventory, trust and local issuer options refuse after the original admission.</summary>
    [Theory]
    [InlineData("observer")]
    [InlineData("trust")]
    [InlineData("identity")]
    public async Task MutableAdmissionInputsAreFreshlyRevalidated(string changed)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        if (changed == "observer") fixture.DenyObserver = true;
        if (changed == "trust") fixture.DenyTrust = true;
        if (changed == "identity") fixture.Options.Identity = fixture.Options.Identity! with { AuthenticationFingerprint = new string('a', 64) };
        using var request = HeldMetadataDispatchFixture.Request();
        if (Enabled) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default));
        else (await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default)).ShouldBeNull();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.AcquireAsync(default));
    }

    /// <summary>Private frozen activation copies cannot poison later fresh validation.</summary>
    [Fact]
    public async Task ExposedActivationCopyCannotMutatePrivateIssuerBytes()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request();
        using var first = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { first.ShouldBeNull(); return; }
        byte[] copy = first.ShouldNotBeNull().Activation; copy[0] ^= 0xff;
        using var second = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        second.ShouldNotBeNull().Activation[0].ShouldNotBe(copy[0]);
    }

    /// <summary>A foreign authority cannot reuse another issuer's actual receiver lease.</summary>
    [Fact]
    public async Task ForeignIssuerIsRejected()
    {
        await using var first = new HeldMetadataDispatchFixture(); await first.StartAsync();
        await using var foreign = new HeldMetadataDispatchFixture(); await foreign.StartAsync();
        await using var parent = await first.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request();
        if (Enabled) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => foreign.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default));
        else (await foreign.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default)).ShouldBeNull();
    }

    /// <summary>Closure during blocked fresh validation refuses before any new remote factory can run.</summary>
    [Fact]
    public async Task ClosingOriginalScopeDuringValidationCannotEscapeIntoRemoteEnrollment()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request();
        fixture.ObserverBlock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ObserverEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RouteCatalogHeldMetadataDispatch?> pending = fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { (await pending).ShouldBeNull(); return; }
        await fixture.ObserverEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        scope.Dispose(); fixture.ObserverBlock.TrySetResult();
        await Should.ThrowAsync<OperationCanceledException>(async () => { using var late = await pending; });
        RouteCatalogRemoteCall.Current.ShouldBeEmpty();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
    }

    /// <summary>All actual ancestor fences are retained on late parent cancellation, even with no public ambient boundary.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedParentCancellationRetainsActualAncestorsWithoutPublicBoundary(bool foreignBoundary)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var outer = await fixture.Authority.AcquireAsync(default);
        await using var inner = await fixture.Authority.AcquireAsync(default);
        using CancellationTokenSource original = new();
        using var outerScope = RouteCatalogRemoteCall.EnterReceiver(outer, original.Token);
        using var innerScope = RouteCatalogRemoteCall.EnterReceiver(inner, default);
        var publicBoundary = Substitute.For<IRouteCatalogRemoteBoundary>();
        RouteCatalogRemoteExecution.Current = foreignBoundary ? publicBoundary : null;
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", CancellationToken.None);
        if (!Enabled) { held.ShouldBeNull(); return; }
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously), finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var before = (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Select(item => item.LeaseId).Order().ToArray();
        Task<HttpResponseMessage> remote = held.ShouldNotBeNull().InvokeAsync(async _ =>
        { started.SetResult(); await finish.Task.ConfigureAwait(false); return new(HttpStatusCode.NoContent); });
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        outer.DrainRemoteCallsAsync().IsCompleted.ShouldBeFalse(); inner.DrainRemoteCallsAsync().IsCompleted.ShouldBeFalse();
        original.Cancel(); finish.SetResult();
        await Should.ThrowAsync<OperationCanceledException>(async () => { using var response = await remote; });
        await outer.DrainRemoteCallsAsync(); await inner.DrainRemoteCallsAsync();
        await outer.DisposeAsync(); await inner.DisposeAsync();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Select(item => item.LeaseId).Order().ShouldBe(before);
        publicBoundary.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A throwing cancellation callback cannot keep an old receiver marker or boundary alive.</summary>
    [Fact]
    public async Task ThrowingClosureCallbackRestoresAmbientStateAndDoubleDisposeIsSafe()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        var prior = RouteCatalogRemoteExecution.Current;
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { held.ShouldBeNull(); scope.Dispose(); return; }
        using var callback = held.ShouldNotBeNull().Token.Register(() => throw new InvalidOperationException("callback"));
        Should.Throw<AggregateException>(scope.Dispose);
        RouteCatalogRemoteCall.Current.ShouldBeEmpty(); RouteCatalogRemoteExecution.Current.ShouldBeSameAs(prior);
        scope.Dispose();
    }

    /// <summary>Range, no-proof, rebuild, wrong target and ambiguous bodies never qualify as metadata reuse.</summary>
    [Theory]
    [InlineData("range")]
    [InlineData("proof")]
    [InlineData("projection")]
    [InlineData("cursor")]
    [InlineData("other-target")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    public async Task OnlyExactMetadataSemanticsQualify(string changed)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        var body = new StreamReadRequest("tenant-a", "counter", "counter-a", 0, 0, PageSize: 1) { RequireNativeProof = true };
        if (changed == "range") body = body with { ToSequence = 1 };
        if (changed == "proof") body = body with { RequireNativeProof = false };
        if (changed == "projection") body = body with { ProjectionName = "rebuild" };
        if (changed == "cursor") body = body with { Checkpoint = new("tenant-a", "counter", "rebuild", "counter-a", "operation", 1, ProjectionRebuildStatus.Succeeded, DateTimeOffset.UtcNow, null) };
        using var request = HeldMetadataDispatchFixture.Request(changed == "other-target" ? "other" : "eventstore", body);
        if (changed is "unknown" or "duplicate") request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonSerializerOptions.Web).TrimEnd('}') + (changed == "unknown" ? ",\"extra\":true}" : ",\"pageSize\":1}"), Encoding.UTF8, "application/json");
        if (Enabled && changed is "unknown" or "duplicate")
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default));
        else
        {
            using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, changed == "other-target" ? "other" : "eventstore", "api/v1/streams/read/workload", default);
            held.ShouldBeNull();
        }
    }

    /// <summary>Mutation of the public message during fresh validation cannot change the privately owned wire request.</summary>
    [Fact]
    public async Task OriginalMessageMutationDuringValidationCannotChangeCapturedWire()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request(); request.Headers.TryAddWithoutValidation("X-Hexalith-Workload-Assertion", "original");
        fixture.ObserverBlock = new(TaskCreationOptions.RunContinuationsAsynchronously); fixture.ObserverEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { (await pending).ShouldBeNull(); return; }
        await fixture.ObserverEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        request.RequestUri = new("http://unowned.invalid/payload"); request.Content = new StringContent("payload");
        request.Headers.Remove("X-Hexalith-Workload-Assertion"); request.Headers.TryAddWithoutValidation("X-Hexalith-Workload-Assertion", "changed");
        fixture.ObserverBlock.SetResult(); using var held = await pending;
        held.ShouldNotBeNull().Request!.RequestUri!.Host.ShouldBe("127.0.0.1");
        held.Request.Headers.GetValues("X-Hexalith-Workload-Assertion").Single().ShouldBe("original");
        using var json = JsonDocument.Parse(await held.Request.Content!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("toSequence").GetInt64().ShouldBe(0);
    }

    /// <summary>The actual guarded DomainService filter and deferred result both carry receiver provenance to the SDK stream client.</summary>
    [Fact]
    public async Task RealDomainFilterAndResultComposeWithSdkMetadataClient()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync("sample");
        int calls = 0;
        fixture.Options.InvocationTransportFactory = _ => new HeldMetadataTestTransport((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(
                new StreamReadPage("tenant-a", "counter", "counter-a", [], new(0, 0, null, 0, 0, false, null))) });
        });
        using var handler = new RouteCatalogInvocationHandler(fixture.Authority, new HeldMetadataTestTransport((_, _) => throw new InvalidOperationException("ordinary")), Options.Create(fixture.Options));
        using var http = new HttpClient(handler) { BaseAddress = new("http://127.0.0.1:3500/v1.0/invoke/eventstore/method/") };
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>("test-assertion"));
        var client = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }), assertions);
        var command = fixture.Receiver.Catalog.Envelope.Declarations.Single(item => item.RouteId == "counter-increment-counter");
        var cell = await fixture.Receiver.Catalog.Store.ReadAsync(default); var activation = cell.State.Committed!;
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity([
            new(RouteCatalogSurfacePolicy.ClaimType, "service"), new(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "eventstore")], "verified-unit")) };
        context.Request.Path = "/process"; context.Request.Method = "POST";
        context.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = activation.RootDigest;
        context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = cell.State.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Request.Headers[RouteCatalogInvocationHandler.BindingHeader] = RouteCatalogActivationBinding.ComputeDigest(activation);
        var envelope = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", command.Domain, "counter-a", command.MessageType!,
            "{}"u8.ToArray(), "correlation", null, "actor", null);
        var invocation = new DefaultEndpointFilterInvocationContext(context, new DomainServiceRequest(envelope, null));
        var guard = new DomainRouteCatalogGuard(fixture.Authority, Options.Create(fixture.Options));
        async Task ReadAsync()
        {
            int reads = fixture.Reads;
            await client.ReadWorkloadStreamAsync(new("tenant-a", "counter", "counter-a", 0, 0, PageSize: 1) { RequireNativeProof = true }, TestContext.Current.CancellationToken).ConfigureAwait(false);
            if (Enabled) fixture.Reads.ShouldBe(reads); else fixture.Reads.ShouldBeGreaterThan(reads);
        }
        object? result = await new DomainRouteCatalogFilter(guard).InvokeAsync(invocation, async _ =>
        { await ReadAsync().ConfigureAwait(false); return new HeldMetadataTestResult(ReadAsync); });
        await result.ShouldBeOfType<DomainRouteCatalogResult>().ExecuteAsync(context);
        calls.ShouldBe(2);
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
    /// <summary>Repeated closure cannot overwrite a newer genuine receiver context.</summary>
    [Fact]
    public async Task DisposingOldScopeAgainCannotClobberNewScope()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var first = await fixture.Authority.AcquireAsync(default);
        var old = RouteCatalogRemoteCall.EnterReceiver(first, default); old.Dispose();
        await using var second = await fixture.Authority.AcquireAsync(default);
        using var current = RouteCatalogRemoteCall.EnterReceiver(second, default);
        old.Dispose();
        RouteCatalogRemoteCall.Current.Single().ShouldBeSameAs(second);
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (Enabled) held.ShouldNotBeNull().Lease.ShouldBeSameAs(second); else held.ShouldBeNull();
    }

    /// <summary>Unrelated methods retain ordinary fresh admission even under mixed public scopes.</summary>
    [Fact]
    public async Task NonmetadataMethodKeepsOriginalFreshSenderPathUnderMixedScopes()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var receiver = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var ordinary = RouteCatalogRemoteCall.Enter(parent);
        var target = fixture.Receiver.Catalog.Envelope.Routes.First(route => route.AppId == "sample");
        int effects = 0;
        fixture.Options.InvocationTransportFactory = _ => new HeldMetadataTestTransport((_, _) =>
        { effects++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); });
        using var transport = new RouteCatalogInvocationHandler(fixture.Authority, new HeldMetadataTestTransport((_, _) => throw new InvalidOperationException("ordinary")), Options.Create(fixture.Options));
        using var invoker = new HttpMessageInvoker(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3500/v1.0/invoke/" + target.AppId + "/method/" + target.Method);
        int reads = fixture.Reads;
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent); effects.ShouldBe(1); fixture.Reads.ShouldBeGreaterThan(reads);
    }

    /// <summary>Wrong or duplicate roles and incomplete caller membership cannot widen metadata eligibility.</summary>
    [Theory]
    [InlineData("duplicate-role")]
    [InlineData("duplicate-core")]
    [InlineData("domain")]
    [InlineData("operation")]
    [InlineData("method")]
    [InlineData("caller")]
    [InlineData("verb")]
    [InlineData("invocation-path")]
    [InlineData("gateway-name-only")]
    [InlineData("http")]
    [InlineData("native")]
    [InlineData("sidecar")]
    [InlineData("callback")]
    [InlineData("domain-auxiliary")]
    public async Task GatewayRoleAndCoreTupleAreExactAndUnambiguous(string changed)
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        var cell = await fixture.Receiver.Catalog.Store.ReadAsync(default);
        var activation = cell.State.Committed!;
        CommittedRouteCatalogSnapshot Snapshot(RouteCatalogActivation value) => new(value, cell.State.Epoch, "eventstore", fixture.Store.Coordinates);
        RouteCatalogMetadataRequest.IsGateway(Snapshot(activation), "eventstore", "api/v1/streams/read/workload", HttpMethod.Post).ShouldBeTrue();
        var core = activation.AuxiliaryOperations!.Single();
        string digest = new('a', 64);
        core = changed switch
        {
            "domain" => core with { Domain = "not-gateway" },
            "operation" => core with { Operation = "eventstore:stream-read-other" },
            "method" => core with { Method = core.Method + "/payload" },
            "http" => core with { Http = new("owner", "POST", digest) },
            "native" => core with { NativeActor = new("actor", "PUT", digest, digest, digest) },
            "sidecar" => core with { Sidecar = new("owner", digest, "pubsub", "topic") },
            "callback" => core with { DaprCallback = new("callback", "POST", digest, digest, digest) },
            "domain-auxiliary" => core with { DomainAuxiliary = new("owner", "request", "scope", digest) },
            _ => core,
        };
        activation = activation with { AuxiliaryOperations = changed == "duplicate-core" ? [core, core] : [core] };
        var gateway = activation.TechnicalHttpReceivers!.Single();
        if (changed == "duplicate-role") activation = activation with { TechnicalHttpReceivers = [gateway, gateway with { AppId = "sample" }] };
        if (changed is "caller" or "verb" or "invocation-path") activation = activation with
        {
            TechnicalHttpReceivers = [gateway with { Invocations = gateway.Invocations!.Select(invocation => invocation with
            {
                CallerAppId = changed == "caller" ? "foreign" : invocation.CallerAppId,
                HttpMethods = changed == "verb" ? ["GET"] : invocation.HttpMethods,
                Method = changed == "invocation-path" ? invocation.Method + "/other" : invocation.Method,
            }).ToArray() }],
        };
        if (changed == "gateway-name-only") activation = activation with { TechnicalHttpReceivers = [gateway with
        { Endpoints = gateway.Endpoints.Select(endpoint => endpoint with { ActionIdentity = "Hexalith.EventStore.Gateway.Fake, fake-action" }).ToArray() }] };
        if (changed is "duplicate-core" or "http" or "native" or "sidecar" or "callback")
            Should.Throw<RouteCatalogUnavailableException>(() => Snapshot(activation));
        else RouteCatalogMetadataRequest.IsGateway(Snapshot(activation), "eventstore", "api/v1/streams/read/workload", HttpMethod.Post).ShouldBeFalse();
    }

    /// <summary>Known cancellation before actual enrollment invokes nothing and does not retain an unrelated fence.</summary>
    [Fact]
    public async Task CancellationBeforeEnrollmentHasNoRemoteEffectOrFalseRetention()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using CancellationTokenSource caller = new();
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", caller.Token);
        if (!Enabled) { held.ShouldBeNull(); return; }
        caller.Cancel(); int effects = 0;
        await Should.ThrowAsync<OperationCanceledException>(() => held.ShouldNotBeNull().InvokeAsync(_ =>
        { effects++; return Task.FromResult(new HttpResponseMessage()); }));
        effects.ShouldBe(0); parent.DrainRemoteCallsAsync().IsCompleted.ShouldBeTrue();
        await parent.DisposeAsync();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Transport faults retain every captured genuine ancestor and still complete remote enrollment exactly once.</summary>
    [Fact]
    public async Task RemoteFaultRetainsAllExactAncestorFencesAndDrains()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var outer = await fixture.Authority.AcquireAsync(default);
        await using var inner = await fixture.Authority.AcquireAsync(default);
        using var first = RouteCatalogRemoteCall.EnterReceiver(outer, default);
        using var second = RouteCatalogRemoteCall.EnterReceiver(inner, default);
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { held.ShouldBeNull(); return; }
        var expected = (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Select(item => item.LeaseId).Order().ToArray();
        await Should.ThrowAsync<IOException>(() => held.ShouldNotBeNull().InvokeAsync(_ => Task.FromException<HttpResponseMessage>(new IOException("uncertain transport"))));
        await outer.DrainRemoteCallsAsync(); await inner.DrainRemoteCallsAsync();
        await outer.DisposeAsync(); await inner.DisposeAsync();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Select(item => item.LeaseId).Order().ShouldBe(expected);
    }

    /// <summary>An escaped async context cannot revive a closed genuine receiver whose lease has not yet disposed.</summary>
    [Fact]
    public async Task EscapedClosedReceiverContextCannotBorrowStillOpenLease()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        TaskCompletionSource begin = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RouteCatalogHeldMetadataDispatch?> escaped = Task.Run(async () =>
        {
            await begin.Task.ConfigureAwait(false);
            using var request = HeldMetadataDispatchFixture.Request();
            return await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default).ConfigureAwait(false);
        }, TestContext.Current.CancellationToken);
        scope.Dispose(); begin.SetResult();
        if (Enabled) await Should.ThrowAsync<RouteCatalogUnavailableException>(async () => { using var invalid = await escaped; });
        else (await escaped).ShouldBeNull();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
    }

    /// <summary>Closure after validation but before enrollment invokes nothing and permits exact eventual parent release.</summary>
    [Fact]
    public async Task ClosingScopeAfterHandleCreationPreventsRemoteEffectWithoutFalseRetention()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        await using var parent = await fixture.Authority.AcquireAsync(default);
        using var scope = RouteCatalogRemoteCall.EnterReceiver(parent, default);
        using var request = HeldMetadataDispatchFixture.Request();
        using var held = await fixture.Authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
        if (!Enabled) { held.ShouldBeNull(); return; }
        held.ShouldNotBeNull(); scope.Dispose(); int effects = 0;
        await Should.ThrowAsync<OperationCanceledException>(() => held.InvokeAsync(_ =>
        { effects++; return Task.FromResult(new HttpResponseMessage()); }));
        effects.ShouldBe(0); parent.DrainRemoteCallsAsync().IsCompleted.ShouldBeTrue();
        await parent.DisposeAsync();
        (await fixture.Receiver.Catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Actual SDK bootstrap binding selects the private option without raw profile/technical/fixture selectors.</summary>
    [Fact]
    public async Task ActualBoundPrivateOptionsAndEnvironmentSelectThePath()
    {
        await using var fixture = new HeldMetadataDispatchFixture(); await fixture.StartAsync();
        string directory = Directory.CreateTempSubdirectory("held-metadata-options-").FullName;
        try
        {
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(new Microsoft.AspNetCore.Builder.WebApplicationOptions
            { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development });
            builder.Configuration["EventStore:RouteCatalog:ReuseHeldMetadataDispatch"] = "true";
            builder.Configuration["EventStore:RouteCatalog:ScopedDecodeReuse"] = "true";
            var section = builder.Configuration.GetSection("EventStore:RouteCatalog:DevelopmentBootstrap");
            section["Directory"] = directory; section["HostId"] = fixture.Options.Identity!.HostId;
            section["CoordinatorHostId"] = fixture.Receiver.Catalog.Hosts[1].HostId;
            section["AttemptId"] = "held-metadata-options"; section["BindingVersion"] = "2";
            for (int index = 0; index < fixture.Receiver.Catalog.Hosts.Length; index++) section["RequiredHostIds:" + index] = fixture.Receiver.Catalog.Hosts[index].HostId;
            builder.Services.AddSingleton<IRouteCatalogStateStore>(fixture.Store);
            builder.Services.AddEventStoreDevelopmentRouteCatalogBootstrap(builder.Configuration, (_, _) => Task.FromResult(
                fixture.Receiver.Catalog.Recapture!(fixture.Receiver.Catalog.Observations[0])));
            using var provider = builder.Services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value;
            options.Identity = fixture.Options.Identity;
            options.ReuseHeldMetadataDispatch.ShouldBeTrue(); options.DevelopmentFixture.ShouldBeTrue(); options.RequireTechnicalHttpBinding.ShouldBeTrue();
            builder.Configuration["EventStore:RouteCatalog:Profile"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentFixture"].ShouldBeNull();
            builder.Configuration["EventStore:RouteCatalog:RequireTechnicalHttpBinding"].ShouldBeNull();
            var authority = (RouteCatalogAuthority)provider.GetRequiredService<IRouteCatalogAuthority>();
            await using var lease = await authority.AcquireAsync(default);
            using var scope = RouteCatalogRemoteCall.EnterReceiver(lease, default);
            using var request = HeldMetadataDispatchFixture.Request();
            using var held = await authority.GetHeldMetadataDispatchAsync(request, "eventstore", "api/v1/streams/read/workload", default);
            if (Enabled) held.ShouldNotBeNull().Lease.ShouldBeSameAs(lease); else held.ShouldBeNull();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

}
