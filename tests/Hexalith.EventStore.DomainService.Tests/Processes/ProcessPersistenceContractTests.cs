using System.Text.Json;

using Hexalith.EventStore.Client.Processes;
using Hexalith.EventStore.DomainService;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Pins the synthetic process runtime's durable keys and JSON shape.</summary>
public sealed class ProcessPersistenceContractTests
{
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

        string json = JsonSerializer.Serialize(page);

        json.ShouldBe("{\"Version\":1,\"Page\":0,\"Entries\":[{\"Tenant\":\"tenant-a\",\"Domain\":\"sample\",\"Aggregate\":\"source\"}],\"NextPage\":1}");
        ProcessIndexPage<ProcessSource> roundTrip = JsonSerializer.Deserialize<ProcessIndexPage<ProcessSource>>(json).ShouldNotBeNull();
        roundTrip.Version.ShouldBe(1);
        roundTrip.Page.ShouldBe(0);
        roundTrip.NextPage.ShouldBe(1);
        roundTrip.Entries.Single().ShouldBe(source);
    }
}
