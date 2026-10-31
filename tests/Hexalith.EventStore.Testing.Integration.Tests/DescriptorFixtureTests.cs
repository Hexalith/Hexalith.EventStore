using Hexalith.EventStore.Testing.Integration;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

[Collection("Descriptor handoff")]
public sealed class DescriptorFixtureTests
{
    [Theory]
    [InlineData("missing-resource")]
    [InlineData("wrong-module")]
    [InlineData("missing-endpoint")]
    [InlineData("valid")]
    public async Task NonHttpPersistenceBindingIsRequiredBeforeClientExposure(string binding)
    {
        using var handoff = new DescriptorHandoffScope();
        string path = System.Environment.GetEnvironmentVariable("HEXALITH_TEST_ENVIRONMENT_PATH")!;
        System.Text.Json.Nodes.JsonNode root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        System.Text.Json.Nodes.JsonNode environment = root["bindings"]!["environment"]!;
        if (binding != "missing-resource")
        {
            environment["resources"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject
            {
                ["resourceId"] = "redis", ["moduleId"] = binding == "wrong-module" ? "foreign" : "parties",
                ["endpoints"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = binding == "missing-endpoint" ? "other" : "tcp", ["protocol"] = "tcp", ["uri"] = "tcp://127.0.0.1:16379",
                }),
            });
            System.Text.Json.Nodes.JsonNode ready = environment["readiness"]!["resources"]![0]!.DeepClone();
            ready["resourceId"] = "redis";
            environment["readiness"]!["resources"]!.AsArray().Add(ready);
        }
        File.WriteAllText(path, root.ToJsonString());
        await using var fixture = new DescriptorPersistenceFixture();
        if (binding == "valid")
        {
            await fixture.InitializeAsync();
            fixture.Gateway.BaseAddress.ShouldBe(new Uri("http://127.0.0.1:18080"));
            fixture.Descriptor.GetEndpoint("redis", "parties", "tcp").Scheme.ShouldBe("tcp");
            Should.Throw<InvalidOperationException>(() => fixture.PersistenceClient);
        }
        else
        {
            TestEnvironmentDescriptorException failure = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await fixture.InitializeAsync());
            failure.Rule.ShouldBe("HXT109");
            Should.Throw<InvalidOperationException>(() => fixture.Gateway);
            Should.Throw<InvalidOperationException>(() => fixture.Descriptor);
        }
    }

    [Fact]
    public async Task MissingHandoff_FailsBothFixturesWithoutStartupOrSkip()
    {
        using var handoff = new DescriptorHandoffScope(missing: true);
        await using var fixture = new DescriptorTopologyFixture();
        await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await fixture.InitializeAsync());
        await using var domain = new DescriptorDomainFixture();
        await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await domain.InitializeAsync());
        new DaprFactAttribute().Skip.ShouldBeNull();
    }

    [Fact]
    public async Task ReadyHandoff_BindsBothFixturesWithoutLocalPrerequisites()
    {
        using var handoff = new DescriptorHandoffScope();
        await using var fixture = new DescriptorTopologyFixture();
        await fixture.InitializeAsync();
        fixture.Endpoint.ShouldBe(new Uri("http://127.0.0.1:18080"));
        fixture.Descriptor.SyntheticTenantId.ShouldBe("sample-synthetic-tenant");
        await fixture.DisposeAsync();
        Should.Throw<ObjectDisposedException>(() => fixture.Endpoint);
        await using var domain = new DescriptorDomainFixture();
        await domain.InitializeAsync();
        domain.AppEndpoint.ShouldBe("http://127.0.0.1:18080");
        domain.DaprHttpEndpoint.ShouldBe("http://127.0.0.1:18081");
    }

    [Fact]
    public async Task OneResourceWithTwoEndpoints_BindsExactClientsAndRejectsAmbiguousLookup()
    {
        using var handoff = new DescriptorHandoffScope();
        await using var fixture = new DescriptorDomainFixture();
        await fixture.InitializeAsync();
        fixture.DomainClient.BaseAddress.ShouldBe(new Uri("http://127.0.0.1:18080"));
        fixture.SidecarClient.BaseAddress.ShouldBe(new Uri("http://127.0.0.1:18081"));
        fixture.DomainClient.ShouldNotBeSameAs(fixture.SidecarClient);
        fixture.DomainClient.DefaultRequestHeaders.Authorization.ShouldNotBeNull();
        fixture.SidecarClient.DefaultRequestHeaders.Authorization.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => fixture.AmbiguousClient);
        Should.Throw<InvalidOperationException>(() => fixture.ExactClient("eventstore", "other-module", "http"));
        Should.Throw<InvalidOperationException>(() => fixture.ExactClient("eventstore", "Hexalith.EventStore", "HTTP"));
        await fixture.DisposeAsync();
        Should.Throw<ObjectDisposedException>(() => fixture.DomainClient);
    }

    [Fact]
    public async Task ExplicitInvocationRequirement_BindsExactPrivateChannelWithoutPublishingToken()
    {
        using var handoff = new DescriptorHandoffScope(invocation: true);
        await using var fixture = new DescriptorInvocationFixture();
        await fixture.InitializeAsync();
        fixture.InvocationClient.BaseAddress.ShouldBe(new Uri("http://127.0.0.1:18081/v1.0/invoke/declared-owner/method/api/v1/"));
        fixture.InvocationClient.DefaultRequestHeaders.Authorization.ShouldNotBeNull().Parameter.ShouldBe("__HEXALITH_TEST_TOKEN__");
        fixture.InvocationClient.DefaultRequestHeaders.Contains("dapr-api-token").ShouldBeFalse();
        await fixture.DisposeAsync();
        Should.Throw<ObjectDisposedException>(() => fixture.InvocationClient);
    }

    [Fact]
    public async Task ExplicitInvocationRequirement_RefusesMissingPrivateChannelBeforeClientExposure()
    {
        using var handoff = new DescriptorHandoffScope(invocation: true, credentials: false);
        await using var fixture = new DescriptorInvocationFixture();
        TestEnvironmentDescriptorException exception = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await fixture.InitializeAsync());
        exception.Rule.ShouldBe("HXT110");
        Should.Throw<InvalidOperationException>(() => fixture.InvocationClient);
    }

    [Theory]
    [InlineData("HEXALITH_G4_RUN_ID")]
    [InlineData("HEXALITH_G4_TENANT")]
    [InlineData("HEXALITH_G4_RESOURCE_NAMESPACE")]
    [InlineData("HEXALITH_G4_ACCESS_TOKEN")]
    public async Task MissingPrivateIdentity_RefusesInsteadOfMintingCredentials(string key)
    {
        using var handoff = new DescriptorHandoffScope();
        System.Environment.SetEnvironmentVariable(key, null);
        await using var fixture = new DescriptorTopologyFixture();
        TestEnvironmentDescriptorException exception = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await fixture.InitializeAsync());
        exception.Rule.ShouldBe("HXT110");
    }

    [Fact]
    public async Task ConflictingPrivateIdentity_RefusesWithoutExposingCredential()
    {
        using var handoff = new DescriptorHandoffScope();
        System.Environment.SetEnvironmentVariable("HEXALITH_G4_TENANT", "conflicting-secret");
        await using var fixture = new DescriptorTopologyFixture();
        TestEnvironmentDescriptorException exception = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () => await fixture.InitializeAsync());
        exception.Rule.ShouldBe("HXT110");
        exception.Message.ShouldNotContain("conflicting-secret");
    }
}
