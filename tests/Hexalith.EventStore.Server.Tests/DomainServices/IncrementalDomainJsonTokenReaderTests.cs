using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class IncrementalDomainJsonTokenReaderTests
{
    [Fact]
    public async Task SpanningTokenIsChargedBeforeGrowthAndPrivateOwnersClearOnDispose()
    {
        var budget = new DomainResponseBufferBudget();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("[\"" + new string('a', 100_000) + "\"]"));
        using (var reader = new IncrementalDomainJsonTokenReader(source, budget, CancellationToken.None))
        {
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.TokenType.ShouldBe(JsonTokenType.StartArray);
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.TokenType.ShouldBe(JsonTokenType.String);
            reader.RawValue.Length.ShouldBe(100_000);
            budget.LiveBytes.ShouldBe(128 * 1024);
            (string value, long charge) = reader.DecodeString();
            value.Length.ShouldBe(100_000);
            budget.LiveBytes.ShouldBe(128 * 1024 + charge);
            budget.Release(charge);
            (await reader.ReadAsync()).ShouldBeTrue();
            reader.TokenType.ShouldBe(JsonTokenType.EndArray);
            (await reader.ReadAsync()).ShouldBeFalse();
        }
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("[\"\\ud800\"]")]
    [InlineData("[\"\\udc00\"]")]
    [InlineData("[\"\\ud800\\u0061\"]")]
    public async Task InvalidEscapedUnicodeFailsBeforeAnyStringMaterialization(string input)
    {
        var budget = new DomainResponseBufferBudget();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using var reader = new IncrementalDomainJsonTokenReader(source, budget, CancellationToken.None);
        (await reader.ReadAsync()).ShouldBeTrue();
        await Should.ThrowAsync<JsonException>(async () => (await reader.ReadAsync()).ShouldBeTrue());
        budget.LiveBytes.ShouldBe(64 * 1024);
    }

    [Fact]
    public async Task ValidEscapedSupplementaryScalarIsAdmittedAndDecodedExactly()
    {
        var budget = new DomainResponseBufferBudget();
        using var source = new MemoryStream("[\"\\ud83d\\ude00\"]"u8.ToArray());
        using var reader = new IncrementalDomainJsonTokenReader(source, budget, CancellationToken.None);
        (await reader.ReadAsync()).ShouldBeTrue();
        (await reader.ReadAsync()).ShouldBeTrue();
        (string value, long charge) = reader.DecodeString();
        value.ShouldBe("😀");
        budget.Release(charge);
    }

    [Fact]
    public async Task StringDecodeRefusesRemainingWorkspaceBeforeArrayOrStringCreation()
    {
        var budget = new DomainResponseBufferBudget();
        long external = 128L * 1024 * 1024 - 64 * 1024 - 100;
        budget.Reserve(external);
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("[\"" + new string('a', 500) + "\"]"));
        using (var reader = new IncrementalDomainJsonTokenReader(source, budget, CancellationToken.None))
        {
            (await reader.ReadAsync()).ShouldBeTrue();
            (await reader.ReadAsync()).ShouldBeTrue();
            (Should.Throw<InvalidOperationException>(() => reader.DecodeString())).Message.ShouldContain("ScratchLimit");
            budget.LiveBytes.ShouldBe(external + 64 * 1024);
        }
        budget.Release(external);
        budget.LiveBytes.ShouldBe(0);
    }
}
