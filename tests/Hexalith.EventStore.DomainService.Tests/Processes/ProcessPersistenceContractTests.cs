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

    /// <summary>Web-default JSON options preserve checkpoint and sweep control fields after restore.</summary>
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
        ProcessSource appliedSource = new("tenant-a", "sample", "applied");
        string appliedSourceId = ProcessStateKeys.SourceId(appliedSource);
        ProcessSweepState sweep = new(1, 2, 5, false, instant, instant.AddMinutes(-1), source.CatalogKey, false,
            1, 2, 3, 4, false, false, 1, new Dictionary<string, int> { ["source-read"] = 1 },
            new Dictionary<string, ProcessPendingDisposition>
            {
                [sourceId] = new(source, false, 3, 0) { ClaimToken = "claim", ClaimUntil = instant.AddMinutes(1) },
                [appliedSourceId] = new(appliedSource, true, 4, 4),
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
        sweepDocument.RootElement.GetProperty("pending").GetProperty(sourceId).EnumerateObject()
            .Select(static value => value.Name).ShouldBe([
                "source", "applied", "preparedGeneration", "appliedGeneration", "claimToken", "claimUntil",
            ]);
        sweepDocument.RootElement.GetProperty("pending").GetProperty(appliedSourceId).GetProperty("applied").GetBoolean().ShouldBeTrue();
        sweepDocument.RootElement.GetProperty("pending").GetProperty(appliedSourceId).GetProperty("appliedGeneration").GetInt64().ShouldBe(4);
        sweepDocument.RootElement.GetProperty("pendingObservations").GetProperty(sourceId).EnumerateObject()
            .Select(static value => value.Name).ShouldBe(["source", "cycle", "unresolved"]);
        sweepDocument.RootElement.GetProperty("pendingTransitions").GetProperty(sourceId).EnumerateObject()
            .Select(static value => value.Name).ShouldBe(["source", "generation", "token"]);
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

    /// <summary>A stored version-one sweep still reads its disposition, observation, and transition records.</summary>
    [Fact]
    public void VersionOneNestedSweepRecordsRemainReadable()
    {
        ProcessSource source = new("tenant-a", "sample", "source");
        ProcessSource appliedSource = new("tenant-a", "sample", "applied");
        string sourceId = ProcessStateKeys.SourceId(source);
        string appliedSourceId = ProcessStateKeys.SourceId(appliedSource);
        string json = """
            {
              "version":1,"cycle":1,"generation":3,"complete":false,
              "cycleStartedAt":"2026-10-01T09:30:00+00:00","lastCompleteAt":null,
              "catalogAfterKey":null,"catalogComplete":false,
              "tenantPage":0,"tenantOffset":0,"candidatePage":0,"candidateOffset":0,
              "candidatesComplete":false,"nextCatalog":true,"unresolved":0,"reasons":{},
              "pending":{
                "SOURCE_ID":{"source":{"tenant":"tenant-a","domain":"sample","aggregate":"source"},"applied":false,"preparedGeneration":2,"appliedGeneration":0,"claimToken":"claim-v1","claimUntil":"2026-10-01T09:31:00+00:00"},
                "APPLIED_SOURCE_ID":{"source":{"tenant":"tenant-a","domain":"sample","aggregate":"applied"},"applied":true,"preparedGeneration":3,"appliedGeneration":3,"claimToken":null,"claimUntil":null}
              },
              "owner":null,"leaseUntil":null,"fence":0,
              "pendingObservations":{"SOURCE_ID":{"source":{"tenant":"tenant-a","domain":"sample","aggregate":"source"},"cycle":1,"unresolved":true}},
              "pendingTransitions":{"SOURCE_ID":{"source":{"tenant":"tenant-a","domain":"sample","aggregate":"source"},"generation":3,"token":"transition-v1"}}
            }
            """.Replace("APPLIED_SOURCE_ID", appliedSourceId, StringComparison.Ordinal)
                .Replace("SOURCE_ID", sourceId, StringComparison.Ordinal);

        ProcessSweepState restored = JsonSerializer.Deserialize<ProcessSweepState>(json, SerializerOptions).ShouldNotBeNull();
        restored.Validate();
        restored.Pending[sourceId].PreparedGeneration.ShouldBe(2);
        restored.Pending[sourceId].ClaimToken.ShouldBe("claim-v1");
        restored.Pending[sourceId].ClaimUntil.ShouldBe(new DateTimeOffset(2026, 10, 1, 9, 31, 0, TimeSpan.Zero));
        restored.Pending[appliedSourceId].Applied.ShouldBeTrue();
        restored.Pending[appliedSourceId].AppliedGeneration.ShouldBe(3);
        restored.PendingObservations[sourceId].Cycle.ShouldBe(1);
        restored.PendingObservations[sourceId].Unresolved.ShouldBeTrue();
        restored.PendingTransitions[sourceId].Generation.ShouldBe(3);
        restored.PendingTransitions[sourceId].Token.ShouldBe("transition-v1");
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
