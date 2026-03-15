using System.Text.Json;

using Hexalith.EventStore.Contracts.Messages;

namespace Hexalith.EventStore.Contracts.Tests.Messages;

public class MessageTypeTests {
    // ── Parse: valid inputs (AC #1) ───────────────────────────────────

    [Fact]
    public void Parse_SingleSegmentName_ReturnsParsedMessageType() {
        var mt = MessageType.Parse("counter-incremented-v1");

        Assert.Equal("counter", mt.Domain);
        Assert.Equal("incremented", mt.Name);
        Assert.Equal(1, mt.Version);
    }

    [Fact]
    public void Parse_MultiSegmentName_ReturnsParsedMessageType() {
        var mt = MessageType.Parse("tenants-create-tenant-v1");

        Assert.Equal("tenants", mt.Domain);
        Assert.Equal("create-tenant", mt.Name);
        Assert.Equal(1, mt.Version);
    }

    [Fact]
    public void Parse_HigherVersion_ReturnsParsedMessageType() {
        var mt = MessageType.Parse("order-order-item-added-v2");

        Assert.Equal("order", mt.Domain);
        Assert.Equal("order-item-added", mt.Name);
        Assert.Equal(2, mt.Version);
    }

    [Fact]
    public void Parse_RepeatedDomainInName_CorrectlyParses() {
        var mt = MessageType.Parse("counter-counter-incremented-v1");

        Assert.Equal("counter", mt.Domain);
        Assert.Equal("counter-incremented", mt.Name);
        Assert.Equal(1, mt.Version);
    }

