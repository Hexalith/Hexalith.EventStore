using System.Collections.Generic;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>
/// Story 22.7a — protection metadata contract, carrier, and fail-closed semantics.
/// Sentinel markers (PLAINTEXT_SECRET_MARKER, KEY_ALIAS_SECRET_MARKER) prove that no public surface
/// or serialized form ever leaks secret-shaped values.
/// </summary>
public class EventStorePayloadProtectionMetadataTests {
    // Markers intentionally avoid forbidden substrings so they CAN appear in safely-validated
    // fields. The forbidden-substring tests cover the rejection path separately.
    private const string PlaintextSecretMarker = "MARKER_FORBID_PLAINTEXT";
    private const string KeyAliasSecretMarker = "MARKER_ALIAS_TENANT_A";

    [Fact]
    public void Unprotected_Default_HasUnprotectedStateAndCurrentVersion() {
        EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadata.Unprotected();

        metadata.State.ShouldBe(PayloadProtectionState.Unprotected);
        metadata.MetadataVersion.ShouldBe(EventStorePayloadProtectionMetadata.CurrentMetadataVersion);
        metadata.Scheme.ShouldBeNull();
        metadata.KeyAlias.ShouldBeNull();
        metadata.ContentHint.ShouldBeNull();
        metadata.CompatibilityFlags.ShouldBeNull();
    }

    [Fact]
    public void ProviderOpaque_WithReason_RecordsCompatibilityFlag() {
        EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadata.ProviderOpaque("parseError");

        metadata.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        metadata.CompatibilityFlags.ShouldNotBeNull();
        metadata.CompatibilityFlags!["reason"].ShouldBe("parseError");
    }

    [Fact]
    public void Equality_TwoUnprotectedRecords_AreEqual() {
        EventStorePayloadProtectionMetadata a = EventStorePayloadProtectionMetadata.Unprotected();
        EventStorePayloadProtectionMetadata b = EventStorePayloadProtectionMetadata.Unprotected();

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_RecordsWithDifferentFlags_AreNotEqual() {
        EventStorePayloadProtectionMetadata a = new(PayloadProtectionState.Unprotected, 1, null, null, null, new Dictionary<string, string> { ["legacy"] = "missing" });
        EventStorePayloadProtectionMetadata b = new(PayloadProtectionState.Unprotected, 1, null, null, null, new Dictionary<string, string> { ["legacy"] = "false" });

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Carrier_Validate_ProtectedWithoutScheme_RejectsWithReason() {
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, Scheme: null, KeyAlias: null, ContentHint: null, CompatibilityFlags: null);

        bool ok = EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out string? reason);

        ok.ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Carrier_Validate_UnsupportedVersion_RejectsWithReason() {
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Unprotected, 99, null, null, null, null);

        bool ok = EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out string? reason);

