using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogReceiverScopeTests
{
    [Theory]
    [InlineData("current")]
    [InlineData("current-after-commit")]
    [InlineData("current-after-rollback")]
    [InlineData("missing-root")]
    [InlineData("wrong-root")]
    [InlineData("missing-epoch")]
    [InlineData("wrong-epoch")]
    [InlineData("stale-root-after-commit")]
    [InlineData("stale-epoch-after-rollback")]
    public async Task ReceiverRequiresCurrentRootAndEpochBeforeAnyHandlerEffect(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        string root = first.RootDigest;
        string epoch = "1";
        bool accepted = scenario is "current" or "current-after-commit" or "current-after-rollback";
        if (scenario.StartsWith("stale-", StringComparison.Ordinal) || scenario.StartsWith("current-after-", StringComparison.Ordinal))
        {
            var second = fixture.Candidate(2, first.RootDigest);
            await fixture.ActivateAsync(second);
            if (scenario is "stale-epoch-after-rollback" or "current-after-rollback")
                await fixture.Authorities[0].RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), 2, "committed"));
            if (accepted)
            {
                var current = await fixture.Authorities[0].ReadbackAsync();
                root = current.RootDigest;
                epoch = current.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        int index = Array.FindIndex(fixture.Hosts, h => h.AppId == "sample");
        var guard = new DomainRouteCatalogGuard(fixture.Authorities[index], Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[index] }));
        var context = Context("process", Command(), root, epoch);
        if (scenario == "missing-root") context.HttpContext.Request.Headers.Remove(RouteCatalogInvocationHandler.RootHeader);
        if (scenario == "wrong-root") context.HttpContext.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = new string('f', 64);
        if (scenario == "missing-epoch") context.HttpContext.Request.Headers.Remove(RouteCatalogInvocationHandler.EpochHeader);
        if (scenario == "wrong-epoch") context.HttpContext.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = "999";
        int effects = 0;
        if (accepted)
        {
            await using var lease = await guard.AcquireAsync(context);
            effects++;
        }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => guard.AcquireAsync(context));
        effects.ShouldBe(accepted ? 1 : 0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("reconstruction", "valid")]
    [InlineData("reconstruction", "domain")]
    [InlineData("reconstruction", "tenant")]
    [InlineData("dispatch", "valid")]
    [InlineData("dispatch", "domain")]
    [InlineData("dispatch", "tenant")]
    [InlineData("dispatch", "projection")]
    [InlineData("multipart", "valid")]
    [InlineData("multipart", "domain")]
    [InlineData("multipart", "tenant")]
    [InlineData("multipart", "projection")]
    [InlineData("shared", "valid")]
    [InlineData("shared", "domain")]
    [InlineData("shared", "tenant")]
    [InlineData("shared", "projection")]
    [InlineData("owner", "valid")]
    [InlineData("owner", "domain")]
    [InlineData("owner", "tenant")]
    [InlineData("owner", "projection")]
    [InlineData("receipt", "valid")]
    [InlineData("receipt", "domain")]
    [InlineData("receipt", "tenant")]
    [InlineData("receipt", "projection")]
    [InlineData("unknown", "shape")]
    public async Task EveryAuxiliaryReceiverShapeBindsExactDomainTenantAndSelectedProjection(string shape, string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => fixture.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Domain == "counter")
                ? r with { ResolverTenantPattern = "tenant-a", Method = r.RouteId == "counter-projection" ? "project/v2" : r.Method } : r).ToArray(),
        });
        string tenant = fault == "tenant" ? "tenant-b" : "tenant-a";
        string domain = fault == "domain" ? "greeting" : "counter";
        string projection = fault == "projection" ? "undeclared-projection" : "counter";
        const string fingerprint = "named-capability";
        var request = new ProjectionDispatchRequest(new(tenant, domain, "aggregate", []), [projection], "operation", fingerprint);
        var owner = new ProjectionOwnerProofRequest(tenant, domain, "aggregate", projection, fingerprint);
        string method = shape switch
        {
            "reconstruction" => "replay-state", "multipart" => "project/rebuild/part/v1", "shared" => "project/rebuild/shared/v1",
            "owner" => "project/source-guard/owner-proof/v1", "receipt" => "project/source-guard/applied-receipt/v1", _ => "project/rebuild/v1",
        };
        object body = shape switch
        {
            "reconstruction" => new AggregateReconstructionRequest(tenant, domain, "counter", "aggregate", 0, [], false, null),
            "multipart" => new ProjectionRebuildTransportPartRequest(request, 0, []),
            "shared" => new DomainSharedProjectionRebuildRequest(1, default, new(tenant, domain, projection, "operation", fingerprint)),
            "owner" => owner, "receipt" => new ProjectionAppliedReceiptRequest(owner, 1, "message", "owner", "generation"),
            "unknown" => new object(), _ => request,
        };
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            auxiliaryOperations: [new("sample", method, shape == "reconstruction" ? "domain-service:replay-state" : "domain-service:project", "counter")],
            projectionCapabilities: [new("sample", "counter", "v1", fingerprint, ["counter"])]);
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        int outstanding = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            outstanding++;
            return new RouteCatalogDispatchLease(snapshot, () => { outstanding--; return Task.CompletedTask; });
        });
        var guard = new DomainRouteCatalogGuard(authority, Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts.Single(h => h.AppId == "sample") }));
        var context = Context(method, body, snapshot.RootDigest, "1");
        int effects = 0;
        if (fault == "valid")
        {
            await using var lease = await guard.AcquireAsync(context);
            effects++;
        }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => guard.AcquireAsync(context));
        effects.ShouldBe(fault == "valid" ? 1 : 0);
        outstanding.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiverIndependentlyRequiresMandatoryDelegatedCallerContext(bool supplied)
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate(); await fixture.ActivateAsync(candidate);
        int index = Array.FindIndex(fixture.Hosts, h => h.AppId == "tenants");
        var guard = new DomainRouteCatalogGuard(fixture.Authorities[index], Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[index] }));
        var declaration = fixture.Envelope.Declarations.First(d => d.Execution!.RequiresDelegatedContext);
        var target = fixture.Envelope.Routes.Single(r => r.RouteId == declaration.RouteId);
        var query = new QueryEnvelope(target.ResolverTenantPattern is null or "*" ? "tenant-a" : target.ResolverTenantPattern,
            declaration.Domain, "aggregate", declaration.MessageType!, [], "correlation", "user")
        { CallerAuthority = supplied ? new("delegated-owner-query", [], []) : null };
        var context = Context("query", query, candidate.RootDigest, "1");
        if (supplied) { await using var lease = await guard.AcquireAsync(context); }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => guard.AcquireAsync(context));
        // Signature/provenance verification remains the existing dispatcher's independent responsibility.
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiverLeaseRemainsThroughAsyncResultStreamingAndCancellation(bool cancel)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        int index = Array.FindIndex(fixture.Hosts, h => h.AppId == "sample");
        var guard = new DomainRouteCatalogGuard(fixture.Authorities[index], Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[index] }));
        var context = Context("process", Command(), first.RootDigest, "1");
        context.HttpContext.Response.Body = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        context.HttpContext.RequestAborted = cancellation.Token;
        var streamed = new CatalogHeldResult(cancel);
        var returned = await new DomainRouteCatalogFilter(guard).InvokeAsync(context, _ => ValueTask.FromResult<object?>(streamed));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var execution = returned.ShouldBeAssignableTo<IResult>().ExecuteAsync(context.HttpContext);
        await streamed.Started.Task;
        if (cancel) cancellation.Cancel();
        var second = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        streamed.Release.SetResult();
        if (cancel) await Should.ThrowAsync<OperationCanceledException>(() => execution);
        else await execution;
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        foreach (var host in fixture.Authorities) await host.ReadyAsync(second.AttemptId);
        await fixture.Authorities[0].CommitAsync(second.AttemptId);
    }

    [Theory]
    [InlineData("matching-command", true)]
    [InlineData("wildcard-query", false)]
    [InlineData("missing-command", false)]
    [InlineData("wrong-operation", false)]
    public async Task ReconstructionUsesOnlyRelevantCommandTenantScopeAndCanonicalOperation(string scenario, bool accepted)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Where(r => scenario != "missing-command" || !fixture.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Kind == "command" && d.Domain == "counter"))
                .Select(r => fixture.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Domain == "counter" && d.Kind == "command")
                    ? r with { ResolverTenantPattern = "tenant-a" } : r).ToArray(),
            Declarations = fixture.Envelope.Declarations.Where(d => scenario != "missing-command" || d.Kind != "command" || d.Domain != "counter").ToArray(),
            Idempotency = fixture.Envelope.Idempotency.Where(i => scenario != "missing-command" || i.Domain != "counter").ToArray(),
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            auxiliaryOperations: [new("sample", "replay-state", scenario == "wrong-operation" ? "domain-service:project" : "domain-service:replay-state", "counter")]);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        int effects = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var guard = new DomainRouteCatalogGuard(authority, Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts.Single(h => h.AppId == "sample") }));
        var body = new AggregateReconstructionRequest(scenario == "wildcard-query" ? "tenant-b" : "tenant-a", "counter", "counter", "aggregate", 0, [], false, null);
        var invocation = Context("replay-state", body, snapshot.RootDigest, "1");
        if (accepted) { await using var lease = await guard.AcquireAsync(invocation); effects++; }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => guard.AcquireAsync(invocation));
        effects.ShouldBe(accepted ? 1 : 0);
    }

    private static DomainServiceRequest Command() => new(new("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", "counter", "aggregate", "increment-counter", "{}"u8.ToArray(), "correlation", null, "user", null), null);

    private static EndpointFilterInvocationContext Context(string method, object body, string root, string epoch)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/" + method;
        http.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = root;
        http.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = epoch;
        return new DefaultEndpointFilterInvocationContext(http, body);
    }
}
