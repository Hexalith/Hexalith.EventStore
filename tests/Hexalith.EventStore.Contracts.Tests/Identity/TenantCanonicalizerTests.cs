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
    [InlineData("systEM")]
    [InlineData("é")]
    public void InvalidOrReservedTenantIsRejected(string? input)
        => TenantCanonicalizer.TryCanonicalize(input, out _).ShouldBeFalse();

    [Fact]
    public void TenantLongerThan64CharactersIsRejected()
        => TenantCanonicalizer.TryCanonicalize(new string('a', 65), out _).ShouldBeFalse();

    [Fact]
    public void DistinctTenantGrantsRemainDistinctAfterCanonicalization()
    {
        TenantCanonicalizer.TryCanonicalizeGrants(["ACME", "Other"], out IReadOnlySet<string> grants).ShouldBeTrue();
        grants.ShouldContain("acme");
        grants.ShouldContain("other");
    }

    [Theory]
    [InlineData("Acme", "acme")]
    [InlineData("ACME", "bad grant")]
    [InlineData("acme", "system")]
    public void DuplicateOrMalformedGrantsAreRejected(string first, string second)
        => TenantCanonicalizer.TryCanonicalizeGrants([first, second], out _).ShouldBeFalse();

    [Theory]
    [InlineData("platform:tenants", true)]
    [InlineData("platform:global-administrators", true)]
    [InlineData("platform:Tenants", false)]
    [InlineData("platform:bad--scope", false)]
    [InlineData("system", false)]
    public void PlatformScopeHasDisjointGrammar(string value, bool valid)
        => TenantCanonicalizer.IsPlatformScope(value).ShouldBe(valid);
}
