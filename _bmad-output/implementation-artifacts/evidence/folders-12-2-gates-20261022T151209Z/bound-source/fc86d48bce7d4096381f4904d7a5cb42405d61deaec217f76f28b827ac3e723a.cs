using System.Text;

using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class IncrementalDomainBase64AdmissionTests
{
    [Theory]
    [InlineData("\"QQ==\"")]
    [InlineData("\"\\u0051Q==\"")]
    public void JsonEscapeSplitAcrossEveryByteProducesCanonicalDecodedPayload(string json)
    {
        var budget = new DomainResponseBufferBudget();
        byte[] encoded = Encoding.UTF8.GetBytes(json);
        byte[] payload;
        using (var admission = new IncrementalDomainBase64Admission(budget, 16))
        {
            for (int length = 1; length <= encoded.Length; length++) { admission.Observe(encoded.AsSpan(0, length)); }
            payload = admission.Complete();
            payload.ShouldBe([(byte)'A']);
            Should.Throw<FormatException>(() => admission.Complete());
            budget.LiveBytes.ShouldBe(17);
        }
        budget.LiveBytes.ShouldBe(1);
        Array.Clear(payload);
        budget.Release(payload.Length);
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("\"/x==\"")]
    [InlineData("\"AA=A\"")]
    [InlineData("\"A A=\"")]
    [InlineData("\"AA\"")]
    [InlineData("\"QQ==QQ==\"")]
    [InlineData("\"\\u00e9\"")]
    public void RefusesNonCanonicalAlphabetPaddingOrTrailingBits(string json)
    {
        var budget = new DomainResponseBufferBudget();
        using var admission = new IncrementalDomainBase64Admission(budget, 16);
        Should.Throw<FormatException>(() => admission.Observe(Encoding.UTF8.GetBytes(json)));
        Should.Throw<FormatException>(() => admission.Complete());
    }

    [Fact]
    public void DecodedLimitIsEnforcedBeforeTheScalarCloses()
    {
        var budget = new DomainResponseBufferBudget();
        using var admission = new IncrementalDomainBase64Admission(budget, 2);
        (Should.Throw<InvalidOperationException>(() => admission.Observe("\"AAAA"u8))).Message.ShouldContain("PayloadLimit");
        Should.Throw<FormatException>(() => admission.Complete());
        budget.LiveBytes.ShouldBe(2);
    }

    [Fact]
    public async Task LexerInvokesPayloadAdmissionDuringWindowGrowthBeforeCompleteToken()
    {
        var budget = new DomainResponseBufferBudget();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("[\"" + new string('A', 100_000) + "\"]"));
        using var reader = new IncrementalDomainJsonTokenReader(source, budget, CancellationToken.None);
        (await reader.ReadAsync()).ShouldBeTrue();
        using var admission = new IncrementalDomainBase64Admission(budget, 2);
        (await Should.ThrowAsync<InvalidOperationException>(async () =>
            (await reader.ReadAsync(8 * 1024 * 1024, admission)).ShouldBeTrue())).Message.ShouldContain("PayloadLimit");
        // Refusal occurs before doubling the 64 KiB lexer owner.
        budget.LiveBytes.ShouldBe(64 * 1024 + 2);
    }
}
