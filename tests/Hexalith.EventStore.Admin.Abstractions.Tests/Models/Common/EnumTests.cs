using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Common;

public class EnumTests
{
    [Fact]
    public void StreamStatus_HasExpectedMembers()
    {
        Enum.GetNames<StreamStatus>().ShouldBe(["Active", "Idle", "Tombstoned"]);
    }

    [Fact]
    public void TimelineEntryType_HasExpectedMembers()
    {
        Enum.GetNames<TimelineEntryType>().ShouldBe(["Command", "Event", "Query"]);
    }

    [Fact]
    public void ProjectionStatusType_HasExpectedMembers()
    {
        Enum.GetNames<ProjectionStatusType>().ShouldBe(["Running", "Paused", "Error", "Rebuilding"]);
    }

    [Fact]
    public void HealthStatus_HasExpectedMembers()
    {
        Enum.GetNames<HealthStatus>().ShouldBe(["Healthy", "Degraded", "Unhealthy"]);
    }

    [Fact]
    public void TenantStatusType_HasExpectedMembers()
    {
        Enum.GetNames<TenantStatusType>().ShouldBe(["Active", "Disabled"]);
    }

    [Fact]
    public void AdminRole_HasExpectedMembers()
    {
        Enum.GetNames<AdminRole>().ShouldBe(["ReadOnly", "Operator", "Admin"]);
    }
}
