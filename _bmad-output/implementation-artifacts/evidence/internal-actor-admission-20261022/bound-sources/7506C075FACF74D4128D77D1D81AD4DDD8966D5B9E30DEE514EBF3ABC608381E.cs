using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>
/// Deterministic coverage for the canonical form the Story 4.5 provider digest is taken over.
/// The C# strip and the evidence validator's re-derivation (<c>split("\nscopes:", 1)[0]</c>) must
/// agree; these cases pin that agreement, including the shape where they would diverge.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class StateStoreComponentCanonicalizationTests
{
    private const string Body = "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: statestore";

    /// <summary>Verifies a terminal scopes block is removed and the remainder is untouched.</summary>
    [Fact]
    public void StripTerminalScopes_TerminalBlock_ReturnsEverythingBeforeIt()
    {
        string component = $"{Body}\nscopes:\n  - app-one\n  - app-two";

        string canonical = DaprTestContainerFixture.StripTerminalScopes(component);

        canonical.ShouldBe(Body);
        canonical.ShouldBe(component.Split("\nscopes:", 2)[0], "the validator re-derives the canonical form this way");
        canonical.ShouldNotContain("scopes:");
    }

    /// <summary>Verifies a document with no scopes block is returned unchanged apart from trailing newlines.</summary>
    [Fact]
    public void StripTerminalScopes_NoScopesBlock_ReturnsDocument()
        => DaprTestContainerFixture.StripTerminalScopes($"{Body}\n").ShouldBe(Body);

    /// <summary>Verifies CRLF input is normalized before stripping.</summary>
    [Fact]
    public void StripTerminalScopes_CrLfInput_IsNormalized()
        => DaprTestContainerFixture
            .StripTerminalScopes($"{Body}\r\nscopes:\r\n  - app-one".Replace("\n", "\n", StringComparison.Ordinal))
            .ShouldBe(Body);

    /// <summary>
    /// Verifies a non-terminal scopes block throws instead of silently diverging from the
    /// validator, which assumes everything after <c>scopes:</c> belongs to that block.
    /// </summary>
    [Fact]
    public void StripTerminalScopes_KeyAfterScopes_Throws()
    {
        string component = $"{Body}\nscopes:\n  - app-one\nauth:\n  secretStore: local";

        InvalidOperationException error = Should.Throw<InvalidOperationException>(
            () => DaprTestContainerFixture.StripTerminalScopes(component));

        error.Message.ShouldContain("terminal scopes block");
    }
}
