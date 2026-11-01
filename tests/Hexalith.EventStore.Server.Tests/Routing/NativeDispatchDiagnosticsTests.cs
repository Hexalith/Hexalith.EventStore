using System.Net;
using System.Net.Http.Json;
using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Drives the installed native receiver middleware and real SDK endpoint inventory without a sidecar.</summary>
public sealed class NativeDispatchDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OuterReceiverDisposesItsRemoteScopeBeforeAwaitingItsLeaseRelease(bool holdAcquisitionAndCancelRequest)
    {
        var log = new DispatchDiagnosticLog();
        await using var fixture = new TechnicalHttpCatalogFixture();
        using var cancellation = new CancellationTokenSource();
        var requestCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration cancellationRegistration = default;
        var acquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<AdminOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (holdAcquisitionAndCancelRequest) await acquire.Task;
            var original = await fixture.Catalog.Authorities[0].AcquireAsync(call.Arg<CancellationToken>());
            return new RouteCatalogDispatchLease(original.Snapshot, async () =>
            {
                RouteCatalogRemoteExecution.Current.ShouldBeNull();
                releaseEntered.SetResult();
                await releaseResume.Task;
                await original.DisposeAsync();
            });
        });
        await fixture.StartAsync(TestContext.Current.CancellationToken, services: services =>
        {
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug)
                .AddFilter(typeof(TechnicalHttpRouteCatalogReceiver).FullName, LogLevel.Debug).AddProvider(log));
            services.AddSingleton(authority);
        });
        fixture.TenantCommands.AddUserToTenantAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellationRegistration = call.Arg<CancellationToken>().Register(() => requestCanceled.TrySetResult());
                entered.SetResult();
                return response.Task;
            });
        try
        {
            Task<HttpResponseMessage> pending = fixture.Client.PostAsJsonAsync("/api/v1/admin/tenants/tenant-a/users",
                new { UserId = "private-subject", Role = "Member" }, cancellation.Token);
            if (holdAcquisitionAndCancelRequest)
            {
                await log.WaitForAsync("ReceiverAcquire", "Started");
                CommandDispatchDiagnosticsTests.AssertOpen(log, "ReceiverAcquire");
                entered.Task.IsCompleted.ShouldBeFalse();
                log.Records.ShouldNotContain(record => Equals(record["Stage"], "ReceiverNext"));
                acquire.SetResult();
            }
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            CommandDispatchDiagnosticsTests.AssertOpen(log, "ReceiverNext");
            (await fixture.Catalog.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
            response.SetResult(new(true, "private-correlation", "accepted", null));
            await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            CommandDispatchDiagnosticsTests.AssertClosed(log, "ReceiverNext", "Completed");
            CommandDispatchDiagnosticsTests.AssertOpen(log, "ReceiverRelease");
            pending.IsCompleted.ShouldBeFalse();
            if (holdAcquisitionAndCancelRequest)
            {
                cancellation.Cancel();
                await requestCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            releaseResume.SetResult();
            if (holdAcquisitionAndCancelRequest)
            {
                await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                await log.WaitForAsync("ReceiverRelease", "Completed");
                CommandDispatchDiagnosticsTests.AssertIndependentOperation(log, "ReceiverRelease");
            }
            else
            {
                using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                completed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            }
            CommandDispatchDiagnosticsTests.AssertClosed(log, "ReceiverRelease", "Completed");
            (await fixture.Catalog.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
            CommandDispatchDiagnosticsTests.AssertPrivate(log);
        }
        finally
        {
            acquire.TrySetResult();
            response.TrySetResult(new(true, "private-correlation", "accepted", null));
            releaseResume.TrySetResult();
            cancellationRegistration.Dispose();
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("acquisition")]
    [InlineData("actor-error-header")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task NativeCallbackAndReleaseRemainOrderedAndSupportSafe(string outcome)
    {
        var log = new DispatchDiagnosticLog();
        await using var fixture = new TechnicalHttpCatalogFixture();
        var acquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int releases = 0;
        CommittedRouteCatalogSnapshot? snapshot = null;
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (outcome == "acquisition") await acquire.Task;
            return new RouteCatalogDispatchLease(snapshot!, async () =>
            {
                RouteCatalogRemoteExecution.Current.ShouldBeNull();
                releases++;
                releaseEntered.SetResult();
                await releaseResume.Task;
            });
        });
        await fixture.StartAsync(TestContext.Current.CancellationToken, activate: false, services: services =>
        {
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug)
                .AddFilter(typeof(TechnicalHttpRouteCatalogReceiver).FullName, LogLevel.Debug).AddProvider(log));
            services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
            services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();
            services.AddSingleton(authority);
        }, endpoints: app => app.MapActorsHandlers().RequireEventStoreChannel());
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "actors/{actorTypeName}/{actorId}/method/{methodName}");
        var native = fixture.App.Services.GetRequiredService<IDevelopmentNativeActorReceiver>().Observe(endpoint).ShouldNotBeNull();
        var activation = fixture.Catalog.Candidate() with
        {
            AuxiliaryOperations = [new("eventstore", endpoint.RoutePattern.RawText!, "sdk-native-actor:method", "sdk") { NativeActor = native }],
        };
        snapshot = new(activation, 1, "eventstore", fixture.Catalog.Store.Coordinates);
        snapshot.Envelope.SchemaVersion.ShouldBe(3);
        snapshot.Envelope.Profile.ShouldBe("Development");
        snapshot.ActivationBindingDigest.ShouldNotBeNull();
        snapshot.AuxiliaryOperations.ShouldContain(operation => operation.NativeActor == native);
        using var cancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestServices = fixture.App.Services, RequestAborted = cancellation.Token };
        context.SetEndpoint(endpoint);
        context.Request.RouteValues["actorTypeName"] = "AggregateActor";
        context.Request.RouteValues["actorId"] = "private-actor";
        context.Request.RouteValues["methodName"] = "ProcessCommandAsync";
        context.Request.Headers["private-header"] = "private-header-value";
        context.Request.Body = new MemoryStream("private-body"u8.ToArray());
        Exception? original = outcome == "failure" ? new InvalidOperationException("private-callback-state")
            : outcome == "cancellation" ? new OperationCanceledException("private-callback-cancellation", cancellation.Token) : null;
        RequestDelegate next = async callback =>
        {
            RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
            callbackEntered.SetResult();
            await callbackResume.Task;
            if (outcome == "cancellation") cancellation.Cancel();
            if (original is not null) throw original;
            if (outcome == "actor-error-header") callback.Response.Headers["X-Daprerrorresponseheader"] = "private-actor-error";
            callback.Response.StatusCode = 204;
        };
        // Exercise the exact private production await seam without adding a public test API
        // or making the real Dapr actor runtime contact a sidecar.
        var receiver = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        var invoke = typeof(TechnicalHttpRouteCatalogReceiver).GetMethod("InvokeNativeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<HttpContext, RequestDelegate, RouteEndpoint, RouteCatalogNativeActorTransport?, RouteCatalogDaprCallbackTransport?, Task>>(receiver);
        try
        {
            Task pending = invoke(context, next, endpoint, native, null);
            if (outcome == "acquisition")
            {
                CommandDispatchDiagnosticsTests.AssertOpen(log, "NativeAcquire");
                callbackEntered.Task.IsCompleted.ShouldBeFalse();
                log.Records.ShouldNotContain(record => Equals(record["Stage"], "NativeCallback"));
                acquire.SetResult();
            }
            Task first = await Task.WhenAny(callbackEntered.Task, releaseEntered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            first.ShouldBeSameAs(callbackEntered.Task, string.Join(",", log.Records.Select(record => record["Stage"] + ":" + record["Status"])));
            CommandDispatchDiagnosticsTests.AssertOpen(log, "NativeCallback");
            releases.ShouldBe(0);
            callbackResume.SetResult();
            if (outcome is "success" or "acquisition")
            {
                await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                CommandDispatchDiagnosticsTests.AssertClosed(log, "NativeCallback", "Completed");
                CommandDispatchDiagnosticsTests.AssertOpen(log, "NativeRelease");
                pending.IsCompleted.ShouldBeFalse();
                cancellation.Cancel();
                releaseResume.SetResult();
            }
            Exception? observed = null;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) { observed = exception; }
            observed.ShouldBeSameAs(original);
            if (original is null) context.Response.StatusCode.ShouldBe(204);
            CommandDispatchDiagnosticsTests.AssertClosed(log, "NativeCallback", outcome is "failure" or "cancellation" ? "Failed" : "Completed");
            CommandDispatchDiagnosticsTests.AssertClosed(log, "NativeRelease", "Completed");
            releases.ShouldBe(outcome is "success" or "acquisition" ? 1 : 0);
            if (outcome is "success" or "acquisition" or "cancellation")
                CommandDispatchDiagnosticsTests.AssertIndependentOperation(log, "NativeRelease");
            CommandDispatchDiagnosticsTests.AssertPrivate(log);
        }
        finally
        {
            acquire.TrySetResult();
            callbackResume.TrySetResult();
            releaseResume.TrySetResult();
        }
    }
}
