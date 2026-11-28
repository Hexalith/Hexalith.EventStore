using Hexalith.EventStore.Contracts.Identity;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Identity;

public class TenantCanonicalizerTests
{
    [Theory]
    [InlineData("Acme", "acme")]
    [InlineData("A", "a")]
    [InlineData("ACME-123", "acme-123")]
    public void ExplicitTenantIsCanonicalized(string input, string expected)
    {
        TenantCanonicalizer.TryCanonicalize(input, out string canonical).ShouldBeTrue();
        canonical.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" Acme")]
    [InlineData("acme ")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("acme:other")]
    [InlineData("system")]
    [InlineData("systEM")]
    [InlineData("\u212Aelvin")]
    [InlineData("acme-\u212A1")]
    [InlineData("caf\u00E9")]
    [InlineData("acme\uFF11")]
    [InlineData("acme\U0001F600")]
    public void InvalidOrReservedTenantIsRejected(string? input)
    {
        TenantCanonicalizer.TryCanonicalize(input, out string canonical).ShouldBeFalse();
        canonical.ShouldBeEmpty();
    }

    [Fact]
    public void TenantWith64CharactersIsCanonicalized()
    {
        TenantCanonicalizer.TryCanonicalize(new string('A', 64), out string canonical).ShouldBeTrue();
        canonical.ShouldBe(new string('a', 64));
    }

    [Fact]
    public void TenantLongerThan64CharactersIsRejected()
        => TenantCanonicalizer.TryCanonicalize(new string('a', 65), out _).ShouldBeFalse();

    [Fact]
    public void DistinctTenantGrantsRemainDistinctAfterCanonicalization()
    {
        TenantCanonicalizer.TryCanonicalizeGrants(["ACME", "Other", "Kelvin"], out IReadOnlySet<string> grants).ShouldBeTrue();
        grants.ShouldContain("acme");
        grants.ShouldContain("other");
        grants.ShouldContain("kelvin");
    }

    [Theory]
    [InlineData("\u212Aelvin")]
    [InlineData("caf\u00E9")]
    [InlineData("acme\uFF11")]
    [InlineData("acme\U0001F600")]
    public void NonAsciiTenantGrantIsRejected(string input)
    {
        TenantCanonicalizer.TryCanonicalizeGrants([input], out IReadOnlySet<string> grants).ShouldBeFalse();
        grants.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Acme", "acme")]
    [InlineData("ACME", "bad grant")]
    [InlineData("acme", "system")]
    [InlineData("Kelvin", "kelvin")]
    [InlineData("acme", "\u212Aelvin")]
    [InlineData("kelvin", "\u212Aelvin")]
    [InlineData("\u212Aelvin", "kelvin")]
    public void DuplicateOrMalformedGrantsAreRejected(string first, string second)
    {
        TenantCanonicalizer.TryCanonicalizeGrants([first, second], out IReadOnlySet<string> grants).ShouldBeFalse();
        grants.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("platform:tenants", true)]
    [InlineData("platform:global-administrators", true)]
    [InlineData("platform:Tenants", false)]
    [InlineData("platform:bad--scope", false)]
    [InlineData("system", false)]
    public void PlatformScopeHasDisjointGrammar(string value, bool valid)
        => TenantCanonicalizer.IsPlatformScope(value).ShouldBe(valid);
}
