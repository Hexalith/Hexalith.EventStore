using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

/// <summary>Observes production client composition while isolating only catalog authority and terminal I/O.</summary>
/// <param name="send">The independently owned invocation transport operation.</param>
internal sealed class BoundedInvocationResilienceCapture(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler, IHttpMessageHandlerBuilderFilter
{
    private int _acquired;
    private int _released;

    /// <summary>Gets leases whose real guard disposal has not released the fence.</summary>
    internal int OutstandingLeases => Volatile.Read(ref _acquired) - Volatile.Read(ref _released);

    /// <summary>Gets the actual additional handlers for each constructed production client.</summary>
    internal Dictionary<string, Type[]> HandlerTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the actual primary guard type for each constructed production client.</summary>
    internal Dictionary<string, Type> PrimaryTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>Registers observation before production filters so the final primary guard is visible.</summary>
    internal void Observe(IServiceCollection services)
    {
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(this);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() =>
            new BoundedInvocationResilienceCapture((_, _) => throw new InvalidOperationException("Invocation bypassed its independently owned transport."))));
    }

    /// <summary>Supplies a canonical catalog and counts release callbacks without changing real guard behavior.</summary>
    internal void IsolateCatalog(IServiceCollection services)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        byte[] bytes = File.ReadAllBytes(Path.Combine(directory!.FullName, "deploy/dapr/eventstore-routing-catalog.json"));
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(bytes);
        var snapshot = new CommittedRouteCatalogSnapshot(bytes, RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            [new("eventstore-admin", "api/v1/admin/dapr/components", "admin:read", "admin")], localAppId: "eventstore");
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref _acquired);
            return new RouteCatalogDispatchLease(snapshot, () =>
            {
                Interlocked.Increment(ref _released);
                return Task.CompletedTask;
            });
        });
        services.AddSingleton(authority);
        services.Configure<RouteCatalogHostOptions>(options =>
            options.InvocationTransportFactory = _ => new BoundedInvocationResilienceCapture(send));
    }

    /// <summary>Checks only the changed composition, its validated options, and inherited-client isolation.</summary>
    internal void AssertPolicy(IServiceProvider services, string name, params string[] inheritedClients)
    {
        Type[] handlers = HandlerTypes[name];
        handlers.Count(type => type == typeof(ResilienceHandler)).ShouldBe(1);
        handlers[0].ShouldBe(typeof(ResilienceHandler));
        Array.FindIndex(handlers, type => type.FullName!.Contains("ServiceDiscovery", StringComparison.Ordinal)).ShouldBeGreaterThan(0);
        PrimaryTypes[name].ShouldBe(typeof(RouteCatalogInvocationHandler));
        IOptionsMonitor<HttpStandardResilienceOptions> monitor = services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        HttpStandardResilienceOptions scoped = monitor.Get(name + "-standard");
        var defaults = new HttpStandardResilienceOptions();
        scoped.AttemptTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        scoped.TotalRequestTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        scoped.CircuitBreaker.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(60));
        scoped.Retry.MaxRetryAttempts.ShouldBe(defaults.Retry.MaxRetryAttempts);
        scoped.Retry.Delay.ShouldBe(defaults.Retry.Delay);
        scoped.Retry.BackoffType.ShouldBe(defaults.Retry.BackoffType);
        scoped.Retry.UseJitter.ShouldBe(defaults.Retry.UseJitter);
        scoped.CircuitBreaker.FailureRatio.ShouldBe(defaults.CircuitBreaker.FailureRatio);
        scoped.CircuitBreaker.MinimumThroughput.ShouldBe(defaults.CircuitBreaker.MinimumThroughput);
        scoped.CircuitBreaker.BreakDuration.ShouldBe(defaults.CircuitBreaker.BreakDuration);
        scoped.RateLimiter.DefaultRateLimiterOptions.PermitLimit.ShouldBe(defaults.RateLimiter.DefaultRateLimiterOptions.PermitLimit);
        scoped.RateLimiter.DefaultRateLimiterOptions.QueueLimit.ShouldBe(defaults.RateLimiter.DefaultRateLimiterOptions.QueueLimit);
        HttpStandardResilienceOptions inherited = monitor.Get("-standard");
        inherited.AttemptTimeout.Timeout.ShouldBe(defaults.AttemptTimeout.Timeout);
        inherited.TotalRequestTimeout.Timeout.ShouldBe(defaults.TotalRequestTimeout.Timeout);
        inherited.CircuitBreaker.SamplingDuration.ShouldBe(defaults.CircuitBreaker.SamplingDuration);
        IHttpClientFactory clients = services.GetRequiredService<IHttpClientFactory>();
        foreach (string clientName in inheritedClients)
        {
            using HttpClient client = clients.CreateClient(clientName);
            HandlerTypes[clientName].Count(type => type == typeof(ResilienceHandler)).ShouldBe(1);
        }
    }

    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        // Factory logging surrounds the entire application chain and does not change policy order.
        HandlerTypes[builder.Name] = builder.AdditionalHandlers.Select(handler => handler.GetType())
            .Where(type => type.Namespace != "Microsoft.Extensions.Http.Logging").ToArray();
        PrimaryTypes[builder.Name] = builder.PrimaryHandler.GetType();
    };

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
