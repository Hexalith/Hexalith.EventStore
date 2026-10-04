using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventOptionsManifestCodecTests
{
    private static readonly EventOptionRule[] FixtureRules = [
        new("converters", JsonValueKind.Array, false, "[]"),
        new("implementationId", JsonValueKind.String, true),
        new("reader", JsonValueKind.String, true),
        new("serializer", JsonValueKind.String, true),
        new("signer", JsonValueKind.String, true),
        new("validator", JsonValueKind.String, true),
        new("version", JsonValueKind.Number, true, IntegerOnly: true),
    ];

    [Fact]
    public void V17Options_MatchApprovedIndependentAnswerAndOptionalDefaultExpansion()
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Events", "Fixtures", "EventRegistryV17.json")))!;
        const string full = "{\"converters\":[],\"implementationId\":\"fixture\",\"reader\":\"v1\",\"serializer\":\"json-web\",\"signer\":\"ECDSA-P256-SHA256-P1363\",\"validator\":\"v1\",\"version\":1}\n";
        const string omitted = "{\"implementationId\":\"fixture\",\"reader\":\"v1\",\"serializer\":\"json-web\",\"signer\":\"ECDSA-P256-SHA256-P1363\",\"validator\":\"v1\",\"version\":1}\n";
        Convert.ToHexStringLower(EventOptionsManifestCodec.ComputeHash(Encoding.UTF8.GetBytes(full), FixtureRules))
            .ShouldBe(fixture["OptionsHash"]);
        EventOptionsManifestCodec.ComputeHash(Encoding.UTF8.GetBytes(omitted), FixtureRules)
            .ShouldBe(EventOptionsManifestCodec.ComputeHash(Encoding.UTF8.GetBytes(full), FixtureRules));
    }

    [Theory]
    [InlineData("{\"n\":1}")]
    [InlineData("{\"n\":1}\n\n")]
    [InlineData("{ \"n\":1}\n")]
    [InlineData("{\"n\":1.0}\n")]
    [InlineData("{\"N\":1,\"n\":1}\n")]
    [InlineData("{\"n\":1,\"unknown\":1}\n")]
    [InlineData("{\"n\":\"1\"}\n")]
    [InlineData("{\"n\":1.5}\n")]
    [InlineData("{}\n")]
    public void RejectsNonCanonicalUnknownDuplicateMissingOrWrongTypedOptions(string input)
    {
        EventOptionRule[] rules = [new("n", JsonValueKind.Number, true, IntegerOnly: true)];
        Should.Throw<ArgumentException>(() => EventOptionsManifestCodec.ComputeHash(Encoding.UTF8.GetBytes(input), rules));
    }

    [Fact]
    public void RejectsInvalidOptionalDefaultEvenWhenCallerSuppliesThatOption()
    {
        EventOptionRule[] rules = [new("n", JsonValueKind.Number, false, "1.0")];
        Should.Throw<ArgumentException>(() => EventOptionsManifestCodec.ComputeHash("{\"n\":1}\n"u8.ToArray(), rules));
    }

    [Theory]
    [InlineData("-0.00e+9", "0")]
    [InlineData("123.4500e-2", "1.2345")]
    [InlineData("1e3", "1000")]
    [InlineData("0.00100", "0.001")]
    [InlineData("-10.0", "-10")]
    public void NormalizesExactDecimalsWithoutFloatingPointRounding(string input, string expected)
    {
        EventCanonicalNumber.Normalize(input, 100).ShouldBe(expected);
    }

    [Fact]
    public void RefusesUnboundedDecimalExpansionBeforeAllocation()
    {
        Should.Throw<ArgumentException>(() => EventCanonicalNumber.Normalize("1e1000000", 100));
        Should.Throw<ArgumentException>(() => EventCanonicalNumber.Normalize("0", 0));
    }

    [Fact]
    public void TinyExponentTokenIsRefusedBeforeExpandedNumberAllocation()
    {
        EventOptionRule[] rules = [new("n", JsonValueKind.Number, true)];
        byte[] input = "{\"n\":1e64000000}\n"u8.ToArray();
        _ = EventCanonicalNumber.Measure("1e10", 100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Should.Throw<ArgumentException>(() => EventOptionsManifestCodec.ComputeHash(input, rules));
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(1024 * 1024);
    }

    [Fact]
    public void CanonicalTextPreservesUnicodeAndUsesExactControlEscapes()
    {
        using JsonDocument value = JsonDocument.Parse("{\"z\":\"\\u0001\\n\",\"a\":\"é😀\"}");
        Encoding.UTF8.GetString(EventCanonicalJsonValueCodec.EncodeText(value.RootElement))
            .ShouldBe("{\"a\":\"é😀\",\"z\":\"\\u0001\\n\"}\n");
    }
}
