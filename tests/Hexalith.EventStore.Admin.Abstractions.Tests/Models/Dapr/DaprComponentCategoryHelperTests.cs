using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprComponentCategoryHelperTests {
    [Theory]
    [InlineData("state.redis", DaprComponentCategory.StateStore)]
    [InlineData("state.cosmosdb", DaprComponentCategory.StateStore)]
    [InlineData("pubsub.redis", DaprComponentCategory.PubSub)]
    [InlineData("pubsub.kafka", DaprComponentCategory.PubSub)]
    [InlineData("bindings.http", DaprComponentCategory.Binding)]
    [InlineData("bindings.cron", DaprComponentCategory.Binding)]
    [InlineData("configuration.redis", DaprComponentCategory.Configuration)]
    [InlineData("lock.redis", DaprComponentCategory.Lock)]
    [InlineData("secretstores.local.file", DaprComponentCategory.SecretStore)]
    [InlineData("secretstores.azure.keyvault", DaprComponentCategory.SecretStore)]
    [InlineData("middleware.http.ratelimit", DaprComponentCategory.Middleware)]
    public void FromComponentType_WithValidPrefix_ReturnsCorrectCategory(string componentType, DaprComponentCategory expected) => DaprComponentCategoryHelper.FromComponentType(componentType).ShouldBe(expected);

    [Fact]
    public void FromComponentType_WithNull_ReturnsUnknown() => DaprComponentCategoryHelper.FromComponentType(null).ShouldBe(DaprComponentCategory.Unknown);

    [Fact]
    public void FromComponentType_WithEmpty_ReturnsUnknown() => DaprComponentCategoryHelper.FromComponentType(string.Empty).ShouldBe(DaprComponentCategory.Unknown);

    [Theory]
    [InlineData("workflow.temporal")]
    [InlineData("unknown.type")]
    [InlineData("actor.something")]
    public void FromComponentType_WithUnknownPrefix_ReturnsUnknown(string componentType) => DaprComponentCategoryHelper.FromComponentType(componentType).ShouldBe(DaprComponentCategory.Unknown);

    [Fact]
    public void FromComponentType_WithNoDotSeparator_ReturnsUnknown() => DaprComponentCategoryHelper.FromComponentType("stateonly").ShouldBe(DaprComponentCategory.Unknown);

    [Fact]
    public void FromComponentType_WithStatePrefix_ReturnsStateStore() =>
        // Edge case: just the prefix with a dot
        DaprComponentCategoryHelper.FromComponentType("state.").ShouldBe(DaprComponentCategory.StateStore);
}
