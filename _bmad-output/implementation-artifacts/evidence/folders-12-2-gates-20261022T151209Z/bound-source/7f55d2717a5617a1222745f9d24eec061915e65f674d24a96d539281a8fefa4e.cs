using System.Security.Claims;

using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

/// <summary>Checks that callback isolation depends on routed actor identity and configured token verification.</summary>
public sealed class ActorCallbackRateLimitPartitionTests
{
    private const string ChannelToken = "partition-fixture-channel-token";

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("duplicate")]
    [InlineData("unconfigured")]
    [InlineData("unmatched")]
    [InlineData("missing-type")]
    [InlineData("missing-id")]
    public void UnverifiedOrUnmatchedRequestsCannotMintActorKeys(string scenario)
    {
        using ServiceProvider provider = Services(scenario == "unconfigured" ? null : ChannelToken);
        DefaultHttpContext context = Context(provider, "Type", "actor");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType, DaprSidecarChannelAuthenticationHandler.VerifiedChannel)],
            EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme));
        if (scenario != "missing")
            context.Request.Headers.Append(DaprAppChannelToken.HeaderName, scenario == "wrong" ? "wrong-token" : ChannelToken);
        if (scenario == "duplicate") context.Request.Headers.Append(DaprAppChannelToken.HeaderName, ChannelToken);
        if (scenario == "unmatched") context.SetEndpoint(null);
        if (scenario == "missing-type") context.Request.RouteValues.Remove("actorTypeName");
        if (scenario == "missing-id") context.Request.RouteValues.Remove("actorId");

        EventStoreRateLimitPartition.TrustedActorKey(context).ShouldBeNull();
    }

    [Fact]
    public void ActorKeysSeparateTypesAndIdsWithoutDependingOnMethodOrHumanClaims()
    {
        using ServiceProvider provider = Services(ChannelToken);
        DefaultHttpContext first = Context(provider, "ab", "c");
        first.Request.Headers.Append(DaprAppChannelToken.HeaderName, ChannelToken);
        string firstKey = EventStoreRateLimitPartition.TrustedActorKey(first).ShouldNotBeNull();
        firstKey.ShouldStartWith("sidecar:actor:");
        firstKey.Length.ShouldBe("sidecar:actor:".Length + 64);

        first.Request.RouteValues["methodName"] = "MarkRecoveryAsync";
        first.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "human"), new Claim("eventstore:tenant", "tenant")], "Bearer"));
        EventStoreRateLimitPartition.TrustedActorKey(first).ShouldBe(firstKey);

        foreach ((string type, string id) in new[] { ("a", "bc"), ("ab", "other"), ("OtherType", "c") })
        {
            DefaultHttpContext other = Context(provider, type, id);
            other.Request.Headers.Append(DaprAppChannelToken.HeaderName, ChannelToken);
            EventStoreRateLimitPartition.TrustedActorKey(other).ShouldNotBe(firstKey);
        }
    }

    private static ServiceProvider Services(string? token)
        => new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DaprAppChannelToken.ConfigurationKey] = token }).Build())
            .BuildServiceProvider();

    private static DefaultHttpContext Context(IServiceProvider provider, string actorType, string actorId)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = $"/actors/{actorType}/{actorId}/method/ValidateAuthorityAsync";
        context.Request.RouteValues["actorTypeName"] = actorType;
        context.Request.RouteValues["actorId"] = actorId;
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(EventStoreActorCallbackEndpointMetadata.Instance), "mapped-actor"));
        return context;
    }
}
