using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.DataProtection;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Queries;

public class QueryCursorCodecTests {
    private const string Purpose = "Hexalith.EventStore.Tests.QueryCursor.v1";

    [Fact]
    public void Encode_does_not_expose_raw_query_scope_or_position() {
        IQueryCursorCodec codec = CreateCodec();
        string scope = QueryCursorScope.Create().Add("user", "user-1").Build();

        string cursor = codec.Encode("list-things", scope, "thing-001");

        cursor.ShouldNotContain("thing-001");
        cursor.ShouldNotContain("list-things");
        cursor.ShouldNotContain("user-1");
    }

    [Fact]
    public void TryDecode_returns_position_for_matching_query_and_scope() {
        IQueryCursorCodec codec = CreateCodec();
        string scope = QueryCursorScope.Create().Add("tenant", "tenant-1").Build();
        string cursor = codec.Encode("get-tenant-users", scope, "user-7");

        bool decoded = codec.TryDecode(cursor, "get-tenant-users", scope, out string? position, out string? failureReason);

        decoded.ShouldBeTrue();
        position.ShouldBe("user-7");
        failureReason.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_returns_true_with_null_failure_reason_for_empty_cursor() {
        IQueryCursorCodec codec = CreateCodec();

        bool decoded = codec.TryDecode(null, "list-things", "user:user-1", out string? position, out string? failureReason);

        decoded.ShouldBeTrue();
        position.ShouldBeNull();
        failureReason.ShouldBeNull();
    }

    [Fact]
    public void TryDecode_rejects_wrong_query_type() {
        IQueryCursorCodec codec = CreateCodec();
        string cursor = codec.Encode("list-things", "user:user-1", "thing-001");

        bool decoded = codec.TryDecode(cursor, "get-thing-users", "tenant:tenant-1", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        failureReason.ShouldBe("wrong-query-type");
    }

    [Fact]
    public void TryDecode_rejects_wrong_scope() {
        IQueryCursorCodec codec = CreateCodec();
        string cursor = codec.Encode("list-things", "user:user-1", "thing-001");

        bool decoded = codec.TryDecode(cursor, "list-things", "user:user-2", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        failureReason.ShouldBe("wrong-scope");
    }

    [Fact]
    public void TryDecode_rejects_malformed_cursor() {
        IQueryCursorCodec codec = CreateCodec();

        bool decoded = codec.TryDecode("not-a-protected-cursor", "list-things", "user:user-1", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        failureReason.ShouldBe("tamper-or-key-rotation");
    }

    [Fact]
    public void TryDecode_rejects_cursor_above_length_cap() {
        IQueryCursorCodec codec = CreateCodec();
        string oversized = new('A', QueryPolicyLimits.MaxCursorLength + 1);

        bool decoded = codec.TryDecode(oversized, "list-things", "user:user-1", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        failureReason.ShouldBe("too-large");
    }

    [Fact]
    public void TryDecode_rejects_tampered_cursor() {
        IQueryCursorCodec codec = CreateCodec();
        string cursor = codec.Encode("list-things", "user:user-1", "thing-001");

        // Mutate a byte mid-payload so the change lands in ciphertext rather than base64 padding —
        // a last-character flip can become a no-op for some encodings.
        char[] tamperedChars = cursor.ToCharArray();
        int midIndex = tamperedChars.Length / 2;
        tamperedChars[midIndex] = tamperedChars[midIndex] == 'A' ? 'B' : 'A';
        string tampered = new(tamperedChars);

        bool decoded = codec.TryDecode(tampered, "list-things", "user:user-1", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        // Either the protector rejects the MAC (tamper-or-key-rotation) or the decoded JSON is unparseable (malformed).
        failureReason.ShouldBeOneOf("tamper-or-key-rotation", "malformed");
    }

    [Fact]
    public void TryDecode_rejects_cursor_after_data_protection_key_rotation_equivalent() {
        IQueryCursorCodec originalCodec = CreateCodec();
        IQueryCursorCodec rotatedKeyCodec = CreateCodec();
        string cursor = originalCodec.Encode("list-things", "user:user-1", "thing-001");

        bool decoded = rotatedKeyCodec.TryDecode(cursor, "list-things", "user:user-1", out string? position, out string? failureReason);

        decoded.ShouldBeFalse();
        position.ShouldBeNull();
        failureReason.ShouldBe("tamper-or-key-rotation");
    }

    [Fact]
    public void Cursors_are_isolated_across_purposes() {
        IQueryCursorCodec domainA = new QueryCursorCodec(new EphemeralDataProtectionProvider(), "domain-a.cursor.v1");
        // Same ephemeral key material, different purpose: the cursor must not cross domains.
        IDataProtectionProvider sharedProvider = new EphemeralDataProtectionProvider();
        IQueryCursorCodec sameKeyDomainA = new QueryCursorCodec(sharedProvider, "domain-a.cursor.v1");
        IQueryCursorCodec sameKeyDomainB = new QueryCursorCodec(sharedProvider, "domain-b.cursor.v1");

        string cursor = sameKeyDomainA.Encode("list-things", "user:user-1", "thing-001");

        sameKeyDomainB.TryDecode(cursor, "list-things", "user:user-1", out _, out string? failureReason).ShouldBeFalse();
        failureReason.ShouldBe("tamper-or-key-rotation");
        // domainA uses a different ephemeral key entirely, so it also cannot read it.
        domainA.TryDecode(cursor, "list-things", "user:user-1", out _, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_purpose(string? purpose)
        => Should.Throw<ArgumentException>(() => new QueryCursorCodec(new EphemeralDataProtectionProvider(), purpose!));

    private static IQueryCursorCodec CreateCodec()
        => new QueryCursorCodec(new EphemeralDataProtectionProvider(), Purpose);
}
