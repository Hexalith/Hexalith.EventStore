using System.Reflection;
using System.Security.Claims;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.SignalR;
using Hexalith.EventStore.SignalRHub;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Qualifies production client/hub convergence using reordered RPCs and actual group broadcasts.</summary>
public sealed class ProjectionMembershipTransportTests
{
    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 3, false)]
    [InlineData(true, 3, false)]
    [InlineData(false, 0, true)]
    [InlineData(true, 1, true)]
    [InlineData(false, 4, false)]
    [InlineData(true, 4, false)]
    [InlineData(false, 5, false)]
    [InlineData(true, 5, false)]
    public async Task ReorderedLeaveAndUncertainJoinConvergeWithoutEvictingSurvivors(bool scoped, int race, bool skipNegotiation)
    {
        bool uncertainJoin = race != 0;
        CancellationToken token = TestContext.Current.CancellationToken;
        var barrier = new ProjectionMembershipBarrier();
        var backendFailure = new ProjectionGroupFailure { AddAfterApply = race == 4 };
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR(options => options.MaximumParallelInvocationsPerClient = 4)
            .AddHubOptions<ProjectionChangedHub>(options => { options.AddFilter(barrier); options.AddFilter(backendFailure); });
        builder.Services.AddAuthorization(options => options.AddPolicy("EventStoreHuman", policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddSingleton<ITenantValidator, ClaimsTenantValidator>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SignalROptions { MaxGroupsPerConnection = 2 }));
        await using WebApplication app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("eventstore:tenant", "acme"), new Claim("sub", "test")], "test"));
            await next(context);
        });
        app.MapHub<ProjectionChangedHub>("/hub").AllowAnonymous();
        await app.StartAsync(token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions
        {
            HubUrl = address + "/hub",
            ConfigureHttpConnection = options =>
            {
                options.SkipNegotiation = skipNegotiation;
                if (skipNegotiation) { options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets; }
            },
        });
        using var cancellation = new CancellationTokenSource();
        int survivor = 0, replacement = 0, rawTarget = 0;
        var sentinel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HubConnection connection = (HubConnection)typeof(EventStoreSignalRClient).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        using IDisposable rawSignal = connection.On<string, string>("ProjectionChanged", (projection, _) =>
        {
            if (projection == "counter") { Interlocked.Increment(ref rawTarget); }
            if (projection == "sentinel") { sentinel.TrySetResult(); }
        });
        using IDisposable rawDetail = connection.On<string, string, string?, IReadOnlyDictionary<string, string>>("ProjectionChangedDetail", (projection, _, _, _) =>
        {
            if (projection == "counter") { Interlocked.Increment(ref rawTarget); }
        });
        async Task Subscribe(Action callback, CancellationToken cancellationToken) {
            if (scoped) { await client.SubscribeDetailAsync("counter", "acme", "scope", _ => callback(), cancellationToken); }
            else { await client.SubscribeAsync("counter", "acme", callback, cancellationToken); }
        }
        Task Remove(CancellationToken cancellationToken) => scoped
            ? client.UnsubscribeDetailAsync("counter", "acme", "scope", cancellationToken)
            : client.UnsubscribeAsync("counter", "acme", cancellationToken);
        try
        {
            await client.StartAsync(token);
            client.SupportsAcknowledgedMembership.ShouldBeTrue();
            await client.SubscribeAsync("survivor", "acme", () => Interlocked.Increment(ref survivor), token);
            if (!uncertainJoin || race == 5) { await Subscribe(() => { }, token); }
            if (race >= 4)
            {
                backendFailure.Arm();
                Task failed = race == 4 ? Subscribe(() => { }, token) : Remove(token);
                await Should.ThrowAsync<HubException>(() => failed);
                await barrier.Applied(3).WaitAsync(TimeSpan.FromSeconds(5), token);
            }
            else
            {
            barrier.Arm(after: race == 1 || race == 3);
            Task uncertain = uncertainJoin ? Subscribe(() => { }, cancellation.Token) : Remove(cancellation.Token);
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            Task sameGroupSurvivor = race == 3 ? Subscribe(() => Interlocked.Increment(ref replacement), token) : Task.CompletedTask;
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => uncertain.WaitAsync(TimeSpan.FromSeconds(5), token));
            if (uncertainJoin)
            {
                await barrier.Applied(race == 3 ? 4 : 3).WaitAsync(TimeSpan.FromSeconds(5), token);
            }
            else
            {
                await Subscribe(() => Interlocked.Increment(ref replacement), token);
            }
            await sameGroupSurvivor.WaitAsync(TimeSpan.FromSeconds(5), token);
            barrier.Release.TrySetResult();
            await barrier.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            }
            IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
            for (int round = 0; round < 2; round++)
            {
                if (round == 1)
                {
                    await connection.StopAsync(token);
                    await client.StartAsync(token);
                    client.SupportsAcknowledgedMembership.ShouldBeTrue();
                }
                rawTarget = 0; survivor = 0; replacement = 0;
                sentinel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (scoped) { await hub.Clients.Group("counter:acme:scope").ProjectionChangedDetail("counter", "acme", "scope", new Dictionary<string, string>()); }
                else { await hub.Clients.Group("counter:acme").ProjectionChanged("counter", "acme"); }
                await hub.Clients.Group("survivor:acme").ProjectionChanged("survivor", "acme");
                await hub.Clients.Client(barrier.ConnectionId).ProjectionChanged("sentinel", "acme");
                await sentinel.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                int expected = !uncertainJoin || race == 3 ? 1 : 0;
                rawTarget.ShouldBe(expected, "Raw transport receipt proves actual membership, independently of local callback revocation.");
                replacement.ShouldBe(expected);
                survivor.ShouldBe(1);
            }
        }
        finally { barrier.Release.TrySetResult(); await client.DisposeAsync(); await app.StopAsync(token); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MixedTenantSnapshotRejectsBeforeRemovingOrAddingAnyGroup(bool scoped)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl();
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using HubConnection connection = new HubConnectionBuilder().WithUrl(address + "/hub").Build();
        int survivor = 0, replacement = 0, foreign = 0;
        var sentinel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable signal = connection.On<string, string>("ProjectionChanged", (projection, _) => {
            if (projection == "survivor") { survivor++; }
            if (projection == "replacement") { replacement++; }
            if (projection == "foreign") { foreign++; }
            if (projection == "sentinel") { sentinel.TrySetResult(); }
        });
        await connection.StartAsync(token);
        string scope = scoped ? "scope" : string.Empty;
        (await connection.InvokeAsync<long>("ReconcileProjectionMembership", 1L, new[] { new[] { "survivor", "acme", scope } }, token)).ShouldBe(1);
        await Should.ThrowAsync<HubException>(() => connection.InvokeAsync<long>("ReconcileProjectionMembership", 2L,
            new[] { new[] { "replacement", "acme", scope }, new[] { "foreign", "forbidden", scope } }, token));
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        string suffix = scoped ? ":scope" : string.Empty;
        await hub.Clients.Group("survivor:acme" + suffix).ProjectionChanged("survivor", "acme");
        await hub.Clients.Group("replacement:acme" + suffix).ProjectionChanged("replacement", "acme");
        await hub.Clients.Group("foreign:forbidden" + suffix).ProjectionChanged("foreign", "forbidden");
        await hub.Clients.Client(connection.ConnectionId!).ProjectionChanged("sentinel", "acme");
        await sentinel.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        survivor.ShouldBe(1); replacement.ShouldBe(0); foreign.ShouldBe(0);
        (await connection.InvokeAsync<long>("ReconcileProjectionMembership", 2L, new[] { new[] { "replacement", "acme", scope } }, token)).ShouldBe(2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PartialRevocationPrunesRejectedIntentAndRejoinsAuthorizedSurvivor(bool legacy, bool scoped)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        bool allowAcme = true;
        var control = new ProjectionMembershipRecoveryControl { Legacy = legacy };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => Volatile.Read(ref allowAcme), token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        HubConnection connection = (HubConnection)typeof(EventStoreSignalRClient).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        int authorized = 0, revoked = 0, rawAuthorized = 0, rawRevoked = 0;
        var sentinel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable raw = connection.On<string, string>("ProjectionChanged", (projection, tenant) => {
            if (projection == "sentinel") { sentinel.TrySetResult(); }
            else if (tenant == "acme") { Interlocked.Increment(ref rawRevoked); }
            else { Interlocked.Increment(ref rawAuthorized); }
        });
        await client.StartAsync(token);
        if (scoped) {
            await client.SubscribeDetailAsync("counter", "acme", "scope", _ => revoked++, token);
            await client.SubscribeDetailAsync("counter", "contoso", "scope", _ => authorized++, token);
        }
        else {
            await client.SubscribeAsync("counter", "acme", () => revoked++, token);
            await client.SubscribeAsync("counter", "contoso", () => authorized++, token);
        }
        Volatile.Write(ref allowAcme, false);
        await connection.StopAsync(token);
        await client.StartAsync(token);
        client.SupportsAcknowledgedMembership.ShouldBe(!legacy);
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        string suffix = scoped ? ":scope" : string.Empty;
        await hub.Clients.Group("counter:acme" + suffix).ProjectionChanged("counter", "acme");
        await hub.Clients.Group("counter:contoso" + suffix).ProjectionChanged("counter", "contoso");
        if (scoped) {
            await hub.Clients.Group("counter:acme:scope").ProjectionChangedDetail("counter", "acme", "scope", new Dictionary<string, string>());
            await hub.Clients.Group("counter:contoso:scope").ProjectionChangedDetail("counter", "contoso", "scope", new Dictionary<string, string>());
        }
        await hub.Clients.Client(connection.ConnectionId!).ProjectionChanged("sentinel", "contoso");
        await sentinel.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        authorized.ShouldBe(1); revoked.ShouldBe(0); rawAuthorized.ShouldBe(1); rawRevoked.ShouldBe(0);
    }

    [Fact]
    public async Task FailedAuthorizationProbeSchedulesOwnedRepair()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl();
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        int delivered = 0;
        await client.SubscribeAsync("counter", "contoso", () => delivered++, token);
        control.FailRecoveryProbe();
        await client.StartAsync(token);
        await control.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HubConnection connection = (HubConnection)typeof(EventStoreSignalRClient).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        using IDisposable raw = connection.On<string, string>("ProjectionChanged", (_, _) => received.TrySetResult());
        await app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>()
            .Clients.Group("counter:contoso").ProjectionChanged("counter", "contoso");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        delivered.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HeartbeatsCannotKeepAnIndeterminateMembershipAttemptAliveForever(bool legacy, bool scoped)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl { Legacy = legacy };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        HubConnection connection = (HubConnection)typeof(EventStoreSignalRClient).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        int survivor = 0, rawRemoved = 0;
        await client.StartAsync(token);
        await client.SubscribeAsync("survivor", "contoso", () => Interlocked.Increment(ref survivor), token);
        if (scoped) { await client.SubscribeDetailAsync("counter", "acme", "scope", _ => { }, token); }
        else { await client.SubscribeAsync("counter", "acme", () => { }, token); }
        string original = connection.ConnectionId!;
        control.ArmStall();
        using var cancellation = new CancellationTokenSource();
        Task removal = scoped ? client.UnsubscribeDetailAsync("counter", "acme", "scope", cancellation.Token)
            : client.UnsubscribeAsync("counter", "acme", cancellation.Token);
        try {
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => removal);
            await control.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(12), token);
            connection.ConnectionId.ShouldNotBe(original, "A live heartbeat cannot preserve a hung membership owner indefinitely.");
            var sentinel = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable raw = connection.On<string, string>("ProjectionChanged", (projection, _) => {
                if (projection == "counter") { rawRemoved++; }
                if (projection == "sentinel") { sentinel.TrySetResult(); }
            });
            IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
            await hub.Clients.Group(scoped ? "counter:acme:scope" : "counter:acme").ProjectionChanged("counter", "acme");
            await hub.Clients.Group("survivor:contoso").ProjectionChanged("survivor", "contoso");
            await hub.Clients.Client(connection.ConnectionId!).ProjectionChanged("sentinel", "contoso");
            await sentinel.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            survivor.ShouldBe(1); rawRemoved.ShouldBe(0);
            if (scoped) { await client.SubscribeDetailAsync("counter", "acme", "scope", _ => { }, token); }
            else { await client.SubscribeAsync("counter", "acme", () => { }, token); }
        }
        finally { control.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalCancelsAndAwaitsOwnedMembershipRepair(bool legacy)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl { Legacy = legacy };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        await client.StartAsync(token);
        await client.SubscribeAsync("counter", "acme", () => { }, token);
        control.ArmStall();
        using var cancellation = new CancellationTokenSource();
        Task removal = client.UnsubscribeAsync("counter", "acme", cancellation.Token);
        try {
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => removal);
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), token);
            ((Task)typeof(EventStoreSignalRClient).GetField("_repairTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!).IsCompleted.ShouldBeTrue();
            client.IsConnected.ShouldBeFalse();
        }
        finally { control.Release.TrySetResult(); await client.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermanentOfflineMembershipRejectionSurfacesAndWaitsForChangedIntent(bool legacy)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl { Legacy = legacy };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await client.SubscribeAsync("first", "acme", () => delivered.TrySetResult(), token);
        await client.SubscribeAsync("second", "acme", () => { }, token);
        await client.SubscribeAsync("third", "acme", () => { }, token);
        await Should.ThrowAsync<HubException>(() => client.StartAsync(token));
        int rejectedAttempts = control.MembershipAttempts;
        await Task.Delay(700, token);
        control.MembershipAttempts.ShouldBe(rejectedAttempts, "An unchanged invalid intent must not run an unbounded repair loop.");
        await client.UnsubscribeAsync("third", "acme", token);
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        await hub.Clients.Group("first:acme").ProjectionChanged("first", "acme");
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task TransientMembershipFailuresBackOffAndRecoverActualDelivery()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl { TransientFailures = -1 };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await client.SubscribeAsync("counter", "acme", () => delivered.TrySetResult(), token);
        await client.StartAsync(token);
        await Task.Delay(2200, token);
        control.MembershipAttempts.ShouldBeInRange(3, 6, "Fast backend failures must use bounded exponential backoff rather than retrying every 100ms.");
        long[] attempts = control.AttemptTimes;
        // The initial caller and first background attempt may be adjacent. Later retries grow.
        double laterGap = System.Diagnostics.Stopwatch.GetElapsedTime(attempts[^2], attempts[^1]).TotalMilliseconds;
        laterGap.ShouldBeGreaterThan(500);
        control.TransientFailures = 0;
        await control.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(6), token);
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hub = app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        await hub.Clients.Group("counter:acme").ProjectionChanged("counter", "acme");
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        // A later repair starts again at the initial delay after the successful reconciliation.
        control.ResetRecoveryWitness();
        control.TransientFailures = 2;
        await Should.ThrowAsync<HubException>(() => client.SubscribeAsync("extra", "acme", () => { }, token));
        await control.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        delivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await hub.Clients.Group("counter:acme").ProjectionChanged("counter", "acme");
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task ManualRecoveryPublishesItsFinalGenerationOnlyAfterAcknowledgedRejoin()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var control = new ProjectionMembershipRecoveryControl { HoldReplacementAcknowledgement = true, ReplacementFailures = 1 };
        await using WebApplication app = await StartRecoveryHostAsync(control, () => true, token);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        await client.StartAsync(token);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await client.SubscribeAsync("survivor", "contoso", () => delivered.TrySetResult(), token);
        await client.SubscribeAsync("counter", "acme", () => { }, token);
        control.ArmStall();
        using var cancellation = new CancellationTokenSource();
        Task removal = client.UnsubscribeAsync("counter", "acme", cancellation.Token);
        try
        {
            await control.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => removal);
            await control.ReplacementApplied.Task.WaitAsync(TimeSpan.FromSeconds(12), token);
            client.IsConnected.ShouldBeTrue();
            control.ReplacementFailures.ShouldBe(0, "The pending recovery transition must survive a failed replacement attempt.");
            long sampledDuringAcknowledgementGap = client.RecoveryGeneration;
            await Task.Delay(50, token);
            client.RecoveryGeneration.ShouldBe(sampledDuringAcknowledgementGap);
            control.ReplacementRelease.TrySetResult();
            await control.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            for (int attempt = 0; attempt < 100 && client.RecoveryGeneration == sampledDuringAcknowledgementGap; attempt++) { await Task.Delay(10, token); }
            client.RecoveryGeneration.ShouldBeGreaterThan(sampledDuringAcknowledgementGap, "An observer sampling while connected before acknowledgement must see final catch-up.");
            await app.Services.GetRequiredService<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>()
                .Clients.Group("survivor:contoso").ProjectionChanged("survivor", "contoso");
            await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        }
        finally { control.Release.TrySetResult(); control.ReplacementRelease.TrySetResult(); }
    }

    private static async Task<WebApplication> StartRecoveryHostAsync(ProjectionMembershipRecoveryControl control,
        Func<bool> allowAcme, CancellationToken token)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR(options => { options.KeepAliveInterval = TimeSpan.FromMilliseconds(100); options.MaximumParallelInvocationsPerClient = 1; })
            .AddHubOptions<ProjectionChangedHub>(options => options.AddFilter(control));
        builder.Services.AddAuthorization(options => options.AddPolicy("EventStoreHuman", policy => policy.RequireAuthenticatedUser()));
        builder.Services.AddSingleton<ITenantValidator, ClaimsTenantValidator>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SignalROptions { MaxGroupsPerConnection = 2 }));
        WebApplication app = builder.Build();
        app.Use(async (context, next) => {
            List<Claim> grants = [new("eventstore:tenant", "contoso"), new("sub", "test")];
            if (allowAcme()) { grants.Add(new("eventstore:tenant", "acme")); }
            context.User = new ClaimsPrincipal(new ClaimsIdentity(grants, "test"));
            await next(context);
        });
        app.MapHub<ProjectionChangedHub>("/hub").AllowAnonymous();
        await app.StartAsync(token);
        return app;
    }
}
