using Hexalith.EventStore.Admin.Server.Services;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class KnownActorTypesTests
{
    [Fact]
    public void Types_ContainsThreeKnownActorTypes()
    {
        KnownActorTypes.Types.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData("AggregateActor")]
    [InlineData("ETagActor")]
    [InlineData("ProjectionActor")]
    public void Types_ContainsExpectedActorType(string typeName)
    {
        KnownActorTypes.Types.ShouldContainKey(typeName);
    }

    [Theory]
    [InlineData("AggregateActor")]
    [InlineData("ETagActor")]
    [InlineData("ProjectionActor")]
    public void Types_AllHaveNonEmptyDescription(string typeName)
    {
        KnownActorTypeDescriptor descriptor = KnownActorTypes.Types[typeName];
        descriptor.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("AggregateActor")]
    [InlineData("ETagActor")]
    [InlineData("ProjectionActor")]
    public void Types_AllHaveValidIdFormat(string typeName)
    {
        KnownActorTypeDescriptor descriptor = KnownActorTypes.Types[typeName];
        descriptor.ActorIdFormat.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("AggregateActor")]
    [InlineData("ETagActor")]
    [InlineData("ProjectionActor")]
    public void Types_AllHaveAtLeastOneStateKey(string typeName)
    {
        KnownActorTypeDescriptor descriptor = KnownActorTypes.Types[typeName];
        descriptor.StateKeys.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetDescriptor_KnownType_ReturnsCorrectDescriptor()
    {
        KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor("ETagActor");

        descriptor.Description.ShouldContain("ETag");
        descriptor.StateKeys.ShouldContain("etag");
    }

    [Fact]
    public void GetDescriptor_UnknownType_ReturnsDefault()
    {
        KnownActorTypeDescriptor descriptor = KnownActorTypes.GetDescriptor("UnknownActor");

        descriptor.Description.ShouldBe("Unknown actor type");
        descriptor.ActorIdFormat.ShouldBe("actor-id");
        descriptor.StateKeys.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("etag", false)]
    [InlineData("projection-state", false)]
    [InlineData("pending_command_count", false)]
    [InlineData("{actorId}:metadata", false)]
    [InlineData("{actorId}:events:{N}", true)]
    [InlineData("idempotency:{causationId}", true)]
    [InlineData("drain:{correlationId}", true)]
    [InlineData("{actorId}:pipeline:{correlationId}", true)]
    public void IsDynamicKeyFamily_ClassifiesCorrectly(string stateKey, bool expected)
    {
        KnownActorTypes.IsDynamicKeyFamily(stateKey).ShouldBe(expected);
    }

    [Theory]
    [InlineData("etag", "any-id", "etag")]
    [InlineData("pending_command_count", "any-id", "pending_command_count")]
    [InlineData("{actorId}:metadata", "t:d:a", "t:d:a:metadata")]
    [InlineData("{actorId}:snapshot", "t:d:a", "t:d:a:snapshot")]
    public void ResolveStateKey_ResolvesCorrectly(string stateKey, string actorId, string expected)
    {
        KnownActorTypes.ResolveStateKey(stateKey, actorId).ShouldBe(expected);
    }

    [Theory]
    [InlineData("idempotency:{causationId}", "any-id")]
    [InlineData("drain:{correlationId}", "any-id")]
    [InlineData("{actorId}:events:{N}", "t:d:a")]
    public void ResolveStateKey_DynamicFamilies_ReturnsNull(string stateKey, string actorId)
    {
        KnownActorTypes.ResolveStateKey(stateKey, actorId).ShouldBeNull();
    }

    [Fact]
    public void IsDynamicKeyFamily_NullInput_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => KnownActorTypes.IsDynamicKeyFamily(null!));
    }

    [Fact]
    public void ResolveStateKey_NullStateKey_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => KnownActorTypes.ResolveStateKey(null!, "id"));
    }

    [Fact]
    public void ResolveStateKey_NullActorId_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => KnownActorTypes.ResolveStateKey("key", null!));
    }
}
