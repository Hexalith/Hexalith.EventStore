using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Exercises Story 8.4 per-event compatibility routing (normative section 12.1-12.2, vectors V107-V116).
/// Vector numbers appear in method names only; the Story 8.3 <c>Vector</c> trait set stays unchanged.
/// </summary>
public sealed class CompatibilityEventRoutingTests
{
    /// <summary>A missing or blank carrier over plain JSON passes the stored bytes through unchanged.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task V107_MissingCarrier_PassesLegacyBytesThroughUnchangedAsync(string? carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = CompatibilityTestData.Event(1, CompatibilityTestData.PlainJson(), "json", carrier);

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.LegacyUnprotected);
        result.PayloadBytes.ShouldBeSameAs(record.PayloadBytes);
        result.PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
        result.SerializationFormat.ShouldBe("json");
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadataCarrier.Legacy());
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>A stored carrier equal to the legacy record keeps the legacy classification.</summary>
    [Fact]
    public async Task V107_SerializedLegacyCarrier_StaysLegacyAsync()
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();
        string carrier = CompatibilityTestData.Carrier(EventStorePayloadProtectionMetadataCarrier.Legacy());

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(1, CompatibilityTestData.PlainJson(), "json", carrier));

        result.Route.ShouldBe(CompatibilityReadRoute.LegacyUnprotected);
    }

    /// <summary>An exact unprotected carrier over plain JSON passes through with unprotected metadata.</summary>
    [Fact]
    public async Task V108_UnprotectedCarrier_PassesBytesThroughAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = CompatibilityTestData.UnprotectedEvent(4);

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.SequenceNumber.ShouldBe(4UL);
        result.Route.ShouldBe(CompatibilityReadRoute.Unprotected);
        result.PayloadBytes.ShouldBeSameAs(record.PayloadBytes);
        result.SerializationFormat.ShouldBe("json");
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>A non-reserved custom format passes through byte for byte, whether or not it is JSON.</summary>
    [Theory]
    [InlineData("application/x-protobuf", "binary", "none")]
    [InlineData("application/x-protobuf", "invalid", "unprotected")]
    [InlineData("avro", "plain", "none")]
    [InlineData("Json", "plain", "unprotected")]
    [InlineData("JSON-REDACTED", "plain", "none")]
    public async Task V109_NonReservedCustomFormat_PassesThroughByteForByteAsync(string format, string payload, string carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = CompatibilityTestData.Event(
            2,
            CompatibilityTestData.PayloadFor(payload),
            format,
            CompatibilityTestData.CarrierFor(carrier));
        byte[] original = [.. record.PayloadBytes];

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(carrier == "none" ? CompatibilityReadRoute.LegacyUnprotected : CompatibilityReadRoute.Unprotected);
        result.PayloadBytes.ShouldBeSameAs(record.PayloadBytes);
        result.PayloadBytes.ShouldBe(original);
        result.SerializationFormat.ShouldBe(format);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>A custom-format JSON payload beyond the core depth bound is not provably JSON and passes through.</summary>
    [Fact]
    public async Task V109_CustomFormatBeyondCoreBounds_PassesThroughAsync()
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 65) + new string(']', 65));

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(1, deep, "application/x-nested", null));

        result.Route.ShouldBe(CompatibilityReadRoute.LegacyUnprotected);
        result.PayloadBytes.ShouldBeSameAs(deep);
    }

    /// <summary>Exact <c>json-redacted</c> passes through, keeps its format, and is never re-protected.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("unprotected")]
    public async Task V110_RedactedFormat_PassesThroughWithoutReprotectionAsync(string carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = CompatibilityTestData.Event(
            3,
            CompatibilityTestData.RedactedJson(),
            "json-redacted",
            CompatibilityTestData.CarrierFor(carrier));

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.Redacted);
        result.PayloadBytes.ShouldBeSameAs(record.PayloadBytes);
        result.PayloadBytes.ShouldBe(CompatibilityTestData.RedactedJson());
        result.SerializationFormat.ShouldBe("json-redacted");
        result.Metadata!.State.ShouldBe(PayloadProtectionState.Unprotected);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Exact Parties v1 metadata with a bounded <c>$enc</c> shape is decided by the registered reader.</summary>
    [Fact]
    public async Task V111_ExactPartiesMetadata_RoutesToRegisteredReaderAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        byte[] plaintext = CompatibilityTestData.PlainJson();
        reader.OnEvent = (_, _) => CoreUnprotectionResult.Readable(plaintext);
        CompatibilityEventRecord record = CompatibilityTestData.V1Event(5);

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.RegisteredV1);
        result.PayloadBytes.ShouldBeSameAs(plaintext);
        result.SerializationFormat.ShouldBe("json");
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        reader.EventCalls.ShouldBe(1);
        reader.LastMetadata.ShouldBe(CompatibilityTestData.PartiesV1Metadata());
        resolver.Calls.ShouldBe(0);
        record.PayloadBytes.ShouldBe(CompatibilityTestData.V1Json());
    }

    /// <summary>A missing carrier over <c>json+pdenc-v1</c> keeps current Parties compatibility through the reader.</summary>
    [Fact]
    public async Task V112_MissingMetadataV1_RoutesToRegisteredReaderAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.V1Event(6, withMetadata: false));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.RegisteredV1);
        reader.EventCalls.ShouldBe(1);
        reader.LastMetadata.ShouldBe(EventStorePayloadProtectionMetadataCarrier.Legacy());
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>Without a registered v1 reader a v1 record is opaque and never plaintext.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task V112_V1WithoutRegisteredReader_IsOpaqueAndNeverPlaintextAsync(bool withMetadata)
    {
        var resolver = new CountingKeyResolver();
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync);
        CompatibilityEventRecord record = CompatibilityTestData.V1Event(7, withMetadata);

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeFalse();
        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        result.PayloadBytes.ShouldBeNull();
        result.SerializationFormat.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        resolver.Calls.ShouldBe(0);
        record.PayloadBytes.ShouldBe(CompatibilityTestData.V1Json());
    }

    /// <summary>The registered reader's typed unreadable reason is returned unchanged and any returned buffer is cleared.</summary>
    [Theory]
    [InlineData(UnreadableProtectedDataReason.MissingKey)]
    [InlineData(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted)]
    [InlineData(UnreadableProtectedDataReason.ProviderDenied)]
    [InlineData(UnreadableProtectedDataReason.BytesMetadataMismatch)]
    public async Task V111_ReaderUnreadableReason_IsReturnedAsync(UnreadableProtectedDataReason reason)
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        byte[] stray = CompatibilityTestData.PlainJson();
        reader.OnEvent = (_, _) => new CoreUnprotectionResult(stray, reason);

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.V1Event(8));

        result.Route.ShouldBe(CompatibilityReadRoute.RegisteredV1);
        result.UnreadableReason.ShouldBe(reason);
        result.PayloadBytes.ShouldBeNull();
        stray.ShouldAllBe(static value => value == 0);
    }

    /// <summary>A reader fault maps to provider-unavailable without leaking its text.</summary>
    [Fact]
    public async Task V111_ReaderException_MapsToProviderUnavailableAsync()
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        reader.OnEvent = (_, _) => throw new InvalidOperationException(CompatibilityTestData.Sentinel);

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.V1Event(9));

        result.Route.ShouldBe(CompatibilityReadRoute.RegisteredV1);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        CompatibilityTestData.ShouldNotLeak(result);
    }

    /// <summary>A reader cancellation that the caller did not request is a reader fault, matching the core.</summary>
    [Fact]
    public async Task V111_ReaderForeignCancellation_MapsToProviderUnavailableAsync()
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        reader.OnEvent = (_, _) => throw new OperationCanceledException();

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.V1Event(9));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
    }

    /// <summary>A reader that hands back the stored buffer did not decrypt; the caller's bytes stay intact.</summary>
    [Fact]
    public async Task V111_ReaderReturningStoredBuffer_IsConsistencyMismatchAsync()
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        reader.OnEvent = static (record, _) => CoreUnprotectionResult.Readable(record.PayloadBytes);
        CompatibilityEventRecord record = CompatibilityTestData.V1Event(10);

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        record.PayloadBytes.ShouldBe(CompatibilityTestData.V1Json());
    }

    /// <summary>A reader result that still carries a protected marker or is not JSON is never returned as plaintext.</summary>
    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    [InlineData("invalid")]
    public async Task V111_ReaderReturningPartialPlaintext_IsConsistencyMismatchAndClearedAsync(string payload)
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        byte[] partial = CompatibilityTestData.PayloadFor(payload);
        reader.OnEvent = (_, _) => CoreUnprotectionResult.Readable(partial);

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.V1Event(11));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        result.PayloadBytes.ShouldBeNull();
        partial.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Exact v2 metadata, format, and wrappers authenticate through the core and return unprotected JSON.</summary>
    [Fact]
    public async Task V113_ExactV2_AuthenticatesThroughCoreAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = CompatibilityTestData.V2Event(12);
        byte[] stored = [.. record.PayloadBytes];

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
        result.PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
        result.PayloadBytes.ShouldNotBeSameAs(record.PayloadBytes);
        result.SerializationFormat.ShouldBe("json");
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        resolver.Calls.ShouldBe(1);
        reader.EventCalls.ShouldBe(0);
        record.PayloadBytes.ShouldBe(stored);
    }

    /// <summary>Every core failure returns the core's typed reason and never partial output.</summary>
    [Theory]
    [InlineData("wrong-key", UnreadableProtectedDataReason.BytesMetadataMismatch, 1)]
    [InlineData("missing-key", UnreadableProtectedDataReason.MissingKey, 1)]
    [InlineData("short-key", UnreadableProtectedDataReason.ConsistencyMismatch, 1)]
    [InlineData("outage", UnreadableProtectedDataReason.ProviderUnavailable, 1)]
    [InlineData("other-sequence", UnreadableProtectedDataReason.BytesMetadataMismatch, 1)]
    [InlineData("other-type", UnreadableProtectedDataReason.BytesMetadataMismatch, 1)]
    [InlineData("bad-envelope", UnreadableProtectedDataReason.BytesMetadataMismatch, 0)]
    public async Task V113_CoreFailure_ReturnsTypedReasonWithoutOutputAsync(
        string mutation,
        UnreadableProtectedDataReason expected,
        int expectedResolverCalls)
    {
        var resolver = new CountingKeyResolver(mutation switch
        {
            "wrong-key" => static _ => [.. Enumerable.Repeat((byte)7, 32)],
            "missing-key" => static _ => null,
            "short-key" => static _ => new byte[16],
            "outage" => static _ => throw new TimeoutException(CompatibilityTestData.Sentinel),
            _ => null,
        });
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync);
        CompatibilityEventRecord record = mutation switch
        {
            "other-sequence" => CompatibilityTestData.MisplacedV2Event(13),
            "other-type" => CompatibilityTestData.V2Event(13) with { EventTypeName = "Hexalith.Parties.Contracts.Events.PartyRenamed" },
            "bad-envelope" => CompatibilityTestData.Event(
                13,
                TestFixture.WrapperPayloadBytes("AAAA"),
                "json+pdenc-v2",
                CompatibilityTestData.V2Carrier()),
            _ => CompatibilityTestData.V2Event(13),
        };

        CompatibilityEventReadResult result = await router.ReadEventAsync(record);

        result.IsReadable.ShouldBeFalse();
        result.UnreadableReason.ShouldBe(expected);
        result.PayloadBytes.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        resolver.Calls.ShouldBe(expectedResolverCalls);
        CompatibilityTestData.ShouldNotLeak(result);
    }

    /// <summary>Reserved unknown formats are opaque under every metadata class, with no resolver or reader call.</summary>
    [Theory]
    [InlineData("json+pdenc-v3", "v2")]
    [InlineData("json+pdenc-v3", "none")]
    [InlineData("json+pdenc-v3", "unprotected")]
    [InlineData("json+pdenc-v3", "v1")]
    [InlineData("JSON+PDENC-V2", "v2")]
    [InlineData("json+pdenc-V1", "none")]
    [InlineData("json+pdenc-", "none")]
    [InlineData("xml+pdenc-v1", "none")]
    [InlineData("protected+aes", "unprotected")]
    public async Task V114_ReservedUnknownFormat_IsOpaqueWithoutCallsAsync(string format, string carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.Event(
            14,
            CompatibilityTestData.V2Json(),
            format,
            CompatibilityTestData.CarrierFor(carrier)));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Protected metadata naming an unknown scheme is opaque without any provider call.</summary>
    [Fact]
    public async Task V114_UnknownProtectedScheme_IsOpaqueWithoutCallsAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        string carrier = CompatibilityTestData.Carrier(CompatibilityTestData.V2Metadata() with { Scheme = "acme-envelope-v9" });

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(15, CompatibilityTestData.V2Json(), "json+pdenc-v2", carrier));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Malformed, forbidden, duplicate, or non-allowlisted carriers are malformed metadata with no calls.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"state\":\"Protected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\",\"note\":\"x\"}")]
    [InlineData("{\"state\":\"Bogus\",\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"protected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\"7\",\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"1\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\"Protected, Unprotected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\" Protected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\"1\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\",\"contentHint\":\"application/json\",\"compatibilityFlags\":{\"format\":\"json+pdenc-v2\",\"envelope\":\"pdenc-v2\"}}")]
    [InlineData("{\"state\":\"Protected, Protected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\",\"contentHint\":\"application/json\",\"compatibilityFlags\":{\"format\":\"json+pdenc-v2\",\"envelope\":\"pdenc-v2\"}}")]
    [InlineData("{\"state\":\"Protected \",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\",\"contentHint\":\"application/json\",\"compatibilityFlags\":{\"format\":\"json+pdenc-v2\",\"envelope\":\"pdenc-v2\"}}")]
    [InlineData("{\"state\":1,\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"Unprotected\",\"metadataVersion\":\"1\"}")]
    [InlineData("{\"state\":\"Unprotected\",\"metadataVersion\":1,\"state\":\"Unprotected\"}")]
    [InlineData("{\"state\":\"Unprotected\",\"metadataVersion\":2,\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"Unprotected\",\"st\\u0061te\":\"Unprotected\",\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"Protected\",\"metadataVersion\":1,\"scheme\":\"hexalith-pdenc-v2\",\"contentHint\":\"application/json\",\"compatibilityFlags\":{\"format\":\"json+pdenc-v2\",\"envelope\":\"pdenc-v2\",\"format\":\"json+pdenc-v2\"}}")]
    [InlineData("{\"state\":\"Protected\",\"metadataVersion\":1,\"scheme\":\"plaintext-envelope\"}")]
    [InlineData("{\"state\":\"Protected\",\"metadataVersion\":0,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\"ProviderOpaque\",\"metadataVersion\":1,\"compatibilityFlags\":{\"reason\":\"forbidden\"}}")]
    public async Task V115_MalformedCarrier_IsMalformedMetadataWithoutCallsAsync(string carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(16, CompatibilityTestData.V2Json(), "json+pdenc-v2", carrier));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Known schemes outside their exact allowlist are malformed; the v2 allowlist is exact.</summary>
    [Theory]
    [InlineData("extra-flag")]
    [InlineData("missing-flag")]
    [InlineData("key-alias")]
    [InlineData("content-hint")]
    [InlineData("flag-case")]
    [InlineData("flag-value")]
    [InlineData("v1-extra-flag")]
    public async Task V115_NonAllowlistedKnownScheme_IsMalformedMetadataAsync(string mutation)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        EventStorePayloadProtectionMetadata v2 = CompatibilityTestData.V2Metadata();
        EventStorePayloadProtectionMetadata metadata = mutation switch
        {
            "extra-flag" => v2 with { CompatibilityFlags = CompatibilityTestData.Flags(("format", "json+pdenc-v2"), ("envelope", "pdenc-v2"), ("note", "x")) },
            "missing-flag" => v2 with { CompatibilityFlags = CompatibilityTestData.Flags(("format", "json+pdenc-v2")) },
            "key-alias" => v2 with { KeyAlias = "alias-01" },
            "content-hint" => v2 with { ContentHint = "application/JSON" },
            "flag-case" => v2 with { CompatibilityFlags = CompatibilityTestData.Flags(("Format", "json+pdenc-v2"), ("envelope", "pdenc-v2")) },
            "flag-value" => v2 with { CompatibilityFlags = CompatibilityTestData.Flags(("format", "json+pdenc-v2 "), ("envelope", "pdenc-v2")) },
            _ => CompatibilityTestData.PartiesV1Metadata() with
            {
                CompatibilityFlags = CompatibilityTestData.Flags(("format", "json+pdenc-v1"), ("field-envelope", "pdenc-v1"), ("data", "x")),
            },
        };
        string format = mutation == "v1-extra-flag" ? "json+pdenc-v1" : "json+pdenc-v2";
        byte[] payload = mutation == "v1-extra-flag" ? CompatibilityTestData.V1Json() : CompatibilityTestData.V2Json();

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(17, payload, format, CompatibilityTestData.Carrier(metadata)));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>An over-version carrier is unknown-version metadata with no calls.</summary>
    [Theory]
    [InlineData("{\"state\":\"Protected\",\"metadataVersion\":2,\"scheme\":\"hexalith-pdenc-v2\"}")]
    [InlineData("{\"state\":\"ProviderOpaque\",\"metadataVersion\":1,\"compatibilityFlags\":{\"reason\":\"unknownVersion\"}}")]
    public async Task V115_OverVersionCarrier_IsUnknownMetadataVersionAsync(string carrier)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(18, CompatibilityTestData.V2Json(), "json+pdenc-v2", carrier));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.UnknownMetadataVersion);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>
    /// The carrier reader accepts a duplicate member and an undefined numeric state, but classification rejects both.
    /// </summary>
    [Theory]
    [InlineData("{\"state\":\"Unprotected\",\"metadataVersion\":1,\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"Unprotected\",\"metadataVersion\":1,\"compatibilityFlags\":{\"legacy\":\"missing\",\"legacy\":\"missing\"}}")]
    [InlineData("{\"state\":\"7\",\"metadataVersion\":1}")]
    public void V115_DuplicateMemberOrUndefinedNumericState_IsMalformedEvenThoughCarrierReadAcceptsIt(string carrier)
    {
        EventStorePayloadProtectionMetadata accepted = EventStorePayloadProtectionMetadataCarrier.Read(carrier);
        accepted.State.ShouldNotBe(PayloadProtectionState.ProviderOpaque);

        CompatibilityClassification classification = PayloadCompatibilityClassifier.ClassifyCarrier(carrier);

        classification.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        classification.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
    }

    /// <summary>A numeric spelling of a defined unprotected state is malformed rather than a plaintext route.</summary>
    [Theory]
    [InlineData("{\"state\":\"0\",\"metadataVersion\":1}")]
    [InlineData("{\"state\":\"Unprotected, Unprotected\",\"metadataVersion\":1}")]
    public async Task V115_NonCanonicalUnprotectedStateSpelling_IsMalformedNotPlaintextAsync(string carrier)
    {
        EventStorePayloadProtectionMetadataCarrier.Read(carrier).State.ShouldBe(PayloadProtectionState.Unprotected);
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(16, CompatibilityTestData.PlainJson(), "json", carrier));

        result.IsReadable.ShouldBeFalse();
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
    }

    /// <summary>An oversized carrier is rejected before the carrier reader parses it.</summary>
    [Fact]
    public void V115_OversizedCarrier_IsMalformedMetadata()
    {
        string carrier = "{\"state\":\"Unprotected\","
            + new string(' ', PayloadCompatibilityClassifier.MaximumCarrierCharacters)
            + "\"metadataVersion\":1}";
        EventStorePayloadProtectionMetadataCarrier.Read(carrier).State.ShouldBe(PayloadProtectionState.Unprotected);

        PayloadCompatibilityClassifier.ClassifyCarrier(carrier).UnreadableReason
            .ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
    }

    /// <summary>The carrier is classified before the serialization format, so a malformed carrier wins.</summary>
    [Theory]
    [InlineData("json+pdenc-v3")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task V115_MalformedCarrierIsClassifiedBeforeFormatAsync(string format)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.Event(
            16,
            CompatibilityTestData.V2Json(),
            format,
            "{\"state\":\"7\",\"metadataVersion\":1}"));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.MalformedMetadata);
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Every metadata, format, and shape disagreement is a local mismatch with no resolver or reader call.</summary>
    [Theory]
    [InlineData("v2", "json+pdenc-v2", "plain")]
    [InlineData("v2", "json+pdenc-v2", "v1")]
    [InlineData("v2", "json+pdenc-v2", "both")]
    [InlineData("v2", "json+pdenc-v2", "invalid")]
    [InlineData("v2", "json", "v2")]
    [InlineData("v2", "json", "plain")]
    [InlineData("v2", "json-redacted", "plain")]
    [InlineData("v2", "json+pdenc-v1", "v1")]
    [InlineData("v2", "application/x-protobuf", "binary")]
    [InlineData("none", "json+pdenc-v2", "v2")]
    [InlineData("unprotected", "json+pdenc-v2", "v2")]
    [InlineData("v1", "json+pdenc-v2", "v2")]
    [InlineData("v1", "json+pdenc-v1", "v2")]
    [InlineData("v1", "json+pdenc-v1", "plain")]
    [InlineData("v1", "json+pdenc-v1", "both")]
    [InlineData("v1", "json", "v1")]
    [InlineData("v1", "json-redacted", "plain")]
    [InlineData("none", "json+pdenc-v1", "v2")]
    [InlineData("none", "json+pdenc-v1", "plain")]
    [InlineData("none", "json+pdenc-v1", "invalid")]
    [InlineData("unprotected", "json+pdenc-v1", "v1")]
    [InlineData("none", "json", "v2")]
    [InlineData("none", "json", "v1")]
    [InlineData("unprotected", "json", "v2")]
    [InlineData("unprotected", "json", "v1")]
    [InlineData("none", "json", "invalid")]
    [InlineData("none", "json", "duplicate")]
    [InlineData("none", "json", "binary")]
    [InlineData("none", "json-redacted", "v2")]
    [InlineData("unprotected", "json-redacted", "v1")]
    [InlineData("none", "json-redacted", "invalid")]
    [InlineData("none", "application/x-custom", "v2")]
    [InlineData("unprotected", "application/x-custom", "v1")]
    [InlineData("none", "", "plain")]
    [InlineData("none", "  ", "plain")]
    public async Task V116_MetadataFormatShapeDisagreement_IsLocalMismatchAsync(string carrier, string format, string payload)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(CompatibilityTestData.Event(
            19,
            CompatibilityTestData.PayloadFor(payload),
            format,
            CompatibilityTestData.CarrierFor(carrier)));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        result.PayloadBytes.ShouldBeNull();
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>The marker scan decodes escaped member names and stays within the core depth bound.</summary>
    [Theory]
    [InlineData("{\"email\":{\"\\u0024enc\":{}}}")]
    [InlineData("{\"email\":{\"\\u0024pdenc\":\"AAAA\"}}")]
    [InlineData("{\"a\":[{\"b\":{\"$enc\":1}}]}")]
    public async Task V116_EscapedOrNestedMarker_IsDetectedInJsonAsync(string json)
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();

        CompatibilityEventReadResult result = await router.ReadEventAsync(
            CompatibilityTestData.Event(20, Encoding.UTF8.GetBytes(json), "json", null));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
    }

    /// <summary>Plain JSON beyond the core depth bound is not valid bounded JSON for the json format.</summary>
    [Fact]
    public async Task V116_JsonBeyondCoreDepth_IsBytesMetadataMismatchAsync()
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();
        byte[] atLimit = Encoding.UTF8.GetBytes(new string('[', 64) + new string(']', 64));
        byte[] beyond = Encoding.UTF8.GetBytes(new string('[', 65) + new string(']', 65));

        (await router.ReadEventAsync(CompatibilityTestData.Event(21, atLimit, "json", null))).IsReadable.ShouldBeTrue();
        (await router.ReadEventAsync(CompatibilityTestData.Event(22, beyond, "json", null))).UnreadableReason
            .ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
    }

    /// <summary>The capability lists exact reader identifiers, formats, and versions; an unregistered reader is absent.</summary>
    [Fact]
    public void Capability_ListsExactRegisteredReaders()
    {
        var resolver = new CountingKeyResolver();
        var withoutReader = new PayloadCompatibilityRouter(resolver.ResolveAsync);
        var withReader = new PayloadCompatibilityRouter(resolver.ResolveAsync, [new FakeLegacyPayloadReader()]);

        withoutReader.Capabilities.ShouldBe(
        [
            new CompatibilityReaderCapability("eventstore-plaintext-passthrough", "json", 0),
            new CompatibilityReaderCapability("eventstore-redacted-passthrough", "json-redacted", 0),
            new CompatibilityReaderCapability("hexalith-pdenc-v2", "json+pdenc-v2", 2),
        ]);
        withReader.Capabilities.ShouldBe(
        [
            new CompatibilityReaderCapability("eventstore-plaintext-passthrough", "json", 0),
            new CompatibilityReaderCapability("eventstore-redacted-passthrough", "json-redacted", 0),
            new CompatibilityReaderCapability("parties-pdenc-v1", "json+pdenc-v1", 1),
            new CompatibilityReaderCapability("hexalith-pdenc-v2", "json+pdenc-v2", 2),
        ]);
        withoutReader.Capabilities.ShouldNotContain(static capability => capability.ReaderId == "parties-pdenc-v1");
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>An unknown, respelled, duplicate, or null legacy reader fails construction.</summary>
    [Theory]
    [InlineData("parties-pdenc-v2", 1)]
    [InlineData("Parties-pdenc-v1", 1)]
    [InlineData("parties-pdenc-v1 ", 1)]
    [InlineData("parties-pdenc-v1", 2)]
    [InlineData(null, 1)]
    public void Router_RejectsUnapprovedOrDuplicateLegacyReaders(string? readerId, int count)
    {
        var resolver = new CountingKeyResolver();
        ILegacyPayloadReader?[] readers = [.. Enumerable.Range(0, count)
            .Select(_ => readerId is null ? null : (ILegacyPayloadReader)new FakeLegacyPayloadReader(readerId))];

        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new PayloadCompatibilityRouter(resolver.ResolveAsync, readers!));

        CompatibilityTestData.ShouldNotLeak(exception, "parties");
    }

    /// <summary>Caller cancellation propagates from every route instead of becoming an unreadable reason.</summary>
    [Theory]
    [InlineData("legacy")]
    [InlineData("redacted")]
    [InlineData("v1")]
    [InlineData("v2")]
    [InlineData("mismatch")]
    public async Task Cancellation_PropagatesFromEveryRouteAsync(string kind)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord record = kind switch
        {
            "legacy" => CompatibilityTestData.LegacyEvent(1),
            "redacted" => CompatibilityTestData.RedactedEvent(1),
            "v1" => CompatibilityTestData.V1Event(1),
            "v2" => CompatibilityTestData.V2Event(1),
            _ => CompatibilityTestData.Event(1, CompatibilityTestData.V2Json(), "json", null),
        };
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadEventAsync(record, source.Token));
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Cancellation observed inside the reader or the key resolver propagates.</summary>
    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Cancellation_DuringReaderOrResolverPropagatesAsync(string kind)
    {
        using var source = new CancellationTokenSource();
        var resolver = new CountingKeyResolver(_ =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        });
        var reader = new FakeLegacyPayloadReader
        {
            OnEvent = (_, _) =>
            {
                source.Cancel();
                throw new OperationCanceledException(source.Token);
            },
        };
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]);
        CompatibilityEventRecord record = kind == "v1" ? CompatibilityTestData.V1Event(1) : CompatibilityTestData.V2Event(1);

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadEventAsync(record, source.Token));
    }

    /// <summary>Unreadable results, records, and argument exceptions never render a planted sentinel.</summary>
    [Fact]
    public async Task UnreadableResultsAndExceptions_CarryNoSentinelAsync()
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        string sentinel = CompatibilityTestData.Sentinel;
        reader.OnEvent = (_, _) => CoreUnprotectionResult.Readable(
            Encoding.UTF8.GetBytes("{\"email\":{\"$enc\":\"" + sentinel + "\"}}"));
        CompatibilityEventRecord[] records =
        [
            CompatibilityTestData.Event(1, Encoding.UTF8.GetBytes("{\"$pdenc\":\"" + sentinel + "\"}"), "json", null),
            CompatibilityTestData.Event(2, CompatibilityTestData.PlainJson(), "json", "{\"state\":\"Unprotected\",\"metadataVersion\":1,\"note\":\"" + sentinel + "\"}"),
            CompatibilityTestData.Event(3, CompatibilityTestData.V2Json(email: sentinel), "json+pdenc-v2", CompatibilityTestData.V2Carrier()) with { EventTypeName = "Other.Type" },
            CompatibilityTestData.Event(4, Encoding.UTF8.GetBytes("{\"" + sentinel + "\":{\"$enc\":1}}"), "json+pdenc-v1", CompatibilityTestData.V1Carrier()),
            CompatibilityTestData.Event(5, Encoding.UTF8.GetBytes(sentinel), "json+pdenc-v9", null),
        ];

        foreach (CompatibilityEventRecord record in records)
        {
            CompatibilityEventReadResult result = await router.ReadEventAsync(record);
            result.IsReadable.ShouldBeFalse();
            CompatibilityTestData.ShouldNotLeak(result);
            CompatibilityTestData.ShouldNotLeak(record.ToString());
        }

        CompatibilityEventRecord foreign = CompatibilityTestData.Event(
            1,
            Encoding.UTF8.GetBytes(sentinel),
            "json",
            null,
            new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-b", "parties", "party-01"));
        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () => await router.ReadStreamAsync(
            [CompatibilityTestData.Event(2, Encoding.UTF8.GetBytes(sentinel), "json", sentinel), foreign]));
        CompatibilityTestData.ShouldNotLeak(exception);
    }

    private static (PayloadCompatibilityRouter Router, CountingKeyResolver Resolver, FakeLegacyPayloadReader Reader) CreateRouter()
    {
        var resolver = new CountingKeyResolver();
        var reader = new FakeLegacyPayloadReader();
        return (new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]), resolver, reader);
    }
}
