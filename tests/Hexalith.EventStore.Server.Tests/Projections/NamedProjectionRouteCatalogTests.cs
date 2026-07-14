using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class NamedProjectionRouteCatalogTests {
    [Fact]
    public void Replace_AtomicallyReplacesTheWholeImmutableSnapshot() {
        var catalog = new NamedProjectionRouteCatalog();
        var first = new NamedProjectionRouteCatalogSnapshot([
            CreateEntry("widget-service", "v1", "widget", "first", ["widget-detail"]),
        ]);
        var second = new NamedProjectionRouteCatalogSnapshot([
            CreateEntry("widget-service", "v2", "widget", "second", ["widget-index"]),
        ]);

        catalog.Replace(first);
        catalog.Replace(second);

        catalog.Current.TryGet("widget-service", "v1", "widget", out _).ShouldBeFalse();
        catalog.Current.TryGet("widget-service", "v2", "widget", out NamedProjectionRouteCatalogEntry? entry).ShouldBeTrue();
        entry.ShouldNotBeNull().ProjectionTypes.ShouldBe(["widget-index"]);
    }

    [Fact]
    public void MetadataValidation_AcceptsExactCapabilityBindingAndFingerprint() {
        AdminOperationalIndexMetadataResponse response = CreateResponse();

        bool accepted = AdminOperationalIndexHostedService.TryCreateNamedProjectionEntries(
            response,
            "widget-service",
            "v1",
            out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries);

        accepted.ShouldBeTrue();
        NamedProjectionRouteCatalogEntry entry = entries.ShouldHaveSingleItem();
        entry.Domain.ShouldBe("widget");
        entry.ProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
    }

    [Fact]
    public void MetadataValidation_RejectsMissingLegacyAndMismatchedCatalogs() {
        AdminOperationalIndexMetadataResponse valid = CreateResponse();
        AdminOperationalIndexMetadataResponse[] invalid = [
            new AdminOperationalIndexMetadataResponse(valid.Domains),
            valid with { AppId = "stale-service" },
            valid with { ServiceVersion = "v0" },
            valid with { DispatchCapability = "unknown-capability" },
            valid with { DispatchVersion = 1 },
            valid with { CatalogFingerprint = new string('0', 64) },
        ];

        foreach (AdminOperationalIndexMetadataResponse response in invalid) {
            bool accepted = AdminOperationalIndexHostedService.TryCreateNamedProjectionEntries(
                response,
                "widget-service",
                "v1",
                out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries);

            accepted.ShouldBeFalse();
            entries.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Upsert_ReplacesOnlyTheExactBinding() {
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(new NamedProjectionRouteCatalogSnapshot([
            CreateEntry("widget-service", "v1", "widget", "old", ["widget-detail"]),
            CreateEntry("orders-service", "v1", "orders", "orders", ["order-detail"]),
        ]));

        catalog.Upsert([
            CreateEntry("widget-service", "v1", "widget", "new", ["widget-index"]),
        ]);

        catalog.Current.TryGet("widget-service", "v1", "widget", out NamedProjectionRouteCatalogEntry? widget).ShouldBeTrue();
        widget.ShouldNotBeNull().CatalogFingerprint.ShouldBe("new");
        catalog.Current.TryGet("orders-service", "v1", "orders", out _).ShouldBeTrue();
    }

    [Fact]
    public void MetadataValidation_RejectsCatalogAboveLocalHandlerLimit() {
        AdminOperationalIndexMetadataResponse response = CreateResponse();
        var options = new ProjectionDispatchOptions {
            MaxHandlersPerDomain = 1,
            MaxOutcomes = 1,
        };

        bool accepted = AdminOperationalIndexHostedService.TryCreateNamedProjectionEntries(
            response,
            "widget-service",
            "v1",
            options,
            out IReadOnlyList<NamedProjectionRouteCatalogEntry> entries);

        accepted.ShouldBeFalse();
        entries.ShouldBeEmpty();
    }

    [Fact]
    public void Fingerprint_LengthPrefixesPreventDelimiterAmbiguity() {
        string first = ProjectionRouteCatalogFingerprint.Compute(
            "app\nversion",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);
        string second = ProjectionRouteCatalogFingerprint.Compute(
            "app",
            "version\nv1",
            [new ProjectionDispatchRoute("widget", "widget-detail")]);

        first.ShouldNotBe(second);
    }

    private static AdminOperationalIndexMetadataResponse CreateResponse() {
        ProjectionDispatchRoute[] routes = [
            new("widget", "widget-index"),
            new("widget", "widget-detail"),
        ];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1", routes);
        return new AdminOperationalIndexMetadataResponse([
            new AdminOperationalIndexDomainMetadata("widget", [], [], [], [], [], []) {
                NamedProjectionTypes = ["widget-index", "widget-detail"],
            },
        ]) {
            AppId = "widget-service",
            ServiceVersion = "v1",
            DispatchVersion = ProjectionDispatchProtocol.Version,
            DispatchCapability = ProjectionDispatchProtocol.Capability,
            CatalogFingerprint = fingerprint,
        };
    }

    private static NamedProjectionRouteCatalogEntry CreateEntry(
        string appId,
        string serviceVersion,
        string domain,
        string fingerprint,
        IReadOnlyList<string> projectionTypes)
        => new(
            appId,
            serviceVersion,
            domain,
            ProjectionDispatchProtocol.Version,
            ProjectionDispatchProtocol.Capability,
            fingerprint,
            projectionTypes);
}
