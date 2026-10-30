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
        domain.DaprHttpEndpoint.ShouldBe("http://127.0.0.1:18080");
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