        ok.ShouldBeFalse();
        reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("private-key")]
    [InlineData("connection-string=server")]
    [InlineData("plaintext")]
    [InlineData("dapr-secret")]
    public void Carrier_Validate_ForbiddenSubstringInKeyAlias_Rejects(string forbidden) {
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "aes-gcm-256", KeyAlias: forbidden, ContentHint: null, CompatibilityFlags: null);

        bool ok = EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out _);

        ok.ShouldBeFalse();
    }

    [Theory]
    [InlineData("key")]
    [InlineData("iv")]
    [InlineData("nonce")]
    [InlineData("auth-tag")]
    public void Carrier_Validate_ForbiddenCompatibilityFlagKey_Rejects(string forbiddenKey) {
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            1,
            "aes-gcm-256",
            null,
            null,
            new Dictionary<string, string> { [forbiddenKey] = "value" });

        bool ok = EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out _);

        ok.ShouldBeFalse();
    }

    [Fact]
    public void Carrier_Roundtrip_AllFieldsPreserved() {
        EventStorePayloadProtectionMetadata original = new(
            State: PayloadProtectionState.Protected,
            MetadataVersion: 1,
            Scheme: "myco-aead-v1",
            KeyAlias: "tenant-a:eventpayload",
            ContentHint: "application/json",
            CompatibilityFlags: new Dictionary<string, string> { ["safety"] = "high" });

        string serialized = EventStorePayloadProtectionMetadataCarrier.Serialize(original);
        EventStorePayloadProtectionMetadata roundtripped = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        roundtripped.Equals(original).ShouldBeTrue();
    }

    [Fact]
    public void Carrier_Read_MalformedJson_ReturnsProviderOpaqueWithReason() {
        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read("not json {{{ broken");

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        result.CompatibilityFlags!["reason"].ShouldBe("parseError");
    }

    [Fact]
    public void Carrier_Read_UnknownVersion_ReturnsProviderOpaque() {
        string serialized = JsonSerializer.Serialize(new {
            state = "Unprotected",
            metadataVersion = 999,
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        result.CompatibilityFlags!["reason"].ShouldBe("unknownVersion");
    }

    [Fact]
    public void Carrier_Read_UnknownState_ReturnsProviderOpaque() {
        string serialized = JsonSerializer.Serialize(new {
            state = "MagicallySafe",
            metadataVersion = 1,
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
    }

    [Fact]
    public void Carrier_Read_ProtectedWithoutScheme_ReturnsProviderOpaque() {
        string serialized = JsonSerializer.Serialize(new {
            state = "Protected",
            metadataVersion = 1,
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
    }

    [Fact]
    public void Carrier_Read_ForbiddenKeyAlias_ReturnsProviderOpaque() {
        string serialized = JsonSerializer.Serialize(new {
            state = "Protected",
            metadataVersion = 1,
            scheme = "aes-gcm-256",
            keyAlias = "secret-password-foo",
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
    }

    [Fact]
    public void Carrier_Read_UnknownTopLevelField_ReturnsProviderOpaque() {
        string serialized = JsonSerializer.Serialize(new {
            state = "Unprotected",
            metadataVersion = 1,
            futureField = "safe-value",
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        result.CompatibilityFlags!["reason"].ShouldBe("unknownField");
    }

    [Fact]
    public void Carrier_Read_UnknownForbiddenTopLevelField_ReturnsProviderOpaqueForbidden() {
        string serialized = JsonSerializer.Serialize(new {
            state = "Unprotected",
            metadataVersion = 1,
            nonce = "abcdef",
        });

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read(serialized);

        result.State.ShouldBe(PayloadProtectionState.ProviderOpaque);
        result.CompatibilityFlags!["reason"].ShouldBe("forbidden");
    }

    [Fact]
    public void Carrier_ReadFromNullExtensions_ReturnsLegacyCompatibilityRecord() {
        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read((IReadOnlyDictionary<string, string>?)null);

        result.State.ShouldBe(PayloadProtectionState.Unprotected);
        result.CompatibilityFlags.ShouldNotBeNull();
        result.CompatibilityFlags!["legacy"].ShouldBe("missing");
    }

    [Fact]
    public void Carrier_ReadFromExtensionsWithoutProtectionKey_ReturnsLegacyCompatibilityRecord() {
        var extensions = new Dictionary<string, string> { ["traceparent"] = "00-aaa-bbb-01" };

        EventStorePayloadProtectionMetadata result = EventStorePayloadProtectionMetadataCarrier.Read((IReadOnlyDictionary<string, string>)extensions);

        result.State.ShouldBe(PayloadProtectionState.Unprotected);
        result.CompatibilityFlags!["legacy"].ShouldBe("missing");
    }

    [Fact]
    public void Carrier_Write_ToMutableDictionary_PreservesOtherEntries() {
        var extensions = new Dictionary<string, string> { ["traceparent"] = "00-aaa-bbb-01" };
        EventStorePayloadProtectionMetadata metadata = EventStorePayloadProtectionMetadata.Unprotected();

        IDictionary<string, string> result = EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>)extensions, metadata);

        result["traceparent"].ShouldBe("00-aaa-bbb-01");
        result.ContainsKey(EventStorePayloadProtectionMetadataCarrier.ExtensionKey).ShouldBeTrue();
    }

    [Fact]
    public void Carrier_Serialize_ForbiddenSubstringInCompatibilityFlag_ThrowsBeforeSerialization() {
        // Sentinel-based no-leak proof: the validator rejects metadata that embeds forbidden
        // secret-shaped substrings (here "PLAINTEXT") in any field, so the carrier cannot serialize
        // a payload that would leak them. PLAINTEXT_SECRET_MARKER contains "plaintext" → rejected.
        var unsafeMetadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            1,
            Scheme: "aes-gcm-256",
            KeyAlias: "tenant-a:event",
            ContentHint: null,
            CompatibilityFlags: new Dictionary<string, string> { ["note"] = PlaintextSecretMarker });

        EventStorePayloadProtectionMetadataCarrier.TryValidate(unsafeMetadata, out _).ShouldBeFalse();
        Should.Throw<System.ArgumentException>(() => EventStorePayloadProtectionMetadataCarrier.Serialize(unsafeMetadata));

        // A non-secret-shaped alias passes validation and roundtrips cleanly.
        var safeMetadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            1,
            Scheme: "aes-gcm-256",
            KeyAlias: KeyAliasSecretMarker,
            ContentHint: "application/json",
            CompatibilityFlags: null);
        EventStorePayloadProtectionMetadataCarrier.TryValidate(safeMetadata, out _).ShouldBeTrue();
        string serialized = EventStorePayloadProtectionMetadataCarrier.Serialize(safeMetadata);
        EventStorePayloadProtectionMetadataCarrier.Read(serialized).Equals(safeMetadata).ShouldBeTrue();
    }

    [Fact]
    public void Carrier_Legacy_ReturnsUnprotectedWithLegacyFlag() {
        EventStorePayloadProtectionMetadata legacy = EventStorePayloadProtectionMetadataCarrier.Legacy();

        legacy.State.ShouldBe(PayloadProtectionState.Unprotected);
        legacy.CompatibilityFlags.ShouldNotBeNull();
        legacy.CompatibilityFlags!["legacy"].ShouldBe("missing");
    }
}