    // ── Parse: invalid inputs (AC #1, #8) ─────────────────────────────

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => MessageType.Parse(null!));

    [Fact]
    public void Parse_EmptyString_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse(string.Empty));

    [Fact]
    public void Parse_Whitespace_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse("   "));

    [Fact]
    public void Parse_NoVersionSuffix_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse("tenants-create-tenant"));

    [Fact]
    public void Parse_MissingDomain_ThrowsFormatException() =>
        // Starts with hyphen — no domain segment
        Assert.Throws<FormatException>(() => MessageType.Parse("-create-tenant-v1"));

    [Fact]
    public void Parse_NoHyphens_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse("invalid"));

    [Fact]
    public void Parse_VersionZero_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse("tenants-create-tenant-v0"));

    [Fact]
    public void Parse_NonNumericVersion_ThrowsFormatException() => Assert.Throws<FormatException>(() => MessageType.Parse("tenants-create-tenant-vabc"));

    // ── TryParse (AC #8, subtask 3.4) ─────────────────────────────────

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndResult() {
        bool success = MessageType.TryParse("tenants-create-tenant-v1", out MessageType? result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("tenants", result.Domain);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalseNoException() {
        bool success = MessageType.TryParse("invalid", out MessageType? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_Null_ReturnsFalseNoException() {
        bool success = MessageType.TryParse(null, out MessageType? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalseNoException() {
        bool success = MessageType.TryParse(string.Empty, out MessageType? result);

        Assert.False(success);
        Assert.Null(result);
    }

    // ── Assemble: PascalCase conversion (AC #2) ───────────────────────

    [Fact]
    public void Assemble_TwoWordType_ProducesKebabCase() {
        var mt = MessageType.Assemble("tenants", typeof(TenantCreated), 1);

        Assert.Equal("tenants", mt.Domain);
        Assert.Equal("tenant-created", mt.Name);
        Assert.Equal(1, mt.Version);
        Assert.Equal("tenants-tenant-created-v1", mt.ToString());
    }

    [Fact]
    public void Assemble_ThreeWordType_ProducesKebabCase() {
        var mt = MessageType.Assemble("order", typeof(OrderItemAdded), 1);

        Assert.Equal("order-item-added", mt.Name);
        Assert.Equal("order-order-item-added-v1", mt.ToString());
    }

    [Fact]
    public void Assemble_SingleWordType_ProducesKebabCase() {
        var mt = MessageType.Assemble("counter", typeof(Incremented), 1);

        Assert.Equal("incremented", mt.Name);
        Assert.Equal("counter-incremented-v1", mt.ToString());
    }

    [Fact]
    public void Assemble_RepeatedDomainSegment_NoDeduplication() {
        // domain=counter, type=CounterIncremented -> counter-counter-incremented-v1
        var mt = MessageType.Assemble("counter", typeof(CounterIncremented), 1);

        Assert.Equal("counter-counter-incremented-v1", mt.ToString());
    }

    [Fact]
    public void Assemble_UnicodeTypeName_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => MessageType.Assemble("tenants", typeof(ÉvénementCréé), 1));

    // ── Assemble: negative cases (AC #8, subtask 3.7) ─────────────────

    [Fact]
    public void Assemble_NullDomain_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => MessageType.Assemble(null!, typeof(TenantCreated), 1));

    [Fact]
    public void Assemble_NullType_ThrowsArgumentNullException() => Assert.Throws<ArgumentNullException>(() => MessageType.Assemble("tenants", null!, 1));

    [Fact]
    public void Assemble_EmptyDomain_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => MessageType.Assemble(string.Empty, typeof(TenantCreated), 1));

    [Fact]
    public void Assemble_VersionZero_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => MessageType.Assemble("tenants", typeof(TenantCreated), 0));

    [Fact]
    public void Assemble_VersionNegative_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => MessageType.Assemble("tenants", typeof(TenantCreated), -1));

    // ── Max length enforcement (AC #9) ────────────────────────────────

    [Fact]
    public void Parse_ExceedingMaxLength_ThrowsFormatException() {
        // Build a string > 192 chars: domain(10) + hyphen + name(180) + "-v1" = 194
        string longName = new('a', 180);
        string oversized = $"domaintest-{longName}-v1";
        Assert.True(oversized.Length > MessageType.MaxLength);

        _ = Assert.Throws<FormatException>(() => MessageType.Parse(oversized));
    }

    [Fact]
    public void Assemble_ExceedingMaxLength_ThrowsArgumentException() =>
        // Use a type with a very long name
        Assert.Throws<ArgumentException>(() => MessageType.Assemble("tenants", typeof(VeryLongTypeNameThatExceedsTheMaximumAllowedLengthForAMessageTypeWhenCombinedWithDomainAndVersionSuffixAndShouldFailValidationBecauseItIsTooLongForTheSystem), 1));

    // ── Value equality (AC #3, subtask 3.10) ──────────────────────────

    [Fact]
    public void Equals_SameValues_AreEqual() {
        var a = MessageType.Parse("tenants-create-tenant-v1");
        var b = MessageType.Parse("tenants-create-tenant-v1");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValues_AreNotEqual() {
        var a = MessageType.Parse("tenants-create-tenant-v1");
        var b = MessageType.Parse("tenants-create-tenant-v2");

        Assert.NotEqual(a, b);
    }

    // ── ToString round-trip (AC #3, subtask 3.11) ─────────────────────

    [Theory]
    [InlineData("tenants-create-tenant-v1")]
    [InlineData("counter-counter-incremented-v1")]
    [InlineData("order-order-item-added-v2")]
    public void ToString_RoundTrip_PreservesValue(string canonical) {
        var original = MessageType.Parse(canonical);
        var roundTripped = MessageType.Parse(original.ToString());

        Assert.Equal(original, roundTripped);
        Assert.Equal(canonical, roundTripped.ToString());
    }

    // ── JSON serialization round-trip (AC #7, subtask 3.12) ───────────

    [Fact]
    public void JsonSerialize_ProducesPlainString() {
        var mt = MessageType.Parse("tenants-create-tenant-v1");

        string json = JsonSerializer.Serialize(mt);

        Assert.Equal("\"tenants-create-tenant-v1\"", json);
    }

    [Fact]
    public void JsonDeserialize_FromString_ReturnsEqualInstance() {
        string json = "\"tenants-create-tenant-v1\"";

        MessageType? mt = JsonSerializer.Deserialize<MessageType>(json);

        Assert.NotNull(mt);
        Assert.Equal("tenants", mt.Domain);
        Assert.Equal("create-tenant", mt.Name);
        Assert.Equal(1, mt.Version);
    }

    [Fact]
    public void JsonRoundTrip_PreservesValueEquality() {
        var original = MessageType.Parse("counter-counter-incremented-v1");

        string json = JsonSerializer.Serialize(original);
        MessageType? deserialized = JsonSerializer.Deserialize<MessageType>(json);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void JsonDeserialize_Null_ThrowsJsonException() => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessageType>("null"));

    // ── Test helper types ─────────────────────────────────────────────
    // Dummy types used for Assemble tests (PascalCase -> kebab conversion)
}

// Types must be outside the test class for typeof() to work cleanly with simple names
public class TenantCreated { }
public class OrderItemAdded { }
public class Incremented { }
public class CounterIncremented { }
public class ÉvénementCréé { }
public class VeryLongTypeNameThatExceedsTheMaximumAllowedLengthForAMessageTypeWhenCombinedWithDomainAndVersionSuffixAndShouldFailValidationBecauseItIsTooLongForTheSystem { }