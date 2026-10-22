using System.Security.Claims;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Only the authenticated EventStore transport can provide a bound ephemeral effect origin.</summary>
public sealed class TrustedEffectOriginTests
{
    /// <summary>Caller-controlled origin, envelope metadata, or bytes cannot establish trusted scope.</summary>
    [Theory]
    [InlineData("anonymous")]
    [InlineData("foreign-workload")]
    [InlineData("human-principal")]
    [InlineData("tenant")]
    [InlineData("target")]
    [InlineData("type")]
    [InlineData("digest")]
    [InlineData("causation")]
    [InlineData("workload")]
    [InlineData("message")]
    public async Task InvalidOriginDeniesBeforeDomainInvocation(string failure)
    {
        DomainServiceRequest request = Request("item");
        CommandEnvelope command = request.Command;
        command = failure switch
        {
            "tenant" => command with { TenantId = "other-tenant" },
            "target" => command with { AggregateId = "other-item" },
            "type" => command with { CommandType = "Altered" },
            "digest" => command with { Payload = [2] },
            "causation" => command with { CausationId = "altered" },
            "workload" => command with { UserId = "altered" },
            "message" => command with { MessageId = "altered" },
            _ => command,
        };
        IAsyncDomainProcessor processor = Substitute.For<IAsyncDomainProcessor>();
        using ServiceProvider services = Services(failure == "anonymous" ? null
            : failure == "foreign-workload" ? "foreign" : "eventstore", processor, failure == "human-principal" ? "human" : EventStoreWorkloadAuthenticationDefaults.WorkloadScheme);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => DomainServiceRequestRouter.ProcessAsync(
            services, request with { Command = command }));

