using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminTenantOptionsProviderTests {
    private static AdminTenantOptionsProvider CreateProvider(
        AdminTenantApiClient? tenants = null,
        AdminStreamApiClient? streams = null) => new(
            tenants ?? Substitute.For<AdminTenantApiClient>(
                Substitute.For<IHttpClientFactory>(),
                NullLogger<AdminTenantApiClient>.Instance),
            streams ?? Substitute.For<AdminStreamApiClient>(
                Substitute.For<IHttpClientFactory>(),
                NullLogger<AdminStreamApiClient>.Instance),
            NullLogger<AdminTenantOptionsProvider>.Instance);

    private static AdminTenantApiClient TenantsReturning(params TenantSummary[] tenants) {
        AdminTenantApiClient client = Substitute.For<AdminTenantApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTenantApiClient>.Instance);
        _ = client.ListTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TenantSummary>>(tenants));
        return client;
    }

    private static AdminTenantApiClient TenantsThrowing(Exception ex) {
        AdminTenantApiClient client = Substitute.For<AdminTenantApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminTenantApiClient>.Instance);
        _ = client.ListTenantsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);
        return client;
    }

    private static AdminStreamApiClient StreamsReturning(params string[] tenantIds) {
        AdminStreamApiClient client = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        IReadOnlyList<StreamSummary> items = tenantIds
            .Where(tid => !string.IsNullOrWhiteSpace(tid))
            .Select((tid, i) => new StreamSummary(
                TenantId: tid,
                Domain: "counter",
                AggregateId: $"counter-{i}",
                LastEventSequence: 1,
                LastActivityUtc: DateTimeOffset.UtcNow,
                EventCount: 1,
                HasSnapshot: false,
                StreamStatus: StreamStatus.Active))
            .ToList();
        _ = client.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<StreamSummary>(items, items.Count, null)));
        return client;
    }

    private static AdminStreamApiClient StreamsThrowing(Exception ex) {
        AdminStreamApiClient client = Substitute.For<AdminStreamApiClient>(
            Substitute.For<IHttpClientFactory>(),
            NullLogger<AdminStreamApiClient>.Instance);
        _ = client.GetRecentlyActiveStreamsAsync(
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(ex);
        return client;
    }

    [Fact]
    public async Task GetTenantOptionsAsync_RegisteredOnly_ReturnsLoadedWithRegisteredProvenance() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("tenant-a", "Acme Corp", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsReturning(); // empty
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Loaded);
        result.Options.Count.ShouldBe(1);
        result.Options[0].TenantId.ShouldBe("tenant-a");
        result.Options[0].DisplayName.ShouldBe("Acme Corp");
        result.Options[0].Provenance.ShouldBe(TenantProvenance.Registered);
        result.Diagnostic.ShouldBeNull();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_ObservedOnly_ReturnsLoadedWithObservedProvenance() {
        AdminTenantApiClient tenants = TenantsReturning(); // empty
        AdminStreamApiClient streams = StreamsReturning("tenant-a");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Loaded);
        result.Options.Count.ShouldBe(1);
        result.Options[0].TenantId.ShouldBe("tenant-a");
        result.Options[0].DisplayName.ShouldBe("tenant-a");
        result.Options[0].Provenance.ShouldBe(TenantProvenance.ObservedOnly);
    }

    [Fact]
    public async Task GetTenantOptionsAsync_MixedSources_DeduplicatesAndPrefersRegisteredProvenance() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("tenant-a", "Acme Corp", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsReturning("tenant-a", "tenant-b");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Loaded);
        result.Options.Count.ShouldBe(2);

        TenantOption a = result.Options.Single(o => o.TenantId == "tenant-a");
        a.Provenance.ShouldBe(TenantProvenance.Registered);
        a.DisplayName.ShouldBe("Acme Corp");

        TenantOption b = result.Options.Single(o => o.TenantId == "tenant-b");
        b.Provenance.ShouldBe(TenantProvenance.ObservedOnly);
        b.DisplayName.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task GetTenantOptionsAsync_NormalizesTenantIdCaseAndWhitespace() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("  Tenant-A  ", "Acme", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsReturning("TENANT-A", "tenant-a", "tenant-b");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Options.Count.ShouldBe(2);
        result.Options.Select(o => o.TenantId).ShouldContain("tenant-a");
        result.Options.Select(o => o.TenantId).ShouldContain("tenant-b");
    }

    [Fact]
    public async Task GetTenantOptionsAsync_SortsDeterministicallyByDisplayName() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("tenant-z", "Zebra", TenantStatusType.Active),
            new TenantSummary("tenant-a", "Acme", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsReturning("tenant-m");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Options.Select(o => o.DisplayName).ShouldBe(["Acme", "tenant-m", "Zebra"]);
    }

    [Fact]
    public async Task GetTenantOptionsAsync_BothEmpty_ReturnsEmptyStatusWithCanonicalCopy() {
        AdminTenantOptionsProvider provider = CreateProvider(TenantsReturning(), StreamsReturning());

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Empty);
        result.Options.ShouldBeEmpty();
        result.Diagnostic.ShouldBe(AdminTenantOptionsProvider.EmptyMessage);
    }

    [Fact]
    public async Task GetTenantOptionsAsync_RegisteredUnauthorized_ReturnsUnauthorized() {
        AdminTenantApiClient tenants = TenantsThrowing(
            new UnauthorizedAccessException("Authentication required."));
        AdminStreamApiClient streams = StreamsReturning("tenant-a");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Unauthorized);
        result.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_ObservedUnauthorized_ReturnsUnauthorized() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("tenant-a", "Acme", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsThrowing(
            new UnauthorizedAccessException("Authentication required."));
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Unauthorized);
        result.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_BothForbidden_ReturnsForbidden() {
        AdminTenantApiClient tenants = TenantsThrowing(new ForbiddenAccessException("denied"));
        AdminStreamApiClient streams = StreamsThrowing(new ForbiddenAccessException("denied"));
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Forbidden);
        result.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_BothUnavailable_ReturnsUnavailable() {
        AdminTenantApiClient tenants = TenantsThrowing(new ServiceUnavailableException("backend down"));
        AdminStreamApiClient streams = StreamsThrowing(new HttpRequestException("network blip"));
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Unavailable);
        result.Options.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_RegisteredFailsObservedSucceeds_ReturnsPartialWithObservedOptions() {
        AdminTenantApiClient tenants = TenantsThrowing(new ServiceUnavailableException("backend down"));
        AdminStreamApiClient streams = StreamsReturning("tenant-a");
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Partial);
        result.Options.Count.ShouldBe(1);
        result.Options[0].TenantId.ShouldBe("tenant-a");
        result.Options[0].Provenance.ShouldBe(TenantProvenance.ObservedOnly);
        result.Diagnostic.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetTenantOptionsAsync_ObservedFailsRegisteredSucceeds_ReturnsPartialWithRegisteredOptions() {
        AdminTenantApiClient tenants = TenantsReturning(
            new TenantSummary("tenant-a", "Acme", TenantStatusType.Active));
        AdminStreamApiClient streams = StreamsThrowing(new HttpRequestException("network blip"));
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        TenantOptionsResult result = await provider.GetTenantOptionsAsync();

        result.Status.ShouldBe(TenantOptionsLoadStatus.Partial);
        result.Options.Count.ShouldBe(1);
        result.Options[0].TenantId.ShouldBe("tenant-a");
        result.Options[0].Provenance.ShouldBe(TenantProvenance.Registered);
    }

    [Fact]
    public async Task GetTenantOptionsAsync_OperationCanceled_PropagatesCancellation() {
        AdminTenantApiClient tenants = TenantsThrowing(new OperationCanceledException());
        AdminStreamApiClient streams = StreamsReturning();
        AdminTenantOptionsProvider provider = CreateProvider(tenants, streams);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => provider.GetTenantOptionsAsync(cts.Token));
    }
}
