
using System.Text;

using Hexalith.EventStore.Server.Queries;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public class QueryActorIdHelperTests {
    [Fact]
    public void DeriveActorId_WithEntityId_ReturnsTier1Format() {
        string actorId = QueryActorIdHelper.DeriveActorId("GetOrder", "tenant1", "order-123", []);

        actorId.ShouldBe("GetOrder:tenant1:order-123");
    }

    [Fact]
    public void DeriveActorId_WithNonEmptyPayloadAndNoEntityId_ReturnsTier2Format() {
        byte[] payload = Encoding.UTF8.GetBytes("{\"filter\":\"active\"}");

        string actorId = QueryActorIdHelper.DeriveActorId("ListOrders", "tenant1", null, payload);

        actorId.ShouldStartWith("ListOrders:tenant1:");
        // Actor ID should have 3 colon-separated parts
        actorId.Split(':').Length.ShouldBe(3);
        // Checksum portion should be 11 chars
        actorId.Split(':')[2].Length.ShouldBe(11);
    }

    [Fact]
    public void DeriveActorId_WithEmptyPayloadAndNoEntityId_ReturnsTier3Format() {
        string actorId = QueryActorIdHelper.DeriveActorId("GetAllOrders", "tenant1", null, []);

        actorId.ShouldBe("GetAllOrders:tenant1");
    }

    [Fact]
    public void DeriveActorId_IdenticalPayloads_ProduceIdenticalChecksums() {
        byte[] payload1 = Encoding.UTF8.GetBytes("{\"filter\":\"active\"}");
        byte[] payload2 = Encoding.UTF8.GetBytes("{\"filter\":\"active\"}");

        string actorId1 = QueryActorIdHelper.DeriveActorId("ListOrders", "tenant1", null, payload1);
        string actorId2 = QueryActorIdHelper.DeriveActorId("ListOrders", "tenant1", null, payload2);

        actorId1.ShouldBe(actorId2);
    }

    [Fact]
    public void ComputeChecksum_ReturnsExactly11Chars() {
        byte[] payload = Encoding.UTF8.GetBytes("test payload");

        string checksum = QueryActorIdHelper.ComputeChecksum(payload);

        checksum.Length.ShouldBe(11);
    }

    [Fact]
    public void ComputeChecksum_ReturnsUrlSafeAlphabet() {
        byte[] payload = Encoding.UTF8.GetBytes("test payload");

        string checksum = QueryActorIdHelper.ComputeChecksum(payload);

        checksum.ShouldNotContain("+");
        checksum.ShouldNotContain("/");
        checksum.ShouldNotContain("=");
    }

    [Fact]
    public void ComputeChecksum_DifferentPayloads_ProduceDifferentChecksums() {
        byte[] payload1 = Encoding.UTF8.GetBytes("payload-one");
        byte[] payload2 = Encoding.UTF8.GetBytes("payload-two");

        string checksum1 = QueryActorIdHelper.ComputeChecksum(payload1);
        string checksum2 = QueryActorIdHelper.ComputeChecksum(payload2);

        checksum1.ShouldNotBe(checksum2);
    }

    [Fact]
    public void ComputeChecksum_SerializationNonDeterminism_ProducesDifferentChecksums() {
        // Two semantically identical JSON objects with different key ordering
        byte[] payload1 = Encoding.UTF8.GetBytes("{\"a\":1,\"b\":2}");
        byte[] payload2 = Encoding.UTF8.GetBytes("{\"b\":2,\"a\":1}");

        string checksum1 = QueryActorIdHelper.ComputeChecksum(payload1);
        string checksum2 = QueryActorIdHelper.ComputeChecksum(payload2);

        // Different byte sequences → different checksums (accepted trade-off per AC #4)
        checksum1.ShouldNotBe(checksum2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DeriveActorId_NullOrEmptyEntityId_TreatedAsAbsent(string? entityId) {
        byte[] payload = Encoding.UTF8.GetBytes("some-payload");

        string actorId = QueryActorIdHelper.DeriveActorId("GetOrder", "tenant1", entityId, payload);

        // Should fall to Tier 2 (payload present), not Tier 1
        actorId.Split(':').Length.ShouldBe(3);
        actorId.Split(':')[2].Length.ShouldBe(11); // Checksum, not entityId
    }

    [Fact]
    public void DeriveActorId_AllTiers_UseColonSeparator() {
        byte[] payload = Encoding.UTF8.GetBytes("data");

        string tier1 = QueryActorIdHelper.DeriveActorId("Q", "T", "E", []);
        string tier2 = QueryActorIdHelper.DeriveActorId("Q", "T", null, payload);
        string tier3 = QueryActorIdHelper.DeriveActorId("Q", "T", null, []);

        tier1.ShouldContain(":");
        tier2.ShouldContain(":");
        tier3.ShouldContain(":");

        // Verify colon is the separator, not hyphen
        tier1.ShouldBe("Q:T:E");
        tier3.ShouldBe("Q:T");
    }

    [Fact]
    public void DeriveActorId_NullPayload_ThrowsArgumentNullException() {
        Should.Throw<ArgumentNullException>(() =>
            QueryActorIdHelper.DeriveActorId("GetOrder", "tenant1", null, null!));
    }

    [Fact]
    public void DeriveActorId_QueryTypeWithColon_ThrowsArgumentException() {
        Should.Throw<ArgumentException>(() =>
            QueryActorIdHelper.DeriveActorId("Get:Order", "tenant1", null, []));
    }

    [Fact]
    public void DeriveActorId_TenantIdWithColon_ThrowsArgumentException() {
        Should.Throw<ArgumentException>(() =>
            QueryActorIdHelper.DeriveActorId("GetOrder", "tenant:1", null, []));
    }

    [Fact]
    public void DeriveActorId_EntityIdWithColon_ThrowsArgumentException() {
        Should.Throw<ArgumentException>(() =>
            QueryActorIdHelper.DeriveActorId("GetOrder", "tenant1", "order:123", []));
    }
}
