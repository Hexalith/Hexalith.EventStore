using Hexalith.EventStore.Client.Gateway;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public sealed class CommandStatusLocationBuilderTests {
    [Fact]
    public void TryBuild_WithConfiguredBase_ReturnsAbsoluteLocationAndTrue() {
        CommandStatusLocationBuilder builder = CreateBuilder(new Uri("https://gateway.example"));

        bool built = builder.TryBuild("01KTESTCOMMANDSTATUS000000", out string? location);

        built.ShouldBeTrue();
        location.ShouldBe("https://gateway.example/api/v1/commands/status/01KTESTCOMMANDSTATUS000000");
        _ = location.ShouldNotBeNull();
        location.ShouldNotStartWith("/");
    }

    [Fact]
    public void TryBuild_WithTrailingSlashBase_ComposesExactlyOneSlash() {
        CommandStatusLocationBuilder builder = CreateBuilder(new Uri("https://gateway.example/"));

        bool built = builder.TryBuild("01KTESTCOMMANDSTATUS000000", out string? location);

        built.ShouldBeTrue();
        location.ShouldBe("https://gateway.example/api/v1/commands/status/01KTESTCOMMANDSTATUS000000");
    }

    [Fact]
    public void TryBuild_WithoutConfiguredBase_FailsClosed() {
        CommandStatusLocationBuilder builder = CreateBuilder(gatewayStatusBase: null);

        bool built = builder.TryBuild("01KTESTCOMMANDSTATUS000000", out string? location);

        built.ShouldBeFalse();
        location.ShouldBeNull();
    }

    [Fact]
    public void TryBuild_EscapesStatusKey() {
        CommandStatusLocationBuilder builder = CreateBuilder(new Uri("https://gateway.example"));

        bool built = builder.TryBuild("a b/c?d", out string? location);

        built.ShouldBeTrue();
        location.ShouldBe("https://gateway.example/api/v1/commands/status/" + Uri.EscapeDataString("a b/c?d"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryBuild_WithBlankStatusKey_Throws(string? statusKey) {
        CommandStatusLocationBuilder builder = CreateBuilder(new Uri("https://gateway.example"));

        _ = Should.Throw<ArgumentException>(() => builder.TryBuild(statusKey!, out _));
    }

    private static CommandStatusLocationBuilder CreateBuilder(Uri? gatewayStatusBase)
        => new(Options.Create(new CommandStatusLocationOptions { GatewayStatusBase = gatewayStatusBase }));
}