        await processor.DidNotReceive().ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
        TrustedEffectOriginScope.Current.ShouldBeNull();
        var store = (InMemoryReadModelStore)services.GetRequiredService<IReadModelStore>();
        TrustedEffectOriginDenialRecord record = store.Snapshot<TrustedEffectOriginDenialRecord>("statestore", DomainServiceTrustedEffectOrigin.DenialKey)!;
        record.ReasonCode.ShouldBe("origin-binding-denied");
        record.Count.ShouldBe(1);
        record.CommandBindingDigest.Length.ShouldBe(52);
        record.OriginBindingDigest.Length.ShouldBe(52);
        // Repeated requests replace one fixed-size document and increment a saturating count.
        await Should.ThrowAsync<UnauthorizedAccessException>(() => DomainServiceRequestRouter.ProcessAsync(services, request with { Command = command }));
        store.Snapshot<TrustedEffectOriginDenialRecord>("statestore", DomainServiceTrustedEffectOrigin.DenialKey)!.Count.ShouldBe(2);
        store.Count.ShouldBe(1);
        string json = System.Text.Json.JsonSerializer.Serialize(record);
        json.ShouldNotContain("reactor");
        json.ShouldNotContain("source-message");
        json.ShouldNotContain("tenant-a");
    }

    /// <summary>Unavailable audit fails closed before domain invocation and exposes only a bounded retryable reason.</summary>
    [Fact]
    public async Task AuditFailureDoesNotAcknowledgeAnUnauditedDenial()
    {
        IAsyncDomainProcessor processor = Substitute.For<IAsyncDomainProcessor>();
        var store = Substitute.For<IReadModelStore>();
        store.GetAsync<TrustedEffectOriginDenialRecord>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ReadModelEntry<TrustedEffectOriginDenialRecord>>(new InvalidOperationException("secret credentials")));
        using ServiceProvider services = Services("foreign", processor, store: store);
        var failure = await Should.ThrowAsync<DomainServiceTrustedEffectAuditUnavailableException>(() =>
            DomainServiceRequestRouter.ProcessAsync(services, Request("item")));
        failure.Message.ShouldBe("trusted-effect-origin-audit-unavailable");
        failure.InnerException.ShouldBeNull();
        processor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Successful, failing, and cancelled invocations restore scope and concurrent calls cannot share origin.</summary>
    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task OriginScopeIsIsolatedAndAlwaysRestored(string outcome)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task RunAsync(string item, bool block)
        {
            IAsyncDomainProcessor processor = Substitute.For<IAsyncDomainProcessor>();
            processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    TrustedEffectOriginScope.Current!.Identity.TargetAggregate.ShouldBe(item);
                    if (block)
                    {
                        entered.TrySetResult();
                        await release.Task;
                    }
                    await Task.Yield();
                    TrustedEffectOriginScope.Current!.Identity.TargetAggregate.ShouldBe(item);
                    if (outcome == "failure") throw new InvalidOperationException("synthetic domain failure");
                    if (outcome == "cancellation") throw new OperationCanceledException();
                    return DomainResult.NoOp();
                });
            using ServiceProvider services = Services("eventstore", processor);
            try
            {
                if (outcome == "failure")
                    await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(services, Request(item)));
                else if (outcome == "cancellation")
                    await Should.ThrowAsync<OperationCanceledException>(() => DomainServiceRequestRouter.ProcessAsync(services, Request(item)));
                else
                    _ = await DomainServiceRequestRouter.ProcessAsync(services, Request(item));
                TrustedEffectOriginScope.Current.ShouldBeNull();
            }
            finally { if (!block) release.TrySetResult(); }
        }

        Task first = Task.Run(() => RunAsync("first", true));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Run(() => RunAsync("second", false));
        await first;
        TrustedEffectOriginScope.Current.ShouldBeNull();
    }

    /// <summary>Adapter authorization denials are audited once even when the origin was absent.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdapterDenialIsAuditedExactlyOnce(bool absentOrigin)
    {
        IAsyncDomainProcessor processor = Substitute.For<IAsyncDomainProcessor>();
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DomainResult>(new UnauthorizedAccessException("sensitive adapter detail")));
        using ServiceProvider services = Services("eventstore", processor);
        DomainServiceRequest request = Request("item");
        if (absentOrigin) request = request with { TrustedEffectOrigin = null };
        await Should.ThrowAsync<UnauthorizedAccessException>(() => DomainServiceRequestRouter.ProcessAsync(services, request));
        var store = (InMemoryReadModelStore)services.GetRequiredService<IReadModelStore>();
        TrustedEffectOriginDenialRecord audit = store.Snapshot<TrustedEffectOriginDenialRecord>("statestore", DomainServiceTrustedEffectOrigin.DenialKey)!;
        audit.Count.ShouldBe(1);
        audit.ReasonCode.ShouldBe("adapter-authorization-denied");
        System.Text.Json.JsonSerializer.Serialize(audit).ShouldNotContain("sensitive adapter detail");
        TrustedEffectOriginScope.Current.ShouldBeNull();
    }

    /// <summary>Ordinary UserId and extensions cannot manufacture an authenticated origin.</summary>
    [Fact]
    public async Task OrdinaryEnvelopeDoesNotCreateOrigin()
    {
        IAsyncDomainProcessor processor = Substitute.For<IAsyncDomainProcessor>();
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                TrustedEffectOriginScope.Current.ShouldBeNull();
                return Task.FromResult(DomainResult.NoOp());
            });
        using ServiceProvider services = Services("eventstore", processor);
        DomainServiceRequest request = Request("item");
        _ = await DomainServiceRequestRouter.ProcessAsync(services, request with
        {
            TrustedEffectOrigin = null,
            Command = request.Command with { Extensions = new() { ["trusted-purpose"] = "cascade-expire" } },
        });
    }

    private static DomainServiceRequest Request(string item)
    {
        var identity = new EffectIdentity("tenant-a", "sample", "source", 3, EffectKindCatalog.CascadeExpire, "sample", item, 0);
        string message = EffectIdentityCodec.ComputeMessageId(identity);
        byte[] payload = [123];
        return new DomainServiceRequest(new CommandEnvelope(message, "tenant-a", "sample", item,
            "Expire", payload, message, "source-message", "reactor", null), null)
        {
            TrustedEffectOrigin = new TrustedEffectOrigin(identity, "reactor", "cascade-expire", "source-message", "Expire",
                EffectIdentityCodec.RenderDigest(SHA256.HashData(payload))),
        };
    }

    private static ServiceProvider Services(string? workload, IAsyncDomainProcessor processor, string scheme = EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, IReadModelStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReadModelStore>(store ?? new InMemoryReadModelStore());
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(workload is null ? new ClaimsIdentity()
                    : new ClaimsIdentity([new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, workload)], scheme)),
            },
        });
        services.AddKeyedSingleton<IAsyncDomainProcessor>("sample", processor);
        return services.BuildServiceProvider();
    }
}
