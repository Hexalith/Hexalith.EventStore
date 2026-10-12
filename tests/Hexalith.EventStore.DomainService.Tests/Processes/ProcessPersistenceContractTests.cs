using System.Text.Json;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.DomainService;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Pins the synthetic process runtime's durable keys and JSON shape.</summary>
public sealed class ProcessPersistenceContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Control and tenant keys retain their versioned, disjoint storage namespaces.</summary>
    [Fact]
    public void VersionOneKeysAreStable()
    {
        ProcessSource source = new("tenant-a", "sample", "source");
        const string sourceId = "91949C37479652A2E37353C5D1CB308932C26F8CDBA85C864B20C0AB1BC20C88";

        ProcessStateKeys.Sweep.ShouldBe("eventstore:process:v1:control:sweep");
        ProcessStateKeys.Tenants(3).ShouldBe("eventstore:process:v1:control:tenants:3");
        ProcessStateKeys.TenantsTail.ShouldBe("eventstore:process:v1:control:tenants:tail");
        ProcessStateKeys.Candidates("tenant-a", 2).ShouldBe("eventstore:process:v1:tenant:dGVuYW50LWE:candidates:2");
        ProcessStateKeys.CandidatesTail("tenant-a").ShouldBe("eventstore:process:v1:tenant:dGVuYW50LWE:candidates:tail");
        ProcessStateKeys.SourceId(source).ShouldBe(sourceId);
        ProcessStateKeys.Source(source).ShouldBe("eventstore:process:v1:tenant:dGVuYW50LWE:source:" + sourceId);
        ProcessStateKeys.CandidateMarker(source).ShouldBe("eventstore:process:v1:tenant:dGVuYW50LWE:source:" + sourceId + ":candidate");
        ProcessStateKeys.TenantMarker("tenant-a").ShouldBe("eventstore:process:v1:tenant:dGVuYW50LWE:published");
    }

    /// <summary>Persisted candidates carry only the canonical identity fields and the versioned page link.</summary>
    [Fact]
    public void CandidatePageJsonShapeIsStable()
    {
        ProcessSource source = new("tenant-a", "sample", "source");
        ProcessIndexPage<ProcessSource> page = new(1, 0, [source], 1);

        string json = JsonSerializer.Serialize(page, SerializerOptions);

        json.ShouldBe("{\"version\":1,\"page\":0,\"entries\":[{\"tenant\":\"tenant-a\",\"domain\":\"sample\",\"aggregate\":\"source\"}],\"nextPage\":1}");
        ProcessIndexPage<ProcessSource> roundTrip = JsonSerializer.Deserialize<ProcessIndexPage<ProcessSource>>(json, SerializerOptions).ShouldNotBeNull();
        roundTrip.Version.ShouldBe(1);
        roundTrip.Page.ShouldBe(0);
        roundTrip.NextPage.ShouldBe(1);
        roundTrip.Entries.Single().ShouldBe(source);
    }

    /// <summary>The production serializer preserves every checkpoint and sweep control field after restore.</summary>
    [Fact]
    public void CheckpointAndSweepJsonShapeIsStable()
    {
        ProcessSource source = new("tenant-a", "sample", "source");
        DateTimeOffset instant = new(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
        ProcessSourceState checkpoint = new(1, source, 7, ProcessSourceStatus.Retrying, 2, 8,
            "effect-unknown", null, null, 3, 4, instant)
        {
            ObservedUnresolved = true,
            LastDispositionGeneration = 2,
            LastTransitionGeneration = 3,
            LastTransitionToken = "fence-token",
        };
        string checkpointJson = JsonSerializer.Serialize(checkpoint, SerializerOptions);
        using JsonDocument checkpointDocument = JsonDocument.Parse(checkpointJson);
        checkpointDocument.RootElement.EnumerateObject().Select(static value => value.Name).ShouldBe([
            "version", "source", "checkpoint", "status", "attempts", "failureSequence", "reasonCode", "owner", "leaseUntil",
            "fence", "observedCycle", "updatedAt", "observedUnresolved", "lastDispositionGeneration", "lastTransitionGeneration", "lastTransitionToken",
        ]);
        checkpointDocument.RootElement.GetProperty("status").GetInt32().ShouldBe(2);
        JsonSerializer.Deserialize<ProcessSourceState>(checkpointJson, SerializerOptions).ShouldBe(checkpoint);

        string sourceId = ProcessStateKeys.SourceId(source);
        ProcessSweepState sweep = new(1, 2, 5, false, instant, instant.AddMinutes(-1), source.CatalogKey, false,
            1, 2, 3, 4, false, false, 1, new Dictionary<string, int> { ["source-read"] = 1 },
            new Dictionary<string, ProcessPendingDisposition>
            {
                [sourceId] = new(source, false, 3, 0) { ClaimToken = "claim", ClaimUntil = instant.AddMinutes(1) },
            }, "replica", instant.AddMinutes(2), 8)
        {
            PendingObservations = new Dictionary<string, ProcessPendingObservation> { [sourceId] = new(source, 2, true) },
            PendingPublications = new Dictionary<string, ProcessSource> { [sourceId] = source },
            PendingTransitions = new Dictionary<string, ProcessPendingTransition> { [sourceId] = new(source, 4, "transition") },
            PublicationRepairAfterKey = sourceId,
            DispositionRepairAfterKey = sourceId,
            NextPublicationRepair = false,
            PublicationCandidateRepairPages = new Dictionary<string, int> { [sourceId] = 3 },
            PublicationTenantRepairPages = new Dictionary<string, int> { [sourceId] = 2 },
            PublicationCandidateUnknownPages = new Dictionary<string, bool> { [sourceId] = true },
            PublicationTenantUnknownPages = new Dictionary<string, bool> { [sourceId] = true },
            ExpectedTenantPage = true,
            ExpectedCandidatePage = true,
            TenantProbeMisses = 2,
            CandidateProbeMisses = 3,
            RetainedCandidates = 9,
            CapacityExceeded = true,
        };
        string sweepJson = JsonSerializer.Serialize(sweep, SerializerOptions);
        using JsonDocument sweepDocument = JsonDocument.Parse(sweepJson);
        sweepDocument.RootElement.EnumerateObject().Select(static value => value.Name).ShouldBe([
            "version", "cycle", "generation", "complete", "cycleStartedAt", "lastCompleteAt", "catalogAfterKey", "catalogComplete",
            "tenantPage", "tenantOffset", "candidatePage", "candidateOffset", "candidatesComplete", "nextCatalog", "unresolved",
            "reasons", "pending", "owner", "leaseUntil", "fence", "pendingObservations", "pendingPublications", "pendingTransitions",
            "publicationRepairAfterKey", "dispositionRepairAfterKey", "nextPublicationRepair", "publicationCandidateRepairPages",
            "publicationTenantRepairPages", "publicationCandidateUnknownPages", "publicationTenantUnknownPages", "expectedTenantPage",
            "expectedCandidatePage", "tenantProbeMisses", "candidateProbeMisses", "retainedCandidates", "capacityExceeded",
        ]);
        ProcessSweepState restored = JsonSerializer.Deserialize<ProcessSweepState>(sweepJson, SerializerOptions).ShouldNotBeNull();
        restored.Validate();
        restored.CatalogAfterKey.ShouldBe(source.CatalogKey);
        restored.TenantPage.ShouldBe(1);
        restored.TenantOffset.ShouldBe(2);
        restored.CandidatePage.ShouldBe(3);
        restored.CandidateOffset.ShouldBe(4);
        restored.NextCatalog.ShouldBeFalse();
        restored.Unresolved.ShouldBe(1);
        restored.Reasons["source-read"].ShouldBe(1);
        restored.Pending[sourceId].PreparedGeneration.ShouldBe(3);
        restored.Pending[sourceId].ClaimToken.ShouldBe("claim");
        restored.Pending[sourceId].ClaimUntil.ShouldBe(instant.AddMinutes(1));
        restored.PendingObservations[sourceId].Cycle.ShouldBe(2);
        restored.PendingObservations[sourceId].Unresolved.ShouldBeTrue();
        restored.PendingPublications[sourceId].ShouldBe(source);
        restored.PendingTransitions[sourceId].Generation.ShouldBe(4);
        restored.PendingTransitions[sourceId].Token.ShouldBe("transition");
        restored.PublicationCandidateRepairPages[sourceId].ShouldBe(3);
        restored.PublicationTenantRepairPages[sourceId].ShouldBe(2);
        restored.PublicationCandidateUnknownPages[sourceId].ShouldBeTrue();
        restored.PublicationTenantUnknownPages[sourceId].ShouldBeTrue();
        restored.NextPublicationRepair.ShouldBeFalse();
        restored.ExpectedTenantPage.ShouldBeTrue();
        restored.ExpectedCandidatePage.ShouldBeTrue();
        restored.TenantProbeMisses.ShouldBe(2);
        restored.CandidateProbeMisses.ShouldBe(3);
        restored.RetainedCandidates.ShouldBe(9);
        restored.CapacityExceeded.ShouldBeTrue();
        JsonSerializer.Serialize(restored, SerializerOptions).ShouldBe(sweepJson);
    }

    /// <summary>Markers, tail hints, and all source-status numeric values are wire-stable.</summary>
    [Fact]
    public void MarkerTailAndStatusJsonIsStable()
    {
        ProcessSource source = new("tenant-a", "sample", "source");
        JsonSerializer.Serialize(new ProcessCandidateMarker(1, source), SerializerOptions)
            .ShouldBe("{\"version\":1,\"source\":{\"tenant\":\"tenant-a\",\"domain\":\"sample\",\"aggregate\":\"source\"}}");
        JsonSerializer.Serialize(new ProcessTenantMarker(1, source.Tenant), SerializerOptions)
            .ShouldBe("{\"version\":1,\"tenant\":\"tenant-a\"}");
        JsonSerializer.Serialize(new ProcessIndexTail(1, 3), SerializerOptions).ShouldBe("{\"version\":1,\"page\":3}");
        JsonSerializer.Deserialize<ProcessCandidateMarker>(JsonSerializer.Serialize(new ProcessCandidateMarker(1, source), SerializerOptions), SerializerOptions)
            .ShouldBe(new ProcessCandidateMarker(1, source));
        JsonSerializer.Deserialize<ProcessTenantMarker>(JsonSerializer.Serialize(new ProcessTenantMarker(1, source.Tenant), SerializerOptions), SerializerOptions)
            .ShouldBe(new ProcessTenantMarker(1, source.Tenant));
        JsonSerializer.Deserialize<ProcessIndexTail>(JsonSerializer.Serialize(new ProcessIndexTail(1, 3), SerializerOptions), SerializerOptions)
            .ShouldBe(new ProcessIndexTail(1, 3));
        ((int)ProcessSourceStatus.Active).ShouldBe(0);
        ((int)ProcessSourceStatus.Incomplete).ShouldBe(1);
        ((int)ProcessSourceStatus.Retrying).ShouldBe(2);
        ((int)ProcessSourceStatus.Parked).ShouldBe(3);
        ((int)ProcessSourceStatus.Quarantined).ShouldBe(4);
        ((int)ProcessSourceStatus.Revalidating).ShouldBe(5);
    }
}
