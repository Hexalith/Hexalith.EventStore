using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Uses real installed Gateway inventory, real closed activation and real durable CAS leases for private metadata controls.</summary>
internal sealed class HeldMetadataDispatchFixture : IAsyncDisposable
{
    internal TechnicalHttpCatalogFixture Receiver { get; } = new();
    internal RouteCatalogAuthority Authority { get; private set; } = null!;
    internal RouteCatalogHostOptions Options { get; private set; } = null!;
    internal IRouteCatalogStateStore Store { get; } = Substitute.For<IRouteCatalogStateStore>();
    internal IRouteCatalogTrustValidator Trust { get; } = Substitute.For<IRouteCatalogTrustValidator>();
    internal bool DenyTrust { get; set; }
    internal bool DenyObserver { get; set; }
    internal TaskCompletionSource? ObserverBlock { get; set; }
    internal TaskCompletionSource? ObserverEntered { get; set; }
    internal int TrustCalls { get; private set; }
    internal int ObserverCalls { get; private set; }
    internal int Reads => Store.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IRouteCatalogStateStore.ReadAsync));
    internal int Writes => Store.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IRouteCatalogStateStore.CompareExchangeAsync));

    /// <summary>Starts the actual SDK MVC inventory and issues leases from the selected local host identity.</summary>
    internal async Task StartAsync(string localApp = "eventstore")
    {
        await Receiver.StartAsync(TestContext.Current.CancellationToken,
            services: services => services.AddControllers().ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new HeldMetadataStreamFeatureProvider())),
            fullInventory: true, activate: false,
            inventoryAssembly: "Hexalith.EventStore.Gateway", invocations: [new(localApp, "api/v1/streams/read/workload", ["POST"])]).ConfigureAwait(false);
        var auxiliary = new RouteCatalogAuxiliaryOperation("eventstore", "api/v1/streams/read/workload", "eventstore:stream-read", "gateway");
        Receiver.Catalog.UpdateObservation(0, Receiver.Catalog.Observations[0] with { AuxiliaryOperations = [auxiliary] });
        var candidate = Receiver.Catalog.Candidate() with { AuxiliaryOperations = [auxiliary] };
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await Receiver.Catalog.ActivateAsync(candidate).ConfigureAwait(false);
        Options = new() { Identity = Receiver.Catalog.Hosts.Single(host => host.AppId == localApp), Profile = "Development",
            DevelopmentFixture = true, RequireTechnicalHttpBinding = true, ScopedDecodeReuse = true,
            RetainValidatedCatalogBytes = true, ReuseHeldMetadataDispatch = true,
            InvocationTransportFactory = _ => new HeldMetadataTestTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))) };
        Store.Coordinates.Returns(Receiver.Catalog.Store.Coordinates);
        Store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call => Receiver.Catalog.Store.ReadAsync(call.Arg<CancellationToken>()));
        Store.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Receiver.Catalog.Store.CompareExchangeAsync(call.Arg<RouteCatalogActivationState>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        var validator = new DevelopmentRouteCatalogTrustValidator(options, Receiver.App.Environment);
        Trust.ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            TrustCalls++;
            await validator.ValidateAsync(call.Arg<RouteCatalogActivation>(), call.Arg<string>(), call.Arg<CancellationToken>()).ConfigureAwait(false);
            if (DenyTrust) throw new RouteCatalogUnavailableException("changed actual trust");
        });
        var observer = new ObservedRouteCatalogHost(async token =>
        {
            ObserverCalls++;
            ObserverEntered?.TrySetResult();
            if (ObserverBlock is { } block) await block.Task.WaitAsync(token).ConfigureAwait(false);
            var actual = Receiver.Catalog.Observations.Single(observation => observation.HostId == Options.Identity!.HostId);
            actual = Receiver.Catalog.Recapture?.Invoke(actual) ?? actual;
            return DenyObserver ? actual with { AuthenticationProfile = "changed-installed-authentication"u8.ToArray() } : actual;
        });
        Authority = new(Store, Trust, options, observer, environment: Receiver.App.Environment);
    }

    /// <summary>Builds the real SDK metadata JSON frame, keeping caller mutation available for adversarial tests.</summary>
    internal static HttpRequestMessage Request(string appId = "eventstore", StreamReadRequest? body = null)
        => new(HttpMethod.Post, "http://127.0.0.1:3500/v1.0/invoke/" + appId + "/method/api/v1/streams/read/workload")
        { Content = JsonContent.Create(body ?? new("tenant-a", "counter", "counter-a", 0, 0, PageSize: 1) { RequireNativeProof = true }) };

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Authority?.Dispose();
        await Receiver.DisposeAsync().ConfigureAwait(false);
    }
}
