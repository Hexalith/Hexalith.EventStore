using Hexalith.EventStore.Testing.Integration;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

[Collection("Descriptor handoff")]
public sealed class DescriptorFixtureTests
{
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
        fixture.Environment.SyntheticTenantId.ShouldBe("sample-synthetic-tenant");
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
        fixture.InvocationClient.DefaultRequestHeaders.Authorization.ShouldNotBeNull().Parameter.ShouldBe("private-runner-token");
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
